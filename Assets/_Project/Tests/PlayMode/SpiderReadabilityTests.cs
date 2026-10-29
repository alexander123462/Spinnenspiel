using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Collections;

namespace Spinnenspiel.Tests
{
    /// <summary>T8: Die drei Spinne-Koerper-Materialien are brightened (+20 % base, warm Rim-Tint), not over-exposed.</summary>
    public class SpiderReadabilityTests
    {
        private static readonly string[] BODY_MATS =
            { "MAT_SpiderCarapace", "MAT_SpiderAbdomenScan", "MAT_SpiderLegScan" };

        [UnityTest]
        public IEnumerator SpinneKoerperMaterial_AufhellungOhneUeberbelichung()
        {
            yield return LoadUrwald();

            var found = new Material[3];
            int n = 0;
            foreach (var name in BODY_MATS)
            {
                var m = FindMaterial(name);
                Assert.IsNotNull(m, "Body-Material " + name + " not present.");
                if (n < found.Length) found[n++] = m;
            }
            Assert.That(n == 3, "Expected 3 body materials, got " + n + ".");

            for (int i = 0; i < n; i++)
            {
                var m = found[i];
                var bcol = m.GetVector("_BaseColor");
                float mean = (bcol.x + bcol.y + bcol.z) / 3f;
                float maxC = Mathf.Max(bcol.x, Mathf.Max(bcol.y, bcol.z));

                Debug.Log("[T8] " + BODY_MATS[i] + " BaseMean=" + mean.ToString("F2")
                    + " -> >=1.2 (Weiess 1.0 +20 %) ; BaseMax=" + maxC.ToString("F2")
                    + " -> <=1.5 (not over-exposed)");
                Assert.That(mean >= 1.2f,
                    BODY_MATS[i] + " base mean " + mean.ToString("F2") + " not lifted >= 1.2.");
                Assert.LessOrEqual(maxC, 1.5f,
                    BODY_MATS[i] + " over-exposed: base max " + maxC.ToString("F2") + " > 1.5.");

                if (m.HasProperty("_SpecularColor"))
                {
                    var scol = m.GetVector("_SpecularColor");
                    Debug.Log("[T8] " + BODY_MATS[i] + " Specular=" + scol.x.ToString("F2")
                        + "," + scol.y.ToString("F2") + "," + scol.z.ToString("F2")
                        + " -> warm Rim-Tint (r>=b)");
                    Assert.That(scol.x >= scol.z - 0.01f,
                        BODY_MATS[i] + " Rim-Tint not warm: r<b.");
                }
            }

            yield return null;
        }

        private static IEnumerator LoadUrwald()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            float timeout = 15f;
            while (timeout > 0f)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) { yield break; }
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static Material FindMaterial(string name)
        {
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name == name) return m;
            return null;
        }
    }
}
