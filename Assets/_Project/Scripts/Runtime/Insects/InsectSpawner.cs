using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Hält immer <see cref="count"/> Insekten (Zustand Flying/Caught) in der Szene.
    /// Spawnpunkt: zufälliger Ring um <see cref="center"/> (standardmäßig die Spinne),
    /// Höhe 0.4–1.4 m über dem Boden. Zu weit entfernte (> despawnDistance) werden ersetzt.
    /// </summary>
    public class InsectSpawner : MonoBehaviour
    {
        public int count = 6;
        [SerializeField] private float spawnMin = 2f;
        [SerializeField] private float spawnMax = 5f;
        [SerializeField] private float despawnDistance = 15f;

        [Tooltip("Zentrum des Spawnerings (standardmäßig die Spinne).")]
        public Transform center;

        [Tooltip("Vorlagen-Objekt (deaktiviert) in der Szene – wird kopiert und aktiviert.")]
        public GameObject template;

        [Tooltip("Optional: dann ist home für alle Insekten homeOverride.position.")]
        public Transform homeOverride;

        [Tooltip("S2: Optional Muecke-Vorlage (deaktivierte). Ist es set, the spawner mixes flies and mosquitoes.")]
        public GameObject mosquitoTemplate;

        [Tooltip("J5: Tag/Nacht-Cycle. Without it the spawner is always active (day/night ignored).")]
        public DayNightCycle cycle;

        [Tooltip("J5: true = this species flies only at night, false = only during the day.")]
        public bool activeAtNight;

        // K4a: Regen-Zustand des WeatherController (Instance singleton).
        private const float RainShareOfCount = 0.3f;   // Zielzahl waahtrend Regen (30 Prozent)
        private const float RainThreshold = 0.5f;      // Rain01 ueber this = "es regnt"
        private const float RainFadeSeconds = 20f;     // Vorhandene Insekten ueber this many s auslaufen
        private const float RainLeadSeconds = 0.5f;    // Kurze Fuerspan: eeste Remove nur nach dem Interval

        // S2: Anteil Muecken among the spawns (of the day species) as a named constant.
        private const float MosquitoShare = 0.4f;      // ~40 % of day-insects are mosquitoes

        private float rainRemovalTimer;   // K4a: Countdown bis naechsten staggered Remove.
        private float rainRemovalInterval; // K4a: Constant interval while raining (fade stays smooth).

        /// <summary>K4a: true while the WeatherController reports rain (no controller = never rains).</summary>
        public bool Raining
        {
            get
            {
                return WeatherController.Instance != null &&
                       WeatherController.Instance.Rain01 >= RainThreshold;
            }
        }

        /// <summary>J5: Share 0..1 of <see cref="count"/> that is out now (day/night ramp).</summary>
        private float ActiveShare
        {
            get
            {
                if (cycle == null)
                {
                    return 1f;
                }

                float dayFactor = Mathf.Clamp01(cycle.CurrentDayFactor);
                return activeAtNight ? 1f - dayFactor : dayFactor;
            }
        }

        private readonly List<FlyingInsect> insects = new List<FlyingInsect>();
        private readonly System.Random rng = new System.Random();

        /// <summary>Anzahl lebender (Flying/Caught) Insekten.</summary>
        public int AliveCount
        {
            get
            {
                int alive = 0;
                foreach (FlyingInsect i in insects)
                {
                    if (i != null && (i.State == FlyingInsect.InsectState.Flying ||
                                     i.State == FlyingInsect.InsectState.Caught))
                    {
                        alive++;
                    }
                }
                return alive;
            }
        }

        private void Awake()
        {
            if (center == null)
            {
                SpiderMovement spider = FindFirstObjectByType<SpiderMovement>();
                if (spider != null)
                {
                    center = spider.transform;
                }
            }

            Reconcile();
        }

        private void Update()
        {
            Reconcile();
        }

        private void Track(FlyingInsect insect)
        {
            if (insect == null || insects.Contains(insect))
            {
                return;
            }

            insects.Add(insect);
            insect.Removed += OnRemoved;
        }

        private void OnRemoved(FlyingInsect insect)
        {
            insects.Remove(insect);
        }

        /// <summary>Entfernte Insekten ersetzen und auf count aufstocken. J5: only the day/night share is kept. K4a: rain lowers the target and staggers removals.</summary>
        private void Reconcile()
        {
            if (template == null)
            {
                return;
            }

            // J5: Target = share of count for this species at the current time (ramp, not abrupt).
            int target = Mathf.RoundToInt(count * ActiveShare);

            // K4a: While it rains the target drops to RainShareOfCount (30 Prozent).
            bool raining = Raining;
            if (raining)
            {
                target = Mathf.RoundToInt(target * RainShareOfCount);
            }

            if (raining)
            {
                // K4a: Existing insects do not vanish abruptly but run out over RainFadeSeconds.
                int excess = AliveCount - target;
                if (excess > 0)
                {
                    if (rainRemovalInterval <= 0f)
                    {
                        rainRemovalInterval = RainFadeSeconds / excess;
                        rainRemovalTimer = rainRemovalInterval + RainLeadSeconds;
                    }
                    else
                    {
                        rainRemovalTimer -= Time.deltaTime;
                        if (rainRemovalTimer <= 0f)
                        {
                            FlyingInsect victim = FirstFlyingVictim();
                            if (victim != null)
                            {
                                victim.Remove();
                                insects.Remove(victim);
                            }

                            rainRemovalTimer += rainRemovalInterval;
                        }
                    }
                }
                else
                {
                    rainRemovalInterval = 0f;
                    rainRemovalTimer = 0f;
                }
            }
            else if (AliveCount > target)
            {
                // Insekten beyond the current target get removed (they retreat at dawn/dusk).
                while (AliveCount > target)
            {
                FlyingInsect victim = null;
                foreach (FlyingInsect candidate in insects)
                {
                    if (candidate != null && candidate.State == FlyingInsect.InsectState.Flying)
                    {
                        victim = candidate;
                        break;
                    }
                }

                if (victim == null)
                {
                    break; // Caught ones stay.
                }

                victim.Remove();
                insects.Remove(victim);
            }
            }

            // Entfernte Insekten aufräumen und zu weit entfernte ersetzen.
            for (int i = insects.Count - 1; i >= 0; i--)
            {
                FlyingInsect insect = insects[i];
                if (insect == null)
                {
                    insects.RemoveAt(i);
                    continue;
                }

                bool isAlive = insect.State == FlyingInsect.InsectState.Flying ||
                               insect.State == FlyingInsect.InsectState.Caught;
                if (!isAlive)
                {
                    insects.RemoveAt(i);
                    continue;
                }

                float dist = Vector3.Distance(insect.transform.position, centerPosition());
                if (dist > despawnDistance)
                {
                    insect.Remove();
                    insects.Remove(insect);
                }
            }

            // Auf die Zielzahl aufstocken.
            int alive = 0;
            foreach (FlyingInsect insect in insects)
            {
                if (insect != null && (insect.State == FlyingInsect.InsectState.Flying ||
                                      insect.State == FlyingInsect.InsectState.Caught))
                {
                    alive++;
                }
            }

            for (int i = alive; i < target; i++)
            {
                Spawn();
            }
        }

        /// <summary>K4a: First still-flying insect that may retreat (Caught ones stay).</summary>
        private FlyingInsect FirstFlyingVictim()
        {
            foreach (FlyingInsect candidate in insects)
            {
                if (candidate != null && candidate.State == FlyingInsect.InsectState.Flying)
                {
                    return candidate;
                }
            }

            return null;
        }

        private Vector3 centerPosition()
        {
            if (homeOverride != null)
            {
                return homeOverride.position;
            }

            if (center != null)
            {
                return center.position;
            }

            return transform.position;
        }

        private void Spawn()
        {
            Vector3 home = centerPosition();

            // Zufälliger Punkt im Ring spawnMin..spawnMax um home.
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Lerp(spawnMin, spawnMax, (float)rng.NextDouble());
            Vector3 pos = home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            // Höhe 0.4–1.4 m über dem Boden (Raycast nach unten).
            pos.y = GroundHeight(pos) + Mathf.Lerp(0.4f, 1.4f, (float)rng.NextDouble());

            // S2: Ist ein Muecke-Vorlage set? Dann ~MosquitoShare der Spawns are mosquitoes.
            GameObject chosenTemplate = template;
            if (mosquitoTemplate != null && rng.NextDouble() < MosquitoShare)
            {
                chosenTemplate = mosquitoTemplate;
            }

            FlyingInsect templateInsect = chosenTemplate.GetComponent<FlyingInsect>();
            if (templateInsect == null)
            {
                return;
            }

            GameObject spawned = Instantiate(chosenTemplate, pos, Quaternion.identity, transform);
            spawned.SetActive(true);
            FlyingInsect insect = spawned.GetComponent<FlyingInsect>();
            if (insect != null)
            {
                if (homeOverride != null)
                {
                    insect.home = homeOverride.position;
                }
                else
                {
                    insect.home = pos;
                }
                Track(insect);
            }
        }

        /// <summary>Bodenhöhe unter einem Punkt (Raycast von oben nach unten).</summary>
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
