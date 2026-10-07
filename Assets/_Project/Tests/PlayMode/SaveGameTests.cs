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
    }
}
