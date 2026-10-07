using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>Z5: JSON-Save (Position, Hunger, Netze) in Application.persistentDataPath.</summary>
    public class SaveGame : MonoBehaviour
    {
        [SerializeField] private string _fileName = "spinspiel_save.json";

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

        /// <summary>Write Position, Hunger and all built Netzes into the JSON save file.</summary>
        public void CaptureSave()
        {
            var payload = new SavePayload();
            payload.position = transform.position;
            var hunger = GetComponent<SpiderHunger>();
            payload.hunger = hunger != null ? hunger.Hunger : 0f;
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

        /// <summary>Load the JSON save and restore Position, Hunger and Netzes.</summary>
        public void ApplySave()
        {
            if (!SaveFileExists())
            {
                return;
            }
            var payload = JsonUtility.FromJson<SavePayload>(File.ReadAllText(FilePath));
            transform.position = payload.position;
            var hunger = GetComponent<SpiderHunger>();
            if (hunger != null)
            {
                hunger.Hunger = Mathf.Clamp01(payload.hunger);
            }
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
        public Vector3 position;
        public float hunger;
        public List<Vector3> anchorPoints = new List<Vector3>();
        public List<int> webAnchorCounts = new List<int>();
        public List<int> radiusCounts = new List<int>();
        public List<float> spiralSpacings = new List<float>();
        public List<float> freeZoneRadii = new List<float>();
    }
}
