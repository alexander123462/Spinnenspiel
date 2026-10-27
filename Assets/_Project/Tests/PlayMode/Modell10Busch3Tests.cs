using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL10: Busch3 kommt jetzt aus dem Blender-Neubau (MAT_Busch3_Leaf/Branch:
    /// 6 aussere + 3 Innenstaelle, Sekundaer-/Tertiaerzweige, Kronenfueller, ~740
    /// Nadelblaetter als echte Geometrie, 5926 Dreiecke, 2 Slots) und ersetzt den
    /// Scan shrub_04 als Busche-Variante 1. Prueft: Variante vorhanden, Dreiecke
    /// 3000..6000, zwei Submeshes (Blatt-Submesh oben), Hoehe 0.6..1.2 m, UV-Atlas,
    /// Fuss am Ursprung (kein Schweben/Verkehrtherum), genug Instanzen mit
    /// Blatt+Ast-Material.
    /// </summary>
    public class Modell10Busch3Tests
    {
        private const string LeafMat = "MAT_Busch3_Leaf";
        private const string BranchMat = "MAT_Busch3_Branch";

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
        public IEnumerator Modell10_Busch3_MeshUndInstanzen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            Mesh[] variants = gen != null && gen.library != null ? gen.library.bushVariants : null;
            Assert.IsNotNull(variants, "MODELL10: keine Busche-Varianten in der Bibliothek.");
            Assert.GreaterOrEqual(variants.Length, 2, "MODELL10: Busche-Variantenliste hat keine Variante 1.");
            Mesh mesh = variants[1];
            Assert.IsNotNull(mesh, "MODELL10: Busch3-Variante 1 hat kein Mesh.");
            Material leafMat = gen.library.bushVariantMats != null && gen.library.bushVariantMats.Length > 1
                ? gen.library.bushVariantMats[1] : null;
            Assert.AreEqual(LeafMat, leafMat != null ? leafMat.name : "null",
                "MODELL10: Busch3-Blattmaterial heisst nicht " + LeafMat + ".");
            Assert.AreEqual(BranchMat, gen.library.busch3BranchMat != null ? gen.library.busch3BranchMat.name : "null",
                "MODELL10: Busch3-Astmaterial heisst nicht " + BranchMat + ".");

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
            Debug.Log("[MODELL10] Busch3-Instanzen (Blatt): " + leafInstances
                      + ", mit Ast+Blatt: " + bothInstances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 3000 && tris <= 6000;
            bool slotOk = mesh.subMeshCount == 2;
            float height = mesh.bounds.size.y;
            float spread = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            bool heightOk = height >= 0.6f && height <= 1.2f;
            bool spreadOk = spread >= 0.4f && spread <= 1.4f;
            Debug.Log("[MODELL10] Dreiecke " + tris + " -> 3000..6000 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL10] Slots " + mesh.subMeshCount + " -> 2 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL10] Hoehe " + height.ToString("F3")
                      + " m -> 0.6..1.2 -> " + (heightOk ? "OK" : "NOK"));
            Debug.Log("[MODELL10] Ausdehnung " + spread.ToString("F3")
                      + " m -> 0.4..1.4 -> " + (spreadOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(3000, 6000), "MODELL10: Dreiecke ausserhalb 3000..6000");
            Assert.AreEqual(2, mesh.subMeshCount, "MODELL10: erwartet genau 2 Submeshes/Slots (Aeste, Blaetter)");
            Assert.That(height, Is.InRange(0.6f, 1.2f), "MODELL10: Hoehe ausserhalb 0.6..1.2 m");
            Assert.That(spread, Is.InRange(0.4f, 1.4f), "MODELL10: Ausdehnung ausserhalb 0.4..1.4 m");

            // Blatt-Submesh muss oben sitzen (AddPlant nutzt global buschLeafSubmesh).
            Assert.Greater(mesh.GetSubMesh(1).bounds.center.y, mesh.GetSubMesh(0).bounds.center.y,
                "MODELL10: Blatt-Submesh ist nicht Submesh 1 (Slot-Reihenfolge vertauscht).");

            // UVs am 3-Zellen-Atlas (Blatt) bzw. Rindenband (Aeste): Wertebereich
            // 0..1, genug eindeutige Koordinaten fuer ~740 Nadeln + ~60 Zweige.
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
            Debug.Log("[MODELL10] UVs " + uvs.Count + "/" + mesh.vertexCount
                      + ", eindeutig " + uniqueU + " -> >=30 -> " + (islandsOk ? "OK" : "NOK"));
            Assert.IsTrue(islandsOk, "MODELL10: UV-Atlas fehlt oder deckt die Nadel-Inseln nicht ab");

            // Kein Schweben/Verkehrtherum: der Asset-Fuss sitzt am Ursprung.
            bool footOk = mesh.bounds.min.y >= -0.05f && mesh.bounds.min.y <= 0.02f;
            Debug.Log("[MODELL10] Fuss y " + mesh.bounds.min.y.ToString("F3")
                      + " -> -0.05..0.02 -> " + (footOk ? "OK" : "NOK"));
            Assert.IsTrue(footOk, "MODELL10: Busch3-Asset hat Fuss nicht am Ursprung (Schweben/Verkehrtherum)");
            Assert.Greater(leafInstances, 100, "MODELL10: zu wenige Busch3-Instanzen in der Welt");
            Assert.Greater(bothInstances, 100, "MODELL10: Busch3-Instanzen haben nicht beide Slot-Materialien");
        }
    }
}
