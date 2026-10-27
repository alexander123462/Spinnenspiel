using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL9: Busch2 kommt jetzt aus dem Blender-Neubau (MAT_Busch2_Leaf/Branch:
    /// 6 Haupttriebe mit Sekundaer-/Tertiaerzweigen, 150 ovale Blaetter als echte
    /// Geometrie, 5552 Dreiecke, 2 Slots) und ersetzt den Scan shrub_03 als
    /// Busche-Variante 0. Prueft: Variante vorhanden, Dreiecke 3000..6000,
    /// zwei Submeshes, Hoehe 0.8..1.4 m, UV-Atlas, Fuss am Ursprung
    /// (kein Schweben/Verkehrtherum), genug Instanzen mit Blatt+Ast-Material.
    /// </summary>
    public class Modell9Busch2Tests
    {
        private const string LeafMat = "MAT_Busch2_Leaf";
        private const string BranchMat = "MAT_Busch2_Branch";

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
        public IEnumerator Modell9_Busch2_MeshUndInstanzen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            Mesh[] variants = gen != null && gen.library != null ? gen.library.bushVariants : null;
            Assert.IsNotNull(variants, "MODELL9: keine Busche-Varianten in der Bibliothek.");
            Assert.Greater(variants.Length, 0, "MODELL9: Busche-Variantenliste ist leer.");
            Mesh mesh = variants[0];
            Assert.IsNotNull(mesh, "MODELL9: Busch2-Variante 0 hat kein Mesh.");
            Material leafMat = gen.library.bushVariantMats != null && gen.library.bushVariantMats.Length > 0
                ? gen.library.bushVariantMats[0] : null;
            Assert.AreEqual(LeafMat, leafMat != null ? leafMat.name : "null",
                "MODELL9: Busch2-Blattmaterial heisst nicht " + LeafMat + ".");
            Assert.AreEqual(BranchMat, gen.library.busch2BranchMat != null ? gen.library.busch2BranchMat.name : "null",
                "MODELL9: Busch2-Astmaterial heisst nicht " + BranchMat + ".");

            int leafInstances = 0, bothInstances = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool leaf = false, branch = false;
                foreach (var m in mr.sharedMaterials)
                {
                    if (m != null && m.name == LeafMat) leaf = true;
                    if (m != null && m.name == BranchMat) branch = true;
                }
                if (leaf) leafInstances++;
                if (leaf && branch) bothInstances++;
            }
            Debug.Log("[MODELL9] Busch2-Instanzen (Blatt): " + leafInstances
                      + ", mit Ast+Blatt: " + bothInstances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 3000 && tris <= 6000;
            bool slotOk = mesh.subMeshCount == 2;
            float height = mesh.bounds.size.y;
            float spread = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            bool heightOk = height >= 0.8f && height <= 1.4f;
            bool spreadOk = spread >= 0.5f && spread <= 1.6f;
            Debug.Log("[MODELL9] Dreiecke " + tris + " -> 3000..6000 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL9] Slots " + mesh.subMeshCount + " -> 2 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL9] Hoehe " + height.ToString("F3")
                      + " m -> 0.8..1.4 -> " + (heightOk ? "OK" : "NOK"));
            Debug.Log("[MODELL9] Ausdehnung " + spread.ToString("F3")
                      + " m -> 0.5..1.6 -> " + (spreadOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(3000, 6000), "MODELL9: Dreiecke ausserhalb 3000..6000");
            Assert.AreEqual(2, mesh.subMeshCount, "MODELL9: erwartet genau 2 Submeshes/Slots (Aeste, Blaetter)");
            Assert.That(height, Is.InRange(0.8f, 1.4f), "MODELL9: Hoehe ausserhalb 0.8..1.4 m");
            Assert.That(spread, Is.InRange(0.5f, 1.6f), "MODELL9: Ausdehnung ausserhalb 0.5..1.6 m");

            // UVs am 3-Zellen-Atlas (Blatt) bzw. Rindenband (Aeste): Wertebereich
            // 0..1, genug eindeutige Koordinaten fuer 150 Blatter + 50 Zweige.
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
            Debug.Log("[MODELL9] UVs " + uvs.Count + "/" + mesh.vertexCount
                      + ", eindeutig " + uniqueU + " -> >=30 -> " + (islandsOk ? "OK" : "NOK"));
            Assert.IsTrue(islandsOk, "MODELL9: UV-Atlas fehlt oder deckt die Blatt-Inseln nicht ab");

            // Kein Schweben/Verkehrtherum: der Asset-Fuss sitzt am Ursprung.
            bool footOk = mesh.bounds.min.y >= -0.05f && mesh.bounds.min.y <= 0.02f;
            Debug.Log("[MODELL9] Fuss y " + mesh.bounds.min.y.ToString("F3")
                      + " -> -0.05..0.02 -> " + (footOk ? "OK" : "NOK"));
            Assert.IsTrue(footOk, "MODELL9: Busch2-Asset hat Fuss nicht am Ursprung (Schweben/Verkehrtherum)");
            Assert.Greater(leafInstances, 100, "MODELL9: zu wenige Busch2-Instanzen in der Welt");
            Assert.Greater(bothInstances, 100, "MODELL9: Busch2-Instanzen haben nicht beide Slot-Materialien");
        }
    }
}
