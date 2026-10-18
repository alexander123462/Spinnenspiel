using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Spinnenspiel
{
    /// <summary>
    /// Tag/Nacht-Cycle: DIRECTIONAL-SUN rotates around the world slowly,
    /// adjusts its brightness and Nebel according to the altitude of the sun,
    /// updates the dynamic ambient light (from Skybox) at regular intervals.
    /// The day length is a configuration value (default 20 min per day).
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        /// <summary>The directional light set by the Builder.</summary>
        public Light sun;

        /// <summary>Duration of one full day in minutes.</summary>
        [Tooltip("Duration of one full day/night cycle in minutes")]
        public float dayLengthMinutes = 20f;

        /// <summary>Brightness of the sun at noon (Lux, HDRP).</summary>
        [Tooltip("Sun brightness at noon in Lux")]
        public float dayLux = 100000f;

        /// <summary>Brightness during night/moonlight (Lux).</summary>
        [Tooltip("Moon-like brightness at night in Lux")]
        public float moonLux = 16000f;

        /// <summary>Interval for updating the dynamic ambient light in seconds.</summary>
        [Tooltip("Interval for DynamicGI.UpdateEnvironment in seconds")]
        public float ambientUpdateSeconds = 2f;

        /// <summary>Progress of the day 0..1 (0 = midnight, 0.5 = noon). Read-only for Tests.</summary>
        public float DayProgress;

        /// <summary>J5: Brightness factor 0 (night) .. 1 (noon). Drives light AND insect activity.</summary>
        public float CurrentDayFactor;

        /// <summary>J5: Night factor 1 (deep night) .. 0 (full day). Ramps over transitionMinutes at dawn/dusk.</summary>
        public float CurrentNightFactor;

        /// <summary>S5 Tagesbilanz: fires exactly once when the cycle crosses sunrise (night -> morning).
        /// Initialized explicitly so runtime-created components (tests) can hook listeners.</summary>
        public UnityEvent MorningFired;

        /// <summary>J5: Minutes of the cycle that are dark (night core, ramps excluded).</summary>
        [Tooltip("Minutes per cycle that are dark (night core, ramps excluded)")]
        [SerializeField] private float nightMinutes = 6f;

        /// <summary>J5: Width in minutes of each dawn/dusk ramp (transition not abrupt).</summary>
        [Tooltip("Width in minutes of each dawn/dusk transition ramp")]
        public float transitionMinutes = 2f;

        /// <summary>Day progress at game start (default morning, 0.3 = just after sunrise).</summary>
        [Tooltip("Day progress at game start (0 = midnight, 0.5 = noon)")]
        [SerializeField] private float startDayFraction = 0.3f;

        private float dayFraction;
        private float ambientTimer;
        private float lastDayFraction; // S5: previous fraction for the sunrise-crossing check.

        // Night color as configuration value (HDRP: Fog-Albedo lives in the Volume,
        // the fog color itself is not changed at runtime).
        [Tooltip("Sun tint at night (moonlight)")]
        private Color moonTint = new Color(0.8f, 0.85f, 1f);

        private void Start()
        {
            dayFraction = startDayFraction - Mathf.Floor(startDayFraction);
            DayProgress = dayFraction;
            lastDayFraction = dayFraction;
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null)
                {
                    Debug.LogWarning("[Spinnenspiel] DayNightCycle: sun not set and RenderSettings.sun empty.");
                }
            }
        }

        /// <summary>Tests/Screenshot: Jump to a day progress (0 = midnight, 0.5 = noon).</summary>
        public void SetDayProgress(float progress)
        {
            dayFraction = progress - Mathf.Floor(progress);
            DayProgress = dayFraction;
            // S5: a Test-Jump is not a natural sunrise — no event, only the baseline moves.
            lastDayFraction = dayFraction;
        }

        private void Update()
        {
            float lengthMinutes = dayLengthMinutes > 0.01f ? dayLengthMinutes : 0.01f;
            dayFraction += Time.deltaTime / (lengthMinutes * 60f);
            if (dayFraction >= 1f) dayFraction -= 1f;
            DayProgress = dayFraction;

            // S5: sunrise at 0.25 — the event fires only on a natural forward crossing
            // (not on wrap-around midnight, not on a Test-Jump via SetDayProgress).
            if (lastDayFraction < 0.25f && dayFraction >= 0.25f)
            {
                if (MorningFired != null)
                {
                    MorningFired.Invoke();
                }
            }
            lastDayFraction = dayFraction;

            if (sun == null) return;

            // Phase: t=0 midnight -> -90°, t=0.25 sunrise, t=0.5 noon +90°, t=0.75 sunset.
            float phase = (dayFraction - 0.25f) * Mathf.PI * 2f;
            float elevDeg = Mathf.Asin(Mathf.Sin(phase)) * Mathf.Rad2Deg;
            float azDeg = phase * Mathf.Rad2Deg + 90f;

            // J5: at night the light acts as a MOON above the horizon (a sun below the
            // horizon lights nothing and would leave the scene pitch black).
            float elevLight = elevDeg >= 0f ? elevDeg : -elevDeg;
            float azLight = elevDeg >= 0f ? azDeg : azDeg + 180f;
            sun.transform.rotation = Quaternion.Euler(elevLight, azLight, 0f);

            // J5: dayFactor from the cycle fraction with explicit dawn/dusk ramps (not abrupt):
            // night core = nightMinutes centred at midnight, ramps of transitionMinutes on both sides.
            float rampFrac = Mathf.Max(transitionMinutes, 0.01f) / lengthMinutes;
            float coreMinutes = Mathf.Min(nightMinutes, Mathf.Max(lengthMinutes - 2f * transitionMinutes, 0f));
            float coreFrac = coreMinutes / lengthMinutes;
            float distFromMidnight = Mathf.Min(dayFraction, 1f - dayFraction);
            float dayFactor = distFromMidnight <= coreFrac * 0.5f ? 0f : 1f;
            if (distFromMidnight > coreFrac * 0.5f && rampFrac > 0f)
            {
                dayFactor = Mathf.Min(1f, (distFromMidnight - coreFrac * 0.5f) / rampFrac);
            }
            CurrentDayFactor = dayFactor;
            CurrentNightFactor = 1f - dayFactor;
            sun.intensity = Mathf.Lerp(moonLux, dayLux, dayFactor);
            sun.color = Color.Lerp(
                new Color(1f, 0.93f, 0.82f), moonTint, 1f - dayFactor);

            ambientTimer += Time.deltaTime;
            if (ambientTimer >= ambientUpdateSeconds)
            {
                ambientTimer = 0f;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
