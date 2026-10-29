using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Diagnose "unsichtbare Wand" (Alex 2026-10-28): Kapsel-Collider, deren Achse ueber lange Strecken
    /// keine sichtbare Geometrie hat (Spinne kann dort hochkrabbeln, sieht aber nichts), und Collider ohne jede
    /// sichtbare Geometrie in der Naehe. Schreibt Logs/ci/unsichtbare_wand.txt, bricht nicht ab (Messlauf).
    /// Sichtbar = Vertex eines lesbaren Meshes nahe der Achse, oder (bei nicht lesbaren Meshes) die Renderer-Bounds.</summary>
    [Timeout(900000)]
    public class UnsichtbareWandTests
    {
        private struct Fund { public float frei; public string text; }

        private class Sicht
        {
            public Bounds b;
            public Vector3[] verts; // null = nicht lesbar -> Bounds zaehlen als sichtbar
        }

        [UnityTest]
        public IEnumerator Messung_Kapseln_ohne_sichtbare_Geometrie()
        {
            var funde = new List<Fund>();
            var ohneSicht = new List<string>();
            var dicke = new List<Fund>();
            int kapseln = 0;
            foreach (int seed in new[] { 4242, 7 })
            {
                JungleWorldGenerator.NextSeed = seed;
                SceneManager.LoadScene("Urwald");
                yield return null;
                JungleWorldGenerator gen = null;
                float timeout = 20f;
                while (timeout > 0f && gen == null)
                {
                    var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                    if (g != null && g.IsReady) gen = g;
                    timeout -= Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.IsNotNull(gen, "Welt nicht bereit (Seed " + seed + ")");
                yield return null;

                // Alle sichtbaren Renderer einmal einsammeln (Vertices pro Mesh gecacht).
                var vcache = new Dictionary<Mesh, Vector3[]>();
                var sichten = new List<Sicht>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var s = new Sicht { b = r.bounds };
                    var mesh = mf.sharedMesh;
                    if (mesh.isReadable)
                    {
                        if (!vcache.TryGetValue(mesh, out var lv)) { lv = mesh.vertices; vcache[mesh] = lv; }
                        int stride = Mathf.Max(1, lv.Length / 3000);
                        var m = mf.transform.localToWorldMatrix;
                        var arr = new Vector3[(lv.Length + stride - 1) / stride];
                        for (int i = 0, k = 0; i < lv.Length; i += stride, k++) arr[k] = m.MultiplyPoint3x4(lv[i]);
                        s.verts = arr;
                    }
                    sichten.Add(s);
                }

                foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                {
                    if (col == null || !col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy) continue;
                    if (col is TerrainCollider) continue;
                    string pfad = Pfad(col.transform);
                    Bounds cb = col.bounds;
                    var nah = new Bounds(cb.center, cb.size + Vector3.one * 1.5f);

                    var nahe = new List<Sicht>();
                    foreach (var s in sichten) if (s.b.Intersects(nah)) nahe.Add(s);
                    if (nahe.Count == 0)
                    {
                        ohneSicht.Add("seed" + seed + " " + pfad + " " + col.GetType().Name + " pos=" + cb.center.ToString("F1") + " size=" + cb.size.ToString("F1"));
                        continue;
                    }

                    var cc = col as CapsuleCollider;
                    if (cc == null) continue;
                    kapseln++;
                    Vector3 axis = cc.direction == 0 ? cc.transform.right : cc.direction == 2 ? cc.transform.forward : cc.transform.up;
                    Vector3 sc = cc.transform.lossyScale;
                    float radScale = cc.direction == 1 ? Mathf.Max(Mathf.Abs(sc.x), Mathf.Abs(sc.z)) : Mathf.Max(Mathf.Abs(sc.y), Mathf.Abs(sc.z));
                    float lenScale = cc.direction == 1 ? Mathf.Abs(sc.y) : cc.direction == 0 ? Mathf.Abs(sc.x) : Mathf.Abs(sc.z);
                    float radius = cc.radius * radScale;
                    float laenge = Mathf.Max(cc.height * lenScale, radius * 2f);
                    Vector3 mitte = cc.transform.TransformPoint(cc.center);
                    Vector3 a0 = mitte - axis * (laenge * 0.5f);

                    float schritt = 0.25f, nahMass = radius + 0.3f;
                    int n = Mathf.Max(1, Mathf.CeilToInt(laenge / schritt));
                    var belegt = new bool[n];
                    var quers = new List<float>[n];
                    for (int k = 0; k < n; k++) quers[k] = new List<float>();
                    foreach (var s in nahe)
                    {
                        if (s.verts == null)
                        {
                            // nicht lesbar: Scheiben markieren, deren Mitte im (leicht vergroesserten) Bounds liegt
                            var bb = s.b; bb.Expand(0.1f);
                            for (int k = 0; k < n; k++)
                                if (bb.Contains(a0 + axis * ((k + 0.5f) * schritt))) belegt[k] = true;
                            continue;
                        }
                        for (int i = 0; i < s.verts.Length; i++)
                        {
                            Vector3 d = s.verts[i] - a0;
                            float t = Vector3.Dot(d, axis);
                            if (t < -nahMass || t > laenge + nahMass) continue;
                            float qd = (d - axis * t).magnitude;
                            if (qd > nahMass) continue;
                            int kk = Mathf.Clamp(Mathf.FloorToInt(t / schritt), 0, n - 1);
                            quers[kk].Add(qd);
                            int lo = Mathf.Clamp(Mathf.FloorToInt((t - 0.15f) / schritt), 0, n - 1);
                            int hi = Mathf.Clamp(Mathf.FloorToInt((t + 0.15f) / schritt), 0, n - 1);
                            for (int k = lo; k <= hi; k++) belegt[k] = true;
                        }
                    }
                    float maxLuecke = 0f; int luckeK = 0;
                    for (int k = 0; k < n; k++)
                    {
                        var ql = quers[k];
                        if (ql.Count < 6) continue;
                        ql.Sort();
                        float med = ql[ql.Count / 2];
                        float luecke = radius - med;
                        if (luecke > maxLuecke) { maxLuecke = luecke; luckeK = k; }
                    }
                    if (maxLuecke >= 0.4f && radius >= 0.3f)
                        dicke.Add(new Fund { frei = maxLuecke, text = "seed" + seed + " Kapsel-r=" + radius.ToString("F2") + " sichtbarer Radius~" + (radius - maxLuecke).ToString("F2") + " => unsichtbare Dicke " + maxLuecke.ToString("F2") + " m bei " + (luckeK * schritt).ToString("F1") + " m, " + pfad + " pos=" + mitte.ToString("F1") });
                    int beste = 0, lauf = 0, laufStart = 0, besteStart = 0;
                    for (int k = 0; k < n; k++)
                    {
                        if (!belegt[k]) { if (lauf == 0) laufStart = k; lauf++; if (lauf > beste) { beste = lauf; besteStart = laufStart; } }
                        else lauf = 0;
                    }
                    float frei = beste * schritt;
                    float naechste = 99f;
                    if (frei >= 0.5f)
                    {
                        float t0 = besteStart * schritt, t1 = t0 + frei;
                        foreach (var s in nahe)
                        {
                            if (s.verts == null) continue;
                            for (int i = 0; i < s.verts.Length; i++)
                            {
                                Vector3 d = s.verts[i] - a0;
                                float t = Vector3.Dot(d, axis);
                                if (t < t0 || t > t1) continue;
                                float q = (d - axis * t).magnitude;
                                if (q < naechste) naechste = q;
                            }
                        }
                    }
                    if (frei >= 0.5f)
                        funde.Add(new Fund
                        {
                            frei = frei,
                            text = "seed" + seed + " frei=" + frei.ToString("F2") + " m von " + laenge.ToString("F2") + " m, r=" + radius.ToString("F2") + " naechsteGeometrie=" + naechste.ToString("F2") + " m"
                                + " ab " + (besteStart * schritt).ToString("F2") + " m, " + pfad + " pos=" + mitte.ToString("F1")
                        });
                }
            }

            funde.Sort((x, y) => y.frei.CompareTo(x.frei));
            var sb = new StringBuilder();
            sb.AppendLine("Kapseln geprueft: " + kapseln + ", mit >=0,5 m leerer Strecke: " + funde.Count + ", Collider ohne Geometrie in 0,6 m Umkreis: " + ohneSicht.Count);
            var gruppen = new Dictionary<string, int>();
            foreach (var f in funde)
            {
                string kurz = "?";
                foreach (var t in f.text.Split(' ')) if (t.Contains("/")) { kurz = t.Substring(0, t.IndexOf('/')); break; }
                gruppen[kurz] = gruppen.TryGetValue(kurz, out var c) ? c + 1 : 1;
            }
            sb.AppendLine("-- Kapseln mit Leerstrecke nach Wurzelobjekt");
            foreach (var kv in gruppen) sb.AppendLine("  " + kv.Key + ": " + kv.Value);
            var og = new Dictionary<string, int>();
            foreach (var o in ohneSicht)
            {
                string kurz = "?";
                foreach (var t in o.Split(' ')) if (t.Contains("/")) { kurz = t.Substring(0, t.IndexOf('/')); break; }
                og[kurz] = og.TryGetValue(kurz, out var c) ? c + 1 : 1;
            }
            sb.AppendLine("-- Collider ohne Geometrie nach Wurzelobjekt");
            foreach (var kv in og) sb.AppendLine("  " + kv.Key + ": " + kv.Value);
            sb.AppendLine("-- Top 6 leere Kapselstrecken je Wurzelobjekt");
            var gez = new Dictionary<string, int>();
            foreach (var f in funde)
            {
                string kurz = "?";
                foreach (var t in f.text.Split(' ')) if (t.Contains("/")) { kurz = t.Substring(0, t.IndexOf('/')); break; }
                gez[kurz] = gez.TryGetValue(kurz, out var c) ? c + 1 : 1;
                if (gez[kurz] <= 6) sb.AppendLine("  " + f.text);
            }
            dicke.Sort((x, y) => y.frei.CompareTo(x.frei));
            sb.AppendLine("-- UNSICHTBARE DICKE: Kapsel breiter als sichtbarer Stamm (>=0,4 m): " + dicke.Count);
            var dg = new Dictionary<string, int>();
            foreach (var f in dicke)
            {
                string kurz = "?";
                foreach (var t in f.text.Split(' ')) if (t.Contains("/")) { kurz = t.Substring(0, t.IndexOf('/')); break; }
                dg[kurz] = dg.TryGetValue(kurz, out var c) ? c + 1 : 1;
                if (dg[kurz] <= 6) sb.AppendLine("  " + f.text);
            }
            foreach (var kv in dg) sb.AppendLine("  Gruppe " + kv.Key + ": " + kv.Value);
            sb.AppendLine("-- Collider ohne Geometrie (bis 40)");
            for (int i = 0; i < Mathf.Min(40, ohneSicht.Count); i++) sb.AppendLine("  " + ohneSicht[i]);

            string dir = Path.Combine(Application.dataPath, "..", "Logs", "ci");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "unsichtbare_wand.txt"), sb.ToString());
            Debug.Log("[UnsichtbareWand]\n" + sb);
        }

        private static string Pfad(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
