using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S8 Tagesziele: reine Ziel-Modell ohne Unity-Abhaenging, so Tests pruefen it
    /// deterministically. Pro Tag drei kleine Ziele drawn from the list with Seed + Tagnummer
    /// (reproducible). Progress fed by the day counters (S5), rewards granted exactly once
    /// per goal (+15 Energie or Haltbarkeit +25 on the web that was built high).
    /// </summary>
    public class DailyGoalsModel
    {
        /// <summary>Default seed for the goal draw (DeathStats may override it).</summary>
        public static readonly int defaultGoalSeed = 20261018;

        /// <summary>How many goals are active per day.</summary>
        public static readonly int goalsPerDay = 3;

        /// <summary>A web counts as "high" when its centre is above this height (m).</summary>
        public static readonly float highWebMinHeightY = 2f;

        /// <summary>Insects the "catch N insects" goal asks for.</summary>
        public static readonly int catchGoalTarget = 3;

        /// <summary>Reward of the energy-type goals.</summary>
        public static readonly float rewardEnergy = 15f;

        /// <summary>Reward of the web-durability goal (percent).</summary>
        public static readonly float rewardDurability = 25f;

        /// <summary>The goal types drawn from. Each has one reward kind.</summary>
        public enum GoalType
        {
            CatchInsects,
            BuildHighWeb,
            StinglessNight
        }

        public static readonly GoalType[] AllGoalTypes = { GoalType.CatchInsects, GoalType.BuildHighWeb, GoalType.StinglessNight };

        /// <summary>Current day number (1 at partie start, +1 each morning).</summary>
        public int DayNumber;

        /// <summary>Seed of the goal draw (constant per partie unless overridden).</summary>
        public int Seed;

        /// <summary>The three goals active today (index-aligned with Completed).</summary>
        public GoalType[] Goals;

        /// <summary>True per goal index once it was completed and rewarded.</summary>
        public bool[] Completed;

        /// <summary>Catches counted since the last roll (drives CatchInsects).</summary>
        private int _catchProgress;

        /// <summary>Stings seen since the last roll (blocks StinglessNight).</summary>
        private int _stingsToday;

        /// <summary>Total reward grants (energy/durability), for tests and stats.</summary>
        public int RewardsGranted;

        /// <summary>FIFO of goal types whose reward was granted but not yet claimed by the glue layer.</summary>
        private System.Collections.Generic.Queue<GoalType> _rewardQueue;

        /// <summary>Mark goal i completed once and queue its reward (granted exactly once).</summary>
        private void Grant(int i)
        {
            Completed[i] = true;
            RewardsGranted++;
            if (_rewardQueue == null)
            {
                _rewardQueue = new System.Collections.Generic.Queue<GoalType>();
            }
            _rewardQueue.Enqueue(Goals[i]);
        }

        /// <summary>Claim the next pending reward from the glue layer. False when none is due.</summary>
        public bool TakePendingReward(out GoalType type)
        {
            type = GoalType.CatchInsects;
            if (_rewardQueue == null || _rewardQueue.Count == 0)
            {
                return false;
            }
            type = _rewardQueue.Dequeue();
            return true;
        }

        /// <summary>Roll the three goals of one day from Seed + dayNumber (deterministic, no duplicates).</summary>
        public void RollGoals(int seed, int dayNumber)
        {
            Seed = seed;
            DayNumber = dayNumber;
            _catchProgress = 0;
            _stingsToday = 0;
            var pool = new List<GoalType>(AllGoalTypes);
            Goals = new GoalType[goalsPerDay];
            Completed = new bool[goalsPerDay];
            // xorshift on (seed, dayNumber) -> reproducible draw of distinct goals.
            var state = ((uint)seed * 2654435761u) ^ (uint)((uint)dayNumber * 40503u + 0x9E3779B9u);
            for (var i = 0; i < goalsPerDay && pool.Count > 0; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                var pick = (int)(state % (uint)pool.Count);
                Goals[i] = pool[pick];
                pool.RemoveAt(pick);
            }
        }

        /// <summary>Insects eaten today (from the S5 day counters): complete CatchInsects at target.</summary>
        public void NotifyInsectEaten()
        {
            _catchProgress++;
            CheckCatchGoals();
        }

        private void CheckCatchGoals()
        {
            for (var i = 0; i < Goals.Length; i++)
            {
                if (Goals[i] == GoalType.CatchInsects && !Completed[i] && _catchProgress >= catchGoalTarget)
                {
                    Grant(i);
                }
            }
        }

        /// <summary>A web finished at world height centreY (m) — high when above highWebMinHeightY. True when this completed the goal.</summary>
        public bool NotifyWebBuilt(float centreY)
        {
            var done = false;
            if (centreY > highWebMinHeightY)
            {
                for (var i = 0; i < Goals.Length; i++)
                {
                    if (Goals[i] == GoalType.BuildHighWeb && !Completed[i])
                    {
                        Grant(i);
                        done = true;
                    }
                }
            }
            return done;
        }

        /// <summary>A wasp sting happened today — blocks the StinglessNight goal.</summary>
        public void NotifySting()
        {
            _stingsToday++;
        }

        /// <summary>
        /// Morning reached: complete StinglessNight if the night was sting-free, then draw the
        /// goals of the new day (DayNumber + 1). Returns true when a goal completed this call.
        /// </summary>
        public bool OnMorning()
        {
            var completed = false;
            for (var i = 0; i < Goals.Length; i++)
            {
                if (Goals[i] == GoalType.StinglessNight && !Completed[i] && _stingsToday == 0)
                {
                    Grant(i);
                    completed = true;
                }
            }
            RollGoals(Seed, DayNumber + 1);
            return completed;
        }

        /// <summary>The goal text line for index i (single source of truth for UI and tests).</summary>
        public string FormatGoalLine(int i)
        {
            if (Goals == null || i < 0 || i >= Goals.Length)
            {
                return "";
            }
            var done = Completed[i] ? " [x]" : "";
            if (Goals[i] == GoalType.CatchInsects)
            {
                return "Catch " + catchGoalTarget + " (" + Mathf.Min(_catchProgress, catchGoalTarget) + ")" + done;
            }
            if (Goals[i] == GoalType.BuildHighWeb)
            {
                return "Web above " + highWebMinHeightY.ToString("F1") + " m" + done;
            }
            return "Night without sting" + done;
        }

        /// <summary>Active goals still open (for the HUD and reward application).</summary>
        public int OpenGoalCount()
        {
            var open = 0;
            for (var i = 0; i < Goals.Length; i++)
            {
                if (!Completed[i])
                {
                    open++;
                }
            }
            return open;
        }

        /// <summary>
        /// HUD rects for the goal panel, small at the right screen edge: Vector4(x, y, w, h) in
        /// pixel space, one row per goal (label bar + progress pips for CatchInsects).
        /// </summary>
        public Vector4[] HudRects(int screenW, int screenH)
        {
            var rects = new List<Vector4>();
            if (Goals == null)
            {
                return rects.ToArray();
            }
            var x = screenW - 260;
            for (var i = 0; i < Goals.Length; i++)
            {
                var y = (int)(screenH * 0.7f) + i * 18;
                rects.Add(new Vector4(x, y, 240, 14));
                if (Goals[i] == GoalType.CatchInsects)
                {
                    for (var p = 0; p < catchGoalTarget; p++)
                    {
                        var filled = p < _catchProgress;
                        rects.Add(new Vector4(x + 8 + p * 16, y + 3, filled ? 12 : 4, 8));
                    }
                }
            }
            return rects.ToArray();
        }
    }
}
