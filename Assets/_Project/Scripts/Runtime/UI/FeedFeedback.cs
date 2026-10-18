using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S4 Fress-Rueckmeldung: when the spider eats prey (SpiderPredation.PreyEaten),
    /// the energy bar flashes brighter for flashDuration s, a short synthetic
    /// Crack/Schmatz plays (ProceduralAudio) and "+25" shows briefly at the screen
    /// edge. The bar flash itself is drawn by SpiderHunger.OnGUI via BarFlash(t).
    /// Lives on the spider next to FangFeedback.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FeedFeedback : MonoBehaviour
    {
        [Tooltip("Duration of the bright bar flash in seconds (BarFlash(0)=1, BarFlash(duration)=0).")]
        [SerializeField] private float flashDuration = 0.4f;

        [Tooltip("Volume of the Crack/Schmatz tone at the eating spider.")]
        [SerializeField] private float toneVolume = 0.5f;

        /// <summary>Number of played eat tones (for tests/diagnose).</summary>
        public int PlayCount { get; private set; }

        /// <summary>Seconds since the last eaten prey, -999 before the first eat.</summary>
        public float TimeSincePreyEaten = -999f;

        /// <summary>Last eaten prey (for tests/diagnose).</summary>
        public FlyingInsect LastPrey { get; private set; }

        /// <summary>The "+25" text the HUD shows right now (empty when no flash window).</summary>
        public string FlashLabel = "";

        private SpiderPredation _predation;
        private AudioSource _source;

        /// <summary>Gained energy of the last eaten prey, captured at eat time.
        /// The prey GameObject is destroyed inside Remove() before the event fires,
        /// so the value must be read synchronously here, not from LastPrey later.</summary>
        private int _lastEnergyValue;

        /// <summary>True once at least one prey was eaten (so the label has a value to show).</summary>
        private bool _hasPrey;

        private void OnEnable()
        {
            // GetComponent here, not in Awake: OnEnable can run before Awake at instantiation.
            _predation = transform.GetComponent<SpiderPredation>();
            if (_predation != null)
            {
                _predation.PreyEaten += OnPreyEaten;
            }
        }

        private void OnDisable()
        {
            if (_predation != null)
            {
                _predation.PreyEaten -= OnPreyEaten;
            }
        }

        /// <summary>Prey eaten: start flash window, show the energy gain, play the tone once.</summary>
        private void OnPreyEaten(FlyingInsect prey)
        {
            if (prey == null)
            {
                return;
            }
            LastPrey = prey;
            TimeSincePreyEaten = 0f;
            // The prey GameObject is destroyed inside Remove() (called before this handler
            // fires), so read the energy synchronously here and keep it for the label.
            _lastEnergyValue = Mathf.RoundToInt(prey.energyValue);
            _hasPrey = true;
            PlayCrunch(toneVolume);
        }

        private void Update()
        {
            if (TimeSincePreyEaten >= 0f)
            {
                TimeSincePreyEaten += Time.deltaTime;
            }

            // The label follows the flash window: inside it shows "+<energy>", else empty.
            FlashLabel = BarFlash(TimeSincePreyEaten, flashDuration) > 0f && _hasPrey
                ? "+" + _lastEnergyValue.ToString()
                : "";
        }

        private void PlayCrunch(float volume)
        {
            AudioClip clip = ProceduralAudio.Crunch();
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }
            if (clip == null || _source == null)
            {
                return;
            }
            _source.PlayOneShot(clip, volume);
            PlayCount++;
        }

        /// <summary>Reine Math: Flash intensity 0..1 at t seconds after the eat.
        /// 1 at t=0, falls linearly to 0 at t=duration, 0 outside the window.</summary>
        public static float BarFlash(float t, float duration)
        {
            if (t <= 0f)
            {
                return t >= 0f ? 1f : 0f;
            }
            if (t >= duration)
            {
                return 0f;
            }
            return 1f - t / duration;
        }

        /// <summary>Seconds since the last eat for the HUD bar flash (-1: no flash).</summary>
        public static float BarFlashTime(float timeSincePreyEaten, float duration)
        {
            if (timeSincePreyEaten >= 0f && timeSincePreyEaten <= duration)
            {
                return timeSincePreyEaten;
            }
            return -1f;
        }

        /// <summary>Current flash intensity 0..1 (read by SpiderHunger.OnGUI and the value-test).</summary>
        public float CurrentBarFlash()
        {
            return BarFlash(TimeSincePreyEaten, flashDuration);
        }

        /// <summary>S4: draw the "+25" gain briefly at the screen edge during the flash window.</summary>
        private void OnGUI()
        {
            if (FlashLabel.Length == 0)
            {
                return;
            }
            GUI.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * CurrentBarFlash());
            GUI.Label(new Rect(200f, Screen.height - 36f, 120f, 24f), FlashLabel);
            GUI.color = Color.white;
        }
    }
}
