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
        }

        public void OnWebBuilt()
        {
            WebsBuilt++;
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
        }

        /// <summary>Load best values from an earlier save (restart of the application keeps them).</summary>
        public void LoadBests(float time, int eaten, int webs)
        {
            BestSurvivalTime = time;
            BestEaten = eaten;
            BestWebs = webs;
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
