using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Passt Höhe und Neigung des Körpers an die Fußpositionen an.
    /// Aus den Füßen werden durchschnittliche Bodenhöhe sowie vorne/hinten- und links/rechts-Höhenunterschiede
    /// bestimmt; daraus entsteht eine Bodenebene, der der Körper weich (exponentiell geglättet) folgt.
    /// Die Beine bleiben dabei einzeln am Boden, weil sie ihre Füße in Weltkoordinaten halten.
    /// </summary>
    [DefaultExecutionOrder(-55)]
    [DisallowMultipleComponent]
    public class SpiderBodyController : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private float bodyHeight = 0.06f;
        [SerializeField] private float heightSharpness = 12f;
        [SerializeField] private float tiltSharpness = 9f;
        [SerializeField] private float maxTilt = 30f;

        [Header("Leben")]
        [SerializeField] private float breathingAmplitude = 0.0015f;
        [SerializeField] private float breathingFrequency = 0.6f;
        [SerializeField] private float stepDip = 0.004f;

        private SpiderLeg[] _legs = new SpiderLeg[0];
        private SpiderGaitController _gait;

        /// <summary>Aus den Füßen geschätzte Boden-Normale in Weltkoordinaten.</summary>
        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        /// <summary>Durchschnittliche Fußhöhe relativ zum Spinnen-Root (m).</summary>
        public float AverageFootHeight { get; private set; }

        public Transform Body => body;

        public void Configure(Transform bodyTransform, float height)
        {
            body = bodyTransform;
            bodyHeight = height;
        }

        private void Awake()
        {
            if (body == null)
            {
                body = transform.Find("Body");
            }

            _legs = GetComponentsInChildren<SpiderLeg>();
            _gait = GetComponent<SpiderGaitController>();
        }

        private void LateUpdate()
        {
            if (body == null || _legs.Length == 0)
            {
                return;
            }

            Vector3 front = Vector3.zero, back = Vector3.zero, left = Vector3.zero, right = Vector3.zero;
            int frontCount = 0, backCount = 0, leftCount = 0, rightCount = 0;
            float heightSum = 0f;
            int grounded = 0;

            foreach (SpiderLeg leg in _legs)
            {
                if (!leg.IsGrounded)
                {
                    continue;
                }

                Vector3 local = transform.InverseTransformPoint(leg.FootPosition);
                heightSum += local.y;
                grounded++;

                if (local.z >= 0f) { front += local; frontCount++; } else { back += local; backCount++; }
                if (local.x < 0f) { left += local; leftCount++; } else { right += local; rightCount++; }
            }

            Vector3 localNormal = Vector3.up;
            float averageHeight = 0f;
            if (grounded > 0)
            {
                averageHeight = heightSum / grounded;
                if (frontCount > 0 && backCount > 0 && leftCount > 0 && rightCount > 0)
                {
                    Vector3 frontToBack = front / frontCount - back / backCount;
                    Vector3 leftToRight = right / rightCount - left / leftCount;
                    Vector3 n = Vector3.Cross(frontToBack, leftToRight);
                    if (n.sqrMagnitude > 1e-10f)
                    {
                        localNormal = n.y < 0f ? -n.normalized : n.normalized;
                    }
                }
            }

            AverageFootHeight = averageHeight;
            GroundNormal = transform.TransformDirection(localNormal);

            float deltaTime = Time.deltaTime;
            Quaternion tilt = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, localNormal), maxTilt);

            float stepping01 = _gait != null && _legs.Length > 0 ? (float)_gait.SteppingCount / _legs.Length : 0f;
            float breathe = Mathf.Sin(Time.time * breathingFrequency * 2f * Mathf.PI) * breathingAmplitude;
            Vector3 targetPosition = new Vector3(0f, averageHeight + bodyHeight + breathe - stepDip * stepping01, 0f);

            body.localPosition = Vector3.Lerp(body.localPosition, targetPosition, 1f - Mathf.Exp(-heightSharpness * deltaTime));
            body.localRotation = Quaternion.Slerp(body.localRotation, tilt, 1f - Mathf.Exp(-tiltSharpness * deltaTime));
        }
    }
}
