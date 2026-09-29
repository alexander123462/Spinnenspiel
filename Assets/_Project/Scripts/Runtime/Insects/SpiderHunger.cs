using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Hunger der Spinne (7.3a1): steigt langsam über die Zeit, sinkt beim Fressen.
    /// Zeigt oben links einen Balken (grün = satt, rot = hungrig).
    /// </summary>
    public class SpiderHunger : MonoBehaviour
    {
        /// <summary>0 = satt, 1 = hungernd.</summary>
        public float Hunger = 0.6f;

        /// <summary>Wie stark der Hunger pro Sekunde steigt (default: voll in ~180 s).</summary>
        public float risePerSecond = 1f / 180f;

        private void Update()
        {
            Hunger = Mathf.Clamp01(Hunger + risePerSecond * Time.deltaTime);
        }

        /// <summary>Beute fressen: Hunger um <paramref name="amount"/> (0..1) senken.</summary>
        public void Eat(float amount)
        {
            Hunger = Mathf.Clamp01(Hunger - amount);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(16f, 16f, 168f, 22f), "Hunger");
            GUI.color = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(Hunger));
            GUI.DrawTexture(new Rect(18f, 18f, 164f * Mathf.Clamp01(Hunger), 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
