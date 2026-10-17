using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;

namespace Spinnenspiel.Tests
{
    // A1 Vegetations-Varianten: the Urwald must use many DIFFERENT plant meshes, each species
    // spread over a scale factor >= 1.5, all deterministic from the Seed. No per-instance
    // material instances (Static Batching stays intact), so no Draw-Call explosion.
    public class VegetationVariantTests
    {
        private JungleWorldGenerator readyGen;

        private IEnumerator WaitReady()
        {
            readyGen = null;
            float timeout = 15f;
            while (timeout > 0f)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady)
                {
                    readyGen = g;
                    break;
                }
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Urwald_UsesManyPlantVariants()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null; // Switch to the new scene before querying for the generator.
            yield return WaitReady();
            Assert.IsTrue(readyGen != null && readyGen.IsReady, "World not ready in time.");

            var meshes = new HashSet<Mesh>();
            int renderers = 0;
            foreach (var go in CollectPlantObjects())
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null) continue;
                var m = mf.sharedMesh;
                if (m == null) continue;
                meshes.Add(m);
                renderers++;
            }

            // Step-3 criterion: >= 12 distinct plant meshes in the scene.
            Debug.Log(string.Format("[A1] distinct plant meshes: {0} -> Kriterium >= 12 -> {1}",
                meshes.Count, meshes.Count >= 12 ? "OK" : "NOK"));
            Assert.That(meshes.Count, Is.AtLeast(12),
                $"Only {meshes.Count} distinct plant meshes (< 12).");
            Assert.That(renderers, Is.AtLeast(3000),
                $"Only {renderers} plant renderers placed.");
        }

        [UnityTest]
        public IEnumerator PlantInstances_VaryInScaleAndRotation()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null; // Switch to the new scene before querying for the generator.
            yield return WaitReady();
            Assert.IsTrue(readyGen != null && readyGen.IsReady, "World not ready in time.");

            var scales = new HashSet<float>();
            var rots = new HashSet<int>();
            foreach (var go in CollectPlantObjects())
            {
                var t = go.transform;
                float s = t.localScale.x;
                if (s <= 0f) continue;
                scales.Add(Mathf.Round(s * 100f));           // 1 cm bins
                rots.Add(Mathf.RoundToInt(t.eulerAngles.y / 4f)); // 4° bins
            }

            // Step-3 criterion: >= 20 scale values and >= 30 rotation bins -> Zufall per Instanz.
            Debug.Log(string.Format("[A1] scale bins: {0} rot bins: {1} -> Kriterium >=20/>=30 -> {2}",
                scales.Count, rots.Count, (scales.Count >= 20 && rots.Count >= 30) ? "OK" : "NOK"));
            Assert.That(scales.Count, Is.AtLeast(20), "Only " + scales.Count + " scale values.");
            Assert.That(rots.Count, Is.AtLeast(30), "Only " + rots.Count + " rotation bins.");
        }

        /// <summary>Alle Render-Objects in the scene whose parent-chain names mark them as plants.</summary>
        private static GameObject[] CollectPlantObjects()
        {
            var plants = new System.Collections.Generic.List<GameObject>();
            string[] groups = { "Unterholz", "Bodendecker", "Baumfarne", "Grundfarne", "Farn-Scatter", "B\u00e4ume" };
            foreach (var name in groups)
            {
                var root = GameObject.Find(name);
                if (root != null)
                    CollectFrom(root.transform, plants);
            }
            return plants.ToArray();
        }

        private static void CollectFrom(Transform t, List<GameObject> sink)
        {
            GameObject go = t.gameObject;
            if (go.GetComponent<MeshRenderer>() != null && go.GetComponent<CapsuleCollider>() == null)
                sink.Add(go);
            for (int i = 0; i < t.childCount; i++)
                CollectFrom(t.GetChild(i), sink);
        }
    }
}

