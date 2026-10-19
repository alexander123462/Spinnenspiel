using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Oberflächen-Haftung statt CharacterController: Die Spinne haftet an jeder Unterlage
    /// (Boden, Wand, Kante, Überhang), richtet ihre up-Achse an der Oberflächen-Normale aus
    /// und läuft tangent zur Fläche. Position wird manuell integriert; ein Kollisionssphärenwurf
    /// begrenzt den Schritt vor Hindernissen. Verliert sie die Unterlage, fällt sie und
    /// haftet beim nächsten Treffer sofort wieder an.
    /// </summary>
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

        [Header("Sprung")]
        [SerializeField, Tooltip("Hubhöhe der Sprung (m)")] private float jumpHeight = 0.5f;
        [SerializeField, Tooltip("Weitsch des Sprung (m)")] private float jumpDistance = 1.2f;
        [SerializeField, Tooltip("Abklingzeit vor dem naechste Sprung (s)")] private float jumpCooldown = 0.6f;

        [Header("Oberflächen-Haftung")]
        [SerializeField] private SpiderSurfaceSensor sensor = new SpiderSurfaceSensor();
        [SerializeField] private float adhesionRate = 20f; // wie schnell (1/s) die Spinne zur Fläche gezogen wird
        [SerializeField] private float alignRate = 6f; // up-Achse zur Normale ausrichten (rad/s)
        [SerializeField] private float detachDelay = 0.2f; // Toleranz nach Letztem Oberflächen-Treffer (s)
        [SerializeField] private float bodyRadius = 0.05f; // Körperkugel für Kollisionsschutz (m)

        [Header("Energie-Folgen (J2c)")]
        [SerializeField, Tooltip("Energie-component of the spider (tempo factor reads it).")] private SpiderEnergy energy;
        [SerializeField, Tooltip("Under this energy the spider moves at the reduced tempo.")] private float lowEnergyThreshold = 25f;
        [SerializeField, Tooltip("Tempo factor under the low-energy threshold.")] private float lowEnergyTempoFactor = 0.6f;

        [Header("Radnetz")]
        [SerializeField] private float webSpeedFactor = 0.6f; // Tempo-Faktor im Netz (Relativ zu Lauf-/Sprinttempo)
        [SerializeField] private float webGrip = 0.12f; // max. Abstand (m) zu einem Faden, damit die Spinne sich weiterbewegt
        [SerializeField] private float webBodyOffset = 0.03f; // Abstand des Körpers zur Netzebene (m)

        [Header("Weltgrenzen")]
        [SerializeField] private bool useWorldBounds; // XZ-Abgrenzung nur, wenn an
        [SerializeField] private Vector3 worldBoundsMin;
        [SerializeField] private Vector3 worldBoundsMax;

        /// <summary>Weltgrenzen zur Laufzeit setzen (z. B. von der Urwald-Generierung).</summary>
        public void SetWorldBounds(Vector3 min, Vector3 max)
        {
            useWorldBounds = true;
            worldBoundsMin = min;
            worldBoundsMax = max;
        }

        private Vector3 _vel;
        private Vector3 _lastVelocity;
        private Vector3 _surfaceNormal = Vector3.up;
        private bool _attached = true;
        private float _lastSurfaceTime = float.NegativeInfinity;
        private float _wallClimbTime = float.NegativeInfinity;
        private Vector3 _lastWallPoint = Vector3.zero;
        private Vector3 _lastWallNormal = Vector3.zero;
        private Vector2? _externalMoveInput;
        private float _ropeLength;
        private Vector3 _ropeVel;
        private float? _externalRope;
        private Vector2? _externalMoveLocal;
        private bool? _externalSprint;
        private float _threadT;
        private float _boardCooldownUntil;
        private bool _warnedNoCamera;
        private SpiderSurfaceDetector _surface;
        private Collider _bodyCollider;
        private float _jumpCooldownUntil = float.NegativeInfinity;
        private Vector3 _jumpVel;
        private bool _inJump;
        private bool _jumpRequest;

        /// <summary>Letzte Gesamtbewegungsgeschwindigkeit in m/s (Positionsdifferenz pro Sekunde).</summary>
        public Vector3 Velocity => _lastVelocity;

        /// <summary>Aktuelle tangentiale Geschwindigkeit in m/s.</summary>
        public float Speed => _vel.magnitude;

        /// <summary>True, solange die Spinne an einer Oberfläche haftet.</summary>
        public bool IsAttached => _attached;

        /// <summary>Normale der zuletzt erkannten Oberfläche (Weltkoordinaten).</summary>
        public Vector3 SurfaceNormal => _surfaceNormal;

        /// <summary>L2: Sprung anfordern (z. B. aus Tests); bei true der naechste Frame ein Absprung versuche.</summary>
        public void RequestJump()
        {
            _jumpRequest = true;
        }

        /// <summary>True, wenn die Spinne an einer Unterlage haftet (Boden, Wand, ...), am Faden hängt oder im Netz läuft.</summary>
        public bool IsGrounded => IsAttached || CurrentThread != null || CurrentWeb != null;

        /// <summary>True, solange die Spinne am Seil (Dragline) hängt.</summary>
        public bool IsOnRope { get; private set; }

        /// <summary>An welchen Seidenfaden die Spinne aktuell hängt/klettert (null = kein Faden).</summary>
        public SilkThread CurrentThread { get; private set; }

        /// <summary>In welchem Radnetz die Spinne aktuell läuft (null = kein Netz).</summary>
        public OrbWeb CurrentWeb { get; private set; }

        public float WalkSpeed => walkSpeed;

        public float SprintSpeed => sprintSpeed;

        /// <summary>True, wenn Sprint gehalten wird und sich die Spinne bewegt.</summary>
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

        /// <summary>J2c: Tempo-Faktor aus der Energie — under the threshold moves at the reduced tempo.
        /// 1 when energy is unknown (no component) so movement never breaks.</summary>
        public float EnergyTempoFactor()
        {
            if (energy == null)
            {
                energy = GetComponent<SpiderEnergy>();
            }
            if (energy == null)
            {
                return 1f;
            }
            return energy.Energy < lowEnergyThreshold ? lowEnergyTempoFactor : 1f;
        }

        /// <summary>S7: Bis zu diesem Moment betauebt (Wespe-Sting): movement input ignored.</summary>
        private float _stunUntil;

        /// <summary>S7: Betaeubing: the spider cannot move for the given seconds.</summary>
        public void SetStun(float seconds)
        {
            if (seconds > 0f)
            {
                _stunUntil = Time.time + seconds;
            }
        }

        /// <summary>S7: True while a stun runs (for tests and HUD).</summary>
        public bool IsStunned { get; private set; }

        /// <summary>
        /// Setzt eine externe, kamerarelative Bewegungsrichtung (z. B. für automatisierte Tests).
        /// Wenn nicht null, ersetzt der Wert die Tastatur-/Gamepad-Eingabe.
        /// </summary>
        public void SetExternalMoveInput(Vector2? moveInput)
        {
            _externalMoveInput = moveInput;
        }

        /// <summary>
        /// Setzt eine externe Bewegungsrichtung RELATIV ZUR EIGENEN BLICKRICHTUNG DER SPINNE
        /// (x = rechts, y = vorwärts). Hat Vorrang vor allen anderen Eingaben. Für Tests an Wänden.
        /// </summary>
        public void SetExternalMoveLocal(Vector2? moveInput)
        {
            _externalMoveLocal = moveInput;
        }

        /// <summary>
        /// Setzt einen externen Sprint-Zustand (z. B. für automatisierte Tests).
        /// Wenn nicht null, ersetzt der Wert die Tastatur-/Gamepad-Eingabe.
        /// </summary>
        public void SetExternalSprint(bool? sprint)
        {
            _externalSprint = sprint;
        }

        /// <summary>True, während im Skriptmodus ein anderes Skript transform direkt steuert.</summary>
        public bool IsScripted { get; private set; }

        /// <summary>
        /// Skriptmodus: bei true überspringt Update() die Bewegung vollständig –
        /// ein anderes Skript bewegt transform direkt. Bei false setzt
        /// Teleport() am aktuellen Ort Geschwindigkeit/Anker zurück.
        /// </summary>
        public void SetScripted(bool on)
        {
            IsScripted = on;
            if (!on)
            {
                Teleport(transform.position, transform.rotation);
            }
        }

        /// <summary>
        /// Teleportiert die Spinne, setzt die Geschwindigkeit auf null und hält sie
        /// im Haftzustand (für Tests, z. B. an Wänden).
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            _vel = Vector3.zero;
            _lastVelocity = Vector3.zero;
            _attached = true;
            _lastSurfaceTime = Time.time;

            var dl = GetComponent<SpiderDragline>();
            if (dl != null)
            {
                dl.ResetAnchor(position, rotation * Vector3.up);
            }
            IsOnRope = false;
        }

        /// <summary>
        /// Shared: Set a landing position so that the body collider bottom rests ON the terrain
        /// surface instead of inside it. A SphereCast-landing hands back the contact point, which
        /// sits at the surface, but the collider bottom (center - halfExtent) would then be BELOW
        /// the terrain -> the spider sinks in (S2). Raises the center so the bottom equals terrain
        /// height plus a small clearance. Root cause shared with S1 spawn grounding.
        /// </summary>
        private Vector3 SettleOnTerrain(Vector3 landing)
        {
            if (_bodyCollider == null)
            {
                return landing;
            }

            Terrain terr = Terrain.activeTerrain;
            if (terr == null)
            {
                return landing;
            }

            float terrainY = terr.SampleHeight(landing) + terr.transform.position.y;
            // Half extent from body center to collider bottom (current collider, pre-move).
            float halfToBottom = transform.position.y - _bodyCollider.bounds.min.y;
            float targetCenterY = terrainY + halfToBottom + 0.03f;
            if (landing.y < targetCenterY)
            {
                landing.y = targetCenterY;
            }

            return landing;
        }

        /// <summary>
        /// Hängt die Spinne an einen Seidenfaden (t = Position 0..1, 0 = anchorA).
        /// Davor laufende Bewegung wird verworfen; das Bein-System greift dann auf den Faden.
        /// </summary>
        public void AttachToThread(SilkThread thread, float t)
        {
            CurrentThread = thread;
            _threadT = Mathf.Clamp01(t);
            _vel = Vector3.zero;
            _lastVelocity = Vector3.zero;
        }

        /// <summary>Entfernt den Fadenbezug (z. B. beim Loslassen an einem Ende).</summary>
        public void DetachFromThread()
        {
            CurrentThread = null;
            _boardCooldownUntil = Time.time + 1f;
        }

        /// <summary>
        /// Hängt die Spinne in ein Radnetz ein: läuft auf den Fäden, Beine greifen selbst
        /// über OrbWeb.TryClosestPoint. Davor laufende Bewegung wird verworfen.
        /// </summary>
        public void AttachToWeb(OrbWeb web)
        {
            CurrentWeb = web;
            CurrentThread = null;
            IsOnRope = false;
            _attached = false;
            _vel = Vector3.zero;
            _lastVelocity = Vector3.zero;
        }

        /// <summary>Entfernt den Netzbezug (z. B. am Netzrand).</summary>
        public void DetachFromWeb()
        {
            CurrentWeb = null;
        }

        /// <summary>
        /// Faden aktiv loslassen: Fadenbezug entfernen und Haftung aufheben, damit die Spinne fällt,
        /// bis sie wieder auf einer Oberfläche landet.
        /// </summary>
        public void LetGo()
        {
            DetachFromThread();
            CurrentWeb = null;
            _boardCooldownUntil = Time.time + 1f;
            _attached = false;
            CatchOnDragline();
        }

        /// <summary>
        /// Von der Oberfläche auf einen vorhandenen Faden steigen: Echte Spinnen laufen einfach
        /// von der Rinde auf einen Faden, wenn sie in dessen Richtung laufen und er in Reichweite ist.
        /// </summary>
        private void TryBoardThread(Vector3 moveDirWorld)
        {
            if (!_attached || CurrentThread != null || IsScripted) return;
            if (moveDirWorld.sqrMagnitude <= 0.01f) return;
            if (Time.time < _boardCooldownUntil) return;

            Vector3 pos = transform.position;
            Vector3 dir = moveDirWorld.normalized;
            foreach (SilkThread thread in SilkThread.All)
            {
                float t = thread.ClosestT(pos);
                Vector3 p = thread.PointAt(t);
                if (Vector3.Distance(p, pos) > 0.08f) continue;

                Vector3 tangent = thread.TangentAt(t).normalized;
                if (tangent.sqrMagnitude < 0.0001f) continue;
                // Richtung in den Faden hinein: bei t < 0.5 nach A zu, sonst nach B zu.
                Vector3 richtung = t < 0.5f ? tangent : -tangent;
                if (Vector3.Dot(dir, richtung) <= 0.6f) continue;

                AttachToThread(thread, Mathf.Clamp(t, 0.03f, 0.97f));
                return;
            }
        }

        /// <summary>
        /// Externe Seillänge-Eingabe setzen (+/- 1 = kürzer/länger). null = Tastatur übernehmen.
        /// </summary>
        public void SetExternalRopeInput(float? v)
        {
            _externalRope = v;
        }

        /// <summary>
        /// Beim Loslassen an den Sicherheitsfaden der Dragline hängen,
        /// wenn sich eine Dragline an der Spinne befindet.
        /// </summary>
        private void CatchOnDragline()
        {
            var dl = GetComponent<SpiderDragline>();
            if (dl == null)
            {
                return;
            }

            IsOnRope = true;
            // Mindestlänge: Spinne sackt sichtbar ab und klebt nicht sofort wieder am Anker.
            _ropeLength = Mathf.Max(0.15f, Vector3.Distance(transform.position, dl.LastAnchor) + 0.05f);
            _ropeVel = Vector3.zero;
        }

        /// <summary>
        /// Seil-Modus: Pendelphysik mit Seillänge als Restriktion; Seillängen-Eingabe
        /// zieht die Spinne zum Anker hin. Kontakt mit einer Oberfläche oder
        /// Hochklettern bis zum Anker beendet den Modus und nimmt Haftung auf.
        /// </summary>
        private void UpdateRope(float dt)
        {
            var dl = GetComponent<SpiderDragline>();
            if (dl == null)
            {
                IsOnRope = false;
                return;
            }

            float ropeIn = _externalRope ?? (input != null
                ? (input.Move.y > 0.3f ? -1f : input.Move.y < -0.3f ? 1f : 0f)
                : 0f);
            _ropeLength = Mathf.Max(0.05f, _ropeLength + ropeIn * 0.35f * dt);

            Vector3 anchor = dl.LastAnchor;
            Vector3 oldPos = transform.position;

            _ropeVel += Physics.gravity * dt;
            _ropeVel *= (1f - 0.8f * dt);

            Vector3 pos = oldPos + _ropeVel * dt;
            Vector3 d = pos - anchor;
            if (d.magnitude > _ropeLength)
            {
                Vector3 n = d.normalized;
                pos = anchor + n * _ropeLength;
                float outward = Vector3.Dot(_ropeVel, n);
                if (outward > 0f)
                {
                    _ropeVel -= n * outward;
                }
            }

            // Kontakt mit einer Oberfläche: landen und Haftung aufnehmen.
            Vector3 step = pos - oldPos;
            if (step.sqrMagnitude > 1e-8f && Physics.SphereCast(
                    oldPos + transform.up * bodyRadius, bodyRadius * 0.9f, step.normalized,
                    out RaycastHit hit, step.magnitude + 0.01f, sensor.mask, QueryTriggerInteraction.Ignore))
            {
                IsOnRope = false;
                _attached = true;
                transform.position = SettleOnTerrain(hit.point);
                transform.rotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                _lastSurfaceTime = Time.time;
                _vel = Vector3.zero;
                return;
            }

            // Hochgeklettert bis zum Anker (nur beim aktiven Hochklettern): Haftung aufnehmen.
            if (ropeIn < 0f && _ropeLength <= 0.06f && Vector3.Distance(pos, anchor) <= 0.07f)
            {
                IsOnRope = false;
                _attached = true;
                transform.position = anchor;
                transform.rotation = Quaternion.FromToRotation(transform.up, dl.LastAnchorNormal) * transform.rotation;
                _lastSurfaceTime = Time.time;
                _vel = Vector3.zero;
                return;
            }

            transform.position = pos;

            // Ausrichtung: Blick zum Anker, Kopf nach oben.
            Vector3 fwd = -(anchor - pos).normalized;
            Vector3 upv = Vector3.ProjectOnPlane(transform.up, fwd);
            if (upv.sqrMagnitude < 1e-6f)
            {
                upv = Vector3.forward;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(fwd, upv.normalized), turnSpeed * dt);
        }

        private void Awake()
        {
            _surface = GetComponent<SpiderSurfaceDetector>();
            _bodyCollider = GetComponent<Collider>();
            if (energy == null)
            {
                energy = GetComponent<SpiderEnergy>();
            }

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
            if (IsScripted)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 up = transform.up;

            // S7: Betaeubing (Wespe-Sting): input ignored, no movement while stunned.
            IsStunned = Time.time < _stunUntil;
            if (IsStunned)
            {
                _externalMoveInput = null;
                _externalMoveLocal = null;
                _externalSprint = null;
            }

            // a) Eingabe -> Richtung in der Ebene senkrecht zu up.
            bool local = _externalMoveLocal.HasValue;
            Vector2 moveInput;
            if (local)
            {
                moveInput = _externalMoveLocal.Value;
            }
            else if (_externalMoveInput.HasValue)
            {
                moveInput = _externalMoveInput.Value;
            }
            else
            {
                moveInput = input != null ? input.Move : Vector2.zero;
            }

            if (IsStunned)
            {
                moveInput = Vector2.zero;
            }

            Vector3 dir = local
                ? GetLocalDirection(moveInput, up)
                : GetCameraRelativeDirection(moveInput, up);

            Vector3 oldPos = transform.position;

            // Einsteigen: von der Oberfläche auf einen vorhandenen, vorauseilenden Faden (einmal pro Frame).
            TryBoardThread(dir);

            // Faden-Modus hat Vorrang: entlang des Fadens klettern, Beine greifen auf den Faden.
            if (CurrentThread != null)
            {
                UpdateThread(dt, dir);
                _lastVelocity = (transform.position - oldPos) / dt;
                return;
            }

            // Netz-Modus: im Radnetz laufen, Beine greifen die Fäden selbst.
            if (CurrentWeb != null)
            {
                UpdateWeb(dt, dir);
                _lastVelocity = (transform.position - oldPos) / dt;
                return;
            }

            // Seil-Modus (Dragline) hat Vorrang vor freiem Fallen.
            if (IsOnRope)
            {
                UpdateRope(dt);
                _lastVelocity = (transform.position - oldPos) / dt;
                return;
            }

            // L2: Sprung (Leertaste am Boden/Ast/Stamm; im Netz/Faden bleibt Leertaste "fallen lassen").
            TryTriggerJump(dir);

            if (_inJump)
            {
                UpdateJump(dt);
                _lastVelocity = (transform.position - oldPos) / dt;
            }
            else if (_attached)
            {
                UpdateAttached(dt, dir, up);
            }
            else
            {
                UpdateFalling(dt);
            }

            // d) XZ-Weltgrenzen.
            if (useWorldBounds)
            {
                Vector3 p = transform.position;
                p.x = Mathf.Clamp(p.x, worldBoundsMin.x, worldBoundsMax.x);
                p.z = Mathf.Clamp(p.z, worldBoundsMin.z, worldBoundsMax.z);
                transform.position = p;
            }

            // B3: Sicherheitsnetz gegen die Terrain-Unterseite. Ist die Koerpermittelpunkt
            // mehr als TERRAIN_FLOOR_TOLERANZ UNTER der Terrainhoehe, wird die Spinne sofort
            // auf die Terrainoberflaeche gesetzt und up wieder zu Welt-up abgleichen. Unter dem
            // Terrain der Terrain-Raycast (GroundBelow/Sense) trifft nichts, so die Sperren aus
            // B2 wirken nicht mehr und die Spinne laeuft kopfueber an der Unterseite (Alex:
            // Baumstemk im Boden steckt -> Kugelwurf kein Blocker -> Spinne unter Terrain).
            EnforceTerrainFloor();

            _lastVelocity = (transform.position - oldPos) / dt;
        }

        /// <summary>B2: Liegt innerhalb 1 m unter der Spinne Boden? (Decken-Klettern nur hoch ueber dem Boden.)</summary>
        private bool GroundBelow()
        {
            return Physics.Raycast(transform.position, Vector3.down, out RaycastHit gh, 1.0f, sensor.mask, QueryTriggerInteraction.Ignore)
                && gh.collider is TerrainCollider;
        }

        /// <summary>
        /// B3: Sicherheitsnetz. SampleHeight gibt die Terrainhoehe INSENSATE, so es wirkt auch
        /// UNTER dem Terrain (wo der Terrain-Raycast GroundBelow/Sense trifft nichts). Liegt der
        /// Koerpermittelpunkt mehr als TERRAIN_FLOOR_TOLERANZ (0.05 m) UNTER der Terrainoberflaeche,
        /// wird die Spinne per SettleOnTerrain sofort auf die Terrainoberflaeche gesetzt, up wieder
        /// zu Welt-up abgleichen und die Unterseite NEVER als haftable surface: _surfaceNormal=up,
        /// _attached=true so der naechste Frame ein normale Erdoberflaenhaftung neust. Ein Baumstemk
        /// im Boden steckt oder ein Luecke im TrunkCapsule kann nicht mehr schneiden den Spinne
        /// unter Terrain; entlanglaufen (SphereCast-Blocker) bleibt der primary path.
        /// </summary>
        private void EnforceTerrainFloor()
        {
            Terrain terr = Terrain.activeTerrain;
            if (terr == null)
            {
                return;
            }

            Vector3 p = transform.position;
            float terrainY = terr.SampleHeight(p) + terr.transform.position.y;
            const float tolerance = 0.05f;
            if (p.y >= terrainY - tolerance)
            {
                return;
            }

            // Unter der Terrain: auf die Oberflaeche setzen (SettleOnTerrain rases the center so
            // the collider bottom rests on the surface + clearance), up back to world-up.
            Vector3 settled = SettleOnTerrain(new Vector3(p.x, terrainY, p.z));
            transform.position = settled;
            // up back to world-up: project forward onto the horizontal plane; if that degenerates
            // (forward ~ vertical while climbing), fall back to the current right axis.
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-6f)
            {
                fwd = Vector3.ProjectOnPlane(transform.right, Vector3.up);
                fwd = fwd.sqrMagnitude < 1e-6f ? Vector3.forward : fwd;
            }
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            _surfaceNormal = Vector3.up;
            _attached = true;
            _lastSurfaceTime = Time.time;
            _vel = Vector3.zero;
        }

        /// <summary>B2: Nahe am Boden ist eine nach unten zeigende Hindernisflaeche (Unterseite von
        /// Stamm/Wurzel ueber dem Weg) KEINE Kletterflaeche – sonst dreht sich die Spinne kopfueber darunter.</summary>
        private bool IsOverheadHitOnGround(Vector3 hitNormal, Vector3 groundNormal)
        {
            return hitNormal.y < -0.1f && GroundBelow();
        }

        /// <summary>L2: Am Boden/Ast/Stamm (hafter, nie am Faden/Netz/Seil) die Leertaste-Absprung versuche.</summary>
        private void TryTriggerJump(Vector3 dir)
        {
            if (input != null && input.JumpPressed)
            {
                _jumpRequest = true;
            }

            if (!_jumpRequest)
            {
                return;
            }

            _jumpRequest = false;
            // Im Netz oder am Faden/Seil bleibt Leertaste "fallen lassen" (SpiderSilk/SpiderWebBuilder).
            if (!_attached || CurrentThread != null || CurrentWeb != null || IsOnRope)
            {
                return;
            }

            if (Time.time < _jumpCooldownUntil)
            {
                return;
            }

            // Absprung: entlang der Flaechennormale plus Blick-/Laufrichtung.
            Vector3 n = SafeNormalize(_surfaceNormal, transform.up);
            Vector3 fwd = dir.sqrMagnitude > 0.01f ? SafeNormalize(dir, transform.forward) : transform.forward;

            // Flight-Parameter: y-Scheitel der Parabola = jumpHeight (m), Horizontaler = jumpDistance (m).
            float g = Physics.gravity.magnitude * gravityScale;
            float vNormal = Mathf.Sqrt(2f * g * jumpHeight);          // Abstoss entlang der Flaechennormale
            float airTime = 2f * vNormal / g;                          // Rueck an Hoehe-Difference 0
            Vector3 horiz = SafeNormalize(Vector3.ProjectOnPlane(fwd, n), Vector3.ProjectOnPlane(fwd, transform.up));
            _jumpVel = horiz * (jumpDistance / airTime) + n * vNormal;
            _inJump = true;
            _jumpCooldownUntil = Time.time + jumpCooldown;
        }

        /// <summary>L2: Sprung-Parabola; Landeg per Sphärenwurf wieder an der Fläche haftet (UpdateFalling-Logik).</summary>
        private void UpdateJump(float dt)
        {
            Vector3 up = transform.up;

            // In der Air soft richts the spider back to world-up.
            Vector3 newUp = Vector3.RotateTowards(up, Vector3.up, 3f * dt, 0f).normalized;
            transform.rotation = Quaternion.FromToRotation(up, newUp) * transform.rotation;

            _jumpVel += Physics.gravity * gravityScale * dt;

            Vector3 step = _jumpVel * dt;
            if (step.sqrMagnitude > 1e-8f &&
                Physics.SphereCast(transform.position + newUp * bodyRadius, bodyRadius, step.normalized,
                    out RaycastHit hit, step.magnitude + 0.01f, sensor.mask, QueryTriggerInteraction.Ignore))
            {
                // Surface hit during flight: land and re-adhere.
                Vector3 n = hit.normal;
                transform.rotation = Quaternion.FromToRotation(transform.up, n) * transform.rotation;
                _vel = Vector3.ProjectOnPlane(_jumpVel, n) * 0.3f;
                _surfaceNormal = n;
                _attached = true;
                _lastSurfaceTime = Time.time;
                transform.position = SettleOnTerrain(hit.point);
                _inJump = false;
            }
            else
            {
                transform.position += step;
            }
        }

        /// <summary>b) Haftend: tangent laufen, up-Achse ausrichten, Kollision begrenzen, an die Fläche haften.</summary>
        private void UpdateAttached(float dt, Vector3 dir, Vector3 up)
        {
            _vel = Vector3.ProjectOnPlane(_vel, up);

            bool sprinting = IsSprintHeld() && dir.sqrMagnitude > 0.0001f;
            float surfaceFactor = _surface != null ? _surface.SpeedMultiplier : 1f;
            float targetSpeed = (sprinting ? sprintSpeed : walkSpeed) * surfaceFactor * EnergyTempoFactor() * Mathf.Clamp01(dir.magnitude);
            Vector3 targetVelocity = dir * targetSpeed;

            // Beschleunigen/Abbremsen mit getrennten Werten.
            bool decelerating = _vel.sqrMagnitude > targetVelocity.sqrMagnitude;
            float rate = decelerating ? deceleration : acceleration;
            _vel = Vector3.MoveTowards(_vel, targetVelocity, rate * dt);

            // In Bewegungsrichtung nach der Unterlage suchen (sonst Blickrichtung).
            Vector3 senseDir = _vel.sqrMagnitude > 0.000001f ? _vel.normalized : transform.forward;

            Vector3 point;
            Vector3 normal;
            bool hit = sensor.Sense(transform.position, up, senseDir, out point, out normal, out bool wallAhead);
            if (!hit)
            {
                // Kein Treffer: nach Toleranzzeit loslassen.
                if (Time.time - _lastSurfaceTime > detachDelay && Time.time - _wallClimbTime > 0.4f)
                {
                    _attached = false;
                    CatchOnDragline();
                }

                return;
            }

            // B2: Aufrecht (< 60 Grad geneigt) darf die Spinne nie auf eine nach unten zeigende Flaeche
            // (Unterseite von Stamm/Wurzel) umspringen: stehen bleiben statt kopfueber zu drehen.
            if (normal.y < -0.1f && GroundBelow())
            {
                _vel = Vector3.zero;
                _lastSurfaceTime = Time.time;
                return;
            }

            _lastSurfaceTime = Time.time;
            _surfaceNormal = normal;

            // Kletterlatch: after a steep Cast-hit the Sensor-Ground-normal must not fight the climb.
            bool wallLatched = Time.time - _wallClimbTime < 0.4f;
            bool groundFightsClimb = wallLatched && Vector3.Angle(normal, Vector3.up) < 45f;

            // up-Achse weich zur Oberflächen-Normale ausrichten (during latch: to the wall normal).
            Vector3 alignNormal = groundFightsClimb ? _lastWallNormal : normal;
            if (alignNormal.y < -0.2f && GroundBelow())
            {
                _vel = Vector3.zero;
                _wallClimbTime = float.NegativeInfinity;
                return;
            }
            Vector3 newUp = Vector3.RotateTowards(up, alignNormal, alignRate * dt, 0f).normalized;
            transform.rotation = Quaternion.FromToRotation(up, newUp) * transform.rotation;

            // Haftung startet am Sensortreffer, bei Steilflächen am Hindernistreffer.
            Vector3 adhesionPoint = groundFightsClimb ? _lastWallPoint : point;
            Vector3 adhesionNormal = groundFightsClimb ? _lastWallNormal : normal;

            // Schritt in Laufrichtung; Sphärenwurf begrenzt bei Hindernis.
            Vector3 oldUp = newUp;
            float step = _vel.magnitude * dt;
            if (step > 0.0001f)
            {
                bool castHitSteep = false;
                Vector3 steepNormal = Vector3.zero;
                Vector3 steepPoint = Vector3.zero;
                if (Physics.SphereCast(transform.position + newUp * bodyRadius, bodyRadius * 0.9f, _vel.normalized,
                        out RaycastHit castHit, step + 0.01f, sensor.mask, QueryTriggerInteraction.Ignore))
                {
                    if (castHit.distance > 0.0001f && Vector3.Angle(castHit.normal, Vector3.up) > 55f && !IsOverheadHitOnGround(castHit.normal, normal))
                    {
                        castHitSteep = true;
                        steepNormal = castHit.normal;
                        steepPoint = castHit.point;
                    }
                    else if (castHit.distance > 0.0001f && !IsOverheadHitOnGround(castHit.normal, normal))
                    {
                        // Flaches Hindernis (z. B. Oberseite eines kleinen Steins):
                        // nicht kappen, sondern entlang der getroffenen Fläche weiterlaufen.
                        Vector3 along = Vector3.ProjectOnPlane(_vel, castHit.normal);
                        if (along.sqrMagnitude > 1e-6f)
                            _vel = along.normalized * _vel.magnitude;
                        adhesionPoint = castHit.point;
                        adhesionNormal = castHit.normal;
                        // step bleibt die volle Schrittweite; Ausrichtung übernimmt der Sensor im nächsten Frame.
                    }
                }
                else if (Physics.SphereCast(transform.position - _vel.normalized * 0.25f + newUp * (bodyRadius + 0.3f), bodyRadius * 1.4f, _vel.normalized,
                        out RaycastHit castHit2, step + 0.6f, sensor.mask, QueryTriggerInteraction.Ignore))
                {
                    if (castHit2.distance > 0.0001f && Vector3.Angle(castHit2.normal, Vector3.up) > 55f && !IsOverheadHitOnGround(castHit2.normal, normal))
                {
                    // Leibhoch-Wurf: Steep Stoeckleib ueber dem Fusspfad (Planting-Absatz).
                    castHitSteep = true;
                    steepNormal = castHit2.normal;
                    steepPoint = castHit2.point;
                }
                }
                if (castHitSteep)
                {
                    // Steiles Hindernis = neue Kletterfläche: umdrehen und entlanglaufen.
                    _wallClimbTime = Time.time;
                    _lastWallPoint = steepPoint;
                    _lastWallNormal = steepNormal;
                    Vector3 wallUp = Vector3.RotateTowards(oldUp, steepNormal, alignRate * 3f * dt, 0f).normalized;
                    transform.rotation = Quaternion.FromToRotation(newUp, wallUp) * transform.rotation;
                    newUp = wallUp;

                    // Geschwindigkeit entlang der Hindernisfläche umlenken, Betrag behalten.
                    Vector3 along = Vector3.ProjectOnPlane(_vel, steepNormal);
                    Vector3 climbDir = along.magnitude < 0.3f * _vel.magnitude
                        ? Vector3.ProjectOnPlane(oldUp, steepNormal).normalized
                        : along.normalized;
                    _vel = climbDir * _vel.magnitude;

                    adhesionPoint = steepPoint;
                    adhesionNormal = steepNormal;
                }
            }

            Vector3 pos = transform.position + (step > 0.0001f ? _vel.normalized * step : Vector3.zero);

            // Haftung: weiches Ziehen auf die Fläche (d > 0: Spinne schwebt, d < 0: drin).
            float d = Vector3.Dot(pos - adhesionPoint, adhesionNormal);
            pos -= adhesionNormal * d * Mathf.Min(1f, adhesionRate * dt);
            transform.position = pos;

            // Weiche Drehung in Laufrichtung (keine Sofortdrehung).
            Vector3 movePlaneDir = Vector3.ProjectOnPlane(dir, newUp);
            if (dir.magnitude > rotationThreshold && movePlaneDir.sqrMagnitude > 0.000001f)
            {
                Quaternion target = Quaternion.LookRotation(movePlaneDir.normalized, newUp);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
            }
        }

        /// <summary>c) Nicht haftend: fallen, up zurück zu Welt-up, bei Sphären-Treffer wieder anhaften.</summary>
        private void UpdateFalling(float dt)
        {
            _vel += Physics.gravity * gravityScale * dt;

            // In der Luft richtet sich die Spinne langsam wieder zu Welt-up aus.
            Vector3 up = transform.up;
            Vector3 newUp = Vector3.RotateTowards(up, Vector3.up, 3f * dt, 0f).normalized;
            transform.rotation = Quaternion.FromToRotation(up, newUp) * transform.rotation;

            float step = _vel.magnitude * dt;
            if (step > 0.0001f &&
                Physics.SphereCast(transform.position + newUp * bodyRadius, bodyRadius, _vel.normalized,
                    out RaycastHit hit, step + 0.01f, sensor.mask, QueryTriggerInteraction.Ignore))
            {
                // Oberfläche im Fall getroffen: sofort anhaften und tangent abgleiten.
                Vector3 n = hit.normal;
                Vector3 curUp = transform.up;
                transform.rotation = Quaternion.FromToRotation(curUp, n) * transform.rotation;
                _vel = Vector3.ProjectOnPlane(_vel, n) * 0.3f;
                _surfaceNormal = n;
                _attached = true;
                _lastSurfaceTime = Time.time;
                transform.position = hit.point;
            }
            else
            {
                transform.position += _vel * dt;
            }
        }

        /// <summary>
        /// Faden-Modus: Die Spinne klettert entlang des angebundenen Fadens in Bewegungsrichtung.
        /// Am Ende des Fadens dreht sie sich um und haftet an der dortigen Fläche.
        /// </summary>
        private void UpdateThread(float dt, Vector3 dir)
        {
            SilkThread thread = CurrentThread;
            if (thread == null)
            {
                return;
            }

            Vector3 tan = thread.TangentAt(_threadT);
            float along = Vector3.Dot(dir, tan);
            float tempo = (IsSprintHeld() ? sprintSpeed : walkSpeed) * 0.7f;
            _threadT = Mathf.Clamp01(_threadT + along * tempo * dt / thread.Length);
            transform.position = thread.PointAt(_threadT);

            // Aufhängung: Bauch Richtung Welt-down (so gut wie möglich) senkrecht zur Faden-Tangente.
            Vector3 hangUp = Vector3.ProjectOnPlane(Vector3.down, tan);
            if (hangUp.sqrMagnitude < 1e-6f)
            {
                hangUp = Vector3.ProjectOnPlane(transform.up, tan);
            }
            if (hangUp.sqrMagnitude > 1e-6f)
            {
                hangUp.Normalize();
            }
            else
            {
                hangUp = Vector3.down;
            }

            // Laufrichtung entlang der Fadenachse: vorwärts = Zeichen von along, sonst Blickrichtung.
            float sign = along < -0.05f ? -1f : along > 0.05f ? 1f
                : (Vector3.Dot(transform.forward, tan) < 0f ? -1f : 1f);
            Quaternion target = Quaternion.LookRotation(tan * sign, hangUp);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);

            // Ende B: an anchorB angekommen – dort anhaften, Blick zur Fläche (−tangent).
            if (_threadT >= 0.999f && along > 0.1f)
            {
                Vector3 curUp = transform.up;
                DetachFromThread();
                transform.position = SettleOnTerrain(thread.anchorB);
                transform.rotation = Quaternion.FromToRotation(curUp, -tan.normalized) * transform.rotation;
                _vel = Vector3.zero;
                _attached = true;
                _lastSurfaceTime = Time.time;
                return;
            }

            // Ende A: an anchorA angekommen – dort anhaften, Blick zur Fläche (+tangent).
            if (_threadT <= 0.001f && along < -0.1f)
            {
                Vector3 curUp = transform.up;
                DetachFromThread();
                transform.position = SettleOnTerrain(thread.anchorA);
                transform.rotation = Quaternion.FromToRotation(curUp, tan.normalized) * transform.rotation;
                _vel = Vector3.zero;
                _attached = true;
                _lastSurfaceTime = Time.time;
            }
        }

        /// <summary>
        /// Netz-Modus: Die Spinne läuft in der Netzebene auf den Fäden; die Beine
        /// greifen die Fäden selbst (OrbWeb.TryClosestPoint in SpiderLeg). Am Netzrand
        /// (kein Faden mehr in Reichweite) bleibt sie stehen. Weltgrenzen gelten hier
        /// bewusst nicht (das Netz darf über/unter der XZ-Grenze hängen).
        /// </summary>
        private void UpdateWeb(float dt, Vector3 dir)
        {
            OrbWeb web = CurrentWeb;
            if (web == null || web.Layout == null)
            {
                // Netz verschwunden (z. B. zerstört): loslassen und fallen.
                LetGo();
                return;
            }

            // Netzebene von der Seite der Spinne aus orientieren.
            Vector3 n = web.PlaneNormal;
            if (Vector3.Dot(n, transform.up) < 0f)
            {
                n = -n;
            }

            dir = Vector3.ProjectOnPlane(dir, n);
            float tempo = (IsSprintHeld() ? sprintSpeed : walkSpeed) * webSpeedFactor;

            Vector3 hub = web.Layout.Hub;
            Vector3 candidate = transform.position + dir * tempo * dt;
            // Auf die Netzebene projizieren und den Körperabstand (webBodyOffset) setzen.
            candidate = candidate - n * Vector3.Dot(candidate - hub, n) + n * webBodyOffset;

            // Nur weiterbewegen, wenn in der Ebene noch ein Faden in Reichweite ist
            // (sonst Netzrand: stehen bleiben).
            if (web.TryClosestPoint(candidate - n * webBodyOffset, webGrip, out _))
            {
                transform.position = candidate;
            }

            // Drehung: Blick in Laufrichtung in der Netzebene; ohne Eingabe nur
            // die up-Achse an die Netzebene ausrichten.
            Quaternion target = dir.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(dir.normalized, n)
                : Quaternion.FromToRotation(transform.up, n) * transform.rotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }

        /// <summary>
        /// Kamerarelative Bewegungsrichtung in der Ebenen senkrecht zu up.
        /// Fällt ohne Kamera auf die eigene Achse der Spinne zurück.
        /// </summary>
        private Vector3 GetCameraRelativeDirection(Vector2 moveInput, Vector3 up)
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

            Vector3 camF = Vector3.ProjectOnPlane(cam.forward, up);
            if (camF.magnitude < 0.2f)
            {
                // Kamera schaut frontal auf die Fläche (z. B. Wand): Kameras-up als Vorwärts nehmen.
                camF = Vector3.ProjectOnPlane(cam.up, up);
            }

            camF = SafeNormalize(camF, transform.forward);
            Vector3 camR = SafeNormalize(Vector3.ProjectOnPlane(cam.right, up), transform.right);

            Vector3 direction = camF * moveInput.y + camR * moveInput.x;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }
            else if (moveInput.magnitude > 0.5f && direction.magnitude < 0.3f)
            {
                // Eingabe fast senkrecht zur Fläche: an der Fläche "nach oben" laufen.
                Vector3 surfaceUp = Vector3.ProjectOnPlane(Vector3.up, up);
                direction = surfaceUp.sqrMagnitude > 0.0001f
                    ? surfaceUp.normalized * moveInput.magnitude
                    : transform.forward * moveInput.magnitude;
            }

            return direction;
        }

        /// <summary>Bewegungsrichtung relativ zur eigenen Blickrichtung (x = rechts, y = vorwärts).</summary>
        private Vector3 GetLocalDirection(Vector2 moveInput, Vector3 up)
        {
            Vector3 forward = SafeNormalize(Vector3.ProjectOnPlane(transform.forward, up), transform.forward);
            Vector3 right = SafeNormalize(Vector3.ProjectOnPlane(transform.right, up), transform.right);

            Vector3 direction = forward * moveInput.y + right * moveInput.x;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return direction;
        }

        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            if (v.sqrMagnitude < 0.000001f)
            {
                return fallback;
            }

            return v.normalized;
        }
    }
}
