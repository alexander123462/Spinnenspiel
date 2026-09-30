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

        [Tooltip("Wie lange (s) sich das Insekt im Netz zappelt, bevor es sich befreit.")]
        public float escapeTime = 10f;

        /// <summary>Wahrscheinlichkeit (0..1) pro Neu-Zielen, ein fertiges Netz anzufliegen (simulierte Flugschneise).</summary>
        public float webAttraction = 0.35f;

        /// <summary>Festhalten, damit es nicht aus dem Netz ausbricht (z. B. beim Einwickeln).</summary>
        public void Hold()
        {
            escapeTime = float.PositiveInfinity;
        }

        /// <summary>Netz, in dem das Insekt gefangen ist (null wenn frei).</summary>
        public OrbWeb CaughtIn;
        /// <summary>Wo es am Fangpunkt sitzt (Zappeln passiert um diesen Punkt).</summary>
        public Vector3 CaughtPoint;

        private float _caughtAt;
        private float _ignoreWebsUntil;
        private float _lastTwitch;
        private Vector3 _twitchOffset;
        private float _lastWebVibe;

        private float _webRunUntil; // Bis zu diesem Zeitpunkt wird die Home-Rückkehr unterdrückt (Flug ins Netz).

        [SerializeField] private float wanderRadius = 2.5f;
        [SerializeField] private float minHeight = 0.25f;   // über dem Boden
        [SerializeField] private float maxHeight = 1.6f;    // über dem Boden

        /// <summary>Aktueller Zustand.</summary>
        public InsectState State { get; private set; } = InsectState.Flying;

        /// <summary>Position des vorigen Frames (für die Netz-Kollision).</summary>
        public Vector3 LastPosition { get; private set; }

        /// <summary>Wird ausgelöst, wenn das Insekt die Szene verlässt (vor Destroy).</summary>
        public event System.Action<FlyingInsect> Removed;

        /// <summary>Wird ausgelöst, wenn sich JEDES Insekt in einem Netz verfängt (Spinne → Anlockung).</summary>
        public static event System.Action<FlyingInsect, OrbWeb> AnyCaught;

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
            switch (State)
            {
                case InsectState.Flying:
                    UpdateFlight();
                    // Netz-Kollision: nur wenn kurz nach dem Befreien kein Immunitätsfenster läuft.
                    if (Time.time >= _ignoreWebsUntil)
                    {
                        CheckWebs(previous);
                    }
                    break;

                case InsectState.Caught:
                    UpdateCaught();
                    break;

                default:
                    // Wrapped (7.3) und Eaten: keine Bewegung.
                    break;
            }

            LastPosition = previous;
            AnimateWings();
        }

        /// <summary>Ziel setzen (z. B. für Tests oder spätere Anlockung): direkt dorthin fliegen.
        /// netzSchneise=true: auf dieser Schiene wird Hindernissen nicht ausgewichen (das Netz wird geflogen).</summary>
        public void FlyTo(Vector3 p, bool netzSchneise = false)
        {
            target = p;
            nextTargetTime = Time.time + 10f;
            if (netzSchneise)
            {
                _webRunUntil = Time.time + 4f;
            }
        }

        /// <summary>
        /// Flugrichtung beim Start setzen (für Tests: die Fliege startet direkt in
        /// Zielrichtung statt mit der Template-Richtung +Z, was einen weiten
        /// Kurvenbogen um das Ziel vermeiden kann).
        /// </summary>
        public void SetInitialDirection(Vector3 dir)
        {
            currentDirection = dir.normalized;
            hasDirection = true;
        }

        /// <summary>
        /// Prüft alle Netze: hat das Segment (vorherige → aktuelle Position) ein
        /// fangfähiges Netz innerhalb des Rahmens durchquert? Dann fangen.
        /// </summary>
        private void CheckWebs(Vector3 from)
        {
            Vector3 to = transform.position;
            foreach (OrbWeb web in OrbWeb.All)
            {
                Vector3 hit;
                if (web.TryIntersectSegment(from, to, out hit))
                {
                    Catch(web, hit);
                    return;
                }
            }
        }

        /// <summary>In das Netz fangen: hinsetzen, Netz vibrieren, allen Bescheid sagen.</summary>
        private void Catch(OrbWeb web, Vector3 hit)
        {
            State = InsectState.Caught;
            CaughtIn = web;
            CaughtPoint = hit;
            transform.position = hit;
            _caughtAt = Time.time;
            _lastTwitch = 0f;
            _lastWebVibe = 0f;
            web.Vibrate(1f);
            System.Action<FlyingInsect, OrbWeb> handler = AnyCaught;
            if (handler != null)
            {
                handler(this, web);
            }
        }

        /// <summary>
        /// Gefangen: um den Fangpunkt zappeln (neuer Zufallsversatz alle ~0.08 s),
        /// Flügel schneller, Netz alle 0.4 s mitvibrieren. Nach escapeTime:
        /// befreien, kurz vor dem Netz wegfliegen, 2 s nicht fangbar.
        /// </summary>
        private void UpdateCaught()
        {
            OrbWeb web = CaughtIn;
            if (web == null)
            {
                // Netz wurde zwischenzeitlich entfernt → einfach frei.
                State = InsectState.Flying;
                _ignoreWebsUntil = Time.time + 2f;
                return;
            }

            // Zappeln: neuer Zufallsversatz (Kugel 3 mm) alle ~0.08 s.
            if (Time.time - _lastTwitch >= 0.08f)
            {
                _lastTwitch = Time.time;
                _twitchOffset = Random.insideUnitSphere * 0.003f;
            }
            transform.position = CaughtPoint + _twitchOffset;

            // Alle 0.4 s das Netz mitvibrieren.
            if (Time.time - _lastWebVibe >= 0.4f)
            {
                _lastWebVibe = Time.time;
                web.Vibrate(0.6f);
            }

            if (Time.time - _caughtAt >= escapeTime)
            {
                // Befreiung: 8 cm auf der Seite WEG vom Netz (Normal oder ihr Spiegel),
                // dann fliegen mit 2 s Fang-Immunität, damit es nicht sofort wieder hängen bleibt.
                Vector3 away = web.PlaneNormal * 0.08f;
                if (Vector3.Dot(away, transform.position - web.Layout.Hub) < 0f)
                {
                    away = -away;
                }
                State = InsectState.Flying;
                transform.position += away;
                _ignoreWebsUntil = Time.time + 2f;
                CaughtIn = null;
            }
        }

        /// <summary>Neues Wandern-Ziel: zufälliger Punkt in der Kugel um home, Höhe über Boden geklemmt.</summary>
        private void Retarget()
        {
            // Simulierte Flugschneise: mit Wahrscheinlichkeit webAttraction fliegt die
            // Fliege durch ein nahes fertiges Netz (Ziel 0.6 m HINTER der Netzebene).
            if (Random.value < webAttraction)
            {
                OrbWeb web = FindNearbyCatchingWeb();
                if (web != null)
                {
                    FlyIntoWeb(web);
                    return;
                }
            }

            Vector3 random = Random.insideUnitSphere * wanderRadius;
            Vector3 pos = home + random;
            pos.y = GroundHeight(pos) + Mathf.Clamp(Random.Range(minHeight, maxHeight), minHeight, maxHeight);
            target = pos;

            // Neues Ziel auch nach 2–4 s, damit das Insekt nicht stillhält.
            nextTargetTime = Time.time + Random.Range(2f, 4f);
        }

        /// <summary>
        /// Lenkt die Fliege auf einen Punkt 0.6 m hinter der Netzebene (relativ
        /// zur aktuellen Seite) – der Weg dorthin kreuzt die Fangspirale.
        /// </summary>
        private void FlyIntoWeb(OrbWeb web)
        {
            Vector3 hub = web.Layout.Hub;
            Vector3 n = web.PlaneNormal;
            int side = Vector3.Dot(transform.position - hub, n) >= 0f ? 1 : -1;
            Vector3 u = Vector3.Cross(n, Vector3.up).normalized;
            if (u.sqrMagnitude < 1e-6f)
            {
                u = Vector3.right;
            }

            Vector3 v = Vector3.Cross(n, u);
            Vector2 o = Random.insideUnitCircle * (web.CaptureRadius * 0.8f);
            target = hub + u * o.x + v * o.y - n * side * 0.6f;
            nextTargetTime = Time.time + 4f;
            _webRunUntil = Time.time + 4f;
        }

        /// <summary>Nächstes Netz mit CanCatch und Hub < 4 m Abstand zur Fliege (null wenn keines).</summary>
        private OrbWeb FindNearbyCatchingWeb()
        {
            OrbWeb best = null;
            float bestDist = 4f;
            foreach (OrbWeb web in OrbWeb.All)
            {
                if (web == null || !web.CanCatch)
                {
                    continue;
                }

                float d = Vector3.Distance(web.Layout.Hub, transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = web;
                }
            }

            return best;
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
            if (Time.time >= _webRunUntil &&
                Physics.SphereCast(transform.position, 0.03f, facing, out blocked, 0.3f,
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
            if (Time.time > _webRunUntil && Vector3.Distance(transform.position, home) > wanderRadius)
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

            // Im Netz gefangen: Flügel schneller schlagen (Faktor 1.6).
            float freq = State == InsectState.Caught ? 28f * 1.6f : 28f;
            float angle = 20f + 55f * Mathf.Sin(Time.time * Mathf.PI * 2f * freq);
            if (wingLeft != null)
            {
                wingLeft.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }

            if (wingRight != null)
            {
                wingRight.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// Einwickeln (7.3a): Fortschritt 0..1. Erstellt den Kokon (Sphere ohne Collider,
        /// Kind des Insekts), dreht ihn beim Wachsen und stellt bei p01 >= 1 auf
        /// Wrapped um und schaltet die Flügel ab.
        /// </summary>
        public void SetWrapProgress(float p01)
        {
            Transform cocoon = transform.Find("Kokon");
            if (cocoon == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.name = "Kokon";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.92f, 0.92f, 0.88f);
                mat.SetFloat("_Smoothness", 0.6f);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                cocoon = go.transform;
            }

            cocoon.localScale = new Vector3(0.03f, 0.03f, 0.045f) * Mathf.Clamp01(p01);

            if (p01 < 1f)
            {
                // Solange gewickelt wird: das Insekt dreht sich um die eigene Achse.
                transform.Rotate(0f, 0f, 720f * Time.deltaTime, Space.Self);
            }
            else
            {
                State = InsectState.Wrapped;
                if (wingLeft != null)
                {
                    wingLeft.gameObject.SetActive(false);
                }

                if (wingRight != null)
                {
                    wingRight.gameObject.SetActive(false);
                }
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
