using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// J2a: Energie der Spinne (0..100) as reine Logik via SpiderEnergyModel. This thin
    /// MonoBehaviour only feeds Time.deltaTime and picks the activity level from
    /// SpiderMovement so that drain matches what the spider is doing. No HUD here
    /// (that is J2b). Feeding is credited by SpiderPredation.
    /// </summary>
    public class SpiderEnergy : MonoBehaviour
    {
        private SpiderEnergyModel _model;

        [Tooltip("Movement component that tells us what the spider is doing.")]
        [SerializeField] private SpiderMovement _movement;

        /// <summary>Current energy 0..100 (read by HUD and consequences).</summary>
        public float Energy { get; private set; }

        private void Awake()
        {
            _model = new SpiderEnergyModel();
            _model.Reset();
            _model.difficultyMultiplier = GameDifficulty.DrainMultiplier();
            if (_movement == null)
            {
                _movement = GetComponent<SpiderMovement>();
            }
            Energy = _model.Energy;
        }

        private void Update()
        {
            if (_model == null)
            {
                return;
            }

            SpiderEnergyModel.Activity activity = SpiderEnergyModel.Activity.Idle;
            if (_movement != null)
            {
                float speed = _movement.Speed;
                if (speed > 0.01f)
                {
                    // Climbing a rope/thread drains harder than plain walking.
                    activity = _movement.IsOnRope
                        ? SpiderEnergyModel.Activity.Climb
                        : SpiderEnergyModel.Activity.Walk;
                }
            }

            _model.Tick(activity, Time.deltaTime);
            Energy = _model.Energy;
        }

        /// <summary>Eat a fly: credit energy. Called by SpiderPredation.</summary>
        public void FeedFly()
        {
            if (_model == null)
            {
                return;
            }
            _model.FeedFly();
            Energy = _model.Energy;
        }

        /// <summary>Eat a moth: credit energy. Called by SpiderPredation.</summary>
        public void FeedMoth()
        {
            if (_model == null)
            {
                return;
            }
            _model.FeedMoth();
            Energy = _model.Energy;
        }

        /// <summary>Credit arbitrary energy (used by SpiderPredation via insect energyValue).</summary>
        public void FeedEnergy(float amount)
        {
            if (_model == null)
            {
                return;
            }
            _model.FeedEnergy(amount);
            Energy = _model.Energy;
        }

        /// <summary>S6: Spend energy for an action (repair). False when not enough energy.</summary>
        public bool SpendEnergy(float amount)
        {
            if (_model == null)
            {
                return false;
            }
            bool ok = _model.SpendEnergy(amount);
            Energy = _model.Energy;
            return ok;
        }

        /// <summary>T11: kind of the last damage that hit the spider (Hunger = none yet).</summary>
        public DeathCauseKind LastDamageKind = DeathCauseKind.Hunger;

        /// <summary>T11: Time.time of the last damage (-1 = never).</summary>
        public float LastDamageTime = -1f;

        /// <summary>S7: A wasp sting takes energy off the spider (delegates to the model).</summary>
        public void ApplySting(float amount)
        {
            if (_model == null)
            {
                return;
            }
            _model.ApplySting(amount);
            Energy = _model.Energy;
            MarkDamage(DeathCauseKind.Wespe);
        }

        /// <summary>T3: A frog tongue-strike takes energy off the spider (delegates to the model).</summary>
        public void ApplyStrike(float amount)
        {
            if (_model == null)
            {
                return;
            }
            _model.ApplyStrike(amount);
            Energy = _model.Energy;
            MarkDamage(DeathCauseKind.Frosch);
        }

        /// <summary>T11: a bird strike takes energy off the spider (delegates to the model).</summary>
        public void ApplyBirdStrike(float amount)
        {
            if (_model == null)
            {
                return;
            }
            _model.ApplyStrike(amount);
            Energy = _model.Energy;
            MarkDamage(DeathCauseKind.Vogel);
        }

        /// <summary>T11: a fall takes energy off the spider (delegates to the model).</summary>
        public void ApplyFallDamage(float amount)
        {
            if (_model == null)
            {
                return;
            }
            _model.ApplyStrike(amount);
            Energy = _model.Energy;
            MarkDamage(DeathCauseKind.Sturz);
        }

        /// <summary>T11: remember which source dealt the last damage and when (for the death cause).</summary>
        private void MarkDamage(DeathCauseKind kind)
        {
            LastDamageKind = kind;
            LastDamageTime = UnityEngine.Time.time;
        }

        /// <summary>Energy as fraction 0..1 (for HUD / consequences).</summary>
        public float Fraction()
        {
            return _model != null ? _model.Fraction() : 0f;
        }

        /// <summary>J2c: fresh partie — energy back to start (bests untouched).</summary>
        public void ResetEnergy()
        {
            if (_model == null)
            {
                return;
            }
            _model.Reset();
            Energy = _model.Energy;
            LastDamageKind = DeathCauseKind.Hunger;
            LastDamageTime = -1f;
        }

        /// <summary>The pure model (read by tests/consequences).</summary>
        public SpiderEnergyModel GetModel()
        {
            return _model;
        }
    }
}
