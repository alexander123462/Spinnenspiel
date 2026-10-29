using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// K5 Wachstum durch Fressen: Nach je 10 gefressenen Insekten grows the spider 8 % larger,
    /// max. 150 % of the start size. All scaling happens here (single source of truth):
    /// the spider-Root scales, the leg lengths follow via SpiderLeg.SetGrowth and the camera
    /// reads GrowthScale directly.
    /// </summary>
    public class SpiderGrowth : MonoBehaviour
    {
        [Tooltip("Anzahl needed gefressenen insects for one growth step.")]
        [SerializeField] private int insectsPerStep = 10;

        [Tooltip("Percent growth per step (0.08 = 8 %).")]
        [SerializeField] private float growthPerStep = 0.08f;

        [Tooltip("Max. scale relative to the start size.")]
        [SerializeField] private float maxScale = 1.5f;

        public int EatenCount;

        public float GrowthScale = 1f;

        /// <summary>S5 Tagesbilanz: current growth step (one per insectsPerStep eaten prey).</summary>
        public int GrowthStage { get => insectsPerStep > 0 ? EatenCount / insectsPerStep : 0; }

        private SpiderPredation _predation;
        private Vector3 _baseLocalScale = Vector3.one;

        [Tooltip("Movement component that gets blocked during a molt (single switch: SetStun).")]
        [SerializeField] private SpiderMovement _movement;

        /// <summary>T4 Haeutungs-Model: after a stage change the spider molts for 5 s.</summary>
        private MoltModel _molt;

        private void Awake()
        {
            _predation = GetComponent<SpiderPredation>();
            _baseLocalScale = transform.localScale;
            _molt = new MoltModel();
            if (_movement == null)
            {
                _movement = GetComponent<SpiderMovement>();
            }
        }

        private void OnEnable()
        {
            // GetComponent here, not in Awake: OnEnable can run before Awake at instantiation.
            _predation = transform.GetComponent<SpiderPredation>();
            if (_predation != null)
            {
                _predation.PreyEaten += OnInsectEaten;
            }
        }

        private void OnDisable()
        {
            if (_predation == null)
            {
                _predation = transform.GetComponent<SpiderPredation>();
            }
            if (_predation != null)
            {
                _predation.PreyEaten -= OnInsectEaten;
            }
        }

        /// <summary>One insect eaten (called on the PreyEaten-Event, Tests can call it too).</summary>
        public void OnInsectEaten(FlyingInsect prey)
        {
            EatenCount++;
            int steps = EatenCount / insectsPerStep;
            float target = Mathf.Min(1f + growthPerStep * steps, maxScale);
            if (Mathf.Abs(target - GrowthScale) > 0.0001f)
            {
                ApplyGrowth(target);
                // T4: a new growth stage triggers a molt (movement blocked, damage x2).
                BeginMolt();
            }
        }

        /// <summary>T4: the molt blocks movement for 5 s and doubles hazard damage.</summary>
        private void BeginMolt()
        {
            _molt.Begin();
            // Single switch for movement: the existing stun blocks all input-driven motion.
            // +0.25 s margin so the stun can never expire while a molt tick is still pending.
            if (_movement != null)
            {
                _movement.SetStun(MoltModel.moltDuration + 0.25f);
            }
            // Hazard damage x2 while molting (read by SpiderEnergyModel).
            SpiderEnergy energy = GetComponent<SpiderEnergy>();
            if (energy != null)
            {
                energy.GetModel().hazardDamageMultiplier = MoltModel.damageFactorWhileMolting;
            }
        }

        private void Update()
        {
            if (!_molt.IsMolting)
            {
                return;
            }
            if (_molt.Tick(Time.deltaTime))
            {
                return; // still molting: no movement, doubled hazard damage
            }
            // Molt over: hazard damage back to normal.
            MoltsCompleted++;
            SpiderEnergy energy = GetComponent<SpiderEnergy>();
            if (energy != null)
            {
                energy.GetModel().hazardDamageMultiplier = 1f;
            }
        }

        /// <summary>T4: true while the spider is molting (movement blocked, damage x2).</summary>
        public bool IsMolting => _molt != null && _molt.IsMolting;

        /// <summary>T4: hazard damage multiplier right now (2 while molting, else 1).</summary>
        public float DamageFactor()
        {
            return _molt != null ? _molt.DamageFactor() : 1f;
        }

        /// <summary>T4: number of completed molts (for tests/diagnose).</summary>
        public int MoltsCompleted { get; private set; }

        /// <summary>T9: remaining seconds of a running molt (0 when not molting), for the savegame.</summary>
        public float MoltRemaining => _molt != null ? _molt.RemainingSeconds : 0f;

        /// <summary>
        /// T9: restore a saved growth state — eaten count applied WITHOUT a new molt, plus the
        /// pending molt window (movement re-blocked for the remaining seconds).
        /// </summary>
        public void Restore(int savedEatenCount, float moltRemaining)
        {
            EatenCount = Mathf.Max(0, savedEatenCount);
            int steps = insectsPerStep > 0 ? EatenCount / insectsPerStep : 0;
            ApplyGrowth(Mathf.Min(1f + growthPerStep * steps, maxScale));
            if (_molt == null)
            {
                _molt = new MoltModel();
            }
            _molt.Restore(moltRemaining);
            if (_molt.IsMolting && _movement != null)
            {
                _movement.SetStun(_molt.RemainingSeconds + 0.25f);
            }
        }

        private void ApplyGrowth(float scale)
        {
            GrowthScale = scale;
            transform.localScale = _baseLocalScale * scale;
            foreach (SpiderLeg leg in GetComponentsInChildren<SpiderLeg>())
            {
                leg.SetGrowth(scale);
            }
        }

        /// <summary>T4 Haeutung: show "Haeutung" at the screen edge while the molt runs.</summary>
        private void OnGUI()
        {
            if (!_molt.IsMolting)
            {
                return;
            }
            GUI.color = new Color(1f, 0.85f, 0.4f, 0.9f);
            GUI.Label(new Rect(Screen.width - 230f, Screen.height * 0.5f, 200f, 24f), "Haeutung");
            GUI.color = Color.white;
        }
    }
}
