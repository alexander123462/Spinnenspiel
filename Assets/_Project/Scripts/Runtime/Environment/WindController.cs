using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Globaler Wind: langsam drehende Richtung, Grundstärke und Böen (Perlin über die Zeit).
    /// Setzt die Shader-Variable _SpinnenWind (Vegetation) und stellt den Wind für Audio/Partikel bereit.
    /// </summary>
    [DisallowMultipleComponent]
    public class WindController : MonoBehaviour
    {
        private static readonly int WindId = Shader.PropertyToID("_SpinnenWind");

        [SerializeField] private float baseDirectionDegrees = 35f;
        [SerializeField] private float directionWander = 25f;
        [SerializeField] private float baseStrength = 1f;
        [SerializeField] private float gustStrength = 1f;
        [SerializeField] private float gustFrequency = 0.12f;

        public static WindController Instance { get; private set; }

        /// <summary>Windrichtung in der XZ-Ebene (normalisiert).</summary>
        public Vector3 Direction { get; private set; } = Vector3.forward;

        /// <summary>Aktuelle Böe 0..1.</summary>
        public float Gust { get; private set; }

        public float Strength => baseStrength;

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            Shader.SetGlobalVector(WindId, Vector4.zero);
        }

        private void Update()
        {
            float t = Time.time;
            float angle = (baseDirectionDegrees + (Mathf.PerlinNoise(t * 0.03f, 1.7f) - 0.5f) * 2f * directionWander) * Mathf.Deg2Rad;
            Direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

            float gustNoise = Mathf.PerlinNoise(t * gustFrequency, 7.3f);
            Gust = Mathf.Clamp01((gustNoise - 0.45f) * 2.2f) * gustStrength;

            Shader.SetGlobalVector(WindId, new Vector4(Direction.x, Direction.z, baseStrength, Gust));
        }
    }
}
