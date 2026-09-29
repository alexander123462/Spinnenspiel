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
        private const float AbdomenLiftDuration = 0.2f;
        private const float AbdomenLiftAngle = -25f;
        private const float LegSwitchInterval = 0.12f;

        // Spinnpose per API (Netz-Vorbereitung): Zustand von BeginSpinningPose().
        private bool _poseActive;
        private float _poseLiftT;
        private float _poseT;
        private Transform _poseAbdomen;
        private Vector3 _poseAbdOrigPos;
        private Quaternion _poseAbdOrigRot;
        private SpiderLeg _poseRearA;
        private SpiderLeg _poseRearB;

        public SilkThread LastThread { get; private set; }

        /// <summary>True, während ein Faden ausgefahren wird (Spinn-Animation aktiv).</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>Spinnwarzen-Transform (öffentliche API, z. B. für Netzbau-Skripte).</summary>
        public Transform Spinnerets => spinnerets;

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
            SpiderMovement movement = GetComponent<SpiderMovement>();
            Vector3 normalA = movement != null ? movement.SurfaceNormal : Vector3.up;
            thread.Init(anchorA, anchorB, 0.02f * Vector3.Distance(anchorA, anchorB), normalA, hit.normal);

            LastThread = thread;
            StartCoroutine(ExtendAndTighten(thread));
            return thread;
        }

        /// <summary>
        /// Führt die Spinn-Animation aus: Hinterleib hebt sich (0.2 s), die Hinterbeine
        /// (PairIndex 3) führen abwechselnd den Faden an die Spinnwarzen, während er
        /// ausgefahren wird; danach wird er straffgezogen und der Hinterleib senkt sich.
        /// </summary>
        /// <summary>
        /// Stellt die Pose des Hinterleibs absolut aus der Ausgangspose ein: Winkel in Grad
        /// (0 = Ausgangspose) um die Body-Rechts-Achse, Drehpunkt Pedicel (Body-lokal).
        /// </summary>
        private static void ApplyAbdomenPose(Transform abdomen, Vector3 origPos, Quaternion origRot, float angle)
        {
            Vector3 pivot = new Vector3(0f, 0.002f, -0.006f);
            Quaternion q = Quaternion.AngleAxis(angle, Vector3.right);
            abdomen.localPosition = pivot + q * (origPos - pivot);
            abdomen.localRotation = q * origRot;
        }

        /// <summary>
        /// Startet die Spinnpose ohne Faden (Vorbereitung für den Netzbau):
        /// IsSpinning wird true, der Hinterleib hebt sich über AbdomenLiftDuration
        /// absolut aus der Ausgangspose (Update()); die Hinterbeine (PairIndex 3)
        /// stehen für UpdateSpinningPose() bereit.
        /// </summary>
        public void BeginSpinningPose()
        {
            IsSpinning = true;
            _poseActive = true;
            _poseLiftT = 0f;
            _poseT = 0f;

            _poseAbdomen = FindRecursive(transform, "Abdomen");
            _poseAbdOrigPos = Vector3.zero;
            _poseAbdOrigRot = Quaternion.identity;
            if (_poseAbdomen != null)
            {
                _poseAbdOrigPos = _poseAbdomen.localPosition;
                _poseAbdOrigRot = _poseAbdomen.localRotation;
            }

            _poseRearA = null;
            _poseRearB = null;
            foreach (SpiderLeg leg in GetComponentsInChildren<SpiderLeg>())
            {
                if (leg == null || leg.PairIndex != 3)
                {
                    continue;
                }

                if (_poseRearA == null)
                {
                    _poseRearA = leg;
                }
                else
                {
                    _poseRearB = leg;
                }
            }
        }

        /// <summary>
        /// Führt die Spinnpose weiter (jeden Frame aufrufbar): die Hinterbeine
        /// (PairIndex 3) führen abwechselnd den Punkt zwischen Spinnwarzen und
        /// <paramref name="silkWorldPoint"/> an – gleiche Wechsel-Logik/Zeiten wie
        /// Schritt 2 von ExtendAndTighten.
        /// </summary>
        public void UpdateSpinningPose(Vector3 silkWorldPoint)
        {
            _poseT += Time.deltaTime;
            int activeLeg = (int)Mathf.Floor(_poseT / LegSwitchInterval) % 2;
            SpiderLeg active = activeLeg == 0 ? _poseRearA : _poseRearB;
            SpiderLeg idle = activeLeg == 0 ? _poseRearB : _poseRearA;
            if (active == null)
            {
                return;
            }

            Vector3 spinneretPos = spinnerets != null ? spinnerets.position : transform.position;
            Transform body = _poseAbdomen != null ? _poseAbdomen.parent : null;
            Vector3 right = (body != null ? body : transform).right;
            Vector3 offset = activeLeg == 0 ? right : -right;
            Vector3 target = Vector3.Lerp(spinneretPos, silkWorldPoint, 0.5f);
            active.SetOverrideFoot(target + transform.up * 0.01f + offset * 0.012f);

            if (idle != null)
            {
                idle.SetOverrideFoot(null);
            }
        }

        /// <summary>
        /// Endet die Spinnpose: Hinterleib exakt in der Ausgangspose (Winkel 0),
        /// beide Hinterbeine frei, IsSpinning = false. Mehrfachaufruf harmlos.
        /// </summary>
        public void EndSpinningPose()
        {
            if (_poseAbdomen != null)
            {
                ApplyAbdomenPose(_poseAbdomen, _poseAbdOrigPos, _poseAbdOrigRot, 0f);
            }

            if (_poseRearA != null)
            {
                _poseRearA.SetOverrideFoot(null);
            }

            if (_poseRearB != null)
            {
                _poseRearB.SetOverrideFoot(null);
            }

            _poseActive = false;
            IsSpinning = false;
        }

        private void Update()
        {
            // Spinnpose (API-Modus): Hinterleib über AbdomenLiftDuration heben,
            // absolut aus der Ausgangspose berechnet (keine Rundungsfehler).
            if (!_poseActive || _poseLiftT >= AbdomenLiftDuration)
            {
                return;
            }

            _poseLiftT += Time.deltaTime;
            float k = Mathf.Clamp01(_poseLiftT / AbdomenLiftDuration);
            if (_poseAbdomen != null)
            {
                ApplyAbdomenPose(_poseAbdomen, _poseAbdOrigPos, _poseAbdOrigRot, AbdomenLiftAngle * k);
            }
        }

        private IEnumerator ExtendAndTighten(SilkThread thread)
        {
            IsSpinning = true;

            Transform spider = transform;
            Transform abdomen = FindRecursive(spider, "Abdomen");
            Transform body = abdomen != null ? abdomen.parent : null;
            Vector3 right = (body != null ? body : spider).right;

            SpiderLeg rearA = null;
            SpiderLeg rearB = null;
            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                if (leg == null || leg.PairIndex != 3)
                {
                    continue;
                }

                if (rearA == null)
                {
                    rearA = leg;
                }
                else
                {
                    rearB = leg;
                }
            }

            // Ausgangspose des Hinterleibs (lokal zu Body). Die Pose wird jeden Frame
            // absolut daraus berechnet, damit sich keine Rundungsfehler ansammeln.
            Vector3 abdOrigPos = Vector3.zero;
            Quaternion abdOrigRot = Quaternion.identity;
            if (abdomen != null)
            {
                abdOrigPos = abdomen.localPosition;
                abdOrigRot = abdomen.localRotation;
            }

            // 1) Hinterleib heben (0.2 s).
            float liftT = 0f;
            while (liftT < AbdomenLiftDuration && thread != null)
            {
                liftT += Time.deltaTime;
                float k = Mathf.Clamp01(liftT / AbdomenLiftDuration);
                if (abdomen != null)
                {
                    ApplyAbdomenPose(abdomen, abdOrigPos, abdOrigRot, AbdomenLiftAngle * k);
                }

                yield return null;
            }

            if (thread == null)
            {
                IsSpinning = false;
                yield break;
            }

            // 2) Faden ausfahren; die Hinterbeine führen abwechselnd den Faden an die Spinnwarzen.
            float t = 0f;
            int activeLeg = 0;
            while (t < ExtendDuration && thread != null)
            {
                t += Time.deltaTime;
                thread.Extend01 = Mathf.Clamp01(t / ExtendDuration);
                thread.Refresh();

                activeLeg = (int)Mathf.Floor(t / LegSwitchInterval) % 2;
                SpiderLeg active = activeLeg == 0 ? rearA : rearB;
                SpiderLeg idle = activeLeg == 0 ? rearB : rearA;
                if (active != null)
                {
                    Vector3 spinneretPos = spinnerets != null ? spinnerets.position : spider.position;
                    Vector3 offset = activeLeg == 0 ? right : -right;
                    active.SetOverrideFoot(spinneretPos + spider.up * 0.01f + offset * 0.012f);
                }

                if (idle != null)
                {
                    idle.SetOverrideFoot(null);
                }

                yield return null;
            }

            if (thread == null)
            {
                IsSpinning = false;
                yield break;
            }

            // Beide Hinterbeine wieder frei.
            if (rearA != null)
            {
                rearA.SetOverrideFoot(null);
            }

            if (rearB != null)
            {
                rearB.SetOverrideFoot(null);
            }

            thread.Extend01 = 1f;
            thread.sag *= 0.2f;
            thread.Refresh();

            // 3) Hinterleib senken (0.2 s).
            float dropT = 0f;
            while (dropT < AbdomenLiftDuration)
            {
                dropT += Time.deltaTime;
                float k = Mathf.Clamp01(dropT / AbdomenLiftDuration);
                if (abdomen != null)
                {
                    ApplyAbdomenPose(abdomen, abdOrigPos, abdOrigRot, AbdomenLiftAngle * (1f - k));
                }

                yield return null;
            }

            // Exakt auf die Ausgangspose zurücksetzen.
            if (abdomen != null)
            {
                abdomen.localPosition = abdOrigPos;
                abdomen.localRotation = abdOrigRot;
            }

            IsSpinning = false;

            var evt = ThreadAttached;
            if (evt != null)
            {
                evt(thread);
            }

            if (autoAttach)
            {
                var movement = GetComponent<SpiderMovement>();
                if (movement != null)
                {
                    movement.AttachToThread(thread, 0f);
                }
            }
        }

        /// <summary>Sucht rekursiv ein Kind-Transform mit exaktem Namen (Pfad-unabhängig).</summary>
        private static Transform FindRecursive(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform found = FindRecursive(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
