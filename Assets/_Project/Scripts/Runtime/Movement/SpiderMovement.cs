using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Bodenbasierte Bewegung der Spinne über einen CharacterController.
    /// Liest SpiderInput und bewegt die Spinne relativ zur Kamera.
    /// Beschleunigung und Abbremsen laufen über Vector3.MoveTowards mit getrennten Werten.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class SpiderMovement : MonoBehaviour
    {
        [Header("Referenzen")]
        [SerializeField] private SpiderInput input;
        [SerializeField] private Transform cameraTransform;

        [Header("Geschwindigkeit (m/s)")]
        [SerializeField] private float walkSpeed = 0.6f;
        [SerializeField] private float sprintSpeed = 1.2f;

        [Header("Beschleunigung (m/s^2)")]
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 12f;

        [Header("Drehung")]
        [SerializeField] private float turnSpeed = 360f; // Grad pro Sekunde
        [SerializeField] private float rotationThreshold = 0.1f; // erst ab dieser Eingabestärke drehen

        [Header("Schwerkraft")]
        [SerializeField] private float gravityScale = 1f;
        [SerializeField] private float groundAdhesion = 2f; // Haftkraft am Boden, damit die Spinne bergab nicht abhebt

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private Vector3 _lastVelocity;
        private Vector2? _externalMoveInput;
        private bool? _externalSprint;
        private bool _warnedNoCamera;
        private SpiderSurfaceDetector _surface;
        private SpiderClimber _climber;

        /// <summary>Letzte Gesamtbewegungsgeschwindigkeit (horizontal + vertikal).</summary>
        public Vector3 Velocity => _lastVelocity;

        /// <summary>Horizontale Geschwindigkeit in m/s.</summary>
        public float Speed => _horizontalVelocity.magnitude;

        public bool IsGrounded => _controller != null && _controller.isGrounded;

        public float WalkSpeed => walkSpeed;

        public float SprintSpeed => sprintSpeed;

        /// <summary>True, wenn Sprint gehalten wird und sich die Spinne horizontal bewegt.</summary>
        public bool IsSprinting => IsSprintHeld() && Speed > rotationThreshold;

        /// <summary>Sprint-Zustand: extern gesetzter Wert hat Vorrang, sonst die Eingabe.</summary>
        private bool IsSprintHeld()
        {
            if (_externalSprint.HasValue)
            {
                return _externalSprint.Value;
            }

            return input != null && input.SprintHeld;
        }

        /// <summary>
        /// Setzt eine externe Bewegungsrichtung (z. B. für automatisierte Tests).
        /// Wenn nicht null, ersetzt der Wert die Tastatur-/Gamepad-Eingabe.
        /// </summary>
        public void SetExternalMoveInput(Vector2? moveInput)
        {
            _externalMoveInput = moveInput;
        }

        /// <summary>
        /// Setzt einen externen Sprint-Zustand (z. B. für automatisierte Tests).
        /// Wenn nicht null, ersetzt der Wert die Tastatur-/Gamepad-Eingabe.
        /// </summary>
        public void SetExternalSprint(bool? sprint)
        {
            _externalSprint = sprint;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _surface = GetComponent<SpiderSurfaceDetector>();
            _climber = GetComponent<SpiderClimber>();

            if (input == null)
            {
                input = GetComponent<SpiderInput>();
                if (input == null)
                {
                    var found = FindFirstObjectByType<SpiderInput>();
                    if (found != null)
                    {
                        input = found;
                    }
                }
            }
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            Vector2 moveInput;
            if (_externalMoveInput.HasValue)
            {
                moveInput = _externalMoveInput.Value;
            }
            else
            {
                moveInput = input != null ? input.Move : Vector2.zero;
            }

            Vector3 moveDirection = GetCameraRelativeDirection(moveInput);

            // Zielgeschwindigkeit: Richtung * Geschwindigkeit (analoge Eingabe wird skaliert).
            bool sprinting = IsSprintHeld() && moveDirection.sqrMagnitude > 0.0001f;
            float surfaceFactor = _surface != null ? _surface.SpeedMultiplier : 1f;
            float targetSpeed = (sprinting ? sprintSpeed : walkSpeed) * surfaceFactor * Mathf.Clamp01(moveDirection.magnitude);
            Vector3 targetVelocity = moveDirection * targetSpeed;

            // Beschleunigen/Abbremsen mit getrennten Werten.
            bool decelerating = _horizontalVelocity.sqrMagnitude > targetVelocity.sqrMagnitude;
            float rate = decelerating ? deceleration : acceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * Time.deltaTime);

            // Weiche Drehung in Bewegungsrichtung (keine Sofortdrehung).
            if (moveDirection.sqrMagnitude > rotationThreshold * rotationThreshold)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            // Schwerkraft und Bodenhaftung.
            if (_climber != null && _climber.TryGetClimbSpeed(_horizontalVelocity, out float climbUp))
            {
                // Klettern: Hindernis überwinden, normale Schwerkraft/Bodenhaftung für diesen Frame überspringen.
                _verticalVelocity = climbUp;
            }
            else if (_controller.isGrounded)
            {
                if (_verticalVelocity > 0f)
                {
                    _verticalVelocity = 0f;
                }

                _verticalVelocity = -groundAdhesion;
            }
            else
            {
                _verticalVelocity += Physics.gravity.y * gravityScale * Time.deltaTime;
            }

            // Bewegung ausführen.
            _lastVelocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(_lastVelocity * Time.deltaTime);
        }

        /// <summary>
        /// Projektion von Kamera-Forward/-Right auf die XZ-Ebene, daraus die gewünschte
        /// Bewegungsrichtung. Fällt ohne Kamera auf die eigene Achse der Spinne zurück.
        /// </summary>
        private Vector3 GetCameraRelativeDirection(Vector2 moveInput)
        {
            Transform cam = cameraTransform;
            if (cam == null)
            {
                if (!_warnedNoCamera)
                {
                    _warnedNoCamera = true;
                    Debug.LogWarning("SpiderMovement: cameraTransform ist nicht gesetzt – verwende objektrelative Bewegung.", this);
                }

                cam = transform;
            }

            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            right.Normalize();

            Vector3 direction = forward * moveInput.y + right * moveInput.x;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return direction;
        }
    }
}
