using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Ein fliegendes Insekt (Fliege). Wandert um einen Heim-Punkt herum,
    /// weicht Hindernissen aus (SphereCast) und schlägt die Flügel.
    /// Bewegung per Transform (kein Rigidbody) – für spätere Netz-Kollision
    /// steht <see cref="LastPosition"/> zur Verfügung.
    /// </summary>
    public class FlyingInsect : MonoBehaviour
    {
        /// <summary>Zustandsmaschine: fliegen, gefangen, eingespult, gefressen.</summary>
        public enum InsectState
        {
            Flying,
            Caught,
            Wrapped,
            Eaten
        }

        [SerializeField] private float speed = 0.8f;
        [SerializeField] private float turnRate = 220f; // Grad pro Sekunde

        [Tooltip("Heimpunkt, um den das Insekt herumwandert.")]
        public Vector3 home;

        [SerializeField] private float wanderRadius = 2.5f;
        [SerializeField] private float minHeight = 0.25f;   // über dem Boden
        [SerializeField] private float maxHeight = 1.6f;    // über dem Boden

        /// <summary>Aktueller Zustand.</summary>
        public InsectState State { get; private set; } = InsectState.Flying;

        /// <summary>Position des vorigen Frames (für die Netz-Kollision).</summary>
        public Vector3 LastPosition { get; private set; }

        /// <summary>Wird ausgelöst, wenn das Insekt die Szene verlässt (vor Destroy).</summary>
        public event System.Action<FlyingInsect> Removed;

        private Transform wingLeft;
        private Transform wingRight;
        private Vector3 target;
        private Vector3 currentDirection;
        private bool hasDirection;
        private float nextTargetTime;
        private float bobPhase;
        private float lastBob;

        private void Awake()
        {
            Transform wing = transform.Find("Wing_L");
            if (wing != null)
            {
                wingLeft = wing;
            }

            wing = transform.Find("Wing_R");
            if (wing != null)
            {
                wingRight = wing;
            }

            if (home == Vector3.zero)
            {
                home = transform.position;
            }

            target = home + Vector3.up * 0.5f;
            currentDirection = Vector3.forward;
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            nextTargetTime = Time.time + Random.Range(2f, 4f);
        }

        private void Update()
        {
            Vector3 previous = transform.position;
            if (State == InsectState.Flying)
            {
                UpdateFlight();
            }

            LastPosition = previous;
            AnimateWings();
        }

        /// <summary>Neues Wandern-Ziel: zufälliger Punkt in der Kugel um home, Höhe über Boden geklemmt.</summary>
        private void Retarget()
        {
            Vector3 random = Random.insideUnitSphere * wanderRadius;
            Vector3 pos = home + random;
            pos.y = GroundHeight(pos) + Mathf.Clamp(Random.Range(minHeight, maxHeight), minHeight, maxHeight);
            target = pos;

            // Neues Ziel auch nach 2–4 s, damit das Insekt nicht stillhält.
            nextTargetTime = Time.time + Random.Range(2f, 4f);
        }

        /// <summary>Bodenhöhe unter einem Punkt (Raycast nach unten, von oben).</summary>
        private float GroundHeight(Vector3 point)
        {
            Vector3 from = home + Vector3.up * 8f;
            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, Mathf.Infinity,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return home.y;
        }

        private void UpdateFlight()
        {
            float distanceToTarget = Vector3.Distance(transform.position, target);
            if (distanceToTarget < 0.2f || Time.time >= nextTargetTime)
            {
                Retarget();
            }

            // Hindernis? SphereCast 0.3 m voraus – dann Ziel auf die Gegenseite.
            Vector3 facing = hasDirection ? currentDirection.normalized : transform.forward;
            RaycastHit blocked;
            if (Physics.SphereCast(transform.position, 0.03f, facing, out blocked, 0.3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Vector3 away = transform.position + (transform.position - blocked.point).normalized * wanderRadius;
                away.y = GroundHeight(away) + Mathf.Clamp(Random.Range(minHeight, maxHeight), minHeight, maxHeight);
                target = away;
                nextTargetTime = Time.time + Random.Range(2f, 4f);
            }

            Vector3 desired = (target - transform.position).normalized;
            if (desired.sqrMagnitude < 1e-6f)
            {
                desired = transform.forward;
            }

            // Weich drehen (turnRate Grad/s) und vorwärts fliegen, dazu leichtes Auf/Ab (sin, 3 Hz).
            currentDirection = Vector3.RotateTowards(currentDirection, desired, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
            hasDirection = true;
            // Bob als Pose-Änderung: nur die DIFferenz zum letzten Frame wird addiert,
            // sonst drifte das Insekt bei hoher Framerate in den Boden.
            float bob = Mathf.Sin(Time.time * Mathf.PI * 2f * 3f + bobPhase) * 0.03f;
            transform.position += currentDirection * speed * Time.deltaTime + Vector3.up * (bob - lastBob);
            lastBob = bob;

            // Höhe absichern: Raycast startet KNAPP UNTER der Fliege – so trifft er
            // weder den eigenen Collider noch Überhänge/Baumkronen über uns (das
            // würde sonst jeden Frame nach oben teleportieren). Max. 10 m in den Boden.
            RaycastHit ground;
            float groundY;
            if (Physics.Raycast(transform.position - Vector3.up * 0.05f, Vector3.down, out ground, 10f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                groundY = ground.point.y;
            }
            else
            {
                groundY = GroundHeight(transform.position);
            }

            float heightAboveGround = transform.position.y - groundY;
            if (heightAboveGround < minHeight * 0.5f)
            {
                // Zu tief: auf halbe Mindesthöhe über dem Boden und weg davon fliegen.
                // Nur nach oben korrigieren, nie unter die aktuelle Position.
                transform.position = new Vector3(transform.position.x,
                    Mathf.Max(transform.position.y, groundY + minHeight * 0.5f), transform.position.z);
                currentDirection.y = Mathf.Abs(currentDirection.y);
            }
            else if (heightAboveGround > maxHeight + 0.3f)
            {
                // Zu hoch: Richtung nach unten drehen.
                currentDirection.y = -Mathf.Abs(currentDirection.y);
            }

            // Zu weit weg von home: Ziel sofort in Richtung home neu wählen.
            if (Vector3.Distance(transform.position, home) > wanderRadius)
            {
                Vector3 back = home + Vector3.up * Random.Range(minHeight, maxHeight);
                target = back;
                nextTargetTime = Time.time + Random.Range(2f, 4f);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(currentDirection), 0.25f);
        }

        /// <summary>Flügel schlagen um die Körper-Längsachse (Lokale Z): ±(20 + 55·sin(t·2π·28)) Grad.</summary>
        private void AnimateWings()
        {
            if (wingLeft == null && wingRight == null)
            {
                return;
            }

            float angle = 20f + 55f * Mathf.Sin(Time.time * Mathf.PI * 2f * 28f);
            if (wingLeft != null)
            {
                wingLeft.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }

            if (wingRight != null)
            {
                wingRight.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>Nennt das Insekt ab (Ereignis auslösen, dann aus der Szene entfernen).</summary>
        public void Remove()
        {
            State = InsectState.Eaten;
            var handler = Removed;
            if (handler != null)
            {
                handler(this);
            }

            Destroy(gameObject);
        }
    }
}
