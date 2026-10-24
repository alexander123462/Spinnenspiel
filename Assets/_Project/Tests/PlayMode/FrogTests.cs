using NUnit.Framework;
using System.Collections;
using UnityEngine;

namespace Spinnenspiel.Tests
{
    /// <summary>T3: Frosch am Boden — Strike only when the spider sits low to the ground
    /// (<=0.3 m) and within 0.8 m: telegraph 0.6 s, energy -25, pause 5 s. Higher places
    /// never trigger it. One frog per swamp zone, only where water is within 3 m.</summary>
    public class FrogTests
    {
        private const float dt = 0.05f;

        [Test]
        public void Frog_Strike_fires_after_06s_and_takes_25_energy()
        {
            var model = new FrogModel();
            var energy = new SpiderEnergyModel();
            energy.Reset();

            // Spider at 0.2 m height, 0.6 m away: in reach and low -> telegraph then strike.
            float t = 0f;
            float tAimStart = -1f;
            float tStrike = -1f;
            int guard = 0;
            while (guard < 100)
            {
                if (model.state == FrogModel.State.Aiming && tAimStart < 0f)
                {
                    tAimStart = t;
                }

                model.Tick(0.6f, 0.2f, dt);
                t += dt;
                guard++;
                if (model.Struck)
                {
                    tStrike = t;
                    break;
                }
            }

            float telegraph = tStrike - tAimStart;
            Assert.That(model.Struck, "Strike did not fire for a low spider in reach");
            Debug.Log("[T3] strike telegraph time=" + telegraph + " -> 0.6 +-0.1 -> "
                + (telegraph >= 0.5f && telegraph <= 0.7f ? "OK" : "NOK"));
            Assert.That(telegraph >= 0.5f && telegraph <= 0.7f,
                "Strike telegraph not in 0.6 +-0.1 s: t=" + telegraph);

            float before = energy.Energy;
            energy.ApplyStrike(FrogModel.energyCost);
            Assert.That(Mathf.Abs(before - energy.Energy - 25f) <= 1f,
                "Frog strike did not take 25 +-1 energy");
        }

        [Test]
        public void Frog_Never_strikes_high_spider_in_10_of_10()
        {
            int noAttack = 0;
            for (int trial = 0; trial < 10; trial++)
            {
                var model = new FrogModel();
                // Spider at 0.6 m height (> maxStrikeHeight): safe even within reach.
                for (float d = 5f; d >= 0f; d -= 0.25f)
                {
                    model.Tick(d, 0.6f, dt);
                }

                Assert.That(!model.Struck, "Frog struck a spider at 0.6 m height");
                if (!model.Struck)
                {
                    noAttack++;
                }
            }

            Assert.That(noAttack == 10, "Frog attacked in some high-place cases");
        }

        [Test]
        public void Frog_Second_strike_earliest_5s_after_first()
        {
            var model = new FrogModel();
            // First strike (low spider in reach): advance until it lands.
            int warmup = 0;
            while (!model.Struck && warmup < 200)
            {
                model.Tick(0.6f, 0.2f, dt);
                warmup++;
            }

            Assert.That(model.Struck, "No first strike");

            // Spider stays in reach: the pause must delay the second strike >=5 s
            // (measured from the first strike landing, when PauseTimer starts at 0).
            float t = 0f;
            int guard = 0;
            bool leftPause = false;
            while (guard < 400)
            {
                model.Tick(0.6f, 0.2f, dt);
                t += dt;
                guard++;
                if (model.Struck && leftPause)
                {
                    break;
                }

                if (!model.Struck)
                {
                    leftPause = true; // the pause ended / Struck reset
                }
            }

            Assert.That(model.Struck, "Second strike never fired");
            // t counts from the first strike landing to the NEXT strike landing:
            // pause 5 s + telegraph 0.6 s -> >= 5 s after the first strike.
            Debug.Log("[T3] gap between strikes=" + t + " -> >=5.0 -> "
                + (t >= FrogModel.pauseTime - 0.1f ? "OK" : "NOK"));
            Assert.That(t >= FrogModel.pauseTime - 0.1f,
                "Second strike earlier than the 5 s pause: gap=" + t);
        }

        [Test]
        public void Frog_Spots_only_near_water_over_5_seeds()
        {
            int totalSpots = 0;
            for (int seed = 1; seed <= 5; seed++)
            {
                JungleWorld world = new JungleWorld(seed);
                var spots = FrogModel.FindFrogSpots(world);

                foreach (Vector3 s in spots)
                {
                    totalSpots++;
                    Assert.IsFalse(world.IsUnderWater(s.x, s.z),
                        "Frog spot inside water at seed " + seed);
                    Assert.IsTrue(FrogModel.HasWaterNear(world, s.x, s.z, 3f),
                        "Frog spot without water within 3 m at seed " + seed);
                }

                Debug.Log("[T3] seed=" + seed + " frog spots=" + spots.Count);
            }

            Assert.That(totalSpots >= 5,
                "No frog spots found across 5 seeds: " + totalSpots);
        }
    }
}
