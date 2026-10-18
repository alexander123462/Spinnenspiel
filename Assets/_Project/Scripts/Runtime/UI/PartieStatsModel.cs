using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// K6: Pure-Logic model for the end-of-partie statistics (no MonoBehaviour, testable directly).
    /// Tracks survival time, eaten insects and built webs, plus the best values over earlier
    /// partien (persisted together with J6 in the savegame file).
    /// </summary>
    public class PartieStatsModel
    {
        public float SurvivalTime;
        public int EatenCount;
        public int WebsBuilt;

        // S5 Tagesbilanz: per-day counters (reset each morning) plus the snapshot of the
        // day that just ended, so the balance window can show it. Totals stay untouched.
        public int DayEaten;
        public int DayWebs;
        public int DayWarnings;
        public int LastDayEaten;
        public int LastDayWebs;
        public int LastDayWarnings;

        public float BestSurvivalTime;
        public int BestEaten;
        public int BestWebs;

        public bool DeathShown;

        /// <summary>One second passed while alive (the MonoBehaviour feeds Time.deltaTime).</summary>
        public void TickAlive(float dt)
        {
            if (!DeathShown)
            {
                SurvivalTime += dt;
            }
        }

        public void OnInsectEaten()
        {
            EatenCount++;
            DayEaten++;
        }

        public void OnWebBuilt()
        {
            WebsBuilt++;
            DayWebs++;
        }

        /// <summary>S5: one bird pre-warning fired (counts towards the biggest danger of the day).</summary>
        public void OnBirdWarning()
        {
            DayWarnings++;
        }

        /// <summary>S5: morning reached — snapshot the day that just ended, zero the day counters (totals keep rising).</summary>
        public void ResetDay()
        {
            LastDayEaten = DayEaten;
            LastDayWebs = DayWebs;
            LastDayWarnings = DayWarnings;
            DayEaten = 0;
            DayWebs = 0;
            DayWarnings = 0;
            }

        /// <summary>End of the partie: freeze the numbers and merge them into the best values.</summary>
        public void ReportDeath()
        {
            DeathShown = true;
            if (SurvivalTime > BestSurvivalTime)
            {
                BestSurvivalTime = SurvivalTime;
            }
            if (EatenCount > BestEaten)
            {
                BestEaten = EatenCount;
            }
            if (WebsBuilt > BestWebs)
            {
                BestWebs = WebsBuilt;
            }
        }

        /// <summary>J2c: player pressed 'Neue Partie' — clear the counters and live again (bests kept).</summary>
        public void StartNewPartie()
        {
            SurvivalTime = 0f;
            EatenCount = 0;
            WebsBuilt = 0;
            DeathShown = false;
            DayEaten = 0;
            DayWebs = 0;
            DayWarnings = 0;
            LastDayEaten = 0;
            LastDayWebs = 0;
            LastDayWarnings = 0;
        }

        /// <summary>Load best values from an earlier save (restart of the application keeps them).</summary>
        public void LoadBests(float time, int eaten, int webs)
        {
            BestSurvivalTime = time;
            BestEaten = eaten;
            BestWebs = webs;
        }

        /// <summary>
        /// S5 Tagesbilanz: the exact display line for the morning window (single source of truth
        /// for UI and tests). Uses the day that just ended (LastDay values), totals are shown by K6.
        /// </summary>
        public string FormatDayLine(int growthStage)
        {
            return "Tag: Eaten " + LastDayEaten + " | Webs " + LastDayWebs
                + " | Stage " + growthStage
                + " | Danger: " + LastDayWarnings + " Warns";
        }

        /// <summary>
        /// S5: Tick positions for the proofshot of the morning window — one row per eaten prey,
        /// built web and bird warning (from the day that just ended). Vector4(x, y, w, h), pixel space.
        /// </summary>
        public Vector4[] DayBalanceTicks(int screenW, int screenH)
        {
            var ticks = new System.Collections.Generic.List<Vector4>();
            var rowY = screenH * 0.18f;
            for (var i = 0; i < LastDayEaten; i++)
            {
                ticks.Add(new Vector4(20f + i * 18f, rowY, 10f, 10f));
            }
            var row2Y = screenH * 0.24f;
            for (var i = 0; i < LastDayWebs; i++)
            {
                ticks.Add(new Vector4(20f + i * 18f, row2Y, 10f, 10f));
            }
            var row3Y = screenH * 0.30f;
            for (var i = 0; i < LastDayWarnings; i++)
            {
                ticks.Add(new Vector4(screenW - 120f + i * 18f, row3Y, 10f, 10f));
            }
            return ticks.ToArray();
        }

        /// <summary>The exact display line for the death screen (single source of truth for UI and tests).</summary>
        public string FormatLine()
        {
            return "Time " + SurvivalTime.ToString("F1") + "s | Eaten " + EatenCount
                + " | Webs " + WebsBuilt
                + " | Best " + BestSurvivalTime.ToString("F1") + "s / " + BestEaten + " / " + BestWebs;
        }

        /// <summary>
        /// Tick positions for the proofshot: one white unit-tick per eaten insect and per built web,
        /// drawn left-to-right in the death panel. Returns Vector4(x, y, w, h) in pixel space.
        /// </summary>
        public Vector4[] DeathScreenTicks(int screenW, int screenH)
        {
            var ticks = new System.Collections.Generic.List<Vector4>();
            float rowY = screenH * 0.35f;
            for (int i = 0; i < EatenCount; i++)
            {
                ticks.Add(new Vector4(20f + i * 18f, rowY, 10f, 10f));
            }
            float row2Y = screenH * 0.45f;
            for (int i = 0; i < WebsBuilt; i++)
            {
                ticks.Add(new Vector4(20f + i * 18f, row2Y, 10f, 10f));
            }
            return ticks.ToArray();
        }
    }
}
