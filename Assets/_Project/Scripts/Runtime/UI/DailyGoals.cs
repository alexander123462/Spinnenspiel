using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S8 Tagesziele: thin MonoBehaviour around DailyGoalsModel. Polls the counters of
    /// SpiderGrowth (eaten), OrbWeb.All (built webs + height) and SpiderEnergy (stings),
    /// hooks the sunrise event of DayNightCycle for the day roll, applies the rewards
    /// (+15 energy via SpiderEnergy.FeedEnergy, +25 durability on the web) and shows the
    /// three goals small at the right screen edge.
    /// </summary>
    public class DailyGoals : MonoBehaviour
    {
        /// <summary>The pure logic (single source of truth, read by UI and tests).</summary>
        public readonly DailyGoalsModel Goals = new DailyGoalsModel();

        private SpiderGrowth _growth;
        private SpiderEnergy _energy;
        private DayNightCycle _cycle;
        private int _seenEaten;
        private int _seenStings;
        private System.Collections.Generic.HashSet<int> _seenWebIds;

        public bool HasCycle { get => _cycle != null; }

        private void Start()
        {
            // T9: a loaded save already restored the day state (DayNumber >= 1) — rolling again
            // here would overwrite it. Roll only for a fresh partie.
            if (Goals.DayNumber == 0)
            {
                Goals.RollGoals(DailyGoalsModel.defaultGoalSeed, 1);
            }
            // T9: counters restored from a save must NOT feed the day goals again — the glue
            // tracks only events NEWER than Start.
            _growth = transform.GetComponent<SpiderGrowth>();
            if (_growth != null)
            {
                _seenEaten = _growth.EatenCount;
            }
            var energy = transform.GetComponent<SpiderEnergy>();
            if (energy != null)
            {
                _seenStings = energy.GetModel() != null ? energy.GetModel().StingCount : 0;
            }
        }

        /// <summary>Builder/Test: hook the sunrise event of the cycle to the day roll.</summary>
        public void SetCycle(DayNightCycle cycle)
        {
            if (cycle != null && _cycle != cycle)
            {
                if (cycle.MorningFired == null)
                {
                    cycle.MorningFired = new UnityEngine.Events.UnityEvent();
                }
                cycle.MorningFired.AddListener(OnMorning);
                _cycle = cycle;
            }
        }

        private void OnMorning()
        {
            Goals.OnMorning();
        }

        private void Update()
        {
            if (_growth == null)
            {
                _growth = transform.GetComponent<SpiderGrowth>();
            }
            if (_energy == null)
            {
                _energy = transform.GetComponent<SpiderEnergy>();
            }

            // Feed eaten insects from the single source of truth (S5 day counters).
            if (_growth != null)
            {
                while (_seenEaten < _growth.EatenCount)
                {
                    Goals.NotifyInsectEaten();
                    _seenEaten++;
                }
            }

            // Feed stings from the energy model (S8 stingless-night goal).
            if (_energy != null)
            {
                var stings = _energy.GetModel() != null ? _energy.GetModel().StingCount : 0;
                while (_seenStings < stings)
                {
                    Goals.NotifySting();
                    _seenStings++;
                }
            }

            // Feed newly built webs + their height (OrbWeb.All is the registry). Each web is
            // notified exactly once (tracked by instance ID).
            if (_seenWebIds == null)
            {
                _seenWebIds = new System.Collections.Generic.HashSet<int>();
            }
            foreach (OrbWeb web in OrbWeb.All)
            {
                if (web.IsBuilt && !_seenWebIds.Contains(web.GetInstanceID()))
                {
                    _seenWebIds.Add(web.GetInstanceID());
                    float y = web.Layout != null ? web.Layout.Hub.y : 0f;
                    var completedHigh = Goals.NotifyWebBuilt(y);
                    if (completedHigh && web.Durability != null)
                    {
                        web.Durability.Durability = Mathf.Min(
                            NetDurabilityModel.maxDurability,
                            web.Durability.Durability + DailyGoalsModel.rewardDurability);
                        DailyGoalsModel.GoalType consumed;
                        Goals.TakePendingReward(out consumed);
                    }
                }
            }

            ApplyPendingRewards();
        }

        /// <summary>Claim queued rewards: energy goals give +15 energy, the web goal was applied at the web itself.</summary>
        private void ApplyPendingRewards()
        {
            DailyGoalsModel.GoalType type;
            while (Goals.TakePendingReward(out type))
            {
                if (type != DailyGoalsModel.GoalType.BuildHighWeb && _energy != null)
                {
                    _energy.FeedEnergy(DailyGoalsModel.rewardEnergy);
                }
            }
        }

        /// <summary>Show the three goals small at the right screen edge (label bar + pips).</summary>
        private void OnGUI()
        {
            if (Goals.Goals == null)
            {
                return;
            }
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            foreach (UnityEngine.Vector4 r in Goals.HudRects(Screen.width, Screen.height))
            {
                GUI.DrawTexture(new Rect(r.x, r.y, r.z, r.w), Texture2D.whiteTexture);
            }
            GUI.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            for (var i = 0; i < Goals.Goals.Length; i++)
            {
                var y = (int)(Screen.height * 0.7f) + i * 18;
                GUI.Label(new Rect(Screen.width - 252, y, 240f, 16f), Goals.FormatGoalLine(i));
            }
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
