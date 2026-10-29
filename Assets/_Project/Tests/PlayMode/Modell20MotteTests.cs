using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL20: Die Motte kommt aus dem Blender-Neubau (motte_geometrie.py +
    /// motte_textur.py, Spielskalierung ~4.5x wie die Fliege): pelziger dreiteiliger
    /// Koerper (real 15 mm), gefiederte Kammfuehler, 6 Beine, zwei Fluegel mit
    /// Schuppenmuster und Fransensaum (Spannweite real 30 mm). Prueft: Dreiecke
    /// 1500..3500, festen Kindernamen (Fluegelschlag Wing_L/Wing_R), Materialien
    /// MAT_MothBody/MAT_MothWing mit Albedo-Textur, Koerper-Laenge und Spannweite,
    /// keine Collider.
    /// </summary>
    public class Modell20MotteTests
    {
        private const string BodyMat = "MAT_MothBody";
        private const string WingMat = "MAT_MothWing";

        // Welt-Ausdehnung entlang einer Achse (0=x,1=y,2=z) ueber alle Mesh-Filter
        // des Objekts, gerechnet an Mesh-Bounds x Transform (wie Modell19).
        internal static float AxisExtent(GameObject root, int axis)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 w = m.MultiplyPoint3x4(c);
                    float v = axis == 0 ? w.x : (axis == 1 ? w.y : w.z);
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            return max == float.MinValue ? 0f : max - min;
        }

        private static IEnumerator LoadReadyWorld(System.Action<JungleWorldGenerator> done)
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            float timeout = 60f;
            JungleWorldGenerator gen = null;
            while (timeout > 0f && gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(gen, "Welt nicht fertig.");
            done(gen);
        }

        [UnityTest]
        public IEnumerator Modell20_MotteMeshTeileUndFluegel()
        {
            JungleWorldGenerator gen = null;
            yield return LoadReadyWorld(g => gen = g);

            InsectSpawner[] spawner = Object.FindObjectsByType<InsectSpawner>(FindObjectsSortMode.None);
            GameObject moth = null;
            foreach (var s in spawner)
            {
                if (s.template != null && s.template.name == "Vorlage_Motte") moth = s.template;
            }
            Assert.IsNotNull(moth, "Motten-Vorlage fehlt");

            // Kinder mit festen Namen (Fluegelschlag sucht Wing_L/Wing_R direkt).
            Transform koerper = moth.transform.Find("Koerper");
            Transform fuehlerL = moth.transform.Find("Fuehler_L");
            Transform fuehlerR = moth.transform.Find("Fuehler_R");
            Transform beine = moth.transform.Find("Beine");
            Transform wingL = moth.transform.Find("Wing_L");
            Transform wingR = moth.transform.Find("Wing_R");
            bool namesOk = koerper != null && fuehlerL != null && fuehlerR != null
                           && beine != null && wingL != null && wingR != null;
            Debug.Log("[MODELL20] Kinder Koerper/Fuehler_L/Fuehler_R/Beine/Wing_L/Wing_R: "
                      + (koerper != null) + "/" + (fuehlerL != null) + "/" + (fuehlerR != null)
                      + "/" + (beine != null) + "/" + (wingL != null) + "/" + (wingR != null)
                      + " -> alle da -> " + (namesOk ? "OK" : "NOK"));
            Assert.IsTrue(namesOk, "MODELL20: feste Kindernamen fehlen");

            // Dreiecke ueber alle Mesh-Filter der Vorlage (Vorgabe 1500..3500).
            var filters = moth.GetComponentsInChildren<MeshFilter>();
            int tris = 0;
            var matNames = new HashSet<string>();
            Material bodyMat = null, wingMat = null;
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                for (int s2 = 0; s2 < mf.sharedMesh.subMeshCount; s2++)
                    tris += mf.sharedMesh.GetSubMesh(s2).indexCount / 3;
                var mr = mf.GetComponent<MeshRenderer>();
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null) continue;
                    matNames.Add(m.name);
                    if (m.name == BodyMat) bodyMat = m;
                    if (m.name == WingMat) wingMat = m;
                }
            }
            bool trisOk = tris >= 1500 && tris <= 3500;
            Debug.Log("[MODELL20] Dreiecke " + tris + " -> 1500..3500 -> " + (trisOk ? "OK" : "NOK"));
            bool matsOk = matNames.Contains(BodyMat) && matNames.Contains(WingMat);
            Debug.Log("[MODELL20] Materialien " + string.Join(",", matNames)
                      + " -> Koerper/Fluegel -> " + (matsOk ? "OK" : "NOK"));

            // Slot-/Materialvergabe: Fluegel-Teile tragen MAT_MothWing.
            var wingMr = wingL.GetComponent<MeshRenderer>();
            bool wingMatOk = wingMr != null && wingMr.sharedMaterials.Length > 0
                           && wingMr.sharedMaterials[0] != null && wingMr.sharedMaterials[0].name == WingMat;
            Debug.Log("[MODELL20] Wing_L-Material -> " + (wingMatOk ? "OK" : "NOK"));

            // Koerper-Laenge entlang Z (Blender-Y -> Unity-Z): Ziel ~0.076 m (real 15 mm x ~4.5).
            float bodyZ = AxisExtent(koerper.gameObject, 2);
            bool bodyZOk = bodyZ >= 0.060f && bodyZ <= 0.090f;
            Debug.Log("[MODELL20] Koerper-Z " + bodyZ.ToString("F4")
                      + " m -> 0.060..0.090 -> " + (bodyZOk ? "OK" : "NOK"));

            // Ganze Motte: Laenge und Spannweite (Spannweite real 30 mm x ~4.5 = ~0.135).
            float len = AxisExtent(moth, 2);
            float spanX = AxisExtent(moth, 0);
            bool lenOk = len >= 0.085f && len <= 0.120f;
            bool spanOk = spanX >= 0.120f && spanX <= 0.150f;
            Debug.Log("[MODELL20] Laenge " + len.ToString("F4") + " m -> 0.085..0.120 -> "
                      + (lenOk ? "OK" : "NOK") + ", Spannweite " + spanX.ToString("F4")
                      + " -> 0.120..0.150 -> " + (spanOk ? "OK" : "NOK"));

            // Gefiederte Fuehler: eigener Mesh-Knoten je Seite mit Dreiecken.
            bool fuehlerOk = fuehlerL.GetComponent<MeshFilter>() != null
                             && fuehlerR.GetComponent<MeshFilter>() != null
                             && fuehlerL.GetComponent<MeshFilter>().sharedMesh != null
                             && fuehlerL.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0;
            Debug.Log("[MODELL20] Fuehler-Meshes da -> " + (fuehlerOk ? "OK" : "NOK"));

            // Keine Collider (Insekten-Regel) und Albedo-Textur an beiden Materialien.
            int colliders = moth.GetComponentsInChildren<Collider>().Length;
            bool texBodyOk = bodyMat != null && bodyMat.GetTexture("_BaseColorMap") != null;
            bool texWingOk = wingMat != null && wingMat.GetTexture("_BaseColorMap") != null;
            Debug.Log("[MODELL20] Collider " + colliders + " -> 0 -> "
                      + (colliders == 0 ? "OK" : "NOK")
                      + ", Albedo Koerper -> " + (texBodyOk ? "OK" : "NOK")
                      + ", Albedo Fluegel -> " + (texWingOk ? "OK" : "NOK"));

            Assert.IsTrue(namesOk, "MODELL20: feste Kindernamen fehlen");
            Assert.That(tris, Is.InRange(1500, 3500), "MODELL20: Dreiecke ausserhalb 1500..3500");
            Assert.IsTrue(matsOk, "MODELL20: Materialien Koerper/Fluegel fehlen");
            Assert.IsTrue(wingMatOk, "MODELL20: Wing_L traegt nicht MAT_MothWing");
            Assert.That(bodyZ, Is.InRange(0.060f, 0.090f), "MODELL20: Koerper-Laenge ausserhalb 0.060..0.090 m");
            Assert.IsTrue(lenOk, "MODELL20: Mottenlaenge ausserhalb 0.085..0.120 m");
            Assert.IsTrue(spanOk, "MODELL20: Spannweite ausserhalb 0.120..0.150 m");
            Assert.AreEqual(0, colliders, "MODELL20: Insekt hat Collider");
            Assert.IsTrue(texBodyOk, "MODELL20: MAT_MothBody ohne _BaseColorMap (Weiss-Falle)");
            Assert.IsTrue(texWingOk, "MODELL20: MAT_MothWing ohne _BaseColorMap (Weiss-Falle)");
        }
    }
}
