using UnityEngine;
using UnityEngine.Events;

namespace Spinnenspiel
{
    /// <summary>
    /// T5 Vogelruf als Warnung: plays the synthetic alarm call (ProceduralAudio.BirdAlarmCall,
    /// 2 descending tones) when the bird pre-warning fires (BirdThreat.WarnFired).
    /// Exactly once per pre-warning; volume comes from GameSettings.Volume (0 -> silence).
    /// Lives on the spider next to BirdThreat.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class BirdAlarmCall : MonoBehaviour
    {
        [Tooltip("Peak volume of the alarm call at GameSettings volume 1.")]
        [SerializeField] private float baseVolume = 0.6f;

        /// <summary>Number of alarm calls played (for tests/diagnose).</summary>
        public int CallsPlayed { get; private set; }

        private AudioSource _source;
        private GameSettings _settings;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _settings = FindFirstObjectByType<GameSettings>();
            if (_settings == null)
            {
                Debug.LogWarning("BirdAlarmCall: kein GameSettings in der Scene — Ruf uses full volume.", this);
            }
        }

        private void OnEnable()
        {
            BirdThreat birdThreat = GetComponent<BirdThreat>();
            if (birdThreat == null)
            {
                Debug.LogWarning("BirdAlarmCall: Spider ohne BirdThreat — Ruf never fires.", this);
                return;
            }
            if (birdThreat.WarnFired == null)
            {
                birdThreat.WarnFired = new UnityEvent();
            }
            birdThreat.WarnFired.AddListener(OnWarned);
        }

        private void OnDisable()
        {
            BirdThreat birdThreat = GetComponent<BirdThreat>();
            if (birdThreat != null)
            {
                birdThreat.WarnFired.RemoveListener(OnWarned);
            }
        }

        /// <summary>BirdThreat.WarnFired listener: play the alarm call once.</summary>
        public void OnWarned()
        {
            AudioClip clip = ProceduralAudio.BirdAlarmCall();
            if (clip == null || _source == null)
            {
                return;
            }

            float volumeSetting = _settings != null ? _settings.Volume : 1f;
            float volume = baseVolume * Mathf.Clamp01(volumeSetting);
            LastCallVolume = volume;
            _source.PlayOneShot(clip, volume);
            CallsPlayed++;
        }

        /// <summary>Volume of the last played call (0 when the setting is 0; for tests/diagnose).</summary>
        public float LastCallVolume { get; private set; }
    }
}
