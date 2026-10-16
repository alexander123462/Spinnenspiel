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

        private SpiderPredation _predation;
        private Vector3 _baseLocalScale = Vector3.one;

        private void Awake()
        {
            _predation = GetComponent<SpiderPredation>();
            _baseLocalScale = transform.localScale;
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
    }
}
