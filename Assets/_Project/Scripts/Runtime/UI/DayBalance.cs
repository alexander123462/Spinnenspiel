using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// S5 Tagesbilanz: at the transition Night -> Morning (sunrise-crossing of DayNightCycle)
    /// a short window shows the day that just ended: eaten prey, built webs, growth stage
    /// and the biggest danger (bird pre-warnings). Data comes from PartieStatsModel per day.
    /// The window stays open 6 s or as long as the player holds the B-key.
    /// </summary>
    public class DayBalance : MonoBehaviour
    {
        [Tooltip("Seconds the balance window stays open after sunrise.")]
        [SerializeField] private float showSeconds = 6f;

        /// <summary>True while the morning-window is on screen (tests and proofshot).</summary>
        public bool WindowOpen;

        private PartieStatsModel _stats;
        private SpiderGrowth _growth;
        private DayNightCycle _cycle;
        private int _pendingWarnings;
        private float _windowTimer;
        private int _stageShown;

        /// <summary>Builder/Test: hook the sunrise event of the cycle to this window.</summary>
        public void SetCycle(DayNightCycle cycle)
        {
            if (cycle != null && _cycle != cycle)
            {
                // Runtime-created components (tests) have no auto-instantiated event.
                if (cycle.MorningFired == null)
                {
                    cycle.MorningFired = new UnityEvent();
                }
                cycle.MorningFired.AddListener(OnMorning);
                _cycle = cycle;
            }
        }

        /// <summary>BirdThreat.WarnFired: count the pre-warning into the day (S5 biggest danger).</summary>
        public void OnWarning()
        {
            _pendingWarnings++;
        }

        private void Update()
        {
            if (_stats == null)
            {
                var deathStats = transform.GetComponent<DeathStats>();
                if (deathStats != null)
                {
                    _stats = deathStats.Stats;
                }
            }
            if (_growth == null)
            {
                _growth = transform.GetComponent<SpiderGrowth>();
            }

            FlushPendingIntoStats();

            if (_windowTimer > 0f)
            {
                Keyboard keyboard = Keyboard.current;
                bool holdKey = keyboard != null && keyboard.bKey.value > 0.5f;
                _windowTimer -= Time.deltaTime;
                WindowOpen = _windowTimer > 0f || holdKey;
            }
            else
            {
                WindowOpen = false;
            }
        }

        private void OnMorning()
        {
            if (_stats == null)
            {
                return;
            }
            // Snapshot BEFORE the reset, so the window shows the day that just ended.
            FlushPendingIntoStats();
            _stageShown = _growth != null ? _growth.GrowthStage : 0;
            _windowTimer = showSeconds;
            WindowOpen = true; // same frame as the sunrise event, independent of Update order.
            _stats.ResetDay();
        }

        private void FlushPendingIntoStats()
        {
            while (_pendingWarnings > 0 && _stats != null)
            {
                _stats.OnBirdWarning();
                _pendingWarnings--;
            }
        }

        /// <summary>The exact display line for the window (single source of truth for UI and tests).</summary>
        public string FormatBalanceLine()
        {
            return _stats != null ? _stats.FormatDayLine(_stageShown) : "Tag: -";
        }

        private void OnGUI()
        {
            if (!WindowOpen || _stats == null)
            {
                return;
            }

            // Dark panel behind the text (readable over any sky).
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(16f, Screen.height * 0.14f, 520f, 110f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Tick rows: one per eaten prey / built web / warning of the day that ended.
            foreach (var t in _stats.DayBalanceTicks(Screen.width, Screen.height))
            {
                GUI.DrawTexture(new Rect(t.x, t.y, t.z, t.w), Texture2D.whiteTexture);
            }

            // The exact line from the model.
            GUI.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            GUI.Label(new Rect(24f, Screen.height * 0.16f, 620f, 26f), FormatBalanceLine());
            GUI.color = Color.white;
        }

        private void OnDisable()
        {
            if (_cycle != null)
            {
                _cycle.MorningFired.RemoveListener(OnMorning);
            }
        }
    }
}
