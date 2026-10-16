using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Z4 Tag/Nacht: the Sun rotates slowly (one day = 20 min), DayProgress rises
    /// linearly, and the brightness of the sun follows the altitude of the sun.
    /// </summary>
    public class DayNightTests
    {
        /// <summary>J5: Fixed exposure for the proof shots (G6-Lehre: NOT auto-exposure, so
        /// the Mittel of both images is comparable and reproducible across runs).</summary>
        // J5: Raw noon luma ~5.6E-2, night ~2.7E-3 (measured). E=20 puts noon Mittel
        // ~140..190 (window 60..200) and night ~10..20 (window 10..45).
        private const float fixedExposure = 20f;

        /// <summary>J5: Live flies (day species) via their spawner 'Insekten' (activeAtNight=false),
        /// so the disabled template is not miscounted.</summary>
        private static int DayInsectCount()
        {
            GameObject root = GameObject.Find("Insekten");
            Assert.IsNotNull(root, "Spawner 'Insekten' missing in Urwald.");
            InsectSpawner spawner = root.GetComponent<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner missing on 'Insekten'.");
            return spawner.AliveCount;
        }

        /// <summary>J5: Live moths (night species) via their spawner 'Insekten_Motte'.</summary>
        private static int NightInsectCount()
        {
            GameObject root = GameObject.Find("Insekten_Motte");
            Assert.IsNotNull(root, "Spawner 'Insekten_Motte' missing in Urwald.");
            InsectSpawner spawner = root.GetComponent<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner missing on 'Insekten_Motte'.");
            return spawner.AliveCount;
        }

        [UnityTest]
        public IEnumerator Tag_Nacht_SunRotatesWithDayLength()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;

            DayNightCycle cycle = null;
            float timeout = 15f;
            while (timeout > 0f && cycle == null)
            {
                cycle = Object.FindFirstObjectByType<DayNightCycle>();
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(cycle, "No DayNightCycle found in the Urwald scene.");

            Light sun = Object.FindFirstObjectByType<Light>();
            Assert.IsNotNull(sun, "No Sun found in the Urwald scene.");

            // Setting value: one day = 20 min (default from the Inspector).
            Assert.AreEqual(20f, cycle.dayLengthMinutes);

            float progressBefore = cycle.DayProgress;
            Vector3 fwdBefore = sun.transform.forward;

            // Simulate approx. 12 s of GAME time (CI runs at high fps; wall-clock differs).
            const float elapsed = 12f;
            float waited = 0f;
            while (waited < elapsed)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // Expectation: DayProgress rises by elapsed / (dayLength*60).
            float expectedDelta = elapsed / (cycle.dayLengthMinutes * 60f);
            float actualDelta = cycle.DayProgress - progressBefore;
            Assert.Greater(actualDelta, 0f, "DayProgress does not increase.");
            Assert.Less(Mathf.Abs(actualDelta - expectedDelta), 0.15f * expectedDelta,
                "DayProgress rises too strongly/weakeley from the day length ("
                + actualDelta.ToString("F4") + " vs. expected " + expectedDelta.ToString("F4") + ").");

            // The Sun has rotated (forward vector differs).
            float dot = Mathf.Clamp(Vector3.Dot(fwdBefore, sun.transform.forward), -1f, 1f);
            float rotDeg = Mathf.Acos(dot) * (180f / Mathf.PI);
            Assert.GreaterOrEqual(rotDeg, 2, "Sun did not rotate within 12 s.");

            // Brightness follows the altitude: never brighter than dayLux, at night darker.
            Assert.LessOrEqual(sun.intensity, cycle.dayLux * 1.01f, "Sun is brighter than dayLux.");
        }
        /// <summary>
        /// J5: Fixed-exposure capture (NOT auto-exposure!). The G6-Learning says auto-exposure
        /// is not reproducible and would wash out the day/night difference; a fixed factor keeps
        /// Mittel comparable across runs. Returns (Mittel 0..255, Max, Magenta count).
        /// </summary>
        /// <summary>J5: Raw (pre-exposure) luma mean of the last SaveFrameFixed call.</summary>
        private static float lastRawLumaMean;

        private static Vector3 SaveFrameFixed(Camera cam, int w, int h, string file, float exposure)
        {
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var hdr = new Texture2D(w, h, TextureFormat.RGBAHalf, false);
            hdr.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            hdr.Apply(false);
            RenderTexture.active = null;
            cam.targetTexture = null;

            Color[] px = hdr.GetPixels();
            int n = px.Length;
            Color[] outPx = new Color[n];
            double sum = 0.0;
            double rawSum = 0.0;
            int maxv = 0;
            long magenta = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                rawSum += 0.299 * Mathf.Max(c.r, 0f) + 0.587 * Mathf.Max(c.g, 0f) + 0.114 * Mathf.Max(c.b, 0f);
                float r = AcesTone(Mathf.Max(c.r * exposure, 0f));
                float g = AcesTone(Mathf.Max(c.g * exposure, 0f));
                float b = AcesTone(Mathf.Max(c.b * exposure, 0f));
                outPx[i] = new Color(r, g, b, 1f);
                float luma = 0.299f * r + 0.587f * g + 0.114f * b;
                sum += luma * 255.0;
                int ri = (int)(Mathf.Max(r, Mathf.Max(g, b)) * 255f);
                if (ri > maxv) maxv = ri;
                // J5: only near-pure error-magenta (Unity's hole colour is exactly (1,0,1));
                // the Urwald has real pink flowers that a loose threshold would miscount.
                if (r > 0.96f && g < 0.06f && b > 0.96f) magenta++;
            }

            var ldr = new Texture2D(w, h, TextureFormat.RGB24, false);
            ldr.SetPixels(outPx);
            ldr.Apply(false);
            System.IO.File.WriteAllBytes(file, ldr.EncodeToPNG());
            Object.DestroyImmediate(hdr);
            Object.DestroyImmediate(ldr);
            Object.DestroyImmediate(rt);
            lastRawLumaMean = (float)(rawSum / n);
            return new Vector3((float)(sum / n), maxv, magenta);
        }

        private static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
        }

        /// <summary>
        /// Sun-Test: The DayNightCycle rotates the sun and adjusts its brightness
        /// according to the altitude (night darker than day).
        /// </summary>
        [UnityTest]
        public IEnumerator Tag_Nacht_SunRotatesAndDimms()
        {
            GameObject sunGo = new GameObject("SunTest");
            Light testSun = sunGo.AddComponent<Light>();
            testSun.type = LightType.Directional;

            DayNightCycle cycle = sunGo.AddComponent<DayNightCycle>();
            cycle.sun = testSun;
            // Speed up: one day = 12 s so the rotation is measurable in a Test.
            cycle.dayLengthMinutes = 0.2f;

            Vector3 fwdBefore = testSun.transform.forward;
            float waited = 0f;
            while (waited < 3f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(cycle.DayProgress, 0f, "DayProgress does not increase.");

            float dot = Mathf.Clamp(Vector3.Dot(fwdBefore, testSun.transform.forward), -1f, 1f);
            float rotDeg = Mathf.Acos(dot) * (180f / Mathf.PI);
            Assert.Greater(rotDeg, 30f, "Sun did not rotate within 3 s.");

            // The brightness follows the altitude and never exceeds dayLux.
            Assert.LessOrEqual(testSun.intensity, cycle.dayLux * 1.01f, "Sun is brighter than dayLux.");
            Assert.GreaterOrEqual(testSun.intensity, cycle.moonLux * 0.99f, "Sun is darker than moonLux.");

            GameObject destroyed = sunGo;
            destroyed.GetComponent<DayNightCycle>().sun = null;
            GameObject.DestroyImmediate(destroyed);
        }

        /// <summary>
        /// J5: Cycle changes light AND insects. Day flies out by day, moths out at night.
        /// Fixed exposure (G6-Lehre: auto-exposure not reproducible) keeps the two proof
        /// images comparable: noon must be clearly brighter than midnight.
        /// </summary>
        [UnityTest]
        public IEnumerator J5_TagAndNacht_LightAndInsectsFollowCycle()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;

            DayNightCycle cycle = null;
            float timeout = 15f;
            while (timeout > 0f && cycle == null)
            {
                cycle = Object.FindFirstObjectByType<DayNightCycle>();
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(cycle, "No DayNightCycle in Urwald.");

            cycle.dayLengthMinutes = 0.2f; // 12 s per day so both phases are reachable.
            cycle.transitionMinutes = 0.04f; // 2.4 s dawn/dusk ramps for the short test cycle.

            // G6-Lehre: ERST the world ready, THEN capture (otherwise black frames).
            timeout = 25f;
            JungleWorldGenerator worldGen = null;
            while (timeout > 0f && worldGen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady)
                {
                    worldGen = g;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(worldGen, "Urwald world (Seed 4242) never ready.");

            Camera cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "No Camera in Urwald.");

            // Wait until high noon (dayFactor near 1).
            timeout = 40f;
            while (timeout > 0f && cycle.CurrentDayFactor < 0.95f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(timeout, 0f, "Noon never reached.");

            // Count per species via the spawners (FindObjectsByType would count the
            // disabled templates as well). activeAtNight=false -> day species (flies).
            int fliesAtNoon = DayInsectCount();
            int mothsAtNoon = NightInsectCount();
            Vector3 noonStats = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-noon.png", fixedExposure);
            Debug.Log("[J5-noon] Mittel " + noonStats.x.ToString("F1") + " Max " + noonStats.y.ToString("F0") + " Magenta " + noonStats.z.ToString("F0") + " Raw " + lastRawLumaMean.ToString("E2"));

            // Wait until deep night (dayFactor near 0).
            timeout = 40f;
            while (timeout > 0f && cycle.CurrentDayFactor > 0.05f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(timeout, 0f, "Night never reached.");

            int fliesAtNight = DayInsectCount();
            int mothsAtNight = NightInsectCount();
            Vector3 nightStats = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-night.png", fixedExposure);
            Debug.Log("[J5-night] Mittel " + nightStats.x.ToString("F1") + " Max " + nightStats.y.ToString("F0") + " Magenta " + nightStats.z.ToString("F0") + " Raw " + lastRawLumaMean.ToString("E2"));

            // Backlog criteria: day Mittel 60..200, night 10..45, both Magenta 0.
            Assert.GreaterOrEqual(noonStats.x, 60f, "Day Mittel < 60 (fixed exposure).");
            Assert.LessOrEqual(noonStats.x, 200f, "Day Mittel > 200 (blendout).");
            Assert.GreaterOrEqual(nightStats.x, 10f, "Night Mittel < 10 (too dark).");
            Assert.LessOrEqual(nightStats.x, 45f, "Night Mittel > 45 (not dark enough).");
            Assert.LessOrEqual(noonStats.z + nightStats.z, 0f, "Magenta pixels (holes) in proof images.");

            // Species swap: flies out by day (8), retreated at night; moths reversed.
            Assert.GreaterOrEqual(fliesAtNoon, 6, "Day insects not out at noon.");
            Assert.LessOrEqual(fliesAtNight, 2, "Day insects still out at night.");
            // Backlog: Mottenanzahl nachts mindestens dreimal so hoch wie tagsueber.
            Assert.GreaterOrEqual(mothsAtNight, 3 * Mathf.Max(mothsAtNoon, 1),
                "Moths at night not >= 3x moths at noon (" + mothsAtNight + " vs. " + mothsAtNoon + ").");

            // Fixed exposure: noon clearly brighter than midnight (visible light change).
            Assert.Greater(noonStats.x, nightStats.x * 1.4f, "Noon not clearly brighter than night (fixed exposure).");
        }
    }
}
