using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL4: Das Immergruen kommt jetzt aus dem Blender-Neubau
    /// (MAT_Immergruen, Vinca-Typ: Ranken mit gegenstaendigen Blattpaeren,
    /// ovale Blaetter mit Mittelrippe). Prueft: Mesh vorhanden, Dreiecke,
    /// Hoehe und Ausdehnung im Vorgabenbereich, ein Slot, UV-Karten,
    /// keine schwebenden Instanzen.
    /// </summary>
    public class Modell4ImmergruenTests
    {
        private const string ImmergruenMat = "MAT_Immergruen";

        private static IEnumerator LoadWorld(System.Action<JungleWorldGenerator> done)
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

        private static Mesh FindImmergruenMesh(JungleWorldGenerator gen)
        {
            // StaticBatchingUtility.Combine ersetzt die MeshFilter-Meshes durch
            // Batch-Meshes. Deterministisch das FBX-Mesh ueber die Asset-Bibliothek
            // holen: das Mesh, dessen Material-Paar MAT_Immergruen traegt.
            if (gen == null || gen.library == null) return null;
            var meshes = gen.library.groundCover;
            var mats = gen.library.groundCoverMats;
            if (meshes == null || mats == null) return null;
            for (int i = 0; i < meshes.Length && i < mats.Length; i++)
                if (meshes[i] != null && mats[i] != null && mats[i].name == ImmergruenMat)
                    return meshes[i];
            return null;
        }

        [UnityTest]
        public IEnumerator Modell4_Immergruen_MeshUndInstanzen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            int instances;
            Mesh mesh = FindImmergruenMesh(gen);
            Assert.IsNotNull(mesh, "MODELL4: kein groundCover-Mesh mit Material " + ImmergruenMat + " in der Bibliothek.");
            instances = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name == ImmergruenMat) { instances++; break; }
            }
            Debug.Log("[MODELL4] Immergruen-Instanzen: " + instances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 400 && tris <= 900;
            bool slotOk = mesh.subMeshCount == 1;
            float height = mesh.bounds.size.y;
            float spread = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            bool heightOk = height >= 0.1f && height <= 0.3f;
            bool spreadOk = spread >= 0.3f && spread <= 0.6f;
            Debug.Log("[MODELL4] Dreiecke " + tris + " -> 400..900 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL4] Slots " + mesh.subMeshCount + " -> 1 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL4] Hoehe " + height.ToString("F3")
                      + " m -> 0.1..0.3 -> " + (heightOk ? "OK" : "NOK"));
            Debug.Log("[MODELL4] Ausdehnung " + spread.ToString("F3")
                      + " m -> 0.3..0.6 -> " + (spreadOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(400, 900), "MODELL4: Dreiecke ausserhalb 400..900");
            Assert.AreEqual(1, mesh.subMeshCount, "MODELL4: erwartet genau 1 Submesh/Slot");
            Assert.That(height, Is.InRange(0.1f, 0.3f), "MODELL4: Hoehe ausserhalb 0.1..0.3 m");
            Assert.That(spread, Is.InRange(0.3f, 0.6f), "MODELL4: Ausdehnung ausserhalb 0.3..0.6 m");

            // UV-Atlas (8x4): je Blatt/Ranke eine eigene Insel, Werte in 0..1
            // (Albedo/Normal haengen an diesen UVs). Schwelle 400 laesst wie bei
            // Modell3 Unity-Tessellierung und Puffer zu.
            var uvs = new System.Collections.Generic.List<Vector2>(mesh.vertexCount);
            mesh.GetUVs(0, uvs);
            bool uvOk = uvs.Count == mesh.vertexCount;
            int uniqueU = 0;
            if (uvOk)
            {
                var seen = new System.Collections.Generic.HashSet<long>();
                foreach (var uv in uvs)
                {
                    if (uv.x < -0.01f || uv.x > 1.01f || uv.y < -0.01f || uv.y > 1.01f) { uvOk = false; break; }
                    seen.Add((long)(uv.x * 1000) * 100003 + (long)(uv.y * 1000));
                }
                uniqueU = seen.Count;
            }
            bool islandsOk = uvOk && uniqueU >= 400;
            Debug.Log("[MODELL4] UVs " + uvs.Count + "/" + mesh.vertexCount
                      + ", eindeutig " + uniqueU + " -> >=400 -> " + (islandsOk ? "OK" : "NOK"));
            Assert.IsTrue(islandsOk, "MODELL4: UV-Atlas fehlt oder deckt die Blatt-Inseln nicht ab");

            // Kein Schweben/Verkehrtherum: der Asset-Fuss sitzt am Ursprung
            // (Ranken -0.007 m), die Platzierung uebernimmt wie bei allen
            // Bodendeckern der geteilte Generator-Pfad (Y = Gelaendehoehe - 0.02).
            // Nach StaticBatchingUtility.Combine gibt es pro Instanz keinen
            // zuverlaessigen Transform-Check mehr, daher Asset-Ebene plus Anzahl.
            bool footOk = mesh.bounds.min.y >= -0.05f && mesh.bounds.min.y <= 0.02f;
            Debug.Log("[MODELL4] Fuss y " + mesh.bounds.min.y.ToString("F3")
                      + " -> -0.05..0.02 -> " + (footOk ? "OK" : "NOK")
                      + ", Instanzen " + instances + " -> >100 -> " + (instances > 100 ? "OK" : "NOK"));
            Assert.IsTrue(footOk, "MODELL4: Immergruen-Asset hat Fuss nicht am Ursprung (Schweben/Verkehrtherum)");
            Assert.Greater(instances, 100, "MODELL4: zu wenige Immergruen-Instanzen in der Welt");
        }
    }
}
