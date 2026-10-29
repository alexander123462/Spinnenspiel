using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL12: Riesenblatt1 ist ein Blender-Neubau (ein grosses Blatt 1.5-2.5 m
    /// mit Mittelrippe, Seitenadern als Relief, gelappt) statt des alten 7968-Dreieck-
    /// Horstes. Prueft Mesh-Masse (Dreiecke 1500-3000, Blatt-Ausdehnung 1.5-2.5 m, Fuss
    /// bei y~0), 2 Submeshes mit Blatt oben (gemessen) und Atlas am Material.
    /// Ground truth: library.giantLeaves[0] nach UrwaldSceneBuilder.Build.</summary>
    public class Modell12Riesenblatt1Tests
    {
        [UnityTest]
        public IEnumerator M12_Riesenblatt1IsNewBlenderLeaf()
        {
            JungleWorldGenerator.NextSeed = 4242;
            // WALD6 hat Riesenblaetter aus; der Test prueft das Asset -> Override wie in der Galerie.
            JungleWorldGenerator.ForceGiantLeaves = true;
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
            Assert.IsNotNull(lib.giantLeaves, "No giantLeaves in the library.");
            Assert.GreaterOrEqual(lib.giantLeaves.Length, 1, "giantLeaves empty.");
            Mesh leaf = lib.giantLeaves[0];
            Assert.IsNotNull(leaf, "giantLeaves[0] (Riesenblatt1) is null.");

            int tris = 0;
            for (int s = 0; s < leaf.subMeshCount; s++) tris += leaf.GetSubMesh(s).indexCount / 3;
            Bounds b = leaf.bounds;
            float spreadXZ = Mathf.Max(b.size.x, b.size.z);

            Debug.Log(string.Format("[M12] Riesenblatt1: Dreiecke={0} submeshes={1} y {2:0.000}..{3:0.000} spreadXZ={4:0.00} uv={5}",
                tris, leaf.subMeshCount, b.min.y, b.max.y, spreadXZ, leaf.uv.Length));

            // Vorgabe MODELL12: 1500-3000 Dreiecke (alt: 7968).
            Assert.GreaterOrEqual(tris, 1500, "Riesenblatt1 too coarse (old mesh still in use?): " + tris);
            Assert.LessOrEqual(tris, 3000, "Riesenblatt1 too dense: " + tris);

            // 2 Slots (Stiel, Blatt) wie im Original.
            Assert.AreEqual(2, leaf.subMeshCount, "Riesenblatt1 must have 2 submeshes (Stiel+Blatt): " + leaf.subMeshCount);

            // Grosses Blatt: Ausdehnung in XZ 1.5-2.5 m (alt: 1.9 m, bleibt im Band).
            Assert.GreaterOrEqual(spreadXZ, 1.5f, "Riesenblatt blade too small: " + spreadXZ);
            Assert.LessOrEqual(spreadXZ, 2.5f, "Riesenblatt blade too big: " + spreadXZ);

            // Fuss am Ursprung (Steckbasis), kein Schweben/Verkehrtherum.
            Assert.GreaterOrEqual(b.min.y, -0.05f, "Riesenblatt origin not at base: min.y=" + b.min.y);
            Assert.LessOrEqual(b.min.y, 0.02f, "Riesenblatt floats above ground: min.y=" + b.min.y);

            // Blatt-Submesh liegt oben (gemessen): obere Haelfte enthaelt mehr Dreiecke
            // als der Stiel unten.
            int upper = 0, lower = 0;
            Vector3 c = b.center;
            for (int s = 0; s < leaf.subMeshCount; s++)
            {
                var sm = leaf.GetSubMesh(s);
                float midY = sm.bounds.center.y;
                int t = sm.indexCount / 3;
                if (midY >= c.y) upper += t; else lower += t;
            }
            Assert.Greater(upper, lower, "Leaf submesh is not the upper part (stem/leaf swapped?): upper=" + upper + " lower=" + lower);

            // UV vorhanden und Atlas am Blatt-Material verdrahtet (sonst weisses Material).
            Assert.Greater(leaf.uv.Length, 0, "Riesenblatt1 mesh has no UVs.");
            var mat = lib.giantLeafMats[0];
            Assert.IsNotNull(mat, "No leaf material for Riesenblatt1 in the library.");
            Texture baseMap = mat.GetTexture("_BaseColorMap");
            Assert.IsNotNull(baseMap, "MAT_Riesenblatt_1 has no _BaseColorMap (texture not wired).");
            Assert.That(baseMap.name, Does.Contain("Riesenblatt"), "Wrong base map on MAT_Riesenblatt_1: " + baseMap.name);

            // Der Generator setzt Riesenblaetter weiterhin ins Unterholz.
            Assert.Greater(gen.GiantLeafCount, 0, "Generator placed no giant leaves.");
        }
    }
}
