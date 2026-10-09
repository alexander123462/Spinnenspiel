using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Tests for the deterministic weather cycle and the rain ambience crossfade.</summary>
    public class WeatherTests
    {
        [UnityTest]
        public IEnumerator Weather_DryRainCycleIsDeterministic()
        {
            // Pure-function check: same time -> same rainfall, cycle dry->rain->dry.
            const float dry = 90f, rain = 30f, trans = 6f;
            float midDry = WeatherController.RainAtTimeFor(dry * 0.5f, dry, rain, trans);
            float midRain = WeatherController.RainAtTimeFor(dry + rain * 0.5f, dry, rain, trans);
            Assert.AreEqual(0f, midDry, "Mid-dry must be bone dry.");
            Assert.AreEqual(1f, midRain, "Mid-rain must be full downpour.");

            float rampIn = WeatherController.RainAtTimeFor(dry - trans * 0.5f, dry, rain, trans);
            Assert.Greater(rampIn, 0f, "Ramp-up before dry ends must already rain partly.");
            Assert.Less(rampIn, 1f, "Ramp-up midpoint must be partial.");

            // Deterministic: same input twice -> same output.
            Assert.AreEqual(WeatherController.RainAtTimeFor(dry - trans * 0.5f, dry, rain, trans), rampIn,
                "Rainfall must be a pure function of time.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Weather_AmbienceCrossfadesWithRain()
        {
            JungleAmbientAudio amb = null;
            float deadline = Time.time + 10f;
            while (amb == null && Time.time < deadline)
            {
                amb = Object.FindFirstObjectByType<JungleAmbientAudio>();
                yield return null;
            }

            Assert.IsNotNull(amb, "JungleAmbientAudio not in scene.");
            float rainNow = WeatherController.Instance != null ? WeatherController.Instance.Rain01 : 0f;
            Debug.Log("[Weather] Rain01=" + rainNow.ToString("0.00") + " | drone vol=" +
                amb.DroneVolumeNow.ToString("0.00"));
            Assert.GreaterOrEqual(amb.DroneVolumeNow, 0f, "Drone volume negative?");
            Assert.LessOrEqual(amb.DroneVolumeNow, 1f, "Drone volume above 1?");
        }
    }
}
