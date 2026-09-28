using System;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Ermittelt, auf welcher Oberfläche die Spinne gerade steht (Mehrheit der aufgesetzten Füße,
    /// ersatzweise Raycast unter dem Körper). Liefert Wasser-Erkennung und einen Geschwindigkeitsfaktor
    /// je Oberfläche – Grundlage für Bewegung, Sound und späteres Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpiderSurfaceDetector : MonoBehaviour
    {
        [Serializable]
        public struct SurfaceSpeed
        {
            public SurfaceType surface;
            public float multiplier;
        }

        [SerializeField] private float probeHeight = 0.2f;
        [SerializeField] private float probeDepth = 0.4f;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float speedSharpness = 6f;
        [SerializeField] private SurfaceSpeed[] speeds =
        {
            new SurfaceSpeed { surface = SurfaceType.Ground, multiplier = 1f },
            new SurfaceSpeed { surface = SurfaceType.Grass, multiplier = 0.92f },
            new SurfaceSpeed { surface = SurfaceType.Rock, multiplier = 1f },
            new SurfaceSpeed { surface = SurfaceType.Wood, multiplier = 0.95f },
            new SurfaceSpeed { surface = SurfaceType.Water, multiplier = 0.6f },
            new SurfaceSpeed { surface = SurfaceType.Mud, multiplier = 0.75f }
        };

        private SpiderLeg[] _legs = new SpiderLeg[0];
        private readonly int[] _votes = new int[Enum.GetValues(typeof(SurfaceType)).Length];

        public SurfaceType CurrentSurface { get; private set; } = SurfaceType.Ground;
        public bool IsOnWater => CurrentSurface == SurfaceType.Water;
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>Wird ausgelöst, wenn sich die Oberfläche unter der Spinne ändert.</summary>
        public event Action<SurfaceType> SurfaceChanged;

        private void Awake()
        {
            _legs = GetComponentsInChildren<SpiderLeg>();
        }

        private void Update()
        {
            SurfaceType detected = DetectFromLegs(out bool anyGrounded);
            if (!anyGrounded)
            {
                detected = DetectFromBody();
            }

            if (detected != CurrentSurface)
            {
                CurrentSurface = detected;
                SurfaceChanged?.Invoke(detected);
            }

            float target = MultiplierFor(CurrentSurface);
            SpeedMultiplier = Mathf.Lerp(SpeedMultiplier, target, 1f - Mathf.Exp(-speedSharpness * Time.deltaTime));
        }

        private SurfaceType DetectFromLegs(out bool anyGrounded)
        {
            Array.Clear(_votes, 0, _votes.Length);
            anyGrounded = false;
            foreach (SpiderLeg leg in _legs)
            {
                if (leg.IsGrounded && !leg.IsStepping)
                {
                    _votes[(int)leg.FootSurface]++;
                    anyGrounded = true;
                }
            }

            int best = 0;
            for (int i = 1; i < _votes.Length; i++)
            {
                if (_votes[i] > _votes[best])
                {
                    best = i;
                }
            }

            return (SurfaceType)best;
        }

        private SurfaceType DetectFromBody()
        {
            Vector3 origin = transform.position + transform.up * probeHeight;
            RaycastHit hit;
            if (Physics.Raycast(origin, -transform.up, out hit, probeHeight + probeDepth, groundMask, QueryTriggerInteraction.Ignore))
            {
                return SurfaceQuery.FromHit(hit);
            }

            return CurrentSurface;
        }

        public float MultiplierFor(SurfaceType surface)
        {
            foreach (SurfaceSpeed entry in speeds)
            {
                if (entry.surface == surface)
                {
                    return entry.multiplier;
                }
            }

            return 1f;
        }
    }
}
