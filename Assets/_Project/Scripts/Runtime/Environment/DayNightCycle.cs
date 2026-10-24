using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.HighDefinition;

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
        public float moonLux = 50000f;

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

        // WALD2a3: daytime sun color from the Builder (WaldLook.SunColor), so the runtime
        // cycle does not overwrite it with a hardcoded value. Default stays at the old
        // baseline so runtime-created components (tests) keep their behaviour.
        [Tooltip("Sun tint during day (from WaldLook)")]
        public Color daySunColor = new Color(1f, 0.93f, 0.82f);

        // T7 Morgennebel: Nebeldensity follows the time of day. The factor multiplies
        // the baseline fog density (2.0 = twice as dense as noon). Piecewise-linear keys
        // keep the curve continuous (no jumps; sampled on a fine grid the adjacent values
        // differ by well under 0.05). Morning plateau 0.05..0.2, evening ramp to 1.4 at 0.75.
        private static readonly float[] fogKeysT = { 0f, 0.05f, 0.2f, 0.35f, 0.7f, 0.75f, 0.85f, 0.9f, 1f };
        private static readonly float[] fogKeysV = { 1f, 2f, 2f, 1f, 1f, 1.4f, 1.4f, 1f, 1f };

        /// <summary>T7: Nebelfactor at a day progress (0 = midnight, 0.5 = noon).
        /// Morning (0.05..0.2) 2.0x denser than noon, evening 1.4x, night ~1.0x.</summary>
        public static float FogFactor(float dayProgress)
        {
            float t = dayProgress - Mathf.Floor(dayProgress);
            for (int i = 0; i < fogKeysT.Length - 1; i++)
            {
                if (t <= fogKeysT[i + 1])
                {
                    float span = fogKeysT[i + 1] - fogKeysT[i];
                    float u = span > 0f ? (t - fogKeysT[i]) / span : 0f;
                    return Mathf.Lerp(fogKeysV[i], fogKeysV[i + 1], u);
                }
            }
            return fogKeysV[fogKeysV.Length - 1];
        }

        /// <summary>T7: The HDRP Fog volume driven by the cycle (its meanFreePath is
        /// divided by the fog factor: smaller path = denser fog). Set by the Builder.</summary>
        [Tooltip("HDRP Fog volume whose meanFreePath follows the day (set by the Builder)")]
        public Fog fogVolumeComponent;

        private float fogBaseMeanFreePath = -1f; // baseline read once from the volume
        private float fogBaseDensity = -1f; // baseline RenderSettings.fogDensity (visible when volumetric off)
        private bool fogMissingWarned;

        /// <summary>FARBE1a: The HDRP PhysicallyBasedSky built by the Builder. The sky renders a
        /// full-bright DAY sky whenever the main light sits above the horizon — and the cycle
        /// parks the "moon" above the horizon at night (J5). Without this link the night frame
        /// shows a bright day sky (J5 night was even brighter than noon). The cycle drives the
        /// sky's multiplier with the day factor, so the sky goes dark with the night.</summary>
        [Tooltip("HDRP PhysicallyBasedSky whose brightness follows the day factor (set by the Builder)")]
        public PhysicallyBasedSky skyVolume;

        /// <summary>FARBE1a: The HDRP Exposure component of the scene volume. Auto-exposure
        /// (AutomaticHistogram) normalises day AND night to mid-grey, which washes out the
        /// day/night difference in fixed-exposure captures. Tests switch it to Fixed via
        /// SetFixedExposureForTests.</summary>
        [Tooltip("HDRP Exposure component (tests switch it to fixed for reproducible captures)")]
        public Exposure exposureVolume;

        /// <summary>FARBE1a: Sky multiplier at deep night. 0.02 keeps the sky a dim twilight
        /// glow (moon-lit scene reads dark but not pitch black): measured day/night raw ratio
        /// at 0.003 was ~64 (night Mittel 2.2 — below the 10..45 window); the sky (via dynamic
        /// ambient) dominates the frame, so 0.02 lands night at ~20-25 (ratio ~6, matches the
        /// 16000 lx moonlight design where night is ~5-10x darker than day).</summary>
        [Tooltip("Sky brightness multiplier at deep night (0 = pitch black)")]
        public float skyNightMultiplier = 0.01f;

        /// <summary>J5: dayFactor (0 = deep night, 1 = full day) for a day fraction with explicit
        /// dawn/dusk ramps. Pure function so EditMode tests can pin the ramp shape (and the
        /// SetDayProgress shortcut uses the SAME curve as Update). nightMinutes is passed in
        /// because the field is private.</summary>
        public static float DayFactorAt(float dayFraction, float lengthMinutes, float transitionMinutes, float nightMinutes)
        {
            float rampFrac = Mathf.Max(transitionMinutes, 0.01f) / lengthMinutes;
            float coreMinutes = Mathf.Min(nightMinutes, Mathf.Max(lengthMinutes - 2f * transitionMinutes, 0f));
            float coreFrac = coreMinutes / lengthMinutes;
            float distFromMidnight = Mathf.Min(dayFraction, 1f - dayFraction);
            float dayFactor = distFromMidnight <= coreFrac * 0.5f ? 0f : 1f;
            if (distFromMidnight > coreFrac * 0.5f && rampFrac > 0f)
            {
                dayFactor = Mathf.Min(1f, (distFromMidnight - coreFrac * 0.5f) / rampFrac);
            }
            return dayFactor;
        }

        /// <summary>T7: The current scaled meanFreePath of the wired fog volume, or -1 when
        /// no fog volume is wired. Exposed as a plain float so Tests (no HDRP dependency)
        /// can assert the curve really drives the scene's fog.</summary>
        public float CurrentFogMeanFreePath()
        {
            if (fogVolumeComponent == null || !fogVolumeComponent.meanFreePath.overrideState)
            {
                return -1f;
            }
            return fogVolumeComponent.meanFreePath.value;
        }

        /// <summary>T7: The baseline (noon) meanFreePath captured from the volume, or -1
        /// when no fog volume is wired.</summary>
        public float CurrentFogBaseline()
        {
            return fogBaseMeanFreePath;
        }

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
            // T7: capture the baseline fog distance once so the runtime curve scales it.
            if (fogVolumeComponent != null && fogVolumeComponent.meanFreePath.overrideState)
            {
                fogBaseMeanFreePath = fogVolumeComponent.meanFreePath.value;
            }
            if (RenderSettings.fog)
            {
                fogBaseDensity = RenderSettings.fogDensity;
            }
        }

        /// <summary>WALD8c: Tests-only switch of the volumetric fog in the wired volume (plain bool
        /// so the test assembly needs no HDRP reference). False when no volume is wired.</summary>
        public bool SetVolumetricFogForTests(bool on)
        {
            if (fogVolumeComponent == null || !fogVolumeComponent.enableVolumetricFog.overrideState) return false;
            fogVolumeComponent.enableVolumetricFog.Override(on);
            return true;
        }

        /// <summary>FARBE1a: Tests-only switch of the wired HDRP Exposure to Fixed with the given
        /// EV. Auto-exposure normalises night up to day brightness (and adapts over frames),
        /// which destroys the fixed-exposure day/night measurement. False when no volume is wired.</summary>
        public bool SetFixedExposureForTests(float ev)
        {
            if (exposureVolume == null) return false;
            exposureVolume.mode.Override(ExposureMode.Fixed);
            exposureVolume.fixedExposure.Override(ev);
            return true;
        }

        /// <summary>FARBE1a: The current sky multiplier (1 = full day sky), or -1 when no sky
        /// volume is wired. Plain float so Tests (no HDRP reference) can log the sky source.</summary>
        public float CurrentSkyMultiplier()
        {
            return skyVolume == null ? -1f : skyVolume.multiplier.value;
        }

        /// <summary>FARBE1a: Drives the PhysicallyBasedSky brightness with the day factor.
        /// The HDRP sky follows the MAIN LIGHT elevation; the cycle parks the moon ABOVE the
        /// horizon at night (J5, otherwise the scene would be pitch black), so without this link
        /// the sky keeps rendering a full-bright day sky at night. Multiplier 1 at noon matches
        /// the previous EV-0 baseline; deep night goes to skyNightMultiplier (near black), which
        /// also darkens the dynamic ambient (SkyAmbientMode.Dynamic derives from the sky).</summary>
        private void ApplySky()
        {
            if (skyVolume == null) return;
            float m = Mathf.Lerp(skyNightMultiplier, 1f, CurrentDayFactor);
            skyVolume.multiplier.Override(m);
        }

        /// <summary>Tests/Screenshot: Jump to a day progress (0 = midnight, 0.5 = noon).</summary>
        public void SetDayProgress(float progress)
        {
            dayFraction = progress - Mathf.Floor(progress);
            DayProgress = dayFraction;
            // S5: a Test-Jump is not a natural sunrise — no event, only the baseline moves.
            lastDayFraction = dayFraction;
            ApplyFog();
            // FARBE1a: recompute the light/sky state for the jumped fraction immediately,
            // so a capture right after the jump already shows the new time of day.
            float lengthMinutes = dayLengthMinutes > 0.01f ? dayLengthMinutes : 0.01f;
            CurrentDayFactor = DayFactorAt(dayFraction, lengthMinutes, Mathf.Max(transitionMinutes, 0.01f), nightMinutes);
            CurrentNightFactor = 1f - CurrentDayFactor;
            if (sun != null)
            {
                float phase = (dayFraction - 0.25f) * Mathf.PI * 2f;
                float elevDeg = Mathf.Asin(Mathf.Sin(phase)) * Mathf.Rad2Deg;
                float azDeg = phase * Mathf.Rad2Deg + 90f;
                float elevLight = elevDeg >= 0f ? elevDeg : -elevDeg;
                float azLight = elevDeg >= 0f ? azDeg : azDeg + 180f;
                sun.transform.rotation = Quaternion.Euler(elevLight, azLight, 0f);
                sun.intensity = Mathf.Lerp(moonLux, dayLux, CurrentDayFactor);
                sun.color = Color.Lerp(daySunColor, moonTint, 1f - CurrentDayFactor);
            }
            ApplySky();
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

            ApplyFog();

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
            float dayFactor = DayFactorAt(dayFraction, lengthMinutes, Mathf.Max(transitionMinutes, 0.01f), nightMinutes);
            CurrentDayFactor = dayFactor;
            CurrentNightFactor = 1f - dayFactor;
            sun.intensity = Mathf.Lerp(moonLux, dayLux, dayFactor);
            sun.color = Color.Lerp(
                daySunColor, moonTint, 1f - dayFactor);

            // FARBE1a: the sky brightness must follow the day factor (see ApplySky).
            ApplySky();

            ambientTimer += Time.deltaTime;
            if (ambientTimer >= ambientUpdateSeconds)
            {
                ambientTimer = 0f;
                DynamicGI.UpdateEnvironment();
            }
        }

        /// <summary>T7: Scales the HDRP Fog volume's meanFreePath by the fog factor.
        /// A higher factor means denser fog, so the light path gets SHORTER (divided).
        /// No-op when no fog volume is wired (e.g. unit-style setups without volumes).</summary>
        private void ApplyFog()
        {
            float factor = FogFactor(DayProgress);
            // HDRP: the visible fog is either the volumetric Fog volume (meanFreePath) or, when
            // volumetric fog is off (Urwald uses the classic exponential-squared fog for near-field
            // sharpness, see U1), RenderSettings.fogDensity. Scale BOTH so the curve always shows.
            if (fogVolumeComponent != null && fogVolumeComponent.meanFreePath.overrideState)
            {
                if (fogBaseMeanFreePath <= 0f)
                {
                    fogBaseMeanFreePath = fogVolumeComponent.meanFreePath.value;
                }
                if (fogBaseMeanFreePath > 0f)
                {
                    float scaled = fogBaseMeanFreePath / Mathf.Max(factor, 0.01f);
                    fogVolumeComponent.meanFreePath.Override(scaled);
                }
            }
            if (RenderSettings.fog)
            {
                if (fogBaseDensity <= 0f)
                {
                    fogBaseDensity = RenderSettings.fogDensity;
                }
                if (fogBaseDensity > 0f)
                {
                    RenderSettings.fogDensity = fogBaseDensity * factor;
                }
            }
        }
    }
}
