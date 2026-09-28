using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Umgebungsgeräusche: Wind (2D, Lautstärke folgt den Böen), Bachplätschern (3D-Quelle gleitet zum
    /// nächstgelegenen Bachpunkt), Teich und gelegentliche Vogelrufe aus Richtung Wald.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbientAudio : MonoBehaviour
    {
        [SerializeField] private float windVolume = 0.18f;
        [SerializeField] private float streamVolume = 0.5f;
        [SerializeField] private float birdVolume = 0.2f;
        [SerializeField] private Vector2 birdInterval = new Vector2(3f, 9f);

        private AudioSource _wind;
        private AudioSource _stream;
        private AudioSource _pond;
        private AudioSource _birds;
        private float _nextBird;
        private System.Random _random = new System.Random(21);

        public bool IsRunning => _wind != null && _stream != null;

        private void Start()
        {
            _wind = CreateSource("Wind", ProceduralAudio.WindLoop(), 0f, windVolume, true);
            _stream = CreateSource("Stream", ProceduralAudio.StreamLoop(), 1f, streamVolume, true);
            _stream.minDistance = 1.5f;
            _stream.maxDistance = 22f;
            _pond = CreateSource("Pond", ProceduralAudio.StreamLoop(), 1f, streamVolume * 0.35f, true);
            _pond.minDistance = 1f;
            _pond.maxDistance = 12f;
            _pond.pitch = 0.8f;
            _pond.transform.position = new Vector3(WorldLayout.PondCenter.x, WorldLayout.PondWaterHeight, WorldLayout.PondCenter.y);
            _birds = CreateSource("Birds", null, 1f, birdVolume, false);
            _birds.minDistance = 3f;
            _birds.maxDistance = 40f;
            _nextBird = Time.time + 2f;
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null || _wind == null)
            {
                return;
            }

            Vector3 listener = cam.transform.position;

            WindController wind = WindController.Instance;
            float gust = wind != null ? wind.Gust : 0.3f;
            _wind.volume = Mathf.Lerp(_wind.volume, windVolume * (0.5f + gust), 1f - Mathf.Exp(-2f * Time.deltaTime));

            // Bach: Quelle auf den nächsten Punkt der Bachmitte setzen (Suche in X um den Hörer).
            float bestX = listener.x, bestDistance = float.MaxValue;
            for (float dx = -12f; dx <= 12f; dx += 0.5f)
            {
                float x = listener.x + dx;
                Vector3 p = new Vector3(x, WorldLayout.StreamWaterHeight(x), WorldLayout.StreamCenterZ(x));
                float d = (p - listener).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestX = x;
                }
            }

            _stream.transform.position = new Vector3(bestX, WorldLayout.StreamWaterHeight(bestX), WorldLayout.StreamCenterZ(bestX));

            if (Time.time >= _nextBird)
            {
                PlayBird(listener);
                _nextBird = Time.time + Mathf.Lerp(birdInterval.x, birdInterval.y, (float)_random.NextDouble());
            }
        }

        private void PlayBird(Vector3 listener)
        {
            AudioClip[] calls = ProceduralAudio.BirdCalls();
            float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            Vector3 position = listener + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (8f + (float)_random.NextDouble() * 15f);
            // Vögel sitzen eher im Wald (Norden) und in der Höhe.
            position.z = Mathf.Lerp(position.z, WorldLayout.Size * 0.8f, 0.4f);
            position.y = listener.y + 4f + (float)_random.NextDouble() * 4f;
            _birds.transform.position = position;
            _birds.pitch = 0.9f + (float)_random.NextDouble() * 0.3f;
            _birds.PlayOneShot(calls[_random.Next(0, calls.Length)], birdVolume);
        }

        private AudioSource CreateSource(string name, AudioClip clip, float spatial, float volume, bool loop)
        {
            GameObject go = new GameObject("Ambient_" + name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.spatialBlend = spatial;
            source.volume = volume;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            if (loop && clip != null)
            {
                source.time = (float)_random.NextDouble() * clip.length * 0.9f;
                source.Play();
            }

            return source;
        }
    }
}
