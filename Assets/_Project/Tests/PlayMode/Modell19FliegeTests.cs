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
    /// MODELL19: Die Fliege kommt aus dem Blender-Neubau (Fliege, real 8 mm Koerper,
    /// in Spielskalierung ~4.5x): Kopf mit Facettenaugen, Thorax, segmentierter
    /// Hinterleib, 2 Fluegel mit Adern, 6 Beine. Prueft: Dreiecke 1500..3000,
    /// Slots Koerper/Auge/Fluegel (Materialien nach Namen), Kinder Wing_L/Wing_R
    /// (Fluegelschlag), Koerper-Laenge fuer die Muecken-Verhaeltnisse, keine
    /// Collider, Albedo-Textur am Koerper-Material.
    /// </summary>
    public class Modell19FliegeTests
    {
        private const string BodyMat = "MAT_FlyBody";
        private const string EyeMat = "MAT_FlyEye";
        private const string WingMat = "MAT_FlyWing";

        // Welt-Ausdehnung entlang einer Achse (0=x,1=y,2=z) ueber alle Mesh-Filter
        // des Objekts, gerechnet an Mesh-Bounds x Transform (funktioniert auch an
        // inaktiven Vorlagen, ohne Renderer-Bounds-Staleheit).
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
        public IEnumerator Modell19_FliegeMeshSlotsUndFluegel()
        {
            JungleWorldGenerator gen = null;
            yield return LoadReadyWorld(g => gen = g);

            InsectSpawner[] spawner = Object.FindObjectsByType<InsectSpawner>(FindObjectsSortMode.None);
            GameObject fly = null;
            foreach (var s in spawner)
            {
                // Der Fliegen-Spawner heisst im Builder exakt 'Vorlage' (Motte: 'Vorlage_Motte').
                if (s.template != null && s.template.name == "Vorlage") fly = s.template;
            }
            Assert.IsNotNull(fly, "Fliegen-Vorlage fehlt");

            // Kinder mit festen Namen (Fluegelschlag sucht Wing_L/Wing_R direkt).
            Transform koerper = fly.transform.Find("Koerper");
            Transform kopf = fly.transform.Find("Kopf");
            Transform wingL = fly.transform.Find("Wing_L");
            Transform wingR = fly.transform.Find("Wing_R");
            bool namesOk = koerper != null && kopf != null && wingL != null && wingR != null;
            Debug.Log("[MODELL19] Kinder Koerper/Kopf/Wing_L/Wing_R: "
                      + (koerper != null) + "/" + (kopf != null) + "/"
                      + (wingL != null) + "/" + (wingR != null)
                      + " -> alle da -> " + (namesOk ? "OK" : "NOK"));
            Assert.IsTrue(namesOk, "MODELL19: feste Kindernamen fehlen");

            // Dreiecke ueber alle Mesh-Filter der Vorlage.
            var filters = fly.GetComponentsInChildren<MeshFilter>();
            int tris = 0;
            var matNames = new HashSet<string>();
            Material bodyMat = null;
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    tris += mf.sharedMesh.GetSubMesh(s).indexCount / 3;
                var mr = mf.GetComponent<MeshRenderer>();
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null) continue;
                    matNames.Add(m.name);
                    if (m.name == BodyMat) bodyMat = m;
                }
            }
            bool trisOk = tris >= 1500 && tris <= 3000;
            Debug.Log("[MODELL19] Dreiecke " + tris + " -> 1500..3000 -> " + (trisOk ? "OK" : "NOK"));

            bool matsOk = matNames.Contains(BodyMat) && matNames.Contains(EyeMat) && matNames.Contains(WingMat);
            Debug.Log("[MODELL19] Materialien " + string.Join(",", matNames)
                      + " -> Koerper/Auge/Fluegel -> " + (matsOk ? "OK" : "NOK"));

            // Slot-Reihenfolge am Kopf: MAT_FlyBody vor MAT_FlyEye (Slot 0 = Koerper).
            var kopfMr = kopf.GetComponent<MeshRenderer>();
            Mesh kopfMesh = kopf.GetComponent<MeshFilter>() != null ? kopf.GetComponent<MeshFilter>().sharedMesh : null;
            Debug.Log("[MODELL19] Kopf: MR=" + (kopfMr != null)
                      + " Submeshes=" + (kopfMesh != null ? kopfMesh.subMeshCount : -1)
                      + " Mats=" + (kopfMr != null && kopfMr.sharedMaterials != null ? kopfMr.sharedMaterials.Length : -1)
                      + " [0]=" + (kopfMr != null && kopfMr.sharedMaterials != null && kopfMr.sharedMaterials.Length > 0 && kopfMr.sharedMaterials[0] != null ? kopfMr.sharedMaterials[0].name : "-")
                      + " [1]=" + (kopfMr != null && kopfMr.sharedMaterials != null && kopfMr.sharedMaterials.Length > 1 && kopfMr.sharedMaterials[1] != null ? kopfMr.sharedMaterials[1].name : "-"));
            bool slotOrderOk = kopfMr != null && kopfMr.sharedMaterials != null
                               && kopfMr.sharedMaterials.Length >= 2
                               && kopfMr.sharedMaterials[0] != null && kopfMr.sharedMaterials[0].name == BodyMat
                               && kopfMr.sharedMaterials[1] != null && kopfMr.sharedMaterials[1].name == EyeMat;
            Debug.Log("[MODELL19] Kopf-Slots 0=Koerper 1=Auge -> " + (slotOrderOk ? "OK" : "NOK"));

            // Koerper-Laenge (Thorax+Hinterleib): Muecke davon 50..70 % (InsectTests-Kriterium).
            float bodyZ = AxisExtent(koerper.gameObject, 2);
            bool bodyZOk = bodyZ >= 0.030f && bodyZ <= 0.043f;
            Debug.Log("[MODELL19] Koerper-Z " + bodyZ.ToString("F4")
                      + " m -> 0.030..0.043 (Muecke 0.0216 = 50..70%) -> " + (bodyZOk ? "OK" : "NOK"));

            // Ganze Fliege: Laenge und Spannweite (Original ~0.044 m lang).
            float len = AxisExtent(fly, 2);
            float spanX = AxisExtent(fly, 0);
            bool lenOk = len >= 0.035f && len <= 0.075f;
            bool spanOk = spanX >= 0.025f;
            Debug.Log("[MODELL19] Laenge " + len.ToString("F4") + " m -> 0.035..0.075 -> " + (lenOk ? "OK" : "NOK")
                      + ", Spannweite " + spanX.ToString("F4") + " -> >=0.025 -> " + (spanOk ? "OK" : "NOK"));

            // Keine Collider (Insekten-Regel) und Albedo-Textur am Koerper-Material.
            int colliders = fly.GetComponentsInChildren<Collider>().Length;
            bool texOk = bodyMat != null && bodyMat.GetTexture("_BaseColorMap") != null;
            Debug.Log("[MODELL19] Collider " + colliders + " -> 0 -> " + (colliders == 0 ? "OK" : "NOK")
                      + ", Albedo an MAT_FlyBody -> " + (texOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(1500, 3000), "MODELL19: Dreiecke ausserhalb 1500..3000");
            Assert.IsTrue(matsOk, "MODELL19: Materialien Koerper/Auge/Fluegel fehlen");
            Assert.IsTrue(slotOrderOk, "MODELL19: Kopf-Slot-Reihenfolge falsch");
            Assert.That(bodyZ, Is.InRange(0.030f, 0.043f), "MODELL19: Koerper-Laenge ausserhalb 0.030..0.043");
            Assert.IsTrue(lenOk, "MODELL19: Fliegenlaenge ausserhalb 0.035..0.075 m");
            Assert.IsTrue(spanOk, "MODELL19: Spannweite zu klein");
            Assert.AreEqual(0, colliders, "MODELL19: Insekt hat Collider");
            Assert.IsTrue(texOk, "MODELL19: MAT_FlyBody ohne _BaseColorMap (Weiss-Falle)");
        }
    }
}
