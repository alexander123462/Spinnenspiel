using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// Baut alle Spieler-Input-Aktionen im Code auf (ohne .inputactions-Asset)
    /// und stellt sie als einfache Werte bereit.
    /// Move (WASD + Gamepad-Stick), Look (Maus-Delta + rechter Stick), Sprint (linke Shift).
    /// Cursor wird beim Start gesperrt, mit Escape entsperrt und per Linksklick wieder gesperrt.
    /// </summary>
    public class SpiderInput : MonoBehaviour
    {
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _zoomAction;
        private InputAction _silkAction;
        private InputAction _releaseAction;

        /// <summary>Aktuelle Bewegungsrichtung (x = rechts, y = vor), typischerweise -1..1.</summary>
        public Vector2 Move => _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>Roher Look-Input (Maus-Delta in Pixeln oder Gamepad-Stick in -1..1).</summary>
        public Vector2 Look => _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>True, solange die Sprint-Taste gehalten wird.</summary>
        public bool SprintHeld => _sprintAction != null && _sprintAction.ReadValue<float>() > 0.5f;

        /// <summary>True, in dem Frame in dem die Faden-Taste gedrückt wurde (spinnen).</summary>
        public bool SilkPressed => _silkAction != null && _silkAction.WasPressedThisFrame();

        /// <summary>True, in dem Frame in dem die Loslassen-Taste gedrückt wurde (Faden verlassen).</summary>
        public bool ReleasePressed => _releaseAction != null && _releaseAction.WasPressedThisFrame();

        /// <summary>Zoom-Eingabe (Mausrad, positiv = heranzoomen), normiert auf ca. -1..1 pro Rastung.</summary>
        public float Zoom
        {
            get
            {
                if (_zoomAction == null)
                {
                    return 0f;
                }

                // Je nach Plattform liefert das Mausrad 120 oder 1 pro Rastung.
                float raw = _zoomAction.ReadValue<float>();
                return Mathf.Abs(raw) > 1.5f ? Mathf.Clamp(raw / 120f, -3f, 3f) : raw;
            }
        }

        private void OnEnable()
        {
            BuildActions();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void OnDestroy()
        {
            DisableActions();
            Cursor.lockState = CursorLockMode.None;
            _moveAction = null;
            _lookAction = null;
            _sprintAction = null;
            _zoomAction = null;
            _silkAction = null;
            _releaseAction = null;
        }

        private void Update()
        {
            HandleCursorLock();
        }

        private void BuildActions()
        {
            // Move: 2DVector-Komposit für Tastatur, zweite Instanz für den Gamepad-Stick.
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/leftStick/up")
                .With("Down", "<Gamepad>/leftStick/down")
                .With("Left", "<Gamepad>/leftStick/left")
                .With("Right", "<Gamepad>/leftStick/right");

            // Look: Maus-Delta und rechter Stick liefern beide direkt einen Vector2.
            _lookAction = new InputAction("Look", InputActionType.Value);
            _lookAction.AddBinding("<Mouse>/delta");
            _lookAction.AddBinding("<Gamepad>/rightStick");

            // Sprint: einfache Taste.
            _sprintAction = new InputAction("Sprint", InputActionType.Button);
            _sprintAction.AddBinding("<Keyboard>/leftShift");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");

            // Zoom: Mausrad (y-Achse).
            _zoomAction = new InputAction("Zoom", InputActionType.Value);
            _zoomAction.AddBinding("<Mouse>/scroll/y");

            // Silk: Faden spinnen (F / rechte Maustaste / rechter Schulterknopf).
            _silkAction = new InputAction("Silk", InputActionType.Button);
            _silkAction.AddBinding("<Keyboard>/f");
            _silkAction.AddBinding("<Mouse>/rightButton");
            _silkAction.AddBinding("<Gamepad>/rightShoulder");

            // Release: Faden loslassen (Leertaste / B).
            _releaseAction = new InputAction("Release", InputActionType.Button);
            _releaseAction.AddBinding("<Keyboard>/space");
            _releaseAction.AddBinding("<Gamepad>/buttonSouth");

            _moveAction.Enable();
            _lookAction.Enable();
            _sprintAction.Enable();
            _zoomAction.Enable();
            _silkAction.Enable();
            _releaseAction.Enable();
        }

        private void DisableActions()
        {
            if (_moveAction != null)
            {
                _moveAction.Disable();
            }

            if (_lookAction != null)
            {
                _lookAction.Disable();
            }

            if (_sprintAction != null)
            {
                _sprintAction.Disable();
            }

            if (_zoomAction != null)
            {
                _zoomAction.Disable();
            }

            if (_silkAction != null)
            {
                _silkAction.Disable();
            }

            if (_releaseAction != null)
            {
                _releaseAction.Disable();
            }
        }

        private void HandleCursorLock()
        {
            // Escape entsperrt den Cursor.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            // Linksklick sperrt den Cursor (wieder).
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }
}
