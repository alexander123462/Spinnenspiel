using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Q4: The reed (MAT_Reed) is greenish-yellow and darkened (HSV Value 0.35-0.5),
    /// NOT cream-white/bleached, and matte (low Smoothness). Ground truth via library.reedMat
    /// as set by UrwaldSceneBuilder.ReedMaterial during Build.</summary>
    public class Q4SchilfTintTests
    {
        [UnityTest]
        public IEnumerator Q4_ReedIsGreenYellowNotBleached()
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
            var reed = lib.reedMat;
            Assert.IsNotNull(reed, "No reed material in the library.");

            Color col = reed.GetColor("_BaseColor");
            float val = Mathf.Max(col.r, Mathf.Max(col.g, col.b));
            float minC = Mathf.Min(col.r, Mathf.Min(col.g, col.b));
            float sat = val > 0f ? (val - minC) / val : 0f;

            Debug.Log(string.Format("[Q4] MAT_Reed _BaseColor=({0:0.00} {1:0.00} {2:0.00}) V={3:0.000} S={4:0.000} Smoothness={5:0.00}",
                col.r, col.g, col.b, val, sat, reed.GetFloat("_Smoothness")));

            // Backlog-Kriterium: Helligkeit (HSV Value) abdunkeln im Band 0.35-0.5.
            Assert.GreaterOrEqual(val, 0.34f, "Schilf too bright again: V=" + val);
            Assert.LessOrEqual(val, 0.5f, "Schilf too dark: V=" + val);

            // Gellich-gruen: Green groesser als Red, Blue deutlich niedrig (kein cremeweiss).
            Assert.Greater(col.g, col.r, "Not green-dominant: g<=r.");
            Assert.Less(col.b, col.g * 0.6f, "Too desaturated/creamy: blue high.");
            Assert.GreaterOrEqual(sat, 0.45f, "Creameiss again: saturation too low.");

            // Matte: die Specular-Highlights bleached die Blatte (Smoothness 0.35) -> niedrig.
            Assert.LessOrEqual(reed.GetFloat("_Smoothness"), 0.2f, "Reed still glossy (bleaches under light).");
        }
    }
}
