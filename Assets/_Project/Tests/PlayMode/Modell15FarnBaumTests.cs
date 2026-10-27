using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL15: Baumfarn (Farn_Baum) ist ein Blender-Neubau (Narben-Stamm
    /// 1.9/2.2/2.4 m, bogige Wedelkrone aus Atlas-Wedeln) statt der alten Palmen-
    /// Platzhalter. Prueft je Variante Mesh-Masse (Dreiecke 3000-6000, Stammhoehe
    /// 1.5-2.5 m gemessen, Fuss bei y~0), 2 Submeshes mit Wedelkrone oben und die
    /// Atlas-/Stamm-Textur am Material.
    /// Ground truth: library.treeFerns nach UrwaldSceneBuilder.Build.</summary>
    public class Modell15FarnBaumTests
    {
        [UnityTest]
        public IEnumerator M15_FarnBaumIsNewBlenderTreeFern()
        {
            JungleWorldGenerator.NextSeed = 4242;
            // WALD6 hat Baumfarne aus; der Test prueft das Asset -> Override wie in der Galerie.
            JungleWorldGenerator.ForceTreeFerns = true;
            SceneManager.LoadScene("Urwald");
            yield return null;

            JungleWorldGenerator gen = null;
            float timeout = 15f;
            while (timeout > 0f && gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(gen, "JungleWorldGenerator not ready after 15 s.");

            var lib = gen.library;
            Assert.IsNotNull(lib, "No JungleAssetLibrary on the generator.");
            Assert.IsNotNull(lib.treeFerns, "No treeFerns in the library.");
            Assert.GreaterOrEqual(lib.treeFerns.Length, 3, "treeFerns too short.");

            for (int i = 0; i < 3; i++)
            {
                Mesh fern = lib.treeFerns[i];
                Assert.IsNotNull(fern, "treeFerns[" + i + "] (Baumfarn_" + (i + 1) + ") is null.");

                int tris = 0;
                for (int s = 0; s < fern.subMeshCount; s++) tris += fern.GetSubMesh(s).indexCount / 3;
                Bounds b = fern.bounds;

                Debug.Log(string.Format("[M15] Baumfarn_{0}: Dreiecke={1} submeshes={2} y {3:0.000}..{4:0.000} uv={5}",
                    i + 1, tris, fern.subMeshCount, b.min.y, b.max.y, fern.uv.Length));

                // Vorgabe MODELL15: 3000-6000 Dreiecke je Variante.
                Assert.GreaterOrEqual(tris, 3000, "Baumfarn_" + (i + 1) + " too coarse (old mesh still in use?): " + tris);
                Assert.LessOrEqual(tris, 6000, "Baumfarn_" + (i + 1) + " too dense: " + tris);

                // 2 Slots fester Reihenfolge (0 Stamm, 1 Blatt), keine .001-Duplikate.
                Assert.AreEqual(2, fern.subMeshCount, "Baumfarn_" + (i + 1) + " must have 2 submeshes (Stamm+Blatt): " + fern.subMeshCount);

                // Fuss am Ursprung (Steckbasis), kein Schweben.
                Assert.GreaterOrEqual(b.min.y, -0.05f, "Baumfarn_" + (i + 1) + " origin not at base: min.y=" + b.min.y);
                Assert.LessOrEqual(b.min.y, 0.02f, "Baumfarn_" + (i + 1) + " floats above ground: min.y=" + b.min.y);

                // Submesh 0 = Stamm: gemessene Stammhoehe 1.5-2.5 m (Export 1.9/2.2/2.4 m).
                Bounds stemB = fern.GetSubMesh(0).bounds;
                Debug.Log(string.Format("[M15] Baumfarn_{0}: Stamm y {1:0.00}..{2:0.00} (Hoehe {3:0.00})",
                    i + 1, stemB.min.y, stemB.max.y, stemB.size.y));
                Assert.GreaterOrEqual(stemB.size.y, 1.5f, "Baumfarn_" + (i + 1) + " trunk too short: " + stemB.size.y);
                Assert.LessOrEqual(stemB.size.y, 2.5f, "Baumfarn_" + (i + 1) + " trunk too tall: " + stemB.size.y);

                // Submesh 1 = Wedel oben: Kronen-Mitte ueber Stamm-Mitte (nicht verkehrtherum).
                Bounds leafB = fern.GetSubMesh(1).bounds;
                Assert.Greater(leafB.center.y, stemB.center.y,
                    "Baumfarn_" + (i + 1) + " crown below trunk centre (upside-down?): leaf center.y="
                    + leafB.center.y + " stem center.y=" + stemB.center.y);

                // UV vorhanden (Atlas sonst weiss).
                Assert.Greater(fern.uv.Length, 0, "Baumfarn_" + (i + 1) + " mesh has no UVs.");
            }

            // Materialien mit den richtigen Texturen verdrahtet (sonst weisses Material).
            Assert.IsNotNull(lib.treeFernLeafMat, "No treeFernLeafMat in the library.");
            Assert.IsNotNull(lib.treeFernStemMat, "No treeFernStemMat in the library.");
            Texture leafMap = lib.treeFernLeafMat.GetTexture("_BaseColorMap");
            Assert.IsNotNull(leafMap, "MAT_Baumfarn_Leaf has no _BaseColorMap (texture not wired).");
            Assert.That(leafMap.name, Does.Contain("Baumfarn_Leaf"), "Wrong base map on MAT_Baumfarn_Leaf: " + leafMap.name);
            Texture stemMap = lib.treeFernStemMat.GetTexture("_BaseColorMap");
            Assert.IsNotNull(stemMap, "MAT_Baumfarn_Stem has no _BaseColorMap (texture not wired).");
            Assert.That(stemMap.name, Does.Contain("Baumfarn_Stem"), "Wrong base map on MAT_Baumfarn_Stem: " + stemMap.name);

            // Der Generator setzt Baumfarne weiterhin in den Wald (3 garantiert vor dem Spawn).
            Assert.Greater(gen.TreeFernCount, 0, "Generator placed no tree ferns.");
        }
    }
}
