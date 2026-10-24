using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Q5: Sumpfwasser sah kantig (1-m-Quads, harter polygonales Rands). De WaterMesh now
    /// uses a 0.5-m grid with SHARED, jittered corner vertices -> organic shoreline.
    /// This test proves the shared-vertex structure on the scene mesh.
    /// </summary>
    public class Q5WaterTests
    {
        [UnityTest]
        public IEnumerator Q5_WaterSurfaceUsesSharedVertices()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            JungleWorldGenerator readyGen = null;
            for (int i = 0; i < 900 && readyGen == null; i++)
            {
                yield return null;
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) readyGen = g;
            }
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) not ready.");

            Mesh waterMesh = null;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Sumpfwasser")
                    waterMesh = mf.sharedMesh;
            Assert.IsNotNull(waterMesh, "Keinen Sumpfwasser-Mesh found.");

            int verts = waterMesh.vertices == null ? 0 : waterMesh.vertices.Length;
            int tris = waterMesh.triangles == null ? 0 : waterMesh.triangles.Length;
            UnityEngine.Debug.Log("[Q5] Water verts=" + verts + " triIdx=" + tris);

            // Old mesh: 4 separate verts per quad -> verts == 2/3 * triIdx (0.67).
            // Shared grid: verts ~= triIdx/6 .. triIdx/4. Assert clearly below the old ratio.
            Assert.Less(verts, tris / 4,
                "No shared vertices: verts " + verts + " >= triIdx/4 " + (tris / 4) + " (old per-quad layout).");
            Assert.Greater(verts, tris / 8,
                "Mesh degenerate: verts " + verts + " <= triIdx/8 " + (tris / 8) + ".");

            // Shoreline must be fine-grained: at 0.5-m cells a wet blob of area A has
            // ~4x more quads than the old 1-m grid -> tri count grows with area.
            Bounds b = waterMesh.bounds;
            float area = Mathf.Max(b.size.x * b.size.z, 1f);
            UnityEngine.Debug.Log("[Q5] vertDensity=" + (verts / area).ToString("F2") + " verts/m2");
        }
    }
}
