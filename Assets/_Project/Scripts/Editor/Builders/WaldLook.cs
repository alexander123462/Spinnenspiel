using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// WALD2: Farbstimmung und Licht der Mooswald (Referenz Docs/referenz/WALD-ZIEL.md).
    /// EENAKT Konfiguration an EINER Stelle: Nebelfarbe, Sonnenfarbe/-staerke,
    /// Umgebungslicht, Belichtung und Sattierung. WorldLightingBuilder und (nachher)
    /// WALD3+ read here; no magic numbers scattered in Builders.
    /// </summary>
    public static class WaldLook
    {
        // --- Nebel (T7-Morgennebel bleibt: meanFreePath-Basewert, DayNightCycle scales it). ---
        public static Color FogAlbedo = new Color(0.64f, 0.73f, 0.68f); // gruenlich-kuehl PADER bright (baseline ~0.75): a dark fog washes the Nahaufnahme (SharpFrac->0)
        public static float FogMeanFreePath = 60f; // Base-Nebeldistanz (Noon)
        public static float FogDensity = 0.012f; // A2-Base (T7-Curve multiplies); WALD2 keeps Nebeldense, only colors move

        // --- Sonne: weich, gefiltert, warm-neutral; Schatten nicht ganz schwarz. ---
        public static Color SunColor = new Color(1f, 0.95f, 0.86f);
        public static float SunIntensityLux = 75000f; // 100000 -> softer (Sonne gefiltret); not too low or the Bildtests lose contrast
        public static float SunShadowStrength = 0.78f; // 0.85 -> Schatten verioever, nicht schwarz
        public static int SunColorTemperatureK = 5400;

        // --- Umgebungslicht (Trilight-Fallback; gruenlich-kuehl, NEAR Baseline so the Bildtests keep contrast). ---
        public static Color AmbientSky = new Color(0.48f, 0.58f, 0.64f);    // Baseline 0.50/0.58/0.70: cooler sky
        public static Color AmbientEquator = new Color(0.48f, 0.56f, 0.48f); // Baseline 0.52/0.54/0.50: greener horizon
        public static Color AmbientGround = new Color(0.20f, 0.21f, 0.15f);  // Baseline 0.22/0.20/0.16: slightly greener ground

        // --- Belichtung fest (Auto-Histogram blew out die Scene) und Sattierung senken. ---
        public static float FixedExposureEV = 13.8f; // HDRP Fixed = EV100 (kein Multiplikator!): 1 = voellig ueberbelichtet (flache 164er Bilder, r120). Sonne 75000 lx -> ca. EV 13. Iterate per messung.txt.
        public static float SaturationPercent = -12f; // WALD2a1: mild de-sat of brown earth tones (HDRP percent, baseline was +10)
    }
}
