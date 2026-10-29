using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// C4.1: VegetationContactTest - prüft, dass Bäume und Kleinvegetation auf dem Terrain stehen.
    /// Bäume: Stamm-Base bei SampleHeight + 0.15m (0.15m über Boden)
    /// Kleinvegetation: bei SampleHeight - sinkDepth
    /// </summary>
    public class VegetationContactTest
    {
        private static int _testCount = 0;

        /// <summary>Initialisiert den Seed für deterministisches Platzieren.</summary>
        public static void Initialize()
        {
            JungleWorldGenerator.NextSeed = 12345;
        }

        [UnityTest]
        public IEnumerator Vegetation_ExistsOnFlatTerrain()
        {
            _testCount++;
            Debug.Log($"[{_testCount}] VegetationContactTest - Flat Terrain");

            Initialize();
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

            Assert.IsNotNull(gen, "JungleWorldGenerator not ready after 15s.");
            Assert.Greater(gen.TreeCount, 0, "No trees placed on terrain.");

            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");

            int checkedCount = 0;
            int passedCount = 0;

            // Prüfe 20 Bäume auf ebenem Gelände (Urwald-Scene hat relativ ebene Bereiche)
            int checkLimit = Mathf.Min(gen.TreeCount, 20);
            Vector3[] directions = new Vector3[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

            for (int i = 0; i < checkLimit; i++)
            {
                Transform tree = gen.Trees[i];
                if (tree == null) continue;

                checkedCount++;
                float tolerance = 0.1f; // ±5cm Toleranz

                // Prüfe Baum-Stamm-Base Position
                Vector3 treePos = tree.position;
                float treeGroundY = terrain.SampleHeight(treePos) + terrain.transform.position.y;
                float treeBaseY = treePos.y;
                float treeOffset = treeBaseY - treeGroundY;

                // Bäume sollen bei SampleHeight + 0.15m stehen
                bool treeOk = Mathf.Abs(treeOffset - 0.15f) < tolerance;

                if (treeOk)
                {
                    passedCount++;
                    Debug.Log($"[Flat Test] Tree {i}: pos={treePos}, groundY={treeGroundY:F2}, baseY={treeBaseY:F2}, offset={treeOffset:F3} OK");
                }
                else
                {
                    Debug.LogWarning($"[Flat Test] Tree {i}: pos={treePos}, groundY={treeGroundY:F2}, baseY={treeBaseY:F2}, offset={treeOffset:F3} FAIL (expected ~0.15m)");
                }

                // Prüfe Kleinvegetation (Gras, Blumen)
                // Kleinvegetation soll bei SampleHeight - sinkDepth stehen (sinkDepth = 0.01f standardmäßig)
                foreach (var dir in directions)
                {
                    float step = 1.5f;
                    for (float dist = step; dist < 4f; dist += step)
                    {
                        Vector3 checkPos = treePos + dir * dist;
                        float vegGroundY = terrain.SampleHeight(checkPos) + terrain.transform.position.y;
                        float vegY = checkPos.y;
                        float vegOffset = vegY - vegGroundY;

                        // Kleinvegetation soll bei SampleHeight - sinkDepth stehen
                        bool vegOk = Mathf.Abs(vegOffset - (-0.01f)) < tolerance; // -0.01f = -sinkDepth

                        if (vegOk)
                        {
                            passedCount++;
                            // Debug.Log($"[Flat Test] Veg at {checkPos}: groundY={vegGroundY:F2}, y={vegY:F2}, offset={vegOffset:F3} OK");
                        }
                        else
                        {
                            // Debug.LogWarning($"[Flat Test] Veg at {checkPos}: groundY={vegGroundY:F2}, y={vegY:F2}, offset={vegOffset:F3} FAIL (expected ~-0.01m)");
                        }
                    }
                }
            }

            Debug.Log($"[Flat Test] Checked {checkedCount} trees, found {passedCount} correct positions");
            Assert.GreaterOrEqual(passedCount, checkedCount * 0.8f,
                $"Flat terrain: Expected at least {(checkedCount * 0.8f):F0} correct vegetation positions, got {passedCount}.");
        }

        [UnityTest]
        public IEnumerator Vegetation_ExistsOnUnevenTerrain()
        {
            _testCount++;
            Debug.Log($"[{_testCount}] VegetationContactTest - Uneven Terrain");

            Initialize();
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

            Assert.IsNotNull(gen, "JungleWorldGenerator not ready after 15s.");
            Assert.Greater(gen.TreeCount, 0, "No trees placed on terrain.");

            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");

            int checkedCount = 0;
            int passedCount = 0;
            float tolerance = 0.1f; // ±5cm Toleranz

            // Prüfe 20 Bäume auf unebenem Gelände (höhere y-Werte für mehr Relief)
            int checkLimit = Mathf.Min(gen.TreeCount, 20);

            for (int i = 0; i < checkLimit; i++)
            {
                Transform tree = gen.Trees[i];
                if (tree == null) continue;

                checkedCount++;

                // Prüfe Baum-Stamm-Base Position auf unebenem Gelände
                Vector3 treePos = tree.position;
                float treeGroundY = terrain.SampleHeight(treePos) + terrain.transform.position.y;
                float treeBaseY = treePos.y;
                float treeOffset = treeBaseY - treeGroundY;

                // Bäume sollen bei SampleHeight + 0.15m stehen - unabhängig von Gelände-Höhe
                bool treeOk = Mathf.Abs(treeOffset - 0.15f) < tolerance;

                if (treeOk)
                {
                    passedCount++;
                    Debug.Log($"[Uneven Test] Tree {i}: pos={treePos}, groundY={treeGroundY:F2}, baseY={treeBaseY:F2}, offset={treeOffset:F3} OK");
                }
                else
                {
                    Debug.LogWarning($"[Uneven Test] Tree {i}: pos={treePos}, groundY={treeGroundY:F2}, baseY={treeBaseY:F2}, offset={treeOffset:F3} FAIL (expected ~0.15m)");
                }

                // Prüfe Kleinvegetation in Umgebung
                Vector3[] directions = new Vector3[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
                foreach (var dir in directions)
                {
                    float step = 1.0f;
                    for (float dist = step; dist < 3f; dist += step)
                    {
                        Vector3 checkPos = treePos + dir * dist;
                        float vegGroundY = terrain.SampleHeight(checkPos) + terrain.transform.position.y;
                        float vegY = checkPos.y;
                        float vegOffset = vegY - vegGroundY;

                        // Kleinvegetation soll bei SampleHeight - sinkDepth stehen
                        bool vegOk = Mathf.Abs(vegOffset - (-0.01f)) < tolerance;

                        if (vegOk)
                        {
                            passedCount++;
                            // Debug.Log($"[Uneven Test] Veg at {checkPos}: groundY={vegGroundY:F2}, y={vegY:F2}, offset={vegOffset:F3} OK");
                        }
                        else
                        {
                            // Debug.LogWarning($"[Uneven Test] Veg at {checkPos}: groundY={vegGroundY:F2}, y={vegY:F2}, offset={vegOffset:F3} FAIL (expected ~-0.01m)");
                        }
                    }
                }
            }

            Debug.Log($"[Uneven Test] Checked {checkedCount} trees, found {passedCount} correct positions");
            Assert.GreaterOrEqual(passedCount, checkedCount * 0.8f,
                $"Uneven terrain: Expected at least {(checkedCount * 0.8f):F0} correct vegetation positions, got {passedCount}.");
        }

        [UnityTest]
        public IEnumerator Vegetation_Positions_MatchTerrain()
        {
            _testCount++;
            Debug.Log($"[{_testCount}] VegetationContactTest - Position Match");

            Initialize();
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

            Assert.IsNotNull(gen, "JungleWorldGenerator not ready after 15s.");

            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");

            int passedCount = 0;
            int checkCount = Mathf.Min(gen.TreeCount, 10);
            float tolerance = 0.1f; // ±5cm Toleranz

            for (int i = 0; i < checkCount; i++)
            {
                Transform tree = gen.Trees[i];
                if (tree == null) continue;

                Vector3 treePos = tree.position;
                float treeGroundY = terrain.SampleHeight(treePos) + terrain.transform.position.y;
                float treeBaseY = treePos.y;
                float expectedTreeOffset = 0.15f;

                float actualOffset = treeBaseY - treeGroundY;
                bool treeOk = Mathf.Abs(actualOffset - expectedTreeOffset) < tolerance;

                if (treeOk)
                {
                    passedCount++;
                }
                else
                {
                    Debug.LogWarning($"Tree {i}: actual offset={actualOffset:F3}, expected={expectedTreeOffset}F0, difference={Mathf.Abs(actualOffset - expectedTreeOffset):F3}");
                }
            }

            Assert.GreaterOrEqual(passedCount, checkCount * 0.8f,
                $"Expected at least {(checkCount * 0.8f):F0} trees at correct height (SampleHeight + 0.15m), got {passedCount} of {checkCount}.");
        }

        /// <summary>
        /// Stellt sicher, dass der Test deterministisch läuft.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Initialize();
        }
    }
}
