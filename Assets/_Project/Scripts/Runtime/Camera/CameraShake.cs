using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S3 Fang-Rueckmeldung: kurzes Kamera-Wackeln (Dauer, Staerke als Parameter),
    /// triggered via Trigger(strength). Die reine Wackel-Logik ist ShakeModel
    /// (testbar ohne Unity); SpiderCamera per LateUpdate den Offset verhuert.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("Wackeln-Dauers in s (S3: 0.15).")]
        [SerializeField] private float duration = 0.15f;

        /// <summary>Jetzige Wackel-Offset (m) — zero aussen waehrend einem Wackeln.</summary>
        public Vector3 CurrentOffset { get; private set; }

        private SpiderCamera _cameraRig;
        private float _elapsed = float.MaxValue;
        private float _strength;

        private void Awake()
        {
            _cameraRig = GetComponent<SpiderCamera>();
            if (_cameraRig == null)
            {
                Debug.LogWarning("CameraShake: SpiderCamera auf demsame GameObject fehlt.", this);
            }
        }

        /// <summary>Wackeln starten (Strength in m peak-Abweichung); ignored when switched off in GameSettings.</summary>
        public void Trigger(float strength)
        {
            if (!GameSettings.cameraShakeEnabled)
            {
                return;
            }
            _strength = Mathf.Max(0f, strength);
            _elapsed = 0f;
            LastStrength = _strength;
        }

        /// <summary>Peak-Staerke of the last triggered shake (m, for tests/diagnose).</summary>
        public float LastStrength { get; private set; }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            Vector3 offset = ShakeOffset(_elapsed);
            CurrentOffset = offset;
            if (_cameraRig != null)
            {
                _cameraRig.ShakeOffset = offset;
            }
        }

        /// <summary>Offset at time t since trigger, in the camera's local right/up plane.</summary>
        private Vector3 ShakeOffset(float t)
        {
            if (_elapsed >= duration || _strength <= 0f || !GameSettings.cameraShakeEnabled)
            {
                return Vector3.zero;
            }

            // Reine Model: sin-Bump ueber [0..duration], Peak = strength im Mitte, 0 am Raend.
            float bump = Mathf.Sin(Mathf.PI * t / duration);
            float dirAngle = 1.2f; // fixed pseudo-random tilt (deterministic)
            Vector3 local = new Vector3(Mathf.Cos(dirAngle), Mathf.Sin(dirAngle) * 0.7f, 0f).normalized;

            Transform cam = transform;
            return (cam.right * local.x + cam.up * local.y) * (_strength * bump);
        }
    }

    /// <summary>Reine Wackel-Curva: Peak-Staerke in der Dauera-Midpoint, zero aussen. Testbar.</summary>
    public static class ShakeModel
    {
        public static float OffsetAt(float t, float duration, float strength)
        {
            if (t < 0f || t >= duration || strength <= 0f)
            {
                return 0f;
            }
            return strength * Mathf.Sin(Mathf.PI * t / duration);
        }
    }
}
