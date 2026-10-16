using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>Z5: JSON-Save (Position, Hunger, Netze) in Application.persistentDataPath.</summary>
    public class SaveGame : MonoBehaviour
    {
        [SerializeField] private string _fileName = "spinspiel_save.json";

        /// <summary>J6: Format-Version in dem Dateikopf; aeltere staende mit unbekannt Version nicht loaden.</summary>
        private const int SaveVersion = 1;

        /// <summary>J6: Energie der Spinne (100 = volle, 0 = tot). Save/Load must roundtrip exactly.</summary>
        public float Energy = 100f;

        /// <summary>J6: Seed-Override for CaptureSave when no JungleWorldGenerator is in the scene (Tests).</summary>
        private int _worldSeedOverride;
        public void SetWorldSeed(int seed) { _worldSeedOverride = seed; }

        /// <summary>K6: Best values over earlier partien, persisted together with J6 in this file.</summary>
        public float BestSurvivalTime;
        public int BestEaten;
        public int BestWebs;

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
            if (payload.version != SaveVersion)
            {
                Debug.LogWarning("SaveGame: save file has unknown version " + payload.version
                    + ", expected " + SaveVersion + ". Not loaded.", this);
                return;
            }
            transform.position = payload.position;
            Energy = payload.energy;
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
        public List<Vector3> anchorPoints = new List<Vector3>();
        /// <summary>K6: Best values from earlier partien (0 = none yet).</summary>
        public float bestTime;
        public int bestEaten;
        public int bestWebs;
        public List<int> webAnchorCounts = new List<int>();
        public List<int> radiusCounts = new List<int>();
        public List<float> spiralSpacings = new List<float>();
        public List<float> freeZoneRadii = new List<float>();
    }
}
