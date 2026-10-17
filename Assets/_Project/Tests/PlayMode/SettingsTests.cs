using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>K7 Einstellungen merken: Volume, Sensitivity and ViewDistance survive between partien.</summary>
    public class SettingsTests
    {
        [UnityTest]
        public IEnumerator Einstellungen_SurviveBetweenParties()
        {
            // Part 1: set values, then reload from the file (simulates a new partie / app restart).
            var saveGo = new GameObject("SettingsSave");
            var save = saveGo.AddComponent<GameSettings>();
            save.SetVolume(0.35f);
            save.SetSensitivity(2.5f);
            save.SetViewDistance(120f);
            Assert.IsTrue(save.SettingsFileExists(), "Setters must write the settings file.");

            GameObject.DestroyImmediate(saveGo);

            var loadGo = new GameObject("SettingsLoad");
            var load = loadGo.AddComponent<GameSettings>();
            // Awake already ran LoadSettings; force a fresh read from the file.
            load.LoadSettingsFromDisk();
            yield return null;

            Assert.Less(Mathf.Abs(load.Volume - 0.35f), 0.001f, "Volume must survive.");
            Assert.Less(Mathf.Abs(load.MouseSensitivity - 2.5f), 0.001f, "Sensitivity must survive.");
            Assert.Less(Mathf.Abs(load.ViewDistance - 120f), 0.001f, "ViewDistance must survive.");

            load.DeleteSettingsFile();
            GameObject.DestroyImmediate(loadGo);
        }

        [UnityTest]
        public IEnumerator Einstellungen_KorruptDateiFuehrtZuDefaultsOhneKonsolenfehler()
        {
            // A corrupt (unknown version) file must keep defaults and log a warning, not crash or error.
            var go = new GameObject("SettingsCorrupt");
            var settings = go.AddComponent<GameSettings>();

            // Defaults before any file: 0.8 / 1 / 60.
            Assert.Less(Mathf.Abs(settings.Volume - 0.8f), 0.001f, "Default volume is 0.8.");
            Assert.Less(Mathf.Abs(settings.MouseSensitivity - 1f), 0.001f, "Default sensitivity is 1.");
            Assert.Less(Mathf.Abs(settings.ViewDistance - 60f), 0.001f, "Default view distance is 60.");

            settings.WriteRawSettingsJson("{\"version\":999,\"volume\":0.1,\"sensitivity\":3,\"viewDistance\":5}");
            settings.LoadSettingsFromDisk();
            yield return null;

            // Unknown version -> defaults kept (not the corrupt 0.1/3/5).
            Assert.Less(Mathf.Abs(settings.Volume - 0.8f), 0.001f, "Corrupt file keeps default volume.");
            Assert.Less(Mathf.Abs(settings.MouseSensitivity - 1f), 0.001f, "Corrupt file keeps default sensitivity.");
            Assert.Less(Mathf.Abs(settings.ViewDistance - 60f), 0.001f, "Corrupt file keeps default view distance.");

            settings.DeleteSettingsFile();
            GameObject.DestroyImmediate(go);
        }
    }
}
