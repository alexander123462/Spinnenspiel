using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>B8: Startscreen, Esc-Pause (Fortsetzen, Empfindlichkeit, Quit), Settings in PlayerPrefs.</summary>
    public class GameMenu : MonoBehaviour
    {
        private const string SensitivityKey = "MouseSensitivity";

        /// <summary>0 = Startscreen, 1 = Playing, 2 = Paused.</summary>
        private int _state;

        public int State { get => _state; }
        public bool IsPaused { get => _state == 2; }
        public float MouseSensitivity;
        public bool QuitRequested;

        public event System.Action Resumed;
        public event System.Action Quitting;
        public event System.Action Starting;
        public event System.Action NewPartie;

        /// <summary>J6: True when a savegame exists (SaveGame refreshes it on Start).</summary>
        public bool HasSave;

        private void Awake()
        {
            _state = 1;
            MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            HasSave = SaveExists();
        }

        /// <summary>J6: True when the sibling SaveGame component has a file on disk.</summary>
        private bool SaveExists()
        {
            SaveGame save = GetComponent<SaveGame>();
            return save != null && save.SaveFileExists();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null)
            {
                return;
            }
            // Esc toggles pause while playing/paused.
            if (k.escapeKey.wasPressedThisFrame && (_state == 1 || _state == 2))
            {
                if (!EscIsBuildAbort())
                {
                    TogglePause();
                }
            }
            // Q while paused: quit the game (Quitting fires first, e.g. for saving).
            if (k.qKey.wasPressedThisFrame && _state == 2)
            {
                QuitRequested = true;
                System.Action quitting = Quitting;
                if (quitting != null)
                {
                    quitting();
                }
                Application.Quit();
            }
        }

        /// <summary>Z6: True, wenn der aktuelle Esc-Druck statt eine Pause ein Netzbau-Aborting abhannt.</summary>
        private bool EscIsBuildAbort()
        {
            SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
            if (builder == null)
            {
                return false;
            }
            return builder.IsBuilding || builder.LastCancelFrame == Time.frameCount;
        }

        /// <summary>J6: Start from the savegame ("Fortsetzen"). Without a save it does nothing (greyed button), never a crash.</summary>
        public void Fortsetzen()
        {
            if (!HasSave)
            {
                return;
            }
            _state = 1;
            System.Action starting = Starting;
            if (starting != null)
            {
                starting();
            }
        }

        /// <summary>J6: Start a fresh partie ("Neue Partie") — no save gets loaded.</summary>
        public void NeuePartie()
        {
            _state = 1;
            System.Action handler = NewPartie;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>Startscreen -> Playing.</summary>
        public void StartGame()
        {
            _state = 1;
            System.Action starting = Starting;
            if (starting != null)
            {
                starting();
            }
        }

        /// <summary>Playing <-> Paused (Esc). Resume fires Resumed.</summary>
        public void TogglePause()
        {
            if (_state == 1)
            {
                _state = 2;
            }
            else if (_state == 2)
            {
                _state = 1;
                System.Action handler = Resumed;
                if (handler != null)
                {
                    handler();
                }
            }
        }

        /// <summary>Mouse-Empfindlichkeit 0.25..4 speichern in PlayerPrefs.</summary>
        public void SetSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 0.25f, 4f);
            PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
        }

        private void OnGUI()
        {
            if (_state != 2)
            {
                return;
            }
            // Pause-Menu: Esc = Fortsetzen, Q = Quit, Slider Sensitivity.
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, Screen.height / 3f, Screen.width, 48f),
                new GUIContent("Paused — Esc: Fortsetzen | Q: Quit"));
            GUI.Label(new Rect(0f, Screen.height / 3f + 40f, Screen.width, 24f),
                new GUIContent("Sensitivity: " + MouseSensitivity.ToString("F2")));
            // J6: Fortsetzen (greyed when no save exists) next to Neue Partie.
            GUI.Label(new Rect(0f, Screen.height / 3f + 64f, Screen.width, 24f),
                new GUIContent(HasSave ? "Resume: continue from save" : "Resume: -- (no save yet)"));
            GUI.color = HasSave ? Color.white : Color.gray;
            GUI.Label(new Rect(0f, Screen.height / 3f + 88f, Screen.width, 24f),
                new GUIContent("Neue Partie: Q + Neustart"));
            GUI.color = Color.white;
        }
    }
}
