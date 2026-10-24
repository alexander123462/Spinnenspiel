using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// T6 Dynamische Musik: a calm background music bed (ProceduralAudio.DangerMusicBed)
    /// whose intensity follows the danger state — 0.2 in safety, up to 0.8 during danger
    /// (bird pre-warning, live wasp, aiming/striking frog, molting), falling back slowly
    /// over 10 s. Pure rise/fall logic lives in DangerMusicModel; volume is scaled by
    /// GameSettings.Volume (0 -> silence). Own child AudioSource so the spider's own
    /// AudioSource (BirdAlarmCall) stays free.
    /// </summary>
    [DisallowMultipleComponent]
    public class DangerMusic : MonoBehaviour
    {
        [Tooltip("Full volume of the music bed at intensity 1 and GameSettings volume 1.")]
        [SerializeField] private float bedVolume = 0.35f;

        private readonly DangerMusicModel _model = new DangerMusicModel();

        private AudioSource _source;
        private GameSettings _settings;
        private BirdThreat _birdThreat;
        private SpiderGrowth _growth;
        private WaspSpawner _waspSpawner;
        private Frog _frog;
        private bool _frogResolved;

        /// <summary>Current intensity 0..1 (tests/telemetry).</summary>
        public float IntensityNow => _model.GetIntensity();

        /// <summary>Volume actually set on the source last frame (0 when the setting is 0).</summary>
        public float LastVolume { get; private set; }

        private void Awake()
        {
            GameObject go = new GameObject("DangerMusic_Bed");
            go.transform.SetParent(transform, false);
            _source = go.AddComponent<AudioSource>();
            _source.clip = ProceduralAudio.DangerMusicBed();
            _source.loop = true;
            _source.spatialBlend = 0f;
            _settings = Object.FindFirstObjectByType<GameSettings>();

            _birdThreat = transform.GetComponent<BirdThreat>();
            _growth = transform.GetComponent<SpiderGrowth>();
        }

        private void Update()
        {
            if (_source == null || _source.clip == null) return;

            // Danger sources: bird pre-warning, live wasp, aiming/striking frog, molt.
            bool danger =
                (_birdThreat != null && _birdThreat.Warned) ||
                (_waspSpawner != null && _waspSpawner.LiveWasp != null) ||
                FrogStrikesNow() ||
                (_growth != null && _growth.IsMolting);

            _model.Tick(danger, Time.deltaTime);

            float volumeSetting = _settings != null ? _settings.Volume : 1f;
            LastVolume = bedVolume * _model.GetIntensity() * Mathf.Clamp01(volumeSetting);
            _source.volume = LastVolume;
        }

        /// <summary>Frog lookup: typed Find ONE Mal resolved (no name-lookup, no per-frame scan).</summary>
        private bool FrogStrikesNow()
        {
            if (!_frogResolved)
            {
                _frog = Object.FindFirstObjectByType<Frog>();
                _frogResolved = true;
            }
            if (_frog == null) return false;

            FrogModel model = _frog.GetModel();
            return model.Struck || model.state == FrogModel.State.Aiming;
        }

        private void Start()
        {
            if (_waspSpawner == null)
            {
                _waspSpawner = Object.FindFirstObjectByType<WaspSpawner>();
            }
        }
    }
}
