using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Q2: Blatt-Rueckside nicht mehr tiefschwarz. Laeuft-Materialien doppseitig must use
    /// Flip (_DoubleSidedNormalMode=2, _DoubleSidedConstants.b>0) instead of Mirror (b=-1),
    /// the pattern of the good Riesenblatt/Grundfarn. Reads the live scene renderers.
    /// </summary>
    public class Q2LeafDoubleSidedTests
    {
        private static readonly string[] LeafTags =
        {
            "M_periwinkle_plant", // Immergruen
            "MAT_Unkraut",        // Unkraut (MODELL3: Blender-Rosette ersetzt M_weed_plant_02)
            "M_nettle_plant",     // Brennnessel
            "MAT_BushTropisch",   // Busch1
        };

        [UnityTest]
        public IEnumerator Q2_Laeuft_Materialien_Flip_Nicht_Mirror()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);
            Assert.IsNotNull(gen, "Welt nicht fertig.");

            int checkedCount = 0;
            foreach (var tag in LeafTags)
            {
                var mr = FindNearest(tag, Vector3.zero);
                if (mr == null)
                {
                    Debug.LogWarning("[Q2] kein Renderer mit Material " + tag);
                    continue;
                }
                var m = mr.sharedMaterial;
                Assert.AreEqual(1f, m.GetFloat("_DoubleSidedEnable"), tag + ": doppseitig must be on");
                Assert.AreEqual(2f, m.GetFloat("_DoubleSidedNormalMode"), tag + ": Flip (2), not Mirror (1)");
                Assert.Greater(m.GetVector("_DoubleSidedConstants").z, 0f, tag + ": constants b>0 (Flip)");
                checkedCount++;
            }
            Assert.GreaterOrEqual(checkedCount, LeafTags.Length, "alle vier Laeuft-Materialien checked");
        }

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
            for (int i = 0; i < 120; i++) yield return null;
            done(gen);
        }

        private static MeshRenderer FindNearest(string tag, Vector3 from)
        {
            MeshRenderer best = null;
            float bestD = float.MaxValue;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (mr.bounds.center.y > 900f || mr.bounds.size.magnitude > 150f) continue;
                bool hit = false;
                foreach (var m in mr.sharedMaterials) if (m != null && m.name == tag) { hit = true; break; }
                if (!hit) continue;
                float d = (mr.bounds.center - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = mr; }
            }
            return best;
        }
    }
}
