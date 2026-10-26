using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL7: Der Farn_Tropisch kommt jetzt aus dem Blender-Neubau
    /// (MAT_FernTropisch: aufrechte Rosette aus 8 bogigen Wedeln mit je 25
    /// Fiederpaaren, 2 junge Krumme, Rhizom-Kegel). Prueft: Mesh vorhanden,
    /// Dreiecke im Bereich 1500..3000, ein Slot wie im Original, Hoehe
    /// 0.8..1.5 m, UV-Karten am 8x4-Zellen-Atlas, Fuss am Ursprung
    /// (kein Schweben/Verkehrtherum), genug Instanzen.
    /// </summary>
    public class Modell7FernTropischTests
    {
        private const string FarnMat = "MAT_FernTropisch";

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

        [UnityTest]
        public IEnumerator Modell7_FarnTropisch_MeshUndInstanzen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            Mesh mesh = gen != null && gen.library != null ? gen.library.fern : null;
            Assert.IsNotNull(mesh, "MODELL7: kein library.fern-Mesh in der Bibliothek.");
            Assert.AreEqual(FarnMat, gen.library.fernMat != null ? gen.library.fernMat.name : "null",
                "MODELL7: FernMaterial heisst nicht " + FarnMat + ".");

            int instances = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name == FarnMat) { instances++; break; }
            }
            Debug.Log("[MODELL7] Farn_Tropisch-Instanzen: " + instances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 1500 && tris <= 3000;
            bool slotOk = mesh.subMeshCount == 1;
            float height = mesh.bounds.size.y;
            float spread = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            bool heightOk = height >= 0.8f && height <= 1.5f;
            bool spreadOk = spread >= 0.4f && spread <= 1.6f;
            Debug.Log("[MODELL7] Dreiecke " + tris + " -> 1500..3000 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL7] Slots " + mesh.subMeshCount + " -> 1 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL7] Hoehe " + height.ToString("F3")
                      + " m -> 0.8..1.5 -> " + (heightOk ? "OK" : "NOK"));
            Debug.Log("[MODELL7] Ausdehnung " + spread.ToString("F3")
                      + " m -> 0.4..1.6 -> " + (spreadOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(1500, 3000), "MODELL7: Dreiecke ausserhalb 1500..3000");
            Assert.AreEqual(1, mesh.subMeshCount, "MODELL7: erwartet genau 1 Submesh/Slot wie im Original");
            Assert.That(height, Is.InRange(0.8f, 1.5f), "MODELL7: Hoehe ausserhalb 0.8..1.5 m");
            Assert.That(spread, Is.InRange(0.4f, 1.6f), "MODELL7: Ausdehnung ausserhalb 0.4..1.6 m");

            // UVs am 8x4-Zellen-Atlas (Zellen 0..2 Fiedern, Zelle 3 Spindel/
            // Rhizom). Alle Fiedern teilen sich drei Atlaszellen, deshalb pruefen
            // wir Deckung, Wertebereich 0..1 und genug eindeutige Koordinaten.
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
            bool islandsOk = uvOk && uniqueU >= 30;
            Debug.Log("[MODELL7] UVs " + uvs.Count + "/" + mesh.vertexCount
                      + ", eindeutig " + uniqueU + " -> >=30 -> " + (islandsOk ? "OK" : "NOK"));
            Assert.IsTrue(islandsOk, "MODELL7: UV-Atlas fehlt oder deckt die Fieder-Inseln nicht ab");

            // Kein Schweben/Verkehrtherum: der Asset-Fuss sitzt am Ursprung.
            bool footOk = mesh.bounds.min.y >= -0.05f && mesh.bounds.min.y <= 0.02f;
            Debug.Log("[MODELL7] Fuss y " + mesh.bounds.min.y.ToString("F3")
                      + " -> -0.05..0.02 -> " + (footOk ? "OK" : "NOK")
                      + ", Instanzen " + instances + " -> >300 -> " + (instances > 300 ? "OK" : "NOK"));
            Assert.IsTrue(footOk, "MODELL7: Farn_Tropisch-Asset hat Fuss nicht am Ursprung (Schweben/Verkehrtherum)");
            Assert.Greater(instances, 300, "MODELL7: zu wenige Farn_Tropisch-Instanzen in der Welt");
        }
    }
}
