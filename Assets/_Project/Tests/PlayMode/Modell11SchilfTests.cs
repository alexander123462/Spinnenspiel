using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL11: Das Schilf ist ein Blender-Horst (Knoten-Halme, ueberhaengende
    /// Klingen, Rohrkolben) statt des alten prozeduralen GrassClump. Prueft Mesh-Masse
    /// (Hoehe 1.0-1.8 m, Dreiecke 500-1200, Fuss bei y=0), UV-Atlas am Material und
    /// dass der Generator Schilf am Wasserrand setzt. Ground truth: library.reed/reedMat
    /// nach UrwaldSceneBuilder.Build.</summary>
    public class Modell11SchilfTests
    {
        [UnityTest]
        public IEnumerator M11_SchilfIsBlenderClumpWithAtlas()
        {
            JungleWorldGenerator.NextSeed = 4242;
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
            Mesh reed = lib.reed;
            Assert.IsNotNull(reed, "No reed mesh in the library.");

            int tris = 0;
            for (int s = 0; s < reed.subMeshCount; s++) tris += reed.GetSubMesh(s).indexCount / 3;
            Bounds b = reed.bounds;
            float spreadXZ = Mathf.Max(b.size.x, b.size.z);

            Debug.Log(string.Format("[M11] Schilf: Dreiecke={0} submeshes={1} y {2:0.000}..{3:0.000} spreadXZ={4:0.00} uv={5}",
                tris, reed.subMeshCount, b.min.y, b.max.y, spreadXZ, reed.uv.Length));

            // Vorgabe MODELL11: 500-1200 Dreiecke.
            Assert.GreaterOrEqual(tris, 500, "Schilf too coarse (old GrassClump still in use?): " + tris);
            Assert.LessOrEqual(tris, 1200, "Schilf too dense: " + tris);

            // Horst-Fuss am Ursprung, Hoehe im Zielband 1.0-1.8 m.
            Assert.GreaterOrEqual(b.min.y, -0.05f, "Schilf origin not at base: min.y=" + b.min.y);
            Assert.LessOrEqual(b.min.y, 0.02f, "Schilf floats above ground: min.y=" + b.min.y);
            Assert.GreaterOrEqual(b.size.y, 1.0f, "Schilf too short (old GrassClump?): " + b.size.y);
            Assert.LessOrEqual(b.size.y, 1.8f, "Schilf too tall: " + b.size.y);

            // Ausgebreiteter Horst (kein duenner Grasbuenschel): Grundflaeche gut 1 m.
            Assert.GreaterOrEqual(spreadXZ, 0.5f, "Schilf clump too narrow: " + spreadXZ);

            // UV-Atlas vorhanden und am MAT_Reed befestigt (sonst weisses/ungetextures Material).
            Assert.Greater(reed.uv.Length, 0, "Schilf mesh has no UVs.");
            var mat = lib.reedMat;
            Assert.IsNotNull(mat, "No reed material in the library.");
            Assert.AreEqual("MAT_Reed", mat.name, "Unexpected reed material name.");
            Texture baseMap = mat.GetTexture("_BaseColorMap");
            Assert.IsNotNull(baseMap, "MAT_Reed has no atlas _BaseColorMap (MODELL11 texture not wired).");
            Assert.That(baseMap.name, Does.Contain("Schilf"), "Wrong base map on MAT_Reed: " + baseMap.name);

            // Der Generator setzt das Schilf weiterhin am Wasserrand.
            Assert.Greater(gen.ReedCount, 0, "Generator placed no reeds.");
        }
    }
}
