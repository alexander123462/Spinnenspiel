using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL17: Steine sind Blender-Neubauten (5 Varianten Stein_1..5, je
    /// ~1400 Dreiecke, gebrochene Facetten, Schichtstufen, Moos als Vertexfarbe oben)
    /// statt der prozeduralen M_Rock0..4 (320/1280 Dreiecke, blobbig). Ground truth:
    /// library.rocks nach UrwaldSceneBuilder.Build.</summary>
    public class Modell17SteineTests
    {
        [UnityTest]
        public IEnumerator M17_SteineAreNewBlenderRocks()
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
            Assert.IsNotNull(lib.rocks, "No rocks in the library.");
            Assert.GreaterOrEqual(lib.rocks.Length, 5, "rocks too short.");

            for (int i = 0; i < 5; i++)
            {
                Mesh rock = lib.rocks[i];
                Assert.IsNotNull(rock, "rocks[" + i + "] (Stein_" + (i + 1) + ") is null.");

                int tris = 0;
                for (int s = 0; s < rock.subMeshCount; s++) tris += rock.GetSubMesh(s).indexCount / 3;
                Bounds b = rock.bounds;
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

                Debug.Log(string.Format("[M17] Stein_{0}: Dreiecke={1} submeshes={2} maxDim={3:0.00} m min.y={4:0.00}",
                    i + 1, tris, rock.subMeshCount, maxDim, b.min.y));

                // Vorgabe MODELL17: 800-2500 Dreiecke (alt: 320/1280 -> Fallback waere durch).
                Assert.GreaterOrEqual(tris, 800, "Stein_" + (i + 1) + " too coarse (old mesh still in use?): " + tris);
                Assert.LessOrEqual(tris, 2500, "Stein_" + (i + 1) + " too dense: " + tris);

                // 1 Slot fester Reihenfolge (Gestein; Moos kommt als Vertexfarbe).
                Assert.AreEqual(1, rock.subMeshCount, "Stein_" + (i + 1) + " must have 1 submesh: " + rock.subMeshCount);

                // Vorgabe Groesse: 0.3-1.2 m (Mesh-Naturoeffnung; Generator skaliert 0.5-1.5).
                Assert.GreaterOrEqual(maxDim, 0.3f, "Stein_" + (i + 1) + " too small: " + maxDim);
                Assert.LessOrEqual(maxDim, 1.25f, "Stein_" + (i + 1) + " too big: " + maxDim);

                // Ursprung am Fuss (Generator versenkt 0.05 m).
                Assert.GreaterOrEqual(b.min.y, -0.05f, "Stein_" + (i + 1) + " sinks below origin: " + b.min.y);
                Assert.LessOrEqual(b.min.y, 0.02f, "Stein_" + (i + 1) + " floats: " + b.min.y);

                // Vertexfarbe 'Col' vorhanden (Moosansatz oben).
                Color32[] cols = rock.colors32;
                Assert.IsNotNull(cols, "Stein_" + (i + 1) + " has no vertex colors.");
                Assert.AreEqual(rock.vertexCount, cols.Length, "Stein_" + (i + 1) + " vertex color count mismatch.");

                // Moos oben: obere Drittel gruenlicher (G>R) als untere Haelfte.
                float topGreener = 0f, botGreener = 0f;
                int topN = 0, botN = 0;
                float yTop = b.max.y * 0.7f, yBot = b.max.y * 0.4f;
                var verts = rock.vertices;
                for (int v = 0; v < verts.Length; v++)
                {
                    float d = cols[v].g - cols[v].r;
                    if (verts[v].y >= yTop) { topGreener += d; topN++; }
                    else if (verts[v].y <= yBot) { botGreener += d; botN++; }
                }
                float tAvg = topN > 0 ? topGreener / topN : -1f;
                float bAvg = botN > 0 ? botGreener / botN : 0f;
                Debug.Log(string.Format("[M17] Stein_{0}: Moos oben (G-R)={1:0.0} unten={2:0.0}", i + 1, tAvg, bAvg));
                Assert.Greater(tAvg, bAvg + 3f, "Stein_" + (i + 1) + " top not greener than bottom (moss missing): " + tAvg + " vs " + bAvg);
            }
        }
    }
}
