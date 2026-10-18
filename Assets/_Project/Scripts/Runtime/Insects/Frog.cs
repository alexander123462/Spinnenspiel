using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// T3: Die Frosch — EIN Bedaenger, KEINE Beute. It lurks on the ground near water and
    /// lashes its tongue when the spider sits low to the ground (heightAboveGround <= 0.3 m)
    /// AND within reach (0.8 m): first a 0.6 s telegraph, then the strike lands (energy -25),
    /// then a 5 s pause. Higher places (branches, trunks, web) are safe. Deterministic rules
    /// live in <see cref="FrogModel"/>.
    /// </summary>
    public class Frog : MonoBehaviour
    {
        [Tooltip("How far above the spider to start the ground ray so it never hits itself (m).")]
        [SerializeField] private float rayHeadOffset = 0.3f;

        [Tooltip("How deep to probe for the ground below the spider (m).")]
        [SerializeField] private float rayMaxDepth = 3f;

        private FrogModel _model;

        private Transform _spiderTransform;
        private SpiderMovement _spiderMovement;
        private SpiderEnergy _spiderEnergy;
        private bool _spiderResolved;

        private bool _strikeApplied;

        /// <summary>The pure model (read by tests).</summary>
        public FrogModel GetModel()
        {
            return _model;
        }

        private void Awake()
        {
            _model = new FrogModel();
        }

        private void Update()
        {
            UpdateSpiderRef();

            float dist = _spiderTransform != null
                ? HorizontalDistance(transform.position, _spiderTransform.position)
                : float.PositiveInfinity;
            float heightAboveGround = MeasureSpiderHeightAboveGround();

            bool wasStruck = _model.Struck;
            _model.Tick(dist, heightAboveGround, Time.deltaTime);

            if (!wasStruck && _model.Struck && !_strikeApplied)
            {
                ApplyStrike();
            }
        }

        /// <summary>Frog-cache: typed-Lookup ONE Mal resolved (no name-lookup).</summary>
        private void UpdateSpiderRef()
        {
            if (_spiderResolved)
            {
                return;
            }

            SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
            if (spider != null)
            {
                _spiderTransform = spider.transform;
                _spiderMovement = spider;
                _spiderEnergy = spider.GetComponent<SpiderEnergy>();
                _spiderResolved = true;
            }
        }

        /// <summary>Horizontal (XZ) distance from the frog to a point (m).</summary>
        private float HorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Height of the spider above the ground below it (m). Large when no ground found.</summary>
        private float MeasureSpiderHeightAboveGround()
        {
            if (_spiderTransform == null)
            {
                return float.PositiveInfinity;
            }

            Vector3 origin = _spiderTransform.position + Vector3.up * rayHeadOffset;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, rayMaxDepth,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return _spiderTransform.position.y - hit.point.y;
            }

            return float.PositiveInfinity;
        }

        /// <summary>Strike landed: energy -25 on the spider (exact once).</summary>
        private void ApplyStrike()
        {
            _strikeApplied = true;
            if (_spiderEnergy != null)
            {
                _spiderEnergy.ApplyStrike(FrogModel.energyCost);
            }
        }
    }
}
