using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// T6 Dynamische Musik (reine Logik, no Unity dependency): the background music
    /// bed has an intensity 0..1. Baseline 0.2 in safety, rises to 0.8 within 1 s
    /// while a danger is active (bird pre-warning, wasp, frog, molt), and falls
    /// slowly back to the baseline over 10 s once the danger left.
    /// </summary>
    public class DangerMusicModel
    {
        /// <summary>T6: intensity in safety (constant).</summary>
        public static readonly float baseline = 0.2f;

        /// <summary>T6: intensity during danger (constant).</summary>
        public static readonly float dangerLevel = 0.8f;

        /// <summary>T6: seconds from baseline to danger level while danger is active.</summary>
        public static readonly float riseTime = 1f;

        /// <summary>T6: seconds from danger level back to baseline once danger left.</summary>
        public static readonly float fallTime = 10f;

        private float _intensity = baseline;

        /// <summary>Current music intensity (baseline..dangerLevel).</summary>
        public float GetIntensity()
        {
            return _intensity;
        }

        /// <summary>Advance one tick by dt seconds given the current danger state.</summary>
        public void Tick(bool dangerActive, float dt)
        {
            if (dangerActive)
            {
                // Rise: baseline -> dangerLevel over riseTime.
                float step = (dangerLevel - baseline) * (dt / riseTime);
                _intensity = Mathf.Min(dangerLevel, _intensity + step);
            }
            else if (_intensity > baseline)
            {
                // Fall slowly back over fallTime.
                float step = (dangerLevel - baseline) * (dt / fallTime);
                _intensity = Mathf.Max(baseline, _intensity - step);
            }
        }

        /// <summary>Fresh partie: baseline intensity.</summary>
        public void Reset()
        {
            _intensity = baseline;
        }
    }
}
