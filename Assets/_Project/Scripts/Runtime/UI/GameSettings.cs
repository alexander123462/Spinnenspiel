using System;
using System.IO;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// K7 Einstellungen merken: Lautstaerke, Maus-Empfindlichkeit und Sichtweite survive
    /// between partien. Stored in an own JSON file next to the J6 save (Application.persistentDataPath),
    /// with a format version in the head. Missing or corrupt file -> default values plus one log line, never a crash.
    /// </summary>
    public class GameSettings : MonoBehaviour
    {
        [SerializeField] private string _fileName = "spinspiel_settings.json";

        // K7: Format-Version in dem Dateikopf; aeltere or broken files keep defaults.
        private const int SettingsVersion = 1;

        // Clamps (single source of truth for the three settings).
        private const float VolumeMin = 0f, VolumeMax = 1f;
        private const float SensitivityMin = 0.25f, SensitivityMax = 4f;
        private const float ViewDistanceMin = 10f, ViewDistanceMax = 200f;

        // Current values (defaults until a valid file is loaded).
        public float Volume = 0.8f;
        public float MouseSensitivity = 1f;
        public float ViewDistance = 60f;

        /// <summary>S1: selected difficulty level (persisted with the other settings).</summary>
        public GameDifficulty.Level DifficultyLevel = GameDifficulty.Level.Normal;

        private void Awake()
        {
            LoadSettingsFromDisk();
        }

        /// <summary>Path of the settings file under Application.persistentDataPath.</summary>
        private string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

        /// <summary>Set Lautstaerke (0..1) and save immediately.</summary>
        public void SetVolume(float value)
        {
            Volume = Mathf.Clamp(value, VolumeMin, VolumeMax);
            SaveSettings();
        }

        /// <summary>Set Maus-Empfindlichkeit (0.25..4) and save immediately.</summary>
        public void SetSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, SensitivityMin, SensitivityMax);
            SaveSettings();
        }

        /// <summary>Set Sichtweite in meters (10..200) and save immediately.</summary>
        public void SetViewDistance(float value)
        {
            ViewDistance = Mathf.Clamp(value, ViewDistanceMin, ViewDistanceMax);
            SaveSettings();
        }

        /// <summary>S1: select the difficulty level and save immediately.</summary>
        public void SetDifficulty(GameDifficulty.Level level)
        {
            DifficultyLevel = level;
            GameDifficulty.selectedLevel = level;
            SaveSettings();
        }

        /// <summary>True when a settings file exists on disk.</summary>
        public bool SettingsFileExists() => File.Exists(FilePath);

        /// <summary>Delete the settings file (Tests-Cleanup).</summary>
        public void DeleteSettingsFile()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        /// <summary>Test-Helper: write arbitrary JSON as settings file (e.g. a corrupt one).</summary>
        public void WriteRawSettingsJson(string json)
        {
            File.WriteAllText(FilePath, json);
        }

        private void SaveSettings()
        {
            var payload = new SettingsPayload();
            payload.version = SettingsVersion;
            payload.volume = Volume;
            payload.sensitivity = MouseSensitivity;
            payload.viewDistance = ViewDistance;
            payload.difficulty = GameDifficulty.LevelToStored(DifficultyLevel);
            var json = JsonUtility.ToJson(payload);
            try
            {
                File.WriteAllText(FilePath, json);
            }
            catch (System.Exception)
            {
                Debug.LogWarning("GameSettings: cannot write settings file " + FilePath, this);
            }
        }

        /// <summary>Load the settings file if present; corrupt/unknown version keeps defaults plus a log line.</summary>
        public void LoadSettingsFromDisk()
        {
            if (!SettingsFileExists())
            {
                return;
            }
            var payload = JsonUtility.FromJson<SettingsPayload>(System.IO.File.ReadAllText(FilePath));
            // Missing or corrupt file (unknown version) -> defaults plus one log line, never a crash.
            if (payload.version != SettingsVersion)
            {
                Debug.LogWarning("GameSettings: settings file has unknown version " + payload.version
                    + ", expected " + SettingsVersion + ". Defaults kept.", this);
                return;
            }
            Volume = Mathf.Clamp(payload.volume, VolumeMin, VolumeMax);
            MouseSensitivity = Mathf.Clamp(payload.sensitivity, SensitivityMin, SensitivityMax);
            ViewDistance = Mathf.Clamp(payload.viewDistance, ViewDistanceMin, ViewDistanceMax);
            DifficultyLevel = GameDifficulty.LevelFromStored(payload.difficulty);
            GameDifficulty.selectedLevel = DifficultyLevel;
        }
    }

    /// <summary>JSON-Payload of the settings file (flat: only primitives).</summary>
    public class SettingsPayload
    {
        public int version;
        public float volume;
        public float sensitivity;
        public float viewDistance;
        /// <summary>S1: stored difficulty (0 = not stored / legacy -> Normal).</summary>
        public int difficulty;
    }
}
