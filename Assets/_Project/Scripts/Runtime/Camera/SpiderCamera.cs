using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Third-Person-Orbit-Kamera um die Spinne.
    /// - Yaw/Pitch aus SpiderInput.Look, weiche Nachführung (SmoothDamp für den Pivot, exponentielles Slerp für die Rotation)
    /// - Kollision per SphereCast (Kamera geht nicht durch Bäume/Felsen), weiches Zurückfahren
    /// - Zoom per Mausrad, leichtes FOV-Weiten beim Sprinten
    /// - Erweiterbar über Distance/FieldOfViewOffset/Target (Klettern, Kampf, große Gegner)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class SpiderCamera : MonoBehaviour
    {
        [Header("Referenzen")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.05f, 0f); // A2 Groessengefuehl: niedrig/nah
        [SerializeField] private SpiderInput input;

        [Header("Orbit")]
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float pitch = 9f; // C1: näher/tiefer für Referenzlook
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 75f;
        [Tooltip("Grad pro Input-Einheit (Maus-Delta in Pixeln, Stick in -1..1).")]
        [SerializeField] private float lookSensitivity = 0.15f;

        [Header("Distanz / Zoom")]
        [SerializeField] private float desiredDistance = 0.38f; // C1: näher/tiefer für Referenzlook
        [SerializeField] private float minDistance = 0.18f;
        [SerializeField] private float minZoomDistance = 0.25f;
        [SerializeField] private float maxZoomDistance = 3.5f;
        [SerializeField] private float zoomStep = 0.12f;

        [Header("Weiche Nachführung")]
        [SerializeField] private float followTime = 0.12f;
        [SerializeField] private float rotationSmoothing = 14f;
        [SerializeField] private float distanceSmoothing = 8f;
        [Tooltip("Wie schnell die Kamerahochachse der Spinne folgt (z. B. an der Wand).")]
        [SerializeField] private float upFollow = 3f;

        [Header("Kollision")]
        [SerializeField] private float collisionRadius = 0.04f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float groundClearance = 0.05f;

        [Header("Sprint")]
        [SerializeField] private float baseFieldOfView = 55f; // C1: näher/tiefer für Referenzlook
        [SerializeField] private float sprintFieldOfViewBoost = 7f;
        [SerializeField] private float fieldOfViewSmoothing = 5f;

        private Camera _camera;
        private SpiderMovement _movement;
        private SpiderGrowth _growth; // K5: Kamera follows the spider's growth.
        private float _currentDistance;
        private float _zoomDistance;
        private Vector3 _pivotPosition;
        private Vector3 _pivotVelocity;
        private Vector3 _frameUp = Vector3.up;
        private Quaternion _frame = Quaternion.identity;

        /// <summary>Wunschdistanz zur Spinne.</summary>
        public float Distance
        {
            get => _zoomDistance;
            set => _zoomDistance = Mathf.Clamp(value, minZoomDistance, maxZoomDistance);
        }

        /// <summary>Zusätzlicher FOV-Versatz für spätere Kamera-Modi (z. B. Kampf, große Gegner).</summary>
        public float FieldOfViewOffset { get; set; }

        /// <summary>S3: Wackel-Offset (m, camera-local right/up) — CameraShake per Frame setzen; LateUpdate verhuert.</summary>
        public Vector3 ShakeOffset { get; set; }

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public float Yaw => yaw;

        /// <summary>FOTO1: Kamera-Blickwinkel (Grad nach oben) fuer den Foto-Modus setzen.</summary>
        public void SetPitch(float grad)
        {
            pitch = Mathf.Clamp(grad, minPitch, maxPitch);
        }

        /// <summary>K5: Groeffieffeffe of the spider (1 = start size), 1 if no SpiderGrowth attached.</summary>
        private float Growth => _growth != null ? _growth.GrowthScale : 1f;

        private void Awake()
        {
            // Pflanzen blockieren die Kamera nicht (eigene Layer-8-Ebene).
            obstacleMask &= ~GameLayers.PlantMask;
            _camera = GetComponent<Camera>();
            if (target == null)
            {
                SpiderMovement found = FindFirstObjectByType<SpiderMovement>();
                if (found != null)
                {
                    target = found.transform;
                }
                else
                {
                    Debug.LogWarning("SpiderCamera: Kein Ziel gefunden.", this);
                }
            }

            if (target != null)
            {
                _movement = target.GetComponent<SpiderMovement>();
                _growth = target.GetComponent<SpiderGrowth>(); // K5: Kamera follows growth.
                yaw = target.eulerAngles.y;
            }

            if (input == null)
            {
                input = target != null ? target.GetComponent<SpiderInput>() : null;
                if (input == null)
                {
                    input = FindFirstObjectByType<SpiderInput>();
                }
            }

            _zoomDistance = Mathf.Clamp(desiredDistance, minZoomDistance, maxZoomDistance);
            _currentDistance = _zoomDistance;

            if (target != null)
            {
                _frameUp = target.up.normalized;
            }
            _frame = Quaternion.FromToRotation(Vector3.up, _frameUp);
            _pivotPosition = DesiredPivotPosition();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;

            // Kamerarahmen: Hochachse folgt der Spinne (Wand/Stamm), statt festem Welt-oben.
            _frameUp = Vector3.Slerp(_frameUp, target != null ? target.up : Vector3.up, 1f - Mathf.Exp(-upFollow * deltaTime)).normalized;
            _frame = Quaternion.FromToRotation(Vector3.up, _frameUp);

            UpdateOrbit();
            UpdateZoom();

            // K5: effective zoom scales with the spider's growth (camera follows).
            float effZoom = _zoomDistance * Growth;

            _pivotPosition = Vector3.SmoothDamp(_pivotPosition, DesiredPivotPosition(), ref _pivotVelocity, followTime, Mathf.Infinity, deltaTime);

            Quaternion targetRotation = _frame * Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-rotationSmoothing * deltaTime));

            Vector3 direction = transform.rotation * Vector3.back;
            float allowed = effZoom;
            RaycastHit hit;
            if (Physics.SphereCast(_pivotPosition, collisionRadius * Growth, direction, out hit, effZoom + 0.05f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                allowed = Mathf.Max(hit.distance - 0.01f, minDistance);
            }

            // Bei Hindernissen sofort heranziehen (kein Durchblicken), beim Freiwerden weich zurück.
            if (allowed < _currentDistance)
            {
                _currentDistance = allowed;
            }
            else
            {
                _currentDistance = Mathf.Lerp(_currentDistance, allowed, 1f - Mathf.Exp(-distanceSmoothing * deltaTime));
            }

            Vector3 position = _pivotPosition + direction * _currentDistance;
            position = KeepAboveGround(position);
            transform.position = position + ShakeOffset; // S3: Wackeln from CameraShake

            UpdateFieldOfView(deltaTime);
        }

        private void UpdateOrbit()
        {
            Vector2 look = input != null ? input.Look : Vector2.zero;
            if (look.sqrMagnitude < 0.000001f)
            {
                return;
            }

            yaw += look.x * lookSensitivity;
            pitch -= look.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void UpdateZoom()
        {
            float zoom = input != null ? input.Zoom : 0f;
            if (Mathf.Abs(zoom) > 0.001f)
            {
                // Multiplikativ: nah dran feine Schritte, weit weg größere.
                Distance = _zoomDistance * (1f - zoom * zoomStep);
            }
        }

        private void UpdateFieldOfView(float deltaTime)
        {
            if (_camera == null)
            {
                return;
            }

            float sprint01 = 0f;
            if (_movement != null && _movement.IsSprinting)
            {
                sprint01 = Mathf.Clamp01(_movement.Speed / Mathf.Max(0.01f, _movement.SprintSpeed));
            }

            float targetFov = baseFieldOfView + sprintFieldOfViewBoost * sprint01 + FieldOfViewOffset;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 1f - Mathf.Exp(-fieldOfViewSmoothing * deltaTime));
        }

        private Vector3 KeepAboveGround(Vector3 position)
        {
            // Nur bei "normaler" Bodenhaltung: an Wänden/Stämmen wäre der senkrechte Raycast falsch.
            if (Vector3.Angle(_frameUp, Vector3.up) >= 30f)
            {
                return position;
            }

            Vector3 origin = new Vector3(position.x, position.y + 2f, position.z);
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 4f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                float minY = hit.point.y + groundClearance;
                if (position.y < minY)
                {
                    position.y = minY;
                }
            }

            return position;
        }

        private Vector3 DesiredPivotPosition()
        {
            // Offset im Kamerarahmen: an der Wand zeigt „über der Spinne" von der Wand weg.
            // K5: the pivot offset scales with the spider's growth (camera height follows).
            return target != null ? target.position + _frame * (targetOffset * Growth) : _frame * targetOffset;
        }
    }
}
