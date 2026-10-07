using UnityEngine;

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
        public float moonLux = 350f;

        /// <summary>Interval for updating the dynamic ambient light in seconds.</summary>
        [Tooltip("Interval for DynamicGI.UpdateEnvironment in seconds")]
        public float ambientUpdateSeconds = 2f;

        /// <summary>Progress of the day 0..1 (0 = midnight, 0.5 = noon). Read-only for Tests.</summary>
        public float DayProgress;

        private float dayFraction;
        private float ambientTimer;

        // Night color as configuration value (HDRP: Fog-Albedo lives in the Volume,
        // the fog color itself is not changed at runtime).
        [Tooltip("Sun tint at night (moonlight)")]
        private Color moonTint = new Color(0.55f, 0.62f, 0.85f);

        private void Start()
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null)
                {
                    Debug.LogWarning("[Spinnenspiel] DayNightCycle: sun not set and RenderSettings.sun empty.");
                }
            }
        }

        private void Update()
        {
            float lengthMinutes = dayLengthMinutes > 0.01f ? dayLengthMinutes : 0.01f;
            dayFraction += Time.deltaTime / (lengthMinutes * 60f);
            if (dayFraction >= 1f) dayFraction -= 1f;
            DayProgress = dayFraction;

            if (sun == null) return;

            // Phase: t=0 midnight -> -90°, t=0.25 sunrise, t=0.5 noon +90°, t=0.75 sunset.
            float phase = (dayFraction - 0.25f) * Mathf.PI * 2f;
            float elevDeg = Mathf.Asin(Mathf.Sin(phase)) * Mathf.Rad2Deg;
            float azDeg = phase * Mathf.Rad2Deg + 90f;

            sun.transform.rotation = Quaternion.Euler(elevDeg, azDeg, 0f);

            float dayFactor = Mathf.Clamp01((elevDeg + 6f) / 12f);
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
