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

        private void Awake()
        {
            _state = 0;
            MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null)
            {
                return;
            }
            // Startscreen: Enter or E starts the game.
            if (_state == 0 && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
            {
                StartGame();
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
            if (_state == 0)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.black;
                GUI.Label(new Rect(0f, Screen.height / 3f, Screen.width, 48f),
                    new GUIContent("Spinnenspiel — Enter/Space: Start"));
                GUI.color = Color.white;
                return;
            }
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
        }
    }
}
