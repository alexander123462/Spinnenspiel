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

        /// <summary>Entfernte Insekten ersetzen und auf <see cref="count"/> aufstocken.</summary>
        private void Reconcile()
        {
            if (template == null)
            {
                return;
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

            for (int i = alive; i < count; i++)
            {
                Spawn();
            }
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

            FlyingInsect templateInsect = template.GetComponent<FlyingInsect>();
            if (templateInsect == null)
            {
                return;
            }

            GameObject spawned = Instantiate(template, pos, Quaternion.identity, transform);
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
