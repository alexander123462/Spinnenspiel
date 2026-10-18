using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S7: Die Wespe — EIN BEDAENGER, KEINE Beute. Tagdevant searches around its home;
    /// when it notices the spider (5 m) AND the spider sits under open sky (cover like
    /// the bird protects), it attacks and stings on contact (energy -20, 1 s stun),
    /// then withdraws to a safe distance. Caught in a web: tears it (-50 %) and cuts
    /// itself free after 3 s. Deterministic rules live in <see cref="WaspModel"/>.
    /// </summary>
    public class Wasp : MonoBehaviour
    {
        [SerializeField] private float speed = 1.5f;      // fast flyer (withdraws 4m in 3 s)
        [SerializeField] private float turnRate = 300f;   // Grad pro Sekunde

        [Tooltip("Home point the wasp patrols around (set by the spawner).")]
        public Vector3 home;

        [Tooltip("Start the sky-ray this far above the spider so it never hits itself (m) — like BirdThreat.")]
        [SerializeField] private float rayHeadOffset = 0.15f;
        [Tooltip("How high to probe for open sky (m).")]
        [SerializeField] private float rayMaxHeight = 200f;

        private WaspModel _model;

        private Transform _spiderTransform;
        private SpiderMovement _spiderMovement;
        private SpiderEnergy _spiderEnergy;
        private bool _spiderResolved;

        private Vector3 currentDirection = Vector3.forward;
        private Vector3 target;
        private float nextRetargetTime;
        private float _ignoreWebsUntil;
        private OrbWeb _caughtIn;
        private Vector3 _caughtPoint;
        private bool _stingApplied;

        /// <summary>The pure model (read by tests).</summary>
        public WaspModel GetModel()
        {
            return _model;
        }

        /// <summary>True while the wasp sits caught in a web.</summary>
        public bool IsCaughtInWeb { get; private set; }

        private void Awake()
        {
            _model = new WaspModel();
            if (home == Vector3.zero)
            {
                home = transform.position;
            }

            target = home;
            nextRetargetTime = Time.time + 2f;
        }

        private void Update()
        {
            UpdateSpiderRef();

            float dist = _spiderTransform != null
                ? Vector3.Distance(transform.position, _spiderTransform.position)
                : float.PositiveInfinity;
            bool exposed = _spiderTransform != null && HasOpenSkyAbove(_spiderTransform.position);

            bool wasStung = _model.Stung;
            _model.Tick(dist, exposed, Time.deltaTime);

            if (_model.state == WaspModel.State.Caught)
            {
                UpdateCaught();
                return;
            }

            if (Time.time >= _ignoreWebsUntil)
            {
                CheckWebCollision();
            }

            if (_model.state == WaspModel.State.Caught)
            {
                return;
            }

            if (!wasStung && _model.Stung && !_stingApplied)
            {
                ApplySting();
            }

            UpdateFlight(dist);
        }

        /// <summary>S7: Spinne-Cache: typed-Lookup ONE Mal resolved (no name-lookup).</summary>
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

        /// <summary>True when a vertical ray above the spider reaches open sky (no cover) — like BirdThreat.</summary>
        private bool HasOpenSkyAbove(Vector3 spiderPos)
        {
            Vector3 origin = spiderPos + Vector3.up * rayHeadOffset;
            return !Physics.Raycast(origin, Vector3.up, rayMaxHeight);
        }

        /// <summary>Sting landed: energy -20 and 1 s stun on the spider (exact once).</summary>
        private void ApplySting()
        {
            _stingApplied = true;
            if (_spiderEnergy != null)
            {
                _spiderEnergy.ApplySting(WaspModel.stingEnergyCost);
            }

            if (_spiderMovement != null)
            {
                _spiderMovement.SetStun(WaspModel.stunDuration);
            }
        }

        /// <summary>Netz-Kollision: the wasp can get caught (then it tears the net).</summary>
        private void CheckWebCollision()
        {
            Vector3 from = transform.position - currentDirection * speed * Time.deltaTime;
            Vector3 to = transform.position;
            foreach (OrbWeb web in OrbWeb.All)
            {
                Vector3 hit;
                if (web.TryIntersectSegment(from, to, out hit))
                {
                    CatchInWeb(web, hit);
                    return;
                }
            }
        }

        /// <summary>In the net: model catches it, net takes the tear damage (-50 %).</summary>
        private void CatchInWeb(OrbWeb web, Vector3 hit)
        {
            _model.CatchInWeb();
            _caughtIn = web;
            _caughtPoint = hit;
            IsCaughtInWeb = true;
            transform.position = hit;
            if (web.Durability != null)
            {
                web.Durability.AddDamage(WaspModel.waspTearCost);
            }

            web.Vibrate(1f);
        }

        /// <summary>Caught: sit at the catch point, cut free after escapeTime (model counts).</summary>
        private void UpdateCaught()
        {
            if (_caughtIn == null)
            {
                _model.state = WaspModel.State.Searching;
                return;
            }

            transform.position = _caughtPoint;
            if (_model.FreedAfterCatch)
            {
                Vector3 away = _caughtIn.PlaneNormal * 0.08f;
                if (Vector3.Dot(away, transform.position - _caughtIn.Layout.Hub) < 0f)
                {
                    away = -away;
                }

                transform.position += away;
                _caughtIn = null;
                IsCaughtInWeb = false;
                _model.state = WaspModel.State.Searching;
                _ignoreWebsUntil = Time.time + 2f;
            }
        }

        /// <summary>Move: patrol (Searching), chase the spider (Attack) or pull away (Withdraw).</summary>
        private void UpdateFlight(float dist)
        {
            Vector3 desired;
            switch (_model.state)
            {
                case WaspModel.State.Attack:
                    desired = _spiderTransform.position - transform.position;
                    break;

                case WaspModel.State.Withdraw:
                    Vector3 away = transform.position - _spiderTransform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude < 1e-6f)
                    {
                        away = -currentDirection;
                    }

                    desired = away;
                    break;

                default:
                    if (Time.time >= nextRetargetTime ||
                        Vector3.Distance(transform.position, target) < 0.2f)
                    {
                        Vector3 random = UnityEngine.Random.insideUnitSphere * 2f;
                        target = home + random;
                        target.y += 0.4f + UnityEngine.Random.Range(0f, 0.6f);
                        nextRetargetTime = Time.time + 2f;
                    }

                    desired = target - transform.position;
                    break;
            }

            if (desired.sqrMagnitude < 1e-6f)
            {
                return;
            }

            desired.Normalize();
            currentDirection = Vector3.RotateTowards(currentDirection, desired,
                turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
            transform.position += currentDirection * speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(currentDirection), 0.25f);
        }
    }
}
