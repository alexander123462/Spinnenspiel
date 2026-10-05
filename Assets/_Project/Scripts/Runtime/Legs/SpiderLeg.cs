using System;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Ein einzelnes Spinnenbein: Bodenerkennung, Fußplatzierung, Schrittanimation und IK.
    ///
    /// Das GameObject selbst ist die Hüfte (Leg Root) und hängt am Körper.
    /// Die bevorzugte Ruheposition (restTarget) hängt am Spinnen-Root, damit sie nicht mit der
    /// Körperneigung mitkippt. Der Fuß bleibt in Weltkoordinaten stehen, bis der
    /// SpiderGaitController einen Schritt auslöst; dann läuft er über einen Bogen zum neuen Halt.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class SpiderLeg : MonoBehaviour
    {
        [Header("Aufbau")]
        [SerializeField] private Transform restTarget;
        [SerializeField] private Transform bodyCenter;
        [SerializeField] private int gaitGroup;
        [SerializeField] private int pairIndex;
        [SerializeField] private float upperLength = 0.085f;
        [SerializeField] private float lowerLength = 0.095f;
        [SerializeField] private float tarsusLength = 0.028f;
        [SerializeField] private float segmentThickness = 0.015f;

        [Header("Darstellung")]
        [SerializeField] private Transform femurVisual;
        [SerializeField] private Transform tibiaVisual;
        [SerializeField] private Transform tarsusVisual;

        [Header("Boden")]
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float probeHeight = 0.25f;
        [SerializeField] private float probeDepth = 0.35f;
        [SerializeField] private float maxWalkableAngle = 65f;

        private Vector3 _stepStart;
        private Vector3 _stepEnd;
        private Vector3 _stepEndNormal;
        private SurfaceType _stepEndSurface;
        private bool _stepEndGrounded;
        private float _stepProgress;
        private float _stepDuration;
        private float _stepArc;
        private float _stepDelay;
        private bool _initialized;
        private Vector3? _overrideFoot;
        private bool _pendingSnap;

        public int GaitGroup => gaitGroup;
        public int PairIndex => pairIndex;
        public bool IsStepping { get; private set; }
        public bool IsGrounded { get; private set; }
        public Vector3 FootPosition { get; private set; }
        public Vector3 FootNormal { get; private set; } = Vector3.up;
        public SurfaceType FootSurface { get; private set; } = SurfaceType.Ground;
        public int StepCount { get; private set; }
        public Transform RestTarget => restTarget;
        public Vector3 HipPosition => transform.position;

        private SpiderMovement _movement;
        public float MaxReach => (upperLength + lowerLength) * 0.98f + tarsusLength;
        private Vector3 Up => restTarget != null ? restTarget.up : Vector3.up;

        /// <summary>Wird ausgelöst, wenn der Fuß nach einem Schritt aufsetzt (Sound, Partikel, Gameplay).</summary>
        public event Action<SpiderLeg> Planted;

        public void Configure(Transform rest, Transform center, int group, float upper, float lower, float tarsus,
            float thickness, Transform femur, Transform tibia, Transform tarsusVis, int pair = 0)
        {
            restTarget = rest;
            bodyCenter = center;
            gaitGroup = group;
            pairIndex = pair;
            upperLength = upper;
            lowerLength = lower;
            tarsusLength = tarsus;
            segmentThickness = thickness;
            femurVisual = femur;
            tibiaVisual = tibia;
            tarsusVisual = tarsusVis;
        }

        private void Start()
        {
            // Pflanzen sind für die Beine kein festes Hindernis (eigene Layer-8-Ebene).
            groundMask &= ~GameLayers.PlantMask;
            SnapToGround();
        }

        /// <summary>Setzt den Fuß sofort auf den Boden unter der Ruheposition (Spawn, Teleport).</summary>
        public void SnapToGround()
        {
            if (restTarget == null)
            {
                return;
            }

            IsGrounded = TryFindFoothold(restTarget.position, out Vector3 point, out Vector3 normal, out SurfaceType surface);
            FootPosition = point;
            FootNormal = normal;
            FootSurface = surface;
            IsStepping = false;
            _initialized = true;
        }

        /// <summary>
        /// Zwingt den Fuß weich zu einer Welt-Position (z. B. Spinnwarzen beim Seidenziehen):
        /// LateUpdate lerpt mit 1 − exp(−20·dt) dorthin, das Bein greift nicht (
        /// IsGrounded = false, kein Schritt). null: Fuß greift ab dem nächsten Frame
        /// wieder normal auf den Untergrund.
        /// </summary>
        public void SetOverrideFoot(Vector3? worldPos)
        {
            _overrideFoot = worldPos;
            if (worldPos == null)
            {
                _pendingSnap = true;
            }
        }

        /// <summary>Stellt Fuß und Bein-Visuals sofort in Ruhepose (auch außerhalb des Play-Modus, z. B. nach dem Szenenaufbau).</summary>
        public void PoseAtRest()
        {
            SnapToGround();
            SolveAndPlaceVisuals();
        }

        /// <summary>
        /// Sucht einen gültigen Halt nahe der gewünschten Position. Probiert bei ungeeignetem Untergrund
        /// (zu steil, nicht erreichbar, durch ein Hindernis verdeckt) Punkte näher an der Ruheposition.
        /// Als letzten Versuch tastet die Bogen-Suche von der Hüfte aus im Bogen von außen über
        /// unten nach innen, um dünne, runde Stämme an der Kante zu umgreifen.
        /// </summary>
        public bool TryFindFoothold(Vector3 desired, out Vector3 point, out Vector3 normal, out SurfaceType surface)
        {
            // Faden-Modus: das Bein greift auf den Seidenfaden, an dem die Spinne hängt.
            if (_movement == null)
            {
                _movement = GetComponentInParent<SpiderMovement>();
            }
            if (_movement != null && _movement.CurrentThread != null)
            {
                SilkThread thread = _movement.CurrentThread;
                Vector3 onThread = thread.PointAt(thread.ClosestT(desired));
                if ((onThread - HipPosition).magnitude > MaxReach)
                {
                    onThread = thread.PointAt(thread.ClosestT(HipPosition));
                }
                point = onThread;
                normal = Up;
                surface = SurfaceType.Wood;
                return true;
            }

            // Netz-Modus: das Bein greift auf die Fäden eines Radnetzes (Tarsenklauen am Faden).
            if (OrbWeb.All.Count > 0)
            {
                float bestSq = float.MaxValue;
                Vector3 bestPoint = desired;
                OrbWeb bestWeb = null;
                foreach (OrbWeb web in OrbWeb.All)
                {
                    if (web.TryClosestPoint(desired, 0.09f, out Vector3 pt)
                        && (pt - HipPosition).magnitude <= MaxReach)
                    {
                        float sq = (pt - desired).sqrMagnitude;
                        if (sq < bestSq)
                        {
                            bestSq = sq;
                            bestPoint = pt;
                            bestWeb = web;
                        }
                    }
                }
                if (bestWeb != null)
                {
                    point = bestPoint;
                    Vector3 n = bestWeb.PlaneNormal;
                    // Normale zeigt zur Spinne: Dot(normal, Hüfte − Fadenpunkt) > 0.
                    if (Vector3.Dot(n, HipPosition - point) < 0f)
                        n = -n;
                    normal = n;
                    surface = SurfaceType.Wood;
                    return true;
                }
            }

            Vector3 up = Up;
            Vector3 rest = restTarget != null ? restTarget.position : desired;
            for (int i = 0; i < 3; i++)
            {
                Vector3 candidate = Vector3.Lerp(desired, rest, i * 0.4f);
                if (Probe(candidate, up, out point, out normal, out surface)
                    && (point - HipPosition).magnitude <= MaxReach
                    && !IsBlockedFromHip(point, normal))
                {
                    return true;
                }
            }

            // Bogen-Suche: wie eine echte Spinne tastet das Bein vom Ansatz (Hüfte) im Bogen
            // von außen über unten nach innen, bis es eine Fläche findet, die in Richtung
            // Bein zeigt. So umgreifen die Beine dünne, runde Stämme an der Kante.
            Vector3 hip = HipPosition;
            Vector3 outDir = Vector3.ProjectOnPlane(desired - hip, up);
            if (outDir.sqrMagnitude < 1e-6f)
            {
                outDir = Vector3.ProjectOnPlane(rest - hip, up);
            }
            outDir = outDir.normalized;
            if (outDir.sqrMagnitude > 1e-6f)
            {
                // Achse so wählen, dass a=90° „nach unten“ (−up) ergibt: Rotation um 90° um
                // die Achse u liefert u × v. Mit u = up × outDir folgt
                // (up × outDir) × outDir = −up (outDir ⊥ up, beide normiert).
                Vector3 axis = Vector3.Cross(up, outDir).normalized;
                for (int k = 0; k < 10; k++)
                {
                    Vector3 dir = Quaternion.AngleAxis(k * 18f, axis) * outDir;
                    // Strahl startet an der Hüfte, daher ist der Weg frei und der Treffer
                    // automatisch innerhalb von MaxReach.
                    if (Physics.Raycast(hip, dir, out RaycastHit hit, MaxReach, groundMask, QueryTriggerInteraction.Ignore)
                        && Vector3.Angle(hit.normal, -dir) < 80f)
                    {
                        point = hit.point;
                        normal = hit.normal;
                        surface = SurfaceQuery.FromHit(hit);
                        return true;
                    }
                }
            }

            point = rest;
            normal = up;
            surface = SurfaceType.Ground;
            return false;
        }

        public float PlanarDistanceTo(Vector3 desired)
        {
            return Vector3.ProjectOnPlane(FootPosition - desired, Up).magnitude;
        }

        public bool NeedsStep(Vector3 desired, float threshold)
        {
            if (_overrideFoot != null || !_initialized || IsStepping)
            {
                return false;
            }

            if (!IsGrounded)
            {
                return true;
            }

            return PlanarDistanceTo(desired) > threshold || IsOverextended;
        }

        public bool IsOverextended => (FootPosition - HipPosition).magnitude > MaxReach;

        /// <summary>Startet einen Schritt zur besten Fußposition nahe <paramref name="desired"/>.</summary>
        public void BeginStep(Vector3 desired, float duration, float baseHeight, float delay = 0f)
        {
            _stepEndGrounded = TryFindFoothold(desired, out _stepEnd, out _stepEndNormal, out _stepEndSurface);
            _stepStart = FootPosition;
            _stepDuration = Mathf.Max(0.02f, duration);
            _stepProgress = 0f;
            _stepDelay = Mathf.Max(0f, delay);

            // Bogen über Erhebungen: mindestens so hoch wie der Höhenunterschied zum Ziel.
            float rise = Vector3.Dot(_stepEnd - _stepStart, Up);
            _stepArc = baseHeight + Mathf.Max(0f, rise);
            IsStepping = true;
        }

        /// <summary>
        /// Stellt das Ziel eines laufenden Schritts nach, weil der Körper weiterläuft. Wirkt nur,
        /// solange der Schritt zu weniger als 85 % fortgeschritten ist; Startpunkt und Bogen bleiben.
        /// </summary>
        public void UpdateStepTarget(Vector3 desired)
        {
            if (!IsStepping || _stepProgress >= 0.85f)
            {
                return;
            }

            _stepEndGrounded = TryFindFoothold(desired, out _stepEnd, out _stepEndNormal, out _stepEndSurface);
        }

        /// <summary>Wird vom Gangcontroller einmal pro Frame aufgerufen.</summary>
        public void TickStep(float deltaTime)
        {
            if (!IsStepping)
            {
                return;
            }

            // Welle: Beinpaare hinter der Hüfte setzen später ab – Fuß bleibt stehen.
            if (_stepDelay > 0f)
            {
                _stepDelay -= deltaTime;
                return;
            }

            _stepProgress += deltaTime / _stepDuration;
            float t = Mathf.Clamp01(_stepProgress);

            // Horizontal: weiches Anfahren/Abbremsen. Vertikal: Anheben früh im Schritt (Spinnen heben zuerst an).
            float horizontal = t * t * (3f - 2f * t);
            float lift = Mathf.Sin(Mathf.Pow(t, 0.75f) * Mathf.PI);
            FootPosition = Vector3.Lerp(_stepStart, _stepEnd, horizontal) + Up * (lift * _stepArc);
            FootNormal = Vector3.Slerp(FootNormal, _stepEndNormal, t);

            if (_stepProgress >= 1f)
            {
                FootPosition = _stepEnd;
                FootNormal = _stepEndNormal;
                FootSurface = _stepEndSurface;
                IsGrounded = _stepEndGrounded;
                IsStepping = false;
                StepCount++;
                Planted?.Invoke(this);
            }
        }

        private void LateUpdate()
        {
            if (!_initialized || restTarget == null)
            {
                return;
            }

            // Nach Teleport o. Ä. nicht quer durch die Welt greifen.
            float resetDistance = MaxReach * 3f;
            if ((FootPosition - HipPosition).sqrMagnitude > resetDistance * resetDistance)
            {
                SnapToGround();
            }

            if (_overrideFoot != null)
            {
                // Fuß folgt weich der Vorgabe (z. B. Spinnwarzen) und greift nicht.
                float k = 1f - Mathf.Exp(-20f * Time.deltaTime);
                FootPosition = Vector3.Lerp(FootPosition, _overrideFoot.Value, k);
                IsGrounded = false;
            }
            else if (_pendingSnap)
            {
                _pendingSnap = false;
                SnapToGround();
            }

            SolveAndPlaceVisuals();
        }

        private void SolveAndPlaceVisuals()
        {
            Vector3 up = Up;
            Vector3 hip = HipPosition;
            Vector3 center = bodyCenter != null ? bodyCenter.position : transform.parent != null ? transform.parent.position : hip;
            Vector3 outward = Vector3.ProjectOnPlane(hip - center, up);
            outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : transform.right;

            // Horizontale Richtung Hüfte→Fuß (Bein-Ebene).
            Vector3 legDir = Vector3.ProjectOnPlane(FootPosition - hip, up);
            legDir = legDir.sqrMagnitude > 1e-8f ? legDir.normalized : outward;

            // Tarsus steht schräg: vom Fuß aus nach oben und zur Hüfte hin, in der Ebene des Beins.
            Vector3 tarsusDirection = (FootNormal * 0.8f - legDir * 0.6f).normalized;
            Vector3 ankleTarget = FootPosition + tarsusDirection * tarsusLength;

            Vector3 pole = up + legDir * 0.2f;
            Vector3 knee = LegIK.SolveTwoBone(hip, ankleTarget, pole, upperLength, lowerLength, out Vector3 ankle);
            Vector3 foot = ankle - tarsusDirection * tarsusLength;

            PlaceSegment(femurVisual, hip, knee);
            PlaceSegment(tibiaVisual, knee, ankle);
            PlaceSegment(tarsusVisual, ankle, foot);
        }

        private void PlaceSegment(Transform segment, Vector3 from, Vector3 to)
        {
            if (segment == null)
            {
                return;
            }

            Vector3 delta = to - from;
            float length = delta.magnitude;
            segment.position = (from + to) * 0.5f;
            if (length > 1e-5f)
            {
                segment.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            }

            // Unity-Capsule/Cylinder: Höhe 2, Durchmesser 1.
            segment.localScale = new Vector3(segmentThickness, length * 0.5f, segmentThickness);
        }

        private bool Probe(Vector3 candidate, Vector3 up, out Vector3 point, out Vector3 normal, out SurfaceType surface)
        {
            Vector3 origin = candidate + up * probeHeight;
            if (Physics.Raycast(origin, -up, out RaycastHit hit, probeHeight + probeDepth, groundMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(hit.normal, up) <= maxWalkableAngle)
            {
                point = hit.point;
                normal = hit.normal;
                surface = SurfaceQuery.FromHit(hit);
                return true;
            }

            point = candidate;
            normal = up;
            surface = SurfaceType.Ground;
            return false;
        }

        /// <summary>True, wenn zwischen Hüfte und Fußpunkt ein Hindernis liegt (Fuß würde durch Stein/Stamm greifen).</summary>
        private bool IsBlockedFromHip(Vector3 point, Vector3 normal)
        {
            Vector3 to = point + normal * 0.01f;
            return Physics.Linecast(HipPosition, to, groundMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmosSelected()
        {
            if (restTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(restTarget.position, 0.01f);
            }

            Gizmos.color = IsStepping ? Color.yellow : Color.green;
            Gizmos.DrawSphere(FootPosition, 0.008f);
        }
    }
}
