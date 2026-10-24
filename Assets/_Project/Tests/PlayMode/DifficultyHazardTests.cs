using NUnit.Framework;
using System.Collections;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>T12: Difficulty (S1) acts on the new hazards Wespe / Frosch / Libelle.
    /// Frequency multiplier 0.5 / 1.0 / 1.6 and damage multiplier 0.7 / 1.0 / 1.3.</summary>
    public class DifficultyHazardTests
    {
        private static readonly float respawnInterval = 300f; // s, wasp base (WaspSpawner)

        /// <summary>Criterion: expected Wespe count per 10 min at Hart vs Normal = 1.6 +-0.1.</summary>
        [Test]
        public void WespeFrequency_Hart_over_Normal_is_1_6()
        {
            float window = 600f; // 10 min
            float normalCount = ExpectedWaspSpawns(GameDifficulty.Level.Normal, window);
            float hardCount = ExpectedWaspSpawns(GameDifficulty.Level.Hard, window);
            float ratio = hardCount / normalCount;
            Debug.Log("[T12] wasp count Hart/Normal=" + ratio.ToString("F3")
                + " -> 1.6 +-0.1 -> " + (ratio >= 1.5f && ratio <= 1.7f ? "OK" : "NOK"));
            Assert.That(ratio >= 1.5f && ratio <= 1.7f,
                "Wespe Hart/Normal frequency not 1.6 +-0.1: " + ratio);
        }

        /// <summary>Criterion: Frosch damage at Entspannt = 25 x 0.7 = 17.5 +-1.</summary>
        [Test]
        public void FroschDamage_at_Entspannt_is_17_5()
        {
            float expected = FrogModel.energyCost * GameDifficulty.HazardDamageMultiplierOf(GameDifficulty.Level.Relaxed);
            Debug.Log("[T12] frog damage Entspannt=" + expected.ToString("F2")
                + " -> 17.5 +-1 -> " + (Mathf.Abs(expected - 17.5f) <= 1f ? "OK" : "NOK"));
            Assert.That(Mathf.Abs(expected - 17.5f) <= 1f,
                "Frosch damage at Entspannt not 17.5 +-1: " + expected);
        }

        /// <summary>Criterion: Normal changes nothing (multipliers are exactly 1).</summary>
        [Test]
        public void Normal_changes_nothing_on_hazards()
        {
            float freq = GameDifficulty.HazardFrequencyMultiplierOf(GameDifficulty.Level.Normal);
            float dmg = GameDifficulty.HazardDamageMultiplierOf(GameDifficulty.Level.Normal);
            Assert.That(Mathf.Abs(freq - 1f) < 0.001f, "Normal hazard frequency not 1: " + freq);
            Assert.That(Mathf.Abs(dmg - 1f) < 0.001f, "Normal hazard damage not 1: " + dmg);
        }

        /// <summary>Wiring proof: at Entspannt a live Frosch strike really takes ~17.5 energy.</summary>
        [UnityTest]
        public IEnumerator FroschStrike_takes_scaled_damage_at_Entspannt()
        {
            GameDifficulty.selectedLevel = GameDifficulty.Level.Relaxed;

            var energyGo = new GameObject("HazardEnergy");
            var energy = energyGo.AddComponent<SpiderEnergy>();
            yield return null; // Awake runs, model difficulty wired
            yield return null;

            float before = energy.Energy;
            energy.ApplyStrike(FrogModel.energyCost);
            float drop = before - energy.Energy;
            Debug.Log("[T12] live frog strike drop at Entspannt=" + drop.ToString("F2")
                + " -> 17.5 +-1 -> " + (Mathf.Abs(drop - 17.5f) <= 1f ? "OK" : "NOK"));

            GameObject.DestroyImmediate(energyGo);
            GameDifficulty.selectedLevel = GameDifficulty.Level.Normal;

            Assert.That(Mathf.Abs(drop - 17.5f) <= 1f,
                "Live Frosch strike at Entspannt did not take 17.5 +-1: " + drop);
        }

        // ---- helpers ----

        /// <summary>Expected wasp spawns in a window: interval divides by the hazard frequency.</summary>
        private static float ExpectedWaspSpawns(GameDifficulty.Level level, float windowSeconds)
        {
            float interval = respawnInterval / GameDifficulty.HazardFrequencyMultiplierOf(level);
            return windowSeconds / interval;
        }
    }
}
