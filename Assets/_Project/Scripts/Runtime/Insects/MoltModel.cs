using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// T4 Haeutungs-Model (reine Logik, no Unity dependency): when the spider reaches a
    /// new growth stage it molts for 5 s — during that window it cannot move and takes
    /// double hazard damage. The size increase is applied only AFTER the molt finished
    /// (SpiderGrowth holds the pending scale and applies it once Tick ends the molt).
    /// </summary>
    public class MoltModel
    {
        /// <summary>T4: duration of one molt in seconds (constant).</summary>
        public static readonly float moltDuration = 5f;

        /// <summary>T4: hazard-damage multiplier while molting (constant, "doppelt verwundbar").</summary>
        public static readonly float damageFactorWhileMolting = 2f;

        /// <summary>Remaining seconds of the current molt (0 when not molting).</summary>
        private float _remaining;

        /// <summary>True while a molt runs.</summary>
        public bool IsMolting { get; private set; }

        /// <summary>T9: remaining molt seconds, readable for the savegame (0 when not molting).</summary>
        public float RemainingSeconds => _remaining;

        /// <summary>T9: restore a saved molt state (remaining > 0 -> molting again).</summary>
        public void Restore(float remainingSeconds)
        {
            if (remainingSeconds > 0f)
            {
                IsMolting = true;
                _remaining = Mathf.Min(remainingSeconds, moltDuration);
            }
            else
            {
                Reset();
            }
        }

        /// <summary>Start one molt (called on a GrowthStage increase).</summary>
        public void Begin()
        {
            IsMolting = true;
            _remaining = moltDuration;
        }

        /// <summary>
        /// Advance one tick by dt seconds. Returns true while the molt still runs,
        /// false once it ended (the caller applies the pending size then).
        /// </summary>
        public bool Tick(float dt)
        {
            if (!IsMolting)
            {
                return false;
            }
            _remaining -= dt;
            if (_remaining <= 0f)
            {
                IsMolting = false;
                _remaining = 0f;
                return false;
            }
            return true;
        }

        /// <summary>Hazard damage multiplier: 2 while molting, otherwise 1.</summary>
        public float DamageFactor()
        {
            return IsMolting ? damageFactorWhileMolting : 1f;
        }

        /// <summary>Fresh partie: no molt running.</summary>
        public void Reset()
        {
            IsMolting = false;
            _remaining = 0f;
        }
    }
}
