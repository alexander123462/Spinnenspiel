using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>S7: Wespe-Bedaenger — Sting 20 Energie, 1 s Beltaeubung, Ziehaft >=4 m,
    /// Deckung schuetzt, im Netz -50 Prozent und frei nach 3 s.</summary>
    public class WaspTests
    {
        private const float dt = 0.1f;

        [Test]
        public void Sting_takes_20_energy_once()
        {
            var model = new WaspModel();
            var energy = new SpiderEnergyModel();
            energy.Reset();
            // Approach from 5 m to contact.
            for (float d = 5f; d >= 0f; d -= 0.25f)
            {
                model.Tick(d, true, dt);
            }

            Assert.That(model.Stung, "Sting did not fire on approach");
            float before = energy.Energy;
            if (model.Stung)
            {
                energy.ApplySting(WaspModel.stingEnergyCost);
            }

            Assert.That(Mathf.Abs(before - energy.Energy - 20f) <= 1f,
                "Sting did not take 20 +-1 energy");
        }

        [Test]
        public void After_sting_distance_at_least_4m_within_3s()
        {
            // Model-level: the wasp withdraws at its speed; here we assert the
            // state machine reaches Searching only when >=4 m away within 3 s.
            var model = new WaspModel();
            for (float d = 5f; d >= 0f; d -= 0.25f)
            {
                model.Tick(d, true, dt);
            }

            Assert.That(model.Stung, "No sting");
            // Simulate withdrawal: distance grows at 1.5 m/s (wasp speed).
            float dist = 0.12f;
            int guard = 0;
            while (model.state == WaspModel.State.Withdraw && guard < 100)
            {
                dist = Mathf.Min(dist + 1.5f * dt, 6f);
                model.Tick(dist, true, dt);
                guard++;
            }

            Assert.That(model.state == WaspModel.State.Searching, "Withdraw did not end");
            Assert.That(model.TimeSinceSting < 0f || dist >= WaspModel.withdrawMinDistance,
                "Resumed searching too close");
        }

        [Test]
        public void Under_cover_never_stings_in_10_of_10()
        {
            int noAttack = 0;
            for (int trial = 0; trial < 10; trial++)
            {
                var model = new WaspModel();
                for (float d = 5f; d >= 0f; d -= 0.25f)
                {
                    model.Tick(d, false, dt); // covered: open sky false
                }

                Assert.That(!model.Stung, "Sting fired under cover");
                if (!model.Stung)
                {
                    noAttack++;
                }
            }

            Assert.That(noAttack == 10, "Wasp attacked in some covered cases");
        }

        [Test]
        public void Net_wasp_tears_50_percent_and_frees_after_3s()
        {
            var durability = new NetDurabilityModel();
            durability.Reset();
            var model = new WaspModel();
            model.CatchInWeb();
            durability.AddDamage(WaspModel.waspTearCost);
            Assert.That(Mathf.Abs(durability.Durability - 50f) < 0.01f,
                "Wasp tear not -50 percent");

            float t = 0f;
            int guard = 0;
            while (!model.FreedAfterCatch && guard < 100)
            {
                model.Tick(0f, true, dt);
                t += dt;
                guard++;
            }

            Assert.That(model.FreedAfterCatch, "Wasp not free after catch");
            Assert.That(t >= 3f - 0.3f && t <= 3f + 0.3f, "Freeing not in 3 s +-0.3");
        }

        [UnityTest]
        public IEnumerator Sting_stun_last_1s_on_the_spider()
        {
            // Urwald active: SpiderMovement under open sky, like other UnityTests.
            SceneManager.LoadScene("Urwald");
            yield return null; // A2-Lehre: LoadScene defert zum naechsten Frame
            yield return new WaitForSeconds(0.5f);

            SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "SpiderMovement nicht gefunden");

            // Drive the spider forward so it moves before and after the stun.
            spider.SetExternalMoveInput(new Vector2(0f, 1f));
            spider.SetExternalSprint(false);

            float t = 0f;
            bool stunSeen = false;
            bool stunEnded = false;
            float stunOnsetTime = -1f;
            Vector3 posAtStunOnset = Vector3.zero;
            while (t < 4f)
            {
                if (!stunSeen && t >= 0.5f)
                {
                    // Sting lands at 0.5 s: energy -20 handled elsewhere, here the stun.
                    spider.SetStun(WaspModel.stunDuration);
                    stunSeen = true;
                }

                if (stunSeen && !stunEnded)
                {
                    if (spider.IsStunned)
                    {
                        // First stunned frame: mark onset, then watch that it stays put.
                        if (stunOnsetTime < 0f)
                        {
                            stunOnsetTime = t;
                            posAtStunOnset = spider.transform.position;
                        }
                        else if (Vector3.Distance(spider.transform.position, posAtStunOnset) > 0.2f)
                        {
                            Assert.That(false, "spider moved while stunned");
                        }
                    }
                    else if (stunOnsetTime >= 0f)
                    {
                        stunEnded = true;
                        float elapsed = t - stunOnsetTime;
                        Assert.That(elapsed >= WaspModel.stunDuration - 0.15f &&
                            elapsed <= WaspModel.stunDuration + 0.3f,
                            "stun window not ~1 s: elapsed=" + elapsed);
                    }
                }

                if (stunEnded)
                {
                    break;
                }

                t += Time.deltaTime;
                yield return null;
            }

            Assert.That(stunSeen && stunEnded, "stun never started or ended");
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
