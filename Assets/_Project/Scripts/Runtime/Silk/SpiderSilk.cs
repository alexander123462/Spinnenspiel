using System.Collections;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Spinnt von den Spinnwarzen einen Faden zu einer getroffenen Fläche,
    /// fährt ihn über 0.5 s aus und zieht ihn danach straff ("holt ein").
    /// </summary>
    public class SpiderSilk : MonoBehaviour
    {
        /// <summary>Spinnwarzen – Startpunkt der Faden-Raycast (leeres Kind am Hinterleib).</summary>
        public Transform spinnerets;

        /// <summary>
        /// Maximale Reichweite eines Fadens. 8 m: die Akzeptanztests verlangen einen
        /// Treffer auf ~6 m Distanz (Block zu Stamm), 4 m reichen dafür nicht.
        /// </summary>
        public float maxRange = 8f;

        private const float ExtendDuration = 0.5f;

        public SilkThread LastThread { get; private set; }

        /// <summary>Wird nach dem Straffziehen für einen erfolgreichen Faden ausgelöst.</summary>
        public event System.Action<SilkThread> ThreadAttached;

        /// <summary>Wenn true, klettert die Spinne nach dem Ausfahren automatisch auf den Faden (Faden-Modus).</summary>
        public bool autoAttach = true;

        /// <summary>
        /// Schießt einen Faden in Richtung <paramref name="worldDirection"/>.
        /// Kein Treffer in <see cref="maxRange"/> → null.
        /// </summary>
        public SilkThread ShootThread(Vector3 worldDirection)
        {
            Vector3 origin = spinnerets != null ? spinnerets.position : transform.position;
            Vector3 dir = worldDirection.normalized;

            // Layer 2 = Spinne, bewusst ausgenommen.
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, maxRange,
                    Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            var thread = new GameObject("SilkThread").AddComponent<SilkThread>();
            Vector3 anchorA = transform.position;              // Kontaktpunkt der Spinne auf ihrer Fläche
            Vector3 anchorB = hit.point + hit.normal * 0.002f; // End-Anker an der getroffenen Fläche
            thread.Init(anchorA, anchorB, 0.02f * Vector3.Distance(anchorA, anchorB));

            LastThread = thread;
            StartCoroutine(ExtendAndTighten(thread));
            return thread;
        }

        /// <summary>Fährt den Faden aus (Extend01 0→1) und zieht ihn danach auf 20 % Durchhang straff.</summary>
        private IEnumerator ExtendAndTighten(SilkThread thread)
        {
            float t = 0f;
            while (t < ExtendDuration)
            {
                if (thread == null)
                {
                    yield break;
                }

                t += Time.deltaTime;
                thread.Extend01 = Mathf.Clamp01(t / ExtendDuration);
                thread.Refresh();
                yield return null;
            }

            if (thread == null)
            {
                yield break;
            }

            thread.Extend01 = 1f;
            thread.sag *= 0.2f;
            thread.Refresh();

            var evt = ThreadAttached;
            if (evt != null)
            {
                evt(thread);
            }

            if (autoAttach)
            {
                var spider = GetComponent<SpiderMovement>();
                if (spider != null)
                {
                    spider.AttachToThread(thread, 0f);
                }
            }
        }
    }
}
