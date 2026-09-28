using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Koordiniert die acht Beine. Beine sind in Gruppen eingeteilt (Standard: alternierender Tetrapoden-Gang,
    /// L1/R2/L3/R4 gegen R1/L2/R3/L4). Eine Gruppe darf nur losschwingen, wenn keine andere Gruppe schwingt,
    /// dadurch stehen immer mindestens vier Beine am Boden.
    /// Schrittdauer, Schrittschwelle und Vorgriff hängen von der tatsächlichen Geschwindigkeit ab.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(SpiderMovement))]
    [DisallowMultipleComponent]
    public class SpiderGaitController : MonoBehaviour
    {
        [SerializeField] private GaitProfile profile = GaitProfile.Walk;

        private SpiderLeg[] _legs = new SpiderLeg[0];
        private int _groupCount;
        private Vector3 _lastPosition;
        private Vector3 _measuredVelocity;
        private int _lastGroup = -1;

        public GaitProfile Profile => profile;
        public IReadOnlyList<SpiderLeg> Legs => _legs;
        public int SteppingCount { get; private set; }
        public int MaxSteppingObserved { get; private set; }
        public float CurrentStepDuration { get; private set; }
        public float CurrentStepThreshold { get; private set; }

        public void SetProfile(GaitProfile newProfile)
        {
            profile = newProfile;
        }

        public void ResetStatistics()
        {
            MaxSteppingObserved = 0;
        }

        private void Awake()
        {
            _legs = GetComponentsInChildren<SpiderLeg>();
            _groupCount = 0;
            foreach (SpiderLeg leg in _legs)
            {
                _groupCount = Mathf.Max(_groupCount, leg.GaitGroup + 1);
            }

            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (_legs.Length == 0 || deltaTime <= 0f)
            {
                return;
            }

            Vector3 up = transform.up;

            // Tatsächliche Geschwindigkeit (inkl. Kollisionen), nicht nur die gewünschte.
            Vector3 frameVelocity = (transform.position - _lastPosition) / deltaTime;
            _lastPosition = transform.position;
            _measuredVelocity = Vector3.Lerp(_measuredVelocity, frameVelocity, 1f - Mathf.Exp(-20f * deltaTime));
            Vector3 velocity = Vector3.ProjectOnPlane(_measuredVelocity, up);
            float speed = velocity.magnitude;

            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, profile.referenceSpeed));
            float stepDuration = Mathf.Lerp(profile.slowStepDuration, profile.fastStepDuration, speed01);
            float threshold = Mathf.Max(profile.minStepDistance, speed * stepDuration * profile.strideFactor);
            Vector3 lead = velocity * (stepDuration * profile.leadFactor);
            CurrentStepDuration = stepDuration;
            CurrentStepThreshold = threshold;
            bool moving = speed > 0.05f;

            // Der Körper läuft während eines Schritts weiter: laufende Beine folgen der Ruheposition.
            foreach (SpiderLeg leg in _legs)
            {
                if (leg.IsStepping && leg.RestTarget != null)
                {
                    leg.UpdateStepTarget(leg.RestTarget.position + lead);
                }
            }

            int stepping = 0;
            foreach (SpiderLeg leg in _legs)
            {
                leg.TickStep(deltaTime);
                if (leg.IsStepping)
                {
                    stepping++;
                }
            }

            stepping += StartGroupSteps(lead, threshold, stepDuration, stepping, moving);

            // Beim Laufen regelt die feste Gruppenfolge alles; Notschritte nur im Stand.
            if (!moving)
            {
                stepping += StartEmergencySteps(lead, threshold, stepDuration, stepping);
            }

            SteppingCount = stepping;
            MaxSteppingObserved = Mathf.Max(MaxSteppingObserved, stepping);
        }

        private int StartGroupSteps(Vector3 lead, float threshold, float stepDuration, int alreadyStepping, bool moving)
        {
            // Bei Bewegung: Gruppen strikt abwechseln, immer komplett (sonst verpassen Beine das Fenster).
            if (moving)
            {
                if (alreadyStepping > 0)
                {
                    return 0;
                }

                int group = (_lastGroup + 1) % _groupCount;
                _lastGroup = group;

                int startedInGroup = 0;
                foreach (SpiderLeg leg in _legs)
                {
                    if (leg.GaitGroup != group || leg.RestTarget == null)
                    {
                        continue;
                    }

                    Vector3 desired = leg.RestTarget.position + lead;
                    // Welle von hinten nach vorne: hinteres Paar (3) ohne, vorderes (0) mit maximaler Verzögerung.
                    float delay = (3 - leg.PairIndex) * profile.waveDelay * stepDuration;
                    leg.BeginStep(desired, stepDuration - delay, profile.stepHeight, delay);
                    startedInGroup++;
                }

                return startedInGroup;
            }

            int bestGroup = -1;
            float bestUrgency = 0f;
            for (int group = 0; group < _groupCount; group++)
            {
                if (OtherGroupStepping(group))
                {
                    continue;
                }

                float urgency = 0f;
                foreach (SpiderLeg leg in _legs)
                {
                    if (leg.GaitGroup != group || leg.RestTarget == null)
                    {
                        continue;
                    }

                    Vector3 desired = leg.RestTarget.position + lead;
                    if (leg.NeedsStep(desired, threshold))
                    {
                        // Beine ohne Halt oder überdehnte Beine haben höchste Priorität.
                        float legUrgency = !leg.IsGrounded ? 10f : leg.IsOverextended ? 5f : leg.PlanarDistanceTo(desired) / threshold;
                        urgency = Mathf.Max(urgency, legUrgency);
                    }
                }

                if (urgency > bestUrgency)
                {
                    bestUrgency = urgency;
                    bestGroup = group;
                }
            }

            if (bestGroup < 0)
            {
                return 0;
            }

            _lastGroup = bestGroup;

            int started = 0;
            foreach (SpiderLeg leg in _legs)
            {
                if (alreadyStepping + started >= profile.maxSimultaneousSteps)
                {
                    break;
                }

                if (leg.GaitGroup != bestGroup || leg.RestTarget == null)
                {
                    continue;
                }

                Vector3 desired = leg.RestTarget.position + lead;
                if (leg.NeedsStep(desired, threshold))
                {
                    // Welle von hinten nach vorne: hinteres Paar (3) ohne, vorderes (0) mit maximaler Verzögerung.
                    float delay = (3 - leg.PairIndex) * profile.waveDelay * stepDuration;
                    leg.BeginStep(desired, stepDuration - delay, profile.stepHeight, delay);
                    started++;
                }
            }

            return started;
        }

        /// <summary>
        /// Überdehnte Beine dürfen die Gruppensperre umgehen, aber nie mehr als ein Bein über dem Limit.
        /// </summary>
        private int StartEmergencySteps(Vector3 lead, float threshold, float stepDuration, int alreadyStepping)
        {
            if (alreadyStepping > profile.maxSimultaneousSteps)
            {
                return 0;
            }

            foreach (SpiderLeg leg in _legs)
            {
                if (leg.IsStepping || !leg.IsOverextended || leg.RestTarget == null)
                {
                    continue;
                }

                leg.BeginStep(leg.RestTarget.position + lead, stepDuration * 0.8f, profile.stepHeight);
                return 1;
            }

            return 0;
        }

        private bool OtherGroupStepping(int group)
        {
            foreach (SpiderLeg leg in _legs)
            {
                if (leg.GaitGroup != group && leg.IsStepping)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
