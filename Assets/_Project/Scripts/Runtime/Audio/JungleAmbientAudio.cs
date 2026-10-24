using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// M7-10.6: Rainforest-Geräuschbed für den Urwald — Zikaden/Frosch-Loop (2D, allortialias)
    /// plus Sumpf-Blob (3D, glides to the nearest water point near the camera).
    /// Deterministic loops from ProceduralAudio, no audio files.
    /// </summary>
    [DisallowMultipleComponent]
    public class JungleAmbientAudio : MonoBehaviour
    {
        private AudioSource _drone;
        private AudioSource _pond;
        private AudioSource _rain;
        [SerializeField] private float droneVolume = 0.25f;
        [SerializeField] private float pondVolume = 0.3f;
        [SerializeField, Tooltip("Rain-Loop volume at full downpour.")] private float rainMax = 0.35f;

        private JungleWorldGenerator _gen;

        public bool IsRunning => _drone != null && _pond != null;

        /// <summary>Current drone volume (for tests/telemetry).</summary>
        public float DroneVolumeNow => _drone != null ? _drone.volume : 0f;

        private void Start()
        {
            _drone = CreateSource("Drone", ProceduralAudio.RainforestLoop(), 0f, droneVolume);
            _pond = CreateSource("Pond", ProceduralAudio.StreamLoop(), 1f, pondVolume);
            _rain = CreateSource("Rain", ProceduralAudio.RainfallLoop(), 2f, 0f);
            _pond.minDistance = 2f;
            _pond.maxDistance = 30f;
        }

        private void Update()
        {
            if (_drone == null || _pond == null) return;
            float rain = WeatherController.Instance != null ? WeatherController.Instance.Rain01 : 0f;
            if (_rain != null)
            {
                // Rain fades in, the insect-drone fades out while it rains.
                _rain.volume = rainMax * rain;
                _drone.volume = droneVolume * (1f - 0.6f * rain);
            }
            if (_gen == null)
            {
                _gen = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (_gen == null || !_gen.IsReady) return;
            }
            Camera cam = Camera.main;
            Vector3 near = cam != null ? cam.transform.position : transform.position;
            _pond.transform.position = NearestWater(_gen.World, near);
        }

        private Vector3 NearestWater(JungleWorld World, Vector3 near)
        {
            Vector3 best = new Vector3(near.x, JungleWorld.WaterLevel, near.z);
            float bestD = float.MaxValue;
            for (float dz = -15f; dz <= 15f; dz += 1.5f)
            {
                for (float dx = -15f; dx <= 15f; dx += 1.5f)
                {
                    float x = near.x + dx, z = near.z + dz;
                    if (!World.IsUnderWater(x, z)) continue;
                    float d = dx * dx + dz * dz;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = new Vector3(x, JungleWorld.WaterLevel, z);
                    }
                }
            }
            return best;
        }

        private AudioSource CreateSource(string name, AudioClip clip, float spatial, float volume)
        {
            GameObject go = new GameObject("JungleAmbient_" + name);
            go.transform.SetParent(transform, false);
            AudioSource a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.spatialBlend = spatial;
            a.volume = volume;
            a.Play();
            return a;
        }
    }
}
