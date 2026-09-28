using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Partikel beim Aufsetzen der Füße: Wellenringe auf Wasser, Staub auf Erde/Schlamm bei schnellem Laufen.
    /// Nutzt zwei vorkonfigurierte ParticleSystems (Welt-Simulation) und emittiert gezielt per EmitParams.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpiderFootEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem ripples;
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private float dustMinSpeed = 1.0f;

        private SpiderLeg[] _legs = new SpiderLeg[0];
        private SpiderMovement _movement;

        public int RipplesEmitted { get; private set; }
        public int DustEmitted { get; private set; }

        public void Configure(ParticleSystem rippleSystem, ParticleSystem dustSystem)
        {
            ripples = rippleSystem;
            dust = dustSystem;
        }

        private void Awake()
        {
            _legs = GetComponentsInChildren<SpiderLeg>();
            _movement = GetComponent<SpiderMovement>();
        }

        private void OnEnable()
        {
            foreach (SpiderLeg leg in _legs)
            {
                leg.Planted += OnPlanted;
            }
        }

        private void OnDisable()
        {
            foreach (SpiderLeg leg in _legs)
            {
                leg.Planted -= OnPlanted;
            }
        }

        private void OnPlanted(SpiderLeg leg)
        {
            if (!leg.IsGrounded)
            {
                return;
            }

            float speed = _movement != null ? _movement.Speed : 0f;
            if (leg.FootSurface == SurfaceType.Water && ripples != null)
            {
                ParticleSystem.EmitParams ripple = new ParticleSystem.EmitParams
                {
                    position = leg.FootPosition + Vector3.up * 0.002f,
                    velocity = Vector3.zero,
                    startSize = 0.05f + speed * 0.02f,
                    startLifetime = 0.9f,
                    applyShapeToPosition = false
                };
                ripples.Emit(ripple, 1);
                RipplesEmitted++;
            }
            else if ((leg.FootSurface == SurfaceType.Ground || leg.FootSurface == SurfaceType.Mud) && dust != null && speed >= dustMinSpeed)
            {
                ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
                {
                    position = leg.FootPosition + Vector3.up * 0.005f,
                    velocity = Vector3.up * 0.05f + Random.insideUnitSphere * 0.03f,
                    startSize = 0.025f,
                    startLifetime = 0.6f,
                    applyShapeToPosition = false
                };
                dust.Emit(puff, 2);
                DustEmitted++;
            }
        }
    }
}
