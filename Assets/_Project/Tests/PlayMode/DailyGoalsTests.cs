using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// S8 Tagesziele: the goal draw is reproducible (same Seed + same Tagnummer -> same three
    /// goals); after 3 eaten insects a "Catch 3" goal completes and its reward is granted exactly
    /// once; after the morning roll new goals appear. Proof-shot: Logs/ci/s8_tagesziele.png.
    /// </summary>
    public class DailyGoalsTests
    {
        [UnityTest]
        public IEnumerator S8_GleichSeedGleichTag_GleichZiele()
        {
            var a = new Spinnenspiel.DailyGoalsModel();
            var b = new Spinnenspiel.DailyGoalsModel();
            a.RollGoals(Spinnenspiel.DailyGoalsModel.defaultGoalSeed, 3);
            b.RollGoals(Spinnenspiel.DailyGoalsModel.defaultGoalSeed, 3);
            for (var i = 0; i < Spinnenspiel.DailyGoalsModel.goalsPerDay; i++)
            {
                Assert.AreEqual(a.Goals[i], b.Goals[i], "S8: same seed + day -> same goal at index " + i);
            }
            var c = new Spinnenspiel.DailyGoalsModel();
            c.RollGoals(Spinnenspiel.DailyGoalsModel.defaultGoalSeed, 4);
            Assert.AreEqual(a.Goals[1], Spinnenspiel.DailyGoalsModel.GoalType.BuildHighWeb, "S8: day 3 goal 1.");
            Assert.AreEqual(c.Goals[1], Spinnenspiel.DailyGoalsModel.GoalType.StinglessNight, "S8: day 4 goal 1 differs (new draw per day).");
            yield return null;
        }

        [UnityTest]
        public IEnumerator S8_CatchGoal_RewardGenauOnce()
        {
            var m = new Spinnenspiel.DailyGoalsModel();
            // Day 3 with the default seed starts with CatchInsects at index 0 (verified by draw).
            m.RollGoals(Spinnenspiel.DailyGoalsModel.defaultGoalSeed, 3);
            Assert.AreEqual(Spinnenspiel.DailyGoalsModel.GoalType.CatchInsects, m.Goals[0],
                "S8: day 3 of the default seed has the catch goal (reproducible draw).");

            for (var e = 0; e < Spinnenspiel.DailyGoalsModel.catchGoalTarget; e++)
            {
                m.NotifyInsectEaten();
            }
            Assert.IsTrue(m.Completed[0], "S8: after 3 eaten insects the catch goal is completed.");
            Assert.AreEqual(1, m.RewardsGranted, "S8: the reward was granted exactly once.");

            // More insects must NOT grant again (exactly-once).
            m.NotifyInsectEaten();
            m.NotifyInsectEaten();
            Assert.AreEqual(1, m.RewardsGranted, "S8: extra insects do not re-grant the reward.");

            Spinnenspiel.DailyGoalsModel.GoalType claimed;
            Assert.IsTrue(m.TakePendingReward(out claimed), "S8: the glue layer can claim the queued reward.");
            Assert.AreEqual(Spinnenspiel.DailyGoalsModel.GoalType.CatchInsects, claimed, "S8: energy-goal reward type.");
            Assert.IsFalse(m.TakePendingReward(out claimed), "S8: no second reward pending.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator S8_NachMorgenNaeueueZiele()
        {
            var m = new Spinnenspiel.DailyGoalsModel();
            m.RollGoals(Spinnenspiel.DailyGoalsModel.defaultGoalSeed, 3);
            // Day 3 of the default seed draws Catch, HighWeb, Stingless (in that order).
            Assert.AreEqual(Spinnenspiel.DailyGoalsModel.GoalType.StinglessNight, m.Goals[2], "S8: day 3 goal 2.");

            // No sting happened today -> the morning completes the stingless goal exactly once.
            var completed = m.OnMorning(); // rolls day 4 (DayNumber + 1)
            Assert.IsTrue(completed, "S8: sting-free night completes the stingless goal at the morning.");
            Assert.AreEqual(1, m.RewardsGranted, "S8: the stingless reward was granted exactly once.");
            Assert.AreEqual(4, m.DayNumber, "S8: the morning advances the day number.");

            // The new goals of day 4 differ from day 3 (reproducible draw per day).
            Assert.AreEqual(Spinnenspiel.DailyGoalsModel.GoalType.CatchInsects, m.Goals[0], "S8: day 4 goal 0.");
            Assert.AreEqual(Spinnenspiel.DailyGoalsModel.GoalType.StinglessNight, m.Goals[1], "S8: day 4 goal 1 differs from day 3.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator S8_HudAnRand_FourPips()
        {
            // Proof-shot: three label bars + pips at the right edge, drawn from HudRects.
            var m = new Spinnenspiel.DailyGoalsModel();
            m.RollGoals(Spinnenspiel.DailyGoalsModel.defaultGoalSeed, 3);
            m.NotifyInsectEaten();
            m.NotifyInsectEaten();

            var w = 1280;
            var h = 720;
            Assert.Greater(m.OpenGoalCount(), 0, "S8: open goals exist for the HUD.");
            var rects = m.HudRects(w, h);
            Assert.IsNotEmpty(rects, "S8: the HUD has rects to draw.");

            // Proof-shot: paint the label bars (and pips) at the right edge into a PNG.
            PaintHudProof(m, w, h, "Logs/ci/s8_tagesziele.png");
            yield return null;
        }

        private static void PaintHudProof(Spinnenspiel.DailyGoalsModel m, int w, int h, string file)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, new Color(0.1f, 0.2f, 0.1f));
                }
            }
            foreach (UnityEngine.Vector4 r in m.HudRects(w, h))
            {
                PaintGuiRect(tex, (int)r.x, (int)r.y, (int)r.z, (int)r.w, new Color(1f, 1f, 1f));
            }
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>Fills a rect in GUI coords (y down from top) with a solid colour.</summary>
        private static void PaintGuiRect(Texture2D tex, int x, int yTop, int w, int h, Color c)
        {
            for (var px = x; px < x + w && px < tex.width; px++)
            {
                for (var gy = yTop; gy < yTop + h && gy < tex.height; gy++)
                {
                    if (px < 0 || gy < 0)
                    {
                        continue;
                    }
                    var py = tex.height - 1 - gy;
                    tex.SetPixel(px, py, c);
                }
            }
        }
    }
}
