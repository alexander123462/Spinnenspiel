using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Deterministic weather cycle: dry and rain phases alternate (time-based, no RNG).
    /// Rain01 (0..1) drives the audio crossfade (JungleAmbientAudio) and stronger wind gusts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherController : MonoBehaviour
    {
        [SerializeField, Tooltip("Dry phase in s.")] private float drySeconds = 90f;
        [SerializeField, Tooltip("Rain phase in s.")] private float rainSeconds = 30f;
        [SerializeField, Tooltip("Ramp duration for dry->rain and rain->dry in s.")]
        private float transitionSeconds = 6f;

        public static WeatherController Instance { get; private set; }

        /// <summary>Current rainfall 0..1 (0 = dry, 1 = full downpour).</summary>
        public float Rain01 { get; private set; }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            Rain01 = RainAtTime(Time.time);
        }

        /// <summary>Rainfall at absolute time t — pure function, testable without scene.</summary>
        public float RainAtTime(float t)
        {
            return RainAtTimeFor(t, drySeconds, rainSeconds, transitionSeconds);
        }

        /// <summary>Pure cycle math for given phase lengths (used by tests and RainAtTime).</summary>
        public static float RainAtTimeFor(float t, float dry, float rain, float trans)
        {
            float cycle = dry + rain;
            if (cycle <= 0f || trans <= 0f)
            {
                return 0f;
            }

            float phase = t - Mathf.Floor(t / cycle) * cycle;
            if (phase < dry)
            {
                // End of dry phase: ramp up into rain.
                float toRain = dry - phase;
                return toRain < trans ? 1f - toRain / trans : 0f;
            }

            float inRain = phase - dry;
            if (inRain < trans)
            {
                return inRain / trans;
            }

            float toDry = rain - inRain;
            return toDry < trans ? toDry / trans : 1f;
        }
    }
}
