using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>S1: Difficulty levels (Entspannt/Normal/Hart) with multipliers for
    /// energy drain, prey amount and bird danger frequency; level survives save/settings.</summary>
    public class GameDifficultyTests
    {
        private static readonly float dt = 0.1f;

        [Test]
        public void HartDrain_divided_by_NormalDrain_is_1_4()
        {
            // Measure drain per second at Hart vs Normal: ratio must be 1.4 +-0.05.
            float drainedNormal = DrainAtLevel(GameDifficulty.Level.Normal);
            float drainedHard = DrainAtLevel(GameDifficulty.Level.Hard);
            float ratio = drainedHard / drainedNormal;
            Assert.That(ratio >= 1.35f && ratio <= 1.45f,
                "Hart/Normal drain ratio not 1.4 +-0.05");
        }

        [Test]
        public void PreyAmount_at_Entspannt_rounds_6_times_1_3_to_8()
        {
            int baseCount = 6;
            int expected = Mathf.RoundToInt(baseCount * GameDifficulty.PreyMultiplierOf(GameDifficulty.Level.Relaxed));
            Assert.That(expected == 8, "round(6 * 1.3) must be 8");
        }

        [Test]
        public void BirdWarnInterval_Hart_shorter_than_Normal_shorter_than_Entspannt()
        {
            float warn = 8f;
            float relaxed = warn / GameDifficulty.BirdThreatMultiplierOf(GameDifficulty.Level.Relaxed);
            float normal = warn / GameDifficulty.BirdThreatMultiplierOf(GameDifficulty.Level.Normal);
            float hard = warn / GameDifficulty.BirdThreatMultiplierOf(GameDifficulty.Level.Hard);
            Assert.That(hard < normal && normal < relaxed,
                "Bird warn interval not Hart < Normal < Entspannt");

            // The model reacts at the scaled time: Hart fires earlier than Normal.
            float exposureAtWarnHard = ExposureWhenWarned(GameDifficulty.Level.Hard);
            float exposureAtWarnNormal = ExposureWhenWarned(GameDifficulty.Level.Normal);
            Assert.That(exposureAtWarnHard < exposureAtWarnNormal,
                "Hart bird warns not earlier than Normal");
        }

        [UnityTest]
        public IEnumerator SavedLevel_afterReload_is_identical()
        {
            // Save side: capture with Hart.
            var saveGo = new GameObject("DiffSave");
            var save = saveGo.AddComponent<SaveGame>();
            save.DifficultyLevel = GameDifficulty.Level.Hard;
            save.CaptureSave();
            Assert.IsTrue(save.SaveFileExists(), "CaptureSave must write the save file.");
            GameObject.DestroyImmediate(saveGo);

            // Load side: fresh SaveGame restores the same level.
            var loadGo = new GameObject("DiffLoad");
            var load = loadGo.AddComponent<SaveGame>();
            load.ApplySave();
            yield return null;
            Assert.That(load.DifficultyLevel == GameDifficulty.Level.Hard,
                "Saved difficulty level must survive the reload.");

            // Legacy/unsaved file (difficulty 0) -> Normal, never a crash.
            var legacyGo = new GameObject("DiffLegacy");
            var legacy = legacyGo.AddComponent<SaveGame>();
            legacy.WriteRawSaveJson("{\"version\":1,\"worldSeed\":7,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"hunger\":0,\"energy\":100}");
            legacy.ApplySave();
            yield return null;
            Assert.That(legacy.DifficultyLevel == GameDifficulty.Level.Normal,
                "Legacy save without difficulty falls back to Normal.");

            load.DeleteSaveFile();
            GameObject.DestroyImmediate(loadGo);
            GameObject.DestroyImmediate(legacyGo);
        }

        [UnityTest]
        public IEnumerator Difficulty_survives_in_the_settings_file()
        {
            var saveGo = new GameObject("DiffSettingsSave");
            var settings = saveGo.AddComponent<GameSettings>();
            settings.SetDifficulty(GameDifficulty.Level.Relaxed);
            Assert.IsTrue(settings.SettingsFileExists(), "SetDifficulty must write the settings file.");
            GameObject.DestroyImmediate(saveGo);

            var loadGo = new GameObject("DiffSettingsLoad");
            var load = loadGo.AddComponent<GameSettings>();
            load.LoadSettingsFromDisk();
            yield return null;

            Assert.That(load.DifficultyLevel == GameDifficulty.Level.Relaxed,
                "Difficulty must survive in the settings file.");
            Assert.That(GameDifficulty.LevelToStored(load.DifficultyLevel) == 1,
                "Relaxed stores as 1.");

            load.DeleteSettingsFile();
            // Reset the global selection so other tests keep the Normal baseline.
            GameDifficulty.selectedLevel = GameDifficulty.Level.Normal;
            GameObject.DestroyImmediate(loadGo);
        }

        // ---- helpers ----

        private static float DrainAtLevel(GameDifficulty.Level level)
        {
            var model = new SpiderEnergyModel();
            model.Reset();
            model.difficultyMultiplier = GameDifficulty.DrainMultiplierOf(level);
            float before = model.Energy;
            for (int i = 0; i < 100; i++)
            {
                model.Tick(SpiderEnergyModel.Activity.Walk, dt);
            }
            return before - model.Energy;
        }

        private static float ExposureWhenWarned(GameDifficulty.Level level)
        {
            var threat = new BirdThreatModel();
            threat.warnTime = 8f / GameDifficulty.BirdThreatMultiplierOf(level);
            threat.attackDelay = 3f;
            for (int i = 0; i < 200; i++)
            {
                bool before = threat.Warned;
                threat.Tick(true, dt);
                if (!before && threat.Warned)
                {
                    return threat.ExposureSeconds;
                }
            }
            return -1f;
        }
    }
}
