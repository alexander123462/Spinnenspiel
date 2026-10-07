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
    }
}
