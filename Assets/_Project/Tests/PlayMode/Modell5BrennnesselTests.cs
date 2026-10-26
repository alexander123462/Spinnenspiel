using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL5: Die Brennnessel kommt jetzt aus dem Blender-Neubau
    /// (MAT_Brennnessel: 4-kantiger Stengel, 5 gegenstaendige Blattpaare +
    /// Endpaar, gezaeckte herzfoermige Spreiten). Prueft: Mesh vorhanden,
    /// Dreiecke, Hoehe im Vorgabenbereich 0.5..1.0 m, ein Slot, UV-Karten,
    /// Fuss am Ursprung (kein Schweben/Verkehrtherum), genug Instanzen.
    /// </summary>
    public class Modell5BrennnesselTests
    {
        private const string BrennnesselMat = "MAT_Brennnessel";

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

        private static Mesh FindBrennnesselMesh(JungleWorldGenerator gen)
        {
            // StaticBatchingUtility.Combine ersetzt die MeshFilter-Meshes durch
            // Batch-Meshes. Deterministisch das FBX-Mesh ueber die Asset-Bibliothek
            // holen: das Mesh, dessen Material-Paar MAT_Brennnessel traegt.
            if (gen == null || gen.library == null) return null;
            var meshes = gen.library.groundCover;
            var mats = gen.library.groundCoverMats;
            if (meshes == null || mats == null) return null;
            for (int i = 0; i < meshes.Length && i < mats.Length; i++)
                if (meshes[i] != null && mats[i] != null && mats[i].name == BrennnesselMat)
                    return meshes[i];
            return null;
        }

        [UnityTest]
        public IEnumerator Modell5_Brennnessel_MeshUndInstanzen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            Mesh mesh = FindBrennnesselMesh(gen);
            Assert.IsNotNull(mesh, "MODELL5: kein groundCover-Mesh mit Material " + BrennnesselMat + " in der Bibliothek.");
            int instances = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name == BrennnesselMat) { instances++; break; }
            }
            Debug.Log("[MODELL5] Brennnessel-Instanzen: " + instances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 600 && tris <= 1200;
            bool slotOk = mesh.subMeshCount == 1;
            float height = mesh.bounds.size.y;
            float spread = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            bool heightOk = height >= 0.5f && height <= 1.0f;
            bool spreadOk = spread >= 0.25f && spread <= 0.8f;
            Debug.Log("[MODELL5] Dreiecke " + tris + " -> 600..1200 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL5] Slots " + mesh.subMeshCount + " -> 1 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL5] Hoehe " + height.ToString("F3")
                      + " m -> 0.5..1.0 -> " + (heightOk ? "OK" : "NOK"));
            Debug.Log("[MODELL5] Ausdehnung " + spread.ToString("F3")
                      + " m -> 0.25..0.8 -> " + (spreadOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(600, 1200), "MODELL5: Dreiecke ausserhalb 600..1200");
            Assert.AreEqual(1, mesh.subMeshCount, "MODELL5: erwartet genau 1 Submesh/Slot");
            Assert.That(height, Is.InRange(0.5f, 1.0f), "MODELL5: Hoehe ausserhalb 0.5..1.0 m");
            Assert.That(spread, Is.InRange(0.25f, 0.8f), "MODELL5: Ausdehnung ausserhalb 0.25..0.8 m");

            // UV-Atlas (8x4): je Blatt/Stiel/Stengel eine eigene Insel, Werte
            // in 0..1 (Albedo/Normal haengen an diesen UVs). Schwelle 400 laesst
            // wie bei Modell3/4 Unity-Tessellierung und Puffer zu.
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
            Debug.Log("[MODELL5] UVs " + uvs.Count + "/" + mesh.vertexCount
                      + ", eindeutig " + uniqueU + " -> >=400 -> " + (islandsOk ? "OK" : "NOK"));
            Assert.IsTrue(islandsOk, "MODELL5: UV-Atlas fehlt oder deckt die Blatt-Inseln nicht ab");

            // Kein Schweben/Verkehrtherum: der Asset-Fuss sitzt am Ursprung,
            // die Platzierung uebernimmt der geteilte Generator-Pfad.
            bool footOk = mesh.bounds.min.y >= -0.05f && mesh.bounds.min.y <= 0.02f;
            Debug.Log("[MODELL5] Fuss y " + mesh.bounds.min.y.ToString("F3")
                      + " -> -0.05..0.02 -> " + (footOk ? "OK" : "NOK")
                      + ", Instanzen " + instances + " -> >300 -> " + (instances > 300 ? "OK" : "NOK"));
            Assert.IsTrue(footOk, "MODELL5: Brennnessel-Asset hat Fuss nicht am Ursprung (Schweben/Verkehrtherum)");
            Assert.Greater(instances, 300, "MODELL5: zu wenige Brennnessel-Instanzen in der Welt");
        }
    }
}
