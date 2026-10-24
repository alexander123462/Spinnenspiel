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
        public static Color FogAlbedo = new Color(0.38f, 0.55f, 0.38f); // FARBE1: greener haze (player photo farbe_1: green-dominant only 16 %, RGB 60/58/45 brownish)
        public static float FogMeanFreePath = 95f; // WALD9: 60 -> 95, Nebel thins so the forest reads (messung r114: sat 0.1, green 0 %)
        public static float FogDensity = 0.004f; // WALD9: 0.012 -> 0.004, frame was blown-out flat (luma 162, bright 87 %)

        // --- Sonne: weich, gefiltert, warm-neutral; Schatten nicht ganz schwarz. ---
        public static Color SunColor = new Color(1f, 0.96f, 0.90f); // FARBE1: less warm (farbe_1 looked brown/olive; warmer light amplified brown)
        public static float SunIntensityLux = 42000f; // WALD9: 75000 -> 42000, luma mean 149 far above 45..75 target
        public static float MoonIntensityLux = 16000f; // FARBE1: 48000 machte J5-Nacht weiss (Mittel 229); Originalwert, Messung mit Nebel aus entscheidet
        public static float SunShadowStrength = 0.85f; // WALD9: back to 0.85, dark share target 25..45 % needs real shadows
        public static int SunColorTemperatureK = 5400;

        // --- Umgebungslicht (Trilight-Fallback; gruenlich-kuehl, NEAR Baseline so the Bildtests keep contrast). ---
        public static Color AmbientSky = new Color(0.30f, 0.46f, 0.40f);    // FARBE1 d3: green over blue (d2 frame went blue-grey: B 61 > G 60)
        public static Color AmbientEquator = new Color(0.30f, 0.50f, 0.32f); // FARBE1: green horizon bounce
        public static Color AmbientGround = new Color(0.12f, 0.22f, 0.10f);  // FARBE1: greener ground bounce (ground strip G only 46 vs R 42)

        // --- Belichtung fest (Auto-Histogram blew out die Scene) und Sattierung senken. ---
        public static float FixedExposureEV = 13.8f; // HDRP Fixed = EV100 (kein Multiplikator!): 1 = voellig ueberbelichtet (flache 164er Bilder, r120). Sonne 75000 lx -> ca. EV 13. Iterate per messung.txt.
        public static float SaturationPercent = -12f; // WALD2a1: mild de-sat of brown earth tones (HDRP percent, baseline was +10)

        // --- WALD6: Vegetation-Konfiguration der Waldbereich (Tropen-Typen an/aus, Haeufigkeiten). ---
        // UrwaldSceneBuilder these values onto the JungleWorldGenerator copy, so the runtime
        // world and the tests read ONE config. Baseline (all-on, factors 1) = old behaviour.
        public static bool GiantLeavesOn = false;   // WALD6: Riesenblaetter out of the forest
        public static bool TreeFernsOn = false;     // WALD6: Palmen-Baumfarne (Farn_Baum) out
        public static float SwordFernFactor = 4.0f; // WALD9: 2.6 -> 4.0, green-dominant share was only 11 %
        public static float GroundFernFactor = 4.0f; // WALD9: 2.6 -> 4.0
        public static float FernScatterFactor = 3.5f; // WALD9: 2.5 -> 3.5
        public static bool YoungConifersOn = true;
        public static float BushFactor = 1.8f;      // WALD9: 1.3 -> 1.8, more green coverage

        // --- WALD4/5: Mammutbaeume und Blaetterdach (RedwoodFactory). ---
        public static bool RedwoodsOn = true;
        public static float OldTreeShare = 1.0f;   // Anteil der alten Scan-Baeume, der bleibt
        public static bool CanopyOn = true;

        // --- WALD7: Moos an Stamm-Base und Totholz-Oberseite (one shared material, no per-object mats). ---
        public static bool MoosOn = true;           // Moos shells/cards in/ut
        public static float MoosBaseHeightM = 1.5f; // unterste Stamm-Meterage mit Moos (nach oben auslaufend)
        public static Color MoosColor = new Color(0.30f, 0.62f, 0.22f); // hellgruen, Farbton ca. 97 Grad (HSV 80..110)

        // --- WALD8c: Volumetrischer Nebel / Lichtbahnen (Volume-Fog ueber WaldLook-Parameter). ---
        public static bool VolumetricFogOn = true;        // WALD8c: Volumetric-Fog ON -> volumetrischer Nebel mit Lichtbahnen (anisotropy toward the sun).
        public static float FogAnisotropy = 0.7f;         // Streuung toward the sun -> visible light shafts (0 = neutral, 1 = strong).
    }
}
