using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Hunger der Spinne (7.3a1): steigt langsam über die Zeit, sinkt beim Fressen.
    /// J2b: der HUD-Balken oben links zeigt nun Energie (via SpiderEnergy.Fraction),
    /// nicht Hunger. Hunger-Logik und SaveGame-Pfad stays unchanged.</summary>
    public class SpiderHunger : MonoBehaviour
    {
        /// <summary>0 = satt, 1 = hungernd.</summary>
        public float Hunger = 0.6f;

        /// <summary>Wie stark der Hunger pro Sekunde steigt (default: voll in ~180 s).</summary>
        public float risePerSecond = 1f / 180f;

        /// <summary>Beute fressen: Hunger um <paramref name="amount"/> (0..1) senken.</summary>
        public void Eat(float amount)
        {
            Hunger = Mathf.Clamp01(Hunger - amount);
        }

        private void Update()
        {
            Hunger = Mathf.Clamp01(Hunger + risePerSecond * Time.deltaTime);
            if (_energy == null)
            {
                _energy = GetComponent<SpiderEnergy>();
            }
        }

        // ---- J2b: HUD-Balken shows ENERGY (not Hunger). Pure math, testable without scene. ----

        /// <summary>J2b: Fill fraction 0..1 for the bar from energy 0..100 (clamped).</summary>
        public static readonly float energyMax = 100f;
        public static float BarFill(float energy)
        {
            return Mathf.Clamp01(energy / energyMax);
        }

        /// <summary>J2b: Bar color by thresholds: green >= 50 %, yellow 25-50 %, red below 25 %.
        /// Returns index 0=green,1=yellow,2=red (named constants, not magic ints in OnGUI).</summary>
        public static readonly float greenFromFraction = 0.5f;
        public static readonly float yellowFromFraction = 0.25f;
        public static int BarColorIndex(float fraction)
        {
            if (fraction >= greenFromFraction)
            {
                return 0;
            }
            if (fraction >= yellowFromFraction)
            {
                return 1;
            }
            return 2;
        }

        /// <summary>Energy component the bar reads (set by SpiderBuilder).</summary>
        [Tooltip("Energy component whose Fraction drives the HUD bar.")]
        [SerializeField] private SpiderEnergy _energy;

        /// <summary>Fraction the bar currently shows (read by the value-test).</summary>
        public float ShownFill;
        /// <summary>Color index the bar currently shows: 0 green, 1 yellow, 2 red.</summary>
        public int ShownColorIndex = -1;

        private void OnGUI()
        {
            float energy = _energy != null ? _energy.Energy : 0f;
            float fill = BarFill(energy);
            int colorIdx = BarColorIndex(fill);
            ShownFill = fill;
            ShownColorIndex = colorIdx;

            GUI.Box(new Rect(16f, 16f, 168f, 22f), "Energy");
            Color c = colorIdx == 0 ? Color.green : (colorIdx == 1 ? Color.yellow : Color.red);
            GUI.color = c;
            GUI.DrawTexture(new Rect(18f, 18f, 164f * fill, 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
