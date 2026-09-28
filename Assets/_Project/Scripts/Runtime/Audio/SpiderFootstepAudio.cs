using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Spielt beim Aufsetzen jedes Fußes einen leisen, oberflächenabhängigen Tritt.
    /// Lautstärke steigt mit der Geschwindigkeit; Tonhöhe variiert leicht.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class SpiderFootstepAudio : MonoBehaviour
    {
        [SerializeField] private float baseVolume = 0.25f;
        [SerializeField] private float minInterval = 0.012f;

        private AudioSource _source;
        private SpiderLeg[] _legs = new SpiderLeg[0];
        private SpiderMovement _movement;
        private float _lastPlayTime = -1f;
        private System.Random _random = new System.Random(5);

        /// <summary>Anzahl abgespielter Tritte (für Tests/Diagnose).</summary>
        public int StepsPlayed { get; private set; }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 0.3f;
            _source.maxDistance = 6f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.dopplerLevel = 0f;
            _movement = GetComponent<SpiderMovement>();
            _legs = GetComponentsInChildren<SpiderLeg>();
        }

        private void OnEnable()
        {
            foreach (SpiderLeg leg in _legs)
            {
                leg.Planted += OnPlanted;
            }
        }

        private void OnDisable()
        {
            foreach (SpiderLeg leg in _legs)
            {
                leg.Planted -= OnPlanted;
            }
        }

        private void OnPlanted(SpiderLeg leg)
        {
            if (!leg.IsGrounded || Time.time - _lastPlayTime < minInterval)
            {
                return;
            }

            AudioClip[] clips = ProceduralAudio.Footsteps(leg.FootSurface);
            AudioClip clip = clips[_random.Next(0, clips.Length)];
            float speed01 = _movement != null ? Mathf.Clamp01(_movement.Speed / Mathf.Max(0.01f, _movement.SprintSpeed)) : 0.5f;
            _source.pitch = 0.9f + (float)_random.NextDouble() * 0.25f;
            _source.PlayOneShot(clip, baseVolume * (0.35f + 0.65f * speed01));
            _lastPlayTime = Time.time;
            StepsPlayed++;
        }
    }
}
