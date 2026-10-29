using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Z5 Speichern: Position, Hunger and Netze survive a JSON roundtrip.</summary>
    public class SaveGameTests
    {
        [UnityTest]
        public IEnumerator Speichern_PositionHungerNetz_Roundtrip()
        {
            var spiderGo = new GameObject("SaveSpider");
            var save = spiderGo.AddComponent<SaveGame>();
            var hunger = spiderGo.AddComponent<SpiderHunger>();

            spiderGo.transform.position = new Vector3(1.5f, 2.25f, -3.75f);
            hunger.Hunger = 0.42f;

            // A built web in the scene gets saved too (OrbWeb.All).
            var webGo = new GameObject("SaveTestWeb");
            var web = webGo.AddComponent<OrbWeb>();
            var anchors = new Vector3[]
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 0f)
            };
            web.Build(new OrbWebLayout(anchors, 8, 0.05f, 0.04f, 7));
            web.SetProgress(OrbWeb.WebStage.Done, 1f);

            save.CaptureSave();
            Assert.IsTrue(save.SaveFileExists(), "CaptureSave must write the save file.");
            // Remove spider+web, then restore from the JSON file.
            var hubBefore = web.Layout.Hub;
            GameObject.DestroyImmediate(webGo);
            GameObject.DestroyImmediate(spiderGo);

            var loadGo = new GameObject("LoadSpider");
            var loadSave = loadGo.AddComponent<SaveGame>();
            var loadHunger = loadGo.AddComponent<SpiderHunger>();
            loadSave.ApplySave();
            yield return null;

            // Position restored exactly.
            Assert.Less(Vector3.Distance(loadGo.transform.position,
                new Vector3(1.5f, 2.25f, -3.75f)), 0.001f, "Position must survive the roundtrip.");

            // Hunger restored.
            Assert.Less(Mathf.Abs(loadHunger.Hunger - 0.42f), 0.001f, "Hunger must survive the roundtrip.");

            // Web rebuilt with the same deterministic Hub.
            OrbWeb restored = null;
            foreach (OrbWeb candidate in OrbWeb.All)
            {
                if (candidate != web && candidate.IsBuilt)
                {
                    restored = candidate;
                }
            }
            Assert.IsNotNull(restored, "The web must be rebuilt from the save file.");
            Assert.Less(Vector3.Distance(restored.Layout.Hub, hubBefore), 0.001f,
                "Rebuilt web must have the same Hub (same anchors + params).");

            // Cleanup: loeschen the save file, then delete spider and webs.
            save.DeleteSaveFile();
            Assert.IsFalse(save.SaveFileExists(), "DeleteSaveFile must remove the save file.");
            var snapshot = new List<OrbWeb>(OrbWeb.All);
            foreach (OrbWeb leftover in snapshot)
            {
                GameObject.DestroyImmediate(leftover);
            }
            GameObject.DestroyImmediate(loadGo);
        }

        [UnityTest]
        public IEnumerator Speichern_J6_EnergieSeedUndUnknownVersion()
        {
            // Part 1: energy + world seed survive a roundtrip.
            var spiderGo = new GameObject("J6Spider");
            var save = spiderGo.AddComponent<SaveGame>();
            save.Energy = 63.5f;
            save.SetWorldSeed(4242);
            save.CaptureSave();

            Assert.IsTrue(save.SaveFileExists(), "CaptureSave must write the save file.");

            GameObject.DestroyImmediate(spiderGo);

            var loadGo = new GameObject("J6Load");
            var loadSave = loadGo.AddComponent<SaveGame>();
            loadSave.ApplySave();
            yield return null;

            Assert.Less(Mathf.Abs(loadSave.Energy - 63.5f), 0.001f,
                "Energy must survive the roundtrip.");
            Assert.AreEqual(4242, JungleWorldGenerator.NextSeed,
                "Loaded world seed must be queued for the next world generation.");

            // Cleanup of the good save.
            loadSave.DeleteSaveFile();

            // Part 2: a save with an unknown version must NOT be loaded (no crash).
            loadSave.WriteRawSaveJson("{\"version\":999,\"worldSeed\":7,\"position\":[0,0,0],"
                + "\"energy\":10,\"hunger\":0.5,\"webs\":[]}");
            loadSave.Energy = 100f;
            loadSave.ApplySave();
            yield return null;

            Assert.Less(Mathf.Abs(loadSave.Energy - 100f), 0.001f,
                "Unknown-version save must not be loaded (energy stays untouched).");
            Assert.AreEqual(4242, JungleWorldGenerator.NextSeed,
                "Unknown-version save must not change the queued seed.");

            loadSave.DeleteSaveFile();
            GameObject.DestroyImmediate(loadGo);
        }

        [UnityTest]
        public IEnumerator Speichern_T9_NeufelderRoundtripUndAeltereStaende()
        {
            // Part 1: roundtrip of the new v3 fields — difficulty, day/goals, durability, molt/growth.
            var spiderGo = new GameObject("T9Spider");
            var save = spiderGo.AddComponent<SaveGame>();
            var growth = spiderGo.AddComponent<SpiderGrowth>();
            var goals = spiderGo.AddComponent<Spinnenspiel.DailyGoals>();
            save.DifficultyLevel = GameDifficulty.Level.Hard;
            // 12 eaten -> stage 1 (scale 1.08), molt still running at capture time.
            for (var i = 0; i < 12; i++)
            {
                growth.OnInsectEaten(null);
            }
            goals.Goals.RollGoals(DailyGoalsModel.defaultGoalSeed, 3);
            goals.Goals.NotifyWebBuilt(5f); // complete the BuildHighWeb goal if rolled

            var webGo = new GameObject("T9Web");
            var web = webGo.AddComponent<OrbWeb>();
            var anchors = new Vector3[]
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 0f)
            };
            web.Build(new OrbWebLayout(anchors, 8, 0.05f, 0.04f, 7));
            web.SetProgress(OrbWeb.WebStage.Done, 1f);
            web.Durability.AddDamage(37f); // worn net: 63 %

            var eatenBefore = growth.EatenCount;
            var moltBefore = growth.MoltRemaining;
            var dayBefore = goals.Goals.DayNumber;
            var seedBefore = goals.Goals.Seed;
            var flagsBefore = goals.Goals.Completed != null
                ? new List<bool>(goals.Goals.Completed)
                : new List<bool>();
            var durBefore = web.Durability.Durability;

            save.CaptureSave();
            Assert.IsTrue(save.SaveFileExists(), "CaptureSave must write the save file.");

            GameObject.DestroyImmediate(webGo);
            GameObject.DestroyImmediate(spiderGo);

            var loadGo = new GameObject("T9Load");
            var loadSave = loadGo.AddComponent<SaveGame>();
            var loadGrowth = loadGo.AddComponent<SpiderGrowth>();
            var loadGoals = loadGo.AddComponent<Spinnenspiel.DailyGoals>();
            loadSave.ApplySave();
            yield return null;

            // S1: difficulty identical after load.
            Assert.AreEqual(GameDifficulty.Level.Hard, loadSave.DifficultyLevel,
                "Difficulty level must survive the roundtrip.");
            // T4 + growth: eaten count and pending molt survive (the molt may tick down by
            // a few frames of real game time between capture and load -> 0.25 s tolerance).
            Assert.AreEqual(eatenBefore, loadGrowth.EatenCount,
                "Eaten count must survive the roundtrip.");
            Assert.Less(Mathf.Abs(moltBefore - loadGrowth.MoltRemaining), 0.25f,
                "Pending molt seconds must survive the roundtrip (within frame-time drift).");
            // S8: day counter and goal completion flags survive.
            Assert.AreEqual(dayBefore, loadGoals.Goals.DayNumber,
                "Day number must survive the roundtrip.");
            Assert.AreEqual(seedBefore, loadGoals.Goals.Seed,
                "Goal seed must survive the roundtrip.");
            for (var i = 0; i < flagsBefore.Count && loadGoals.Goals.Completed != null
                && i < loadGoals.Goals.Completed.Length; i++)
            {
                Assert.AreEqual(flagsBefore[i], loadGoals.Goals.Completed[i],
                    "Goal completion flag " + i + " must survive the roundtrip.");
            }
            // S6: web durability survives exactly.
            OrbWeb restoredWeb = null;
            foreach (OrbWeb candidate in OrbWeb.All)
            {
                if (candidate != web && candidate.IsBuilt)
                {
                    restoredWeb = candidate;
                }
            }
            Assert.IsNotNull(restoredWeb, "The web must be rebuilt from the save file.");
            Assert.Less(Mathf.Abs(durBefore - restoredWeb.Durability.Durability), 0.001f,
                "Web durability must survive the roundtrip.");

            loadSave.DeleteSaveFile();
            GameObject.DestroyImmediate(loadGo);
            var snapshot = new List<OrbWeb>(OrbWeb.All);
            foreach (OrbWeb leftover in snapshot)
            {
                GameObject.DestroyImmediate(leftover);
            }

            // Part 2: an OLDER save (version 1, no new fields) loads with standard values.
            var legacyGo = new GameObject("T9Legacy");
            var legacySave = legacyGo.AddComponent<SaveGame>();
            legacySave.WriteRawSaveJson("{\"version\":1,\"worldSeed\":7,\"position\":{\"x\":0,\"y\":0,\"z\":0},"
                + "\"energy\":100,\"hunger\":0,\"difficulty\":3}");
            legacySave.Energy = 5f;
            legacySave.ApplySave();
            yield return null;
            Assert.Less(Mathf.Abs(legacySave.Energy - 100f), 0.001f,
                "Legacy v1 save must load (energy restored to 100).");
            Assert.AreEqual(GameDifficulty.Level.Hard, legacySave.DifficultyLevel,
                "Legacy v1 difficulty field still decodes (3 = Hard).");
            Assert.Less(Mathf.Abs(legacySave.Energy - 100f), 0.001f,
                "Legacy save loads position without error.");

            // Part 3: unknown version (999) -> warning, not loaded, no crash (J6 behaviour kept).
            legacySave.WriteRawSaveJson("{\"version\":999,\"worldSeed\":7,\"position\":{\"x\":0,\"y\":0,\"z\":0},"
                + "\"energy\":10,\"hunger\":0.5}");
            legacySave.Energy = 100f;
            legacySave.ApplySave();
            yield return null;
            Assert.Less(Mathf.Abs(legacySave.Energy - 100f), 0.001f,
                "Unknown-version save must not be loaded (energy stays untouched).");

            legacySave.DeleteSaveFile();
            GameObject.DestroyImmediate(legacyGo);
            // T9-Cleanup: the loaded Hard level must not leak into other tests.
            GameDifficulty.selectedLevel = GameDifficulty.Level.Normal;
        }
    }
}
