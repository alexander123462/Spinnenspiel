using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S3 Fang-Rueckmeldung: when an insect gets caught (FlyingInsect.AnyCaught),
    /// a short Zupfton plays and — only when the spider sits in its own web — the
    /// camera shakes (CameraShake.Trigger, strength from the prey body size).
    /// Outside the web: tone only, leise. The net vibration is already handled by
    /// FlyingInsect (web.Vibrate on catch).
    /// Lives on the spider next to SpiderPredation.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FangFeedback : MonoBehaviour
    {
        [Tooltip("Peak-Abweichung in m for a fly-sized prey (scales with body size).")]
        [SerializeField] private float strengthPerFly = 0.006f;

        [Tooltip("Volume of the Zupfton when sitting in the web.")]
        [SerializeField] private float volumeInWeb = 0.5f;

        [Tooltip("Volume of the Zupfton when NOT at the web (only tone, leise).")]
        [SerializeField] private float volumeAwayFromWeb = 0.12f;

        /// <summary>Number of played Zuptons (for tests/diagnose).</summary>
        public int PlayCount { get; private set; }

        private AudioSource _source;
        private SpiderWebBuilder _builder;
        private CameraShake _shake;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _builder = GetComponent<SpiderWebBuilder>();
            _shake = FindFirstObjectByType<CameraShake>();
            if (_shake == null)
            {
                Debug.LogWarning("FangFeedback: kein CameraShake in der Scene — Wackeln geht nicht, Ton still plays.", this);
            }
        }

        private void OnEnable()
        {
            FlyingInsect.AnyCaught += OnCaught;
        }

        private void OnDisable()
        {
            FlyingInsect.AnyCaught -= OnCaught;
        }

        private void OnCaught(FlyingInsect insect, OrbWeb web)
        {
            bool atWeb = _builder != null && _builder.IsSittingInWeb && web == _builder.Web;
            PlayPluck(atWeb ? volumeInWeb : volumeAwayFromWeb);

            if (!atWeb)
            {
                return;
            }
            if (_shake != null)
            {
                _shake.Trigger(strengthPerFly * PreySizeFactor(insect));
            }
        }

        private void PlayPluck(float volume)
        {
            AudioClip clip = ProceduralAudio.Pluck();
            if (clip == null || _source == null)
            {
                return;
            }
            _source.PlayOneShot(clip, volume);
            PlayCount++;
        }

        /// <summary>Body length of the prey relative to the fly (~0.044 m), clamped 0.25..4.</summary>
        private float PreySizeFactor(FlyingInsect insect)
        {
            float maxDim = 0f;
            foreach (MeshRenderer mr in insect.GetComponentsInChildren<MeshRenderer>())
            {
                Bounds b = mr.bounds;
                maxDim = Mathf.Max(maxDim, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            }
            if (maxDim <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp(maxDim / flyReferenceSize, 0.25f, 4f);
        }

        private const float flyReferenceSize = 0.044f; // m, body length of the fly
    }
}
