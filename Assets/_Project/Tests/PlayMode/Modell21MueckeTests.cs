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
    /// MODELL21: Die Muecke kommt aus dem Blender-Neubau (muecke_geometrie.py +
    /// muecke_textur.py): segmentierter Thorax+Leib (~24.5 mm, davon 63 % der
    /// Fliegen-Koerperlaenge 39 mm), 6 lange gegliederte Beine, Stechruessel,
    /// plumose Antennen, zwei schmale Fluegel mit Schuppenfransen (2368 Dreiecke).
    /// Prueft: Dreiecke 1200..2500, festen Kindernamen, Materialien
    /// MAT_MosquitoBody/MAT_MosquitoWing mit Albedo, Koerper-Laenge und Ratio zur
    /// Fliege (50..70 %), keine Collider.
    /// </summary>
    public class Modell21MueckeTests
    {
        private const string BodyMat = "MAT_MosquitoBody";
        private const string WingMat = "MAT_MosquitoWing";

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
        public IEnumerator Modell21_MueckeMeshTeileUndProportionen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadReadyWorld(g => gen = g);

            InsectSpawner[] spawner = Object.FindObjectsByType<InsectSpawner>(FindObjectsSortMode.None);
            GameObject muecke = null, fly = null;
            foreach (var s in spawner)
            {
                if (s.mosquitoTemplate != null && s.mosquitoTemplate.name == "Vorlage_Muecke")
                {
                    muecke = s.mosquitoTemplate;
                    fly = s.template;
                }
            }
            Assert.IsNotNull(muecke, "Muecken-Vorlage fehlt");
            Assert.IsNotNull(fly, "Fliegen-Vorlage am selben Spawner fehlt");

            // Kinder mit festen Namen (Fluegelschlag sucht Wing_L/Wing_R direkt).
            Transform koerper = muecke.transform.Find("Koerper");
            Transform kopf = muecke.transform.Find("Kopf");
            Transform schnabel = muecke.transform.Find("Schnabel");
            Transform beine = muecke.transform.Find("Beine");
            Transform fuehlerL = muecke.transform.Find("Fuehler_L");
            Transform fuehlerR = muecke.transform.Find("Fuehler_R");
            Transform wingL = muecke.transform.Find("Wing_L");
            Transform wingR = muecke.transform.Find("Wing_R");
            bool namesOk = koerper != null && kopf != null && schnabel != null && beine != null
                           && fuehlerL != null && fuehlerR != null && wingL != null && wingR != null;
            Debug.Log("[MODELL21] Kinder Koerper/Kopf/Schnabel/Beine/Fuehler_L/Fuehler_R/"
                      + "Wing_L/Wing_R -> alle da -> " + (namesOk ? "OK" : "NOK"));
            Assert.IsTrue(namesOk, "MODELL21: feste Kindernamen fehlen");

            // Dreiecke ueber alle Mesh-Filter der Vorlage (Vorgabe 1200..2500).
            var filters = muecke.GetComponentsInChildren<MeshFilter>();
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
            bool trisOk = tris >= 1200 && tris <= 2500;
            Debug.Log("[MODELL21] Dreiecke " + tris + " -> 1200..2500 -> " + (trisOk ? "OK" : "NOK"));
            bool matsOk = matNames.Contains(BodyMat) && matNames.Contains(WingMat);
            Debug.Log("[MODELL21] Materialien " + string.Join(",", matNames)
                      + " -> Koerper/Fluegel -> " + (matsOk ? "OK" : "NOK"));

            // Slot-/Materialvergabe: Fluegel-Teile tragen MAT_MosquitoWing.
            var wingMr = wingL.GetComponent<MeshRenderer>();
            bool wingMatOk = wingMr != null && wingMr.sharedMaterials.Length > 0
                           && wingMr.sharedMaterials[0] != null && wingMr.sharedMaterials[0].name == WingMat;
            Debug.Log("[MODELL21] Wing_L-Material -> " + (wingMatOk ? "OK" : "NOK"));

            // Koerper-Laenge entlang Z (Blender-Y -> Unity-Z): Ziel ~0.0245 m.
            float bodyZ = Modell20MotteTests.AxisExtent(koerper.gameObject, 2);
            bool bodyZOk = bodyZ >= 0.019f && bodyZ <= 0.030f;
            Debug.Log("[MODELL21] Koerper-Z " + bodyZ.ToString("F4")
                      + " m -> 0.019..0.030 -> " + (bodyZOk ? "OK" : "NOK"));

            // Proportion zur Fliege: Muecken-Koerper muss 50..70 % der Fliege sein.
            Transform flyBody = fly.transform.Find("Koerper");
            Assert.IsNotNull(flyBody, "Fliegen-Koerper fehlt (Ratio-Check)");
            float flyZ = Modell20MotteTests.AxisExtent(flyBody.gameObject, 2);
            float ratio = flyZ > 0f ? bodyZ / flyZ : 0f;
            bool ratioOk = ratio >= 0.5f && ratio <= 0.7f;
            Debug.Log("[MODELL21] Koerper-Ratio zur Fliege " + ratio.ToString("F2")
                      + " (Fliege-Z " + flyZ.ToString("F4") + ") -> 0.50..0.70 -> "
                      + (ratioOk ? "OK" : "NOK"));

            // Stechruessel + Fuehler: eigene Meshes mit Geometrie (Sichtbarkeits-Merkmale).
            bool schnabelOk = schnabel.GetComponent<MeshFilter>() != null
                              && schnabel.GetComponent<MeshFilter>().sharedMesh != null
                              && schnabel.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0;
            bool fuehlerOk = fuehlerL.GetComponent<MeshFilter>() != null
                             && fuehlerR.GetComponent<MeshFilter>() != null
                             && fuehlerL.GetComponent<MeshFilter>().sharedMesh != null
                             && fuehlerL.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0;
            Debug.Log("[MODELL21] Schnabel-Mesh -> " + (schnabelOk ? "OK" : "NOK")
                      + ", Fuehler-Meshes -> " + (fuehlerOk ? "OK" : "NOK"));

            // Keine Collider (Insekten-Regel) und Albedo-Textur an beiden Materialien.
            int colliders = muecke.GetComponentsInChildren<Collider>().Length;
            bool texBodyOk = bodyMat != null && bodyMat.GetTexture("_BaseColorMap") != null;
            bool texWingOk = wingMat != null && wingMat.GetTexture("_BaseColorMap") != null;
            Debug.Log("[MODELL21] Collider " + colliders + " -> 0 -> "
                      + (colliders == 0 ? "OK" : "NOK")
                      + ", Albedo Koerper -> " + (texBodyOk ? "OK" : "NOK")
                      + ", Albedo Fluegel -> " + (texWingOk ? "OK" : "NOK"));

            Assert.IsTrue(namesOk, "MODELL21: feste Kindernamen fehlen");
            Assert.That(tris, Is.InRange(1200, 2500), "MODELL21: Dreiecke ausserhalb 1200..2500");
            Assert.IsTrue(matsOk, "MODELL21: Materialien Koerper/Fluegel fehlen");
            Assert.IsTrue(wingMatOk, "MODELL21: Wing_L traegt nicht MAT_MosquitoWing");
            Assert.That(bodyZ, Is.InRange(0.019f, 0.030f), "MODELL21: Koerper-Laenge ausserhalb 0.019..0.030 m");
            Assert.IsTrue(ratioOk, "MODELL21: Koerper-Ratio zur Fliege ausserhalb 0.50..0.70: " + ratio);
            Assert.IsTrue(schnabelOk, "MODELL21: Stechruessel-Mesh fehlt");
            Assert.IsTrue(fuehlerOk, "MODELL21: Fuehler-Meshes fehlen");
            Assert.AreEqual(0, colliders, "MODELL21: Insekt hat Collider");
            Assert.IsTrue(texBodyOk, "MODELL21: MAT_MosquitoBody ohne _BaseColorMap (Weiss-Falle)");
            Assert.IsTrue(texWingOk, "MODELL21: MAT_MosquitoWing ohne _BaseColorMap (Weiss-Falle)");
        }
    }
}
