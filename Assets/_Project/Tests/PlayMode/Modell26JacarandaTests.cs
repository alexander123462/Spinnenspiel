using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL26 Jacaranda: Beweisbilder in der echten Welt (Ferne, Stamm, Blick in die Krone) und Messwerte.
    /// Ausgabe: Logs/modell/Baum_Jacaranda/welt_*.png (Ordner per Umgebungsvariable JAC_AUSGABE, Standard "nachher").</summary>
    public class Modell26JacarandaTests
    {
        private const int W = 1280, H = 720;

        private static JungleWorldGenerator _gen;

        private static IEnumerator LadeWelt(int seed)
        {
            JungleWorldGenerator.NextSeed = seed;
            SceneManager.LoadScene("Urwald");
            yield return null;
            float timeout = 60f;
            _gen = null;
            while (timeout > 0f && _gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) _gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(_gen, "Welt nicht fertig.");
            for (int i = 0; i < 90; i++) yield return null;
        }

        private static List<Transform> FindeJacarandas()
        {
            var list = new List<Transform>();
            Mesh lod0 = _gen.library.phTreeLod0[0];
            foreach (var t in _gen.Trees)
            {
                if (t == null) continue;
                var n = t.Find("TreeLOD0");
                var mf = n != null ? n.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh == lod0) list.Add(t);
            }
            return list;
        }

        [UnityTest]
        public IEnumerator Jacaranda_Beweisbilder()
        {
            string ausgabe = System.Environment.GetEnvironmentVariable("JAC_AUSGABE");
            if (string.IsNullOrWhiteSpace(ausgabe)) ausgabe = "nachher";
            string dir = "Logs/modell/Baum_Jacaranda/" + ausgabe;
            System.IO.Directory.CreateDirectory(dir);

            Transform baum = null;
            foreach (int seed in new[] { 4242, 7, 5, 99, 4243 })
            {
                yield return LadeWelt(seed);
                var js = FindeJacarandas();
                Debug.Log("[Jac] Seed " + seed + ": " + js.Count + " Jacaranda-Instanzen von " + _gen.Trees.Count + " Baeumen");
                if (js.Count > 0) { baum = js[0]; break; }
            }
            Assert.IsNotNull(baum, "Keine Jacaranda in den Test-Seeds.");

            var lod0 = _gen.library.phTreeLod0[0];
            Debug.Log("[Jac] Mesh " + lod0.name + " Hoehe " + lod0.bounds.size.y.ToString("F2") + " Scale " + baum.localScale.x.ToString("F2")
                      + " Weltgroesse " + (lod0.bounds.size * baum.localScale.x).ToString("F1") + " Verts " + lod0.vertexCount + " Submeshes " + lod0.subMeshCount);

            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f);
            Q1ObjektGalerieTests.DisableDepthAndMotionBlur();
            Q1ObjektGalerieTests.AddOverrideVolume();
            Q1ObjektGalerieTests.SetFog(true);
            Camera cam = Camera.main;
            Q1ObjektGalerieTests.NoTemporalAA(cam);
            var spiderCam = cam.GetComponent<SpiderCamera>();
            if (spiderCam != null) spiderCam.enabled = false;
            cam.nearClipPlane = 0.2f; cam.farClipPlane = 400f;
            for (int i = 0; i < 60; i++) yield return null;

            Vector3 p = baum.position;
            float s = baum.localScale.x;
            Vector3 mitte = p + Vector3.up * 13f;
            Vector3 weg = new Vector3(0.6f, 0f, -0.8f).normalized;

            yield return Aufnahme(cam, p + weg * 62f + Vector3.up * 3f, mitte, dir + "/welt_fern.png");
            yield return Aufnahme(cam, p + weg * 11f + Vector3.up * 2.2f, p + Vector3.up * 4.5f, dir + "/welt_stamm.png");
            yield return Aufnahme(cam, p + weg * 4f + Vector3.up * 1.2f, p + Vector3.up * 16f, dir + "/welt_krone.png");
            yield return Aufnahme(cam, p + weg * 28f + Vector3.up * 14f, mitte, dir + "/welt_schraeg.png");
            Assert.Greater(lod0.bounds.size.y * s, 24f, "Jacaranda zu klein.");
            Assert.Less(lod0.bounds.size.y * s, 36f, "Jacaranda zu gross.");
        }

        private static IEnumerator Aufnahme(Camera cam, Vector3 pos, Vector3 ziel, string datei)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(ziel);
            yield return null; yield return null; yield return null;
            Vector4 st = UrwaldTests.SaveFrameHdr(cam, W, H, datei);
            for (int warm = 0; warm < 15 && st.y - st.x < 1f; warm++)
            {
                yield return new WaitForSecondsRealtime(1f);
                yield return null;
                st = UrwaldTests.SaveFrameHdr(cam, W, H, datei);
            }
            Debug.Log("[Jac] " + datei + " mittel=" + st.x.ToString("F0") + " max=" + st.y.ToString("F0"));
        }
    }
}
