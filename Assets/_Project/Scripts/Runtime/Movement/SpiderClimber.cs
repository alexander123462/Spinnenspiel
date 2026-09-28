using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Klettern über Hindernisse (z. B. liegende Baumstämme): Prüft per Kapselwurf,
    /// ob vor der Spinne ein steiles Hindernis liegt, dessen Höhe über dem
    /// stepOffset des CharacterControllers liegt, und liefert dafür die
    /// Aufwärtsgeschwindigkeit, mit der SpiderMovement die Spinne überhebt.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class SpiderClimber : MonoBehaviour
    {
        [Header("Klettern")]
        [SerializeField] private float maxClimbHeight = 0.3f; // Max. Höhe, die die Spinne überklettern kann (m)
        [SerializeField] private float probeDistance = 0.08f; // Wie weit vor der Spinne nach Hindernissen gesucht wird (m)
        [SerializeField] private float climbSpeed = 0.45f; // Aufwärtsgeschwindigkeit beim Klettern (m/s)

        private CharacterController _controller;

        /// <summary>True, solange die Spinne in diesem Frame klettert.</summary>
        public bool IsClimbing { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <summary>
        /// Prüft, ob in horizontaler Bewegungsrichtung ein Hindernis liegt, das
        /// die Spinne überklettern kann. Bei Ja enthält upwardSpeed die
        /// Klettergeschwindigkeit für diesen Frame.
        /// </summary>
        public bool TryGetClimbSpeed(Vector3 horizontalVelocity, out float upwardSpeed)
        {
            upwardSpeed = 0f;
            IsClimbing = false;

            if (_controller == null)
            {
                return false;
            }

            // a) Keine nennenswerte horizontale Bewegung -> kein Klettern.
            Vector3 horizontal = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
            if (horizontal.magnitude < 0.05f)
            {
                return false;
            }

            Vector3 dir = horizontal.normalized;

            // Alle Layer außer Layer 2 (IgnoriereLayer für Trigger/Partikel).
            int mask = Physics.DefaultRaycastLayers & ~(1 << 2);

            // b) Kapselwurf in Bewegungsrichtung: Kapsel des Controllers in
            //    Weltkoordinaten, beide Halbkugel-Mittelpunkte, Radius um 5 % verkleinert.
            Vector3 capsuleCenter = transform.position + _controller.center;
            float hemisphereOffset = (_controller.height / 2f) - _controller.radius;
            Vector3 castStart = capsuleCenter - Vector3.up * hemisphereOffset;
            Vector3 castEnd = capsuleCenter + Vector3.up * hemisphereOffset;

            if (!Physics.CapsuleCast(castStart, castEnd, _controller.radius * 0.95f, dir, out RaycastHit hit,
                    probeDistance, mask, QueryTriggerInteraction.Ignore))
            {
                return false; // c) Kein Treffer -> kein Hindernis.
            }

            // c) Treffer-Normale flacher als slopeLimit -> normale Neigung, kein Klettern.
            float minNormalY = Mathf.Cos(Mathf.Deg2Rad * _controller.slopeLimit);
            if (hit.normal.y > minNormalY)
            {
                return false;
            }

            // d) Oberkante des Hindernisses suchen: Raycast senkrecht nach unten
            //    in der Bewegungsebene etwas vor dem Trefferpunkt.
            float footHeight = capsuleCenter.y - (_controller.height / 2f); // Unterkante der Kapsel
            Vector3 rayStart = hit.point + dir * 0.03f;
            rayStart.y = footHeight + maxClimbHeight + 0.05f;

            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit topHit,
                    maxClimbHeight + 0.1f, mask, QueryTriggerInteraction.Ignore))
            {
                return false; // Keine Oberkante in Reichweite.
            }

            // e) Hindernishöhe relativ zur Fußhöhe bestimmen.
            float obstacleHeight = topHit.point.y - footHeight;
            if (obstacleHeight > _controller.stepOffset && obstacleHeight <= maxClimbHeight)
            {
                IsClimbing = true;
                upwardSpeed = climbSpeed;
                return true;
            }

            return false; // Zu niedrig für echtes Klettern oder zu hoch, um es zu schaffen.
        }
    }
}
