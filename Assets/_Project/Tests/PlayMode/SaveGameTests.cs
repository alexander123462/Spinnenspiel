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
    }
}
