using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>Z5: JSON-Save (Position, Hunger, Netze) in Application.persistentDataPath.</summary>
    public class SaveGame : MonoBehaviour
    {
        [SerializeField] private string _fileName = "spinspiel_save.json";

        /// <summary>
        /// J6: Format-Version in dem Dateikopf; aeltere staende mit unbekannt Version nicht loaden.
        /// T9: v2 adds difficulty is already in v1; v3 (here) adds day/goals, web durability,
        /// molt state and growth. v1/v2 staende stay loadable with standard values.
        /// </summary>
        private const int SaveVersion = 3;

        /// <summary>J6: Energie der Spinne (100 = volle, 0 = tot). Save/Load must roundtrip exactly.</summary>
        public float Energy = 100f;

        /// <summary>J6: Seed-Override for CaptureSave when no JungleWorldGenerator is in the scene (Tests).</summary>
        private int _worldSeedOverride;
        public void SetWorldSeed(int seed) { _worldSeedOverride = seed; }

        /// <summary>K6: best values over earlier partien, persisted together with J6 in this file.</summary>
        public float BestSurvivalTime;
        public int BestEaten;
        public int BestWebs;

        /// <summary>S1: difficulty level of the saved partie (persisted with the save).</summary>
        public GameDifficulty.Level DifficultyLevel = GameDifficulty.Level.Normal;

        private GameMenu menu;

        private void Start()
        {
            menu = GetComponent<GameMenu>();
            if (menu != null)
            {
                menu.Quitting += CaptureSave;
                menu.Starting += ApplySave;
            }
        }

        private void OnDestroy()
        {
            if (menu != null)
            {
                menu.Quitting -= CaptureSave;
                menu.Starting -= ApplySave;
            }
        }

        /// <summary>Pfad des Save-File under Application.persistentDataPath.</summary>
        private string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

        /// <summary>Write Position, Hunger, Energie, Seed and all built Netzes into the JSON save file.</summary>
        public void CaptureSave()
        {
            var payload = new SavePayload();
            payload.version = SaveVersion;
            var gen = Object.FindFirstObjectByType<JungleWorldGenerator>();
            payload.worldSeed = _worldSeedOverride != 0
                ? _worldSeedOverride
                : (gen != null ? gen.Seed : 0);
            payload.position = transform.position;
            payload.energy = Energy;
            // S1: the difficulty level travels with the savegame.
            payload.difficulty = GameDifficulty.LevelToStored(DifficultyLevel);
            var hunger = GetComponent<SpiderHunger>();
            payload.hunger = hunger != null ? hunger.Hunger : 0f;
            // K6: best values over earlier partien travel with this save file.
            var deathStats = transform.GetComponent<DeathStats>();
            if (deathStats != null)
            {
                payload.bestTime = deathStats.Stats.BestSurvivalTime;
                payload.bestEaten = deathStats.Stats.BestEaten;
                payload.bestWebs = deathStats.Stats.BestWebs;
            }
            else
            {
                payload.bestTime = BestSurvivalTime;
                payload.bestEaten = BestEaten;
                payload.bestWebs = BestWebs;
            }
            foreach (OrbWeb web in OrbWeb.All)
            {
                if (web == null || !web.IsBuilt)
                {
                    continue;
                }
                foreach (var anchor in web.Layout.FrameAnchors)
                {
                    payload.anchorPoints.Add(anchor);
                }
                payload.webAnchorCounts.Add(web.Layout.FrameAnchors.Length);
                payload.radiusCounts.Add(web.Layout.RadiusCount);
                payload.spiralSpacings.Add(web.Layout.SpiralSpacing);
                payload.freeZoneRadii.Add(web.Layout.FreeZoneRadius);
                // T9 (S6): the durability of each built web travels with the save.
                payload.webDurability.Add(web.Durability != null ? web.Durability.Durability
                    : NetDurabilityModel.maxDurability);
                payload.webTornFlags.Add(web.Durability != null && web.Durability.Torn ? 1 : 0);
            }
            // T9 (S8): the day counter and daily goals travel with the save.
            var dailyGoals = GetComponent<Spinnenspiel.DailyGoals>();
            if (dailyGoals != null)
            {
                payload.dayNumber = dailyGoals.Goals.DayNumber;
                payload.goalSeed = dailyGoals.Goals.Seed;
                if (dailyGoals.Goals.Completed != null)
                {
                    foreach (bool done in dailyGoals.Goals.Completed)
                    {
                        payload.goalCompletedFlags.Add(done ? 1 : 0);
                    }
                }
            }
            // T9 (T4 + growth): eaten count and the pending molt window travel with the save.
            var growth = GetComponent<SpiderGrowth>();
            if (growth != null)
            {
                payload.growthEatenCount = growth.EatenCount;
                payload.moltRemaining = growth.MoltRemaining;
            }
            var json = JsonUtility.ToJson(payload);
            try
            {
                File.WriteAllText(FilePath, json);
            }
            catch (System.Exception)
            {
                Debug.LogWarning("SaveGame: cannot write save file " + FilePath, this);
            }
        }


        /// <summary>True, if the save file exists on disk.</summary>
        public bool SaveFileExists() => File.Exists(FilePath);

        /// <summary>Loeschen the save file (Tests-Cleanup).</summary>
        public void DeleteSaveFile()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        /// <summary>J6-Test-Helper: write arbitrary JSON as save file (e.g. an unknown version).</summary>
        public void WriteRawSaveJson(string json)
        {
            File.WriteAllText(FilePath, json);
        }

        /// <summary>Load the JSON save and restore Position, Hunger, Energie, Seed and Netzes.</summary>
        public void ApplySave()
        {
            if (!SaveFileExists())
            {
                return;
            }
            var payload = JsonUtility.FromJson<SavePayload>(File.ReadAllText(FilePath));
            // J6: unknown version in the file head -> warning, but never a crash.
            // T9: v1/v2 staende stay loadable — JsonUtility leaves the new fields at their
            // defaults (0 / empty), which decode to standard values below.
            if (payload.version < 1 || payload.version > SaveVersion)
            {
                Debug.LogWarning("SaveGame: save file has unknown version " + payload.version
                    + ", expected " + SaveVersion + ". Not loaded.", this);
                return;
            }
            transform.position = payload.position;
            Energy = payload.energy;
            // S1: restore the difficulty level of the saved partie.
            DifficultyLevel = GameDifficulty.LevelFromStored(payload.difficulty);
            GameDifficulty.selectedLevel = DifficultyLevel;
            // K6: restore the best values from earlier partien.
            BestSurvivalTime = payload.bestTime;
            BestEaten = payload.bestEaten;
            BestWebs = payload.bestWebs;
            var hunger = GetComponent<SpiderHunger>();
            if (hunger != null)
            {
                hunger.Hunger = Mathf.Clamp01(payload.hunger);
            }
            // J6: the loaded world must be built from the saved Seed.
            JungleWorldGenerator.NextSeed = payload.worldSeed;
            int anchorIndex = 0;
            for (int i = 0; i < payload.webAnchorCounts.Count; i++)
            {
                int count = payload.webAnchorCounts[i];
                var anchors = new Vector3[count];
                for (int a = 0; a < count; a++)
                {
                    anchors[a] = payload.anchorPoints[anchorIndex++];
                }
                if (anchors.Length != 3 && anchors.Length != 4)
                {
                    Debug.LogWarning("SaveGame: saved web has an invalid anchor count.", this);
                    continue;
                }
                var go = new GameObject("SavedWeb");
                var web = go.AddComponent<OrbWeb>();
                web.Build(new OrbWebLayout(anchors, payload.radiusCounts[i],
                    payload.spiralSpacings[i], payload.freeZoneRadii[i]));
                web.SetProgress(OrbWeb.WebStage.Done, 1f);
                // T9 (S6): restore the saved durability exactly; a legacy file (no entry)
                // keeps the fresh full durability from Build.
                if (payload.webDurability.Count > i)
                {
                    web.Durability.RestoreFromSave(payload.webDurability[i],
                        payload.webTornFlags.Count > i && payload.webTornFlags[i] != 0);
                }
            }
            // T9 (S8): restore day counter + daily goals; a legacy file (dayNumber == 0)
            // keeps the standard fresh day, which DailyGoals.Start() rolls.
            var dailyGoals = GetComponent<Spinnenspiel.DailyGoals>();
            if (dailyGoals != null && payload.dayNumber > 0)
            {
                var flags = new bool[payload.goalCompletedFlags.Count];
                for (int f = 0; f < flags.Length; f++)
                {
                    flags[f] = payload.goalCompletedFlags[f] != 0;
                }
                dailyGoals.Goals.RestoreFromSave(
                    payload.goalSeed != 0 ? payload.goalSeed : DailyGoalsModel.defaultGoalSeed,
                    payload.dayNumber, flags);
            }
            // T9 (T4 + growth): restore eaten count and a pending molt window; a legacy
            // file (both zero) keeps the standard fresh spider.
            var growth = GetComponent<SpiderGrowth>();
            if (growth != null && (payload.growthEatenCount > 0 || payload.moltRemaining > 0f))
            {
                growth.Restore(payload.growthEatenCount, payload.moltRemaining);
            }
        }

    }

    /// <summary>JSON-Payload of one savegame (flat: only primitive/Vector3 lists).</summary>
    public class SavePayload
    {
        public int version;
        public int worldSeed;
        public Vector3 position;
        public float hunger;
        public float energy;
        /// <summary>S1: stored difficulty (0 = not stored / legacy -> Normal).</summary>
        public int difficulty;
        public List<Vector3> anchorPoints = new List<Vector3>();
        /// <summary>K6: Best values from earlier partien (0 = none yet).</summary>
        public float bestTime;
        public int bestEaten;
        public int bestWebs;
        /// <summary>T9: day counter and daily goals of the saved partie (0/empty on older files -> defaults).</summary>
        public int dayNumber;
        public int goalSeed;
        public List<int> goalCompletedFlags = new List<int>();
        /// <summary>T9: per built web the durability value and torn flag (index-aligned with webAnchorCounts).</summary>
        public List<float> webDurability = new List<float>();
        public List<int> webTornFlags = new List<int>();
        /// <summary>T9: growth state of the spider (eaten count + pending molt seconds; 0 -> defaults).</summary>
        public int growthEatenCount;
        public float moltRemaining;
        public List<int> webAnchorCounts = new List<int>();
        public List<int> radiusCounts = new List<int>();
        public List<float> spiralSpacings = new List<float>();
        public List<float> freeZoneRadii = new List<float>();
    }
}
