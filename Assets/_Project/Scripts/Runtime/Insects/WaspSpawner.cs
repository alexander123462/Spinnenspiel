using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S7: Spawns the wasp — ONE danger, rare: at most one live wasp, respawned
    /// after respawnInterval (300 s = 1 per 5 min game time), day species only.
    /// Frequency scales with the difficulty danger multiplier (S1).
    /// </summary>
    public class WaspSpawner : MonoBehaviour
    {
        [Tooltip("Wasp template (deactivated) — cloned and activated on spawn.")]
        public GameObject waspTemplate;

        [Tooltip("Center the wasp spawns near (the spider).")]
        public Transform center;

        [Tooltip("Day/night cycle: wasps fly by day only (null = always day).")]
        public DayNightCycle cycle;

        /// <summary>Seconds between wasp respawns (1 per 5 min game time).</summary>
        [SerializeField] private float respawnInterval = 300f;

        [Tooltip("Spawn this far from the center (m).")]
        [SerializeField] private float spawnRadius = 6f;

        private Wasp _live;
        private float _nextSpawnAt;

        /// <summary>The live wasp (null while none is out).</summary>
        public Wasp LiveWasp { get; private set; }

        private void Awake()
        {
            if (center == null)
            {
                SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
                if (spider != null)
                {
                    center = spider.transform;
                }
            }

            _nextSpawnAt = Time.time + 10f; // first wasp shortly after start
        }

        private void Update()
        {
            if (waspTemplate == null || !IsDay)
            {
                return;
            }

            if (LiveWasp != null && !IsValid(LiveWasp))
            {
                LiveWasp = null;
            }

            if (LiveWasp == null && Time.time >= _nextSpawnAt)
            {
                Spawn();
            }
        }

        /// <summary>True while it is day (no cycle = always day).</summary>
        private bool IsDay
        {
            get
            {
                return cycle == null || cycle.CurrentDayFactor > 0.5f;
            }
        }

        private bool IsValid(Wasp wasp)
        {
            if (wasp == null || !wasp.transform.parent)
            {
                return false;
            }

            if (center != null &&
                Vector3.Distance(wasp.transform.position, center.position) > spawnRadius * 3f)
            {
                Object.Destroy(wasp.gameObject);
                return false;
            }

            return true;
        }

        private void Spawn()
        {
            Vector3 home = center != null ? center.position : transform.position;
            Vector3 offset = UnityEngine.Random.insideUnitSphere * spawnRadius;
            offset.y = 0f;
            Vector3 pos = home + offset.normalized * UnityEngine.Random.Range(spawnRadius * 0.5f, spawnRadius);
            pos.y = GroundHeight(pos) + 0.8f;

            GameObject spawned = Instantiate(waspTemplate, pos, Quaternion.identity, transform);
            spawned.SetActive(true);
            Wasp wasp = spawned.GetComponent<Wasp>();
            if (wasp == null)
            {
                Object.DestroyImmediate(spawned);
                return;
            }

            wasp.home = pos;
            LiveWasp = wasp;
            _nextSpawnAt = Time.time + respawnInterval / Mathf.Max(GameDifficulty.BirdThreatMultiplier(), 0.01f);
        }

        private float GroundHeight(Vector3 point)
        {
            Vector3 from = point + Vector3.up * 8f;
            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, Mathf.Infinity,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return point.y - 0.5f;
        }
    }
}
