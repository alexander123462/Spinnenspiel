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
        public static Color FogAlbedo = new Color(0.40f, 0.58f, 0.41f); // LICHT1c2: 0.36/0.52/0.37 -> 0.40/0.58/0.41. G6-Himmel Max ist die Streu-Leuchthuelle um die Sonne (klass.+volumetrischer Nebel): 181 bei Albedo g=0.52 (r104-f3), DaySkyMultiplier wirkt DAGEGEN (2.1 -> 167, r104-f4: Auto-Exposure normalisiert). Hellere Huellflaeche ist der Hebel fuer Max >200; nachts dimmt der Zyklus auf FogNightDimFactor, die Nacht bleibt davon unberuehrt (0.58x0.12=0.070 vs 0.0624).
        public static float FogNightDimFactor = 0.10f; // LICHT1c6 (Claude-Entscheidung 2026-10-29): 0.015 -> band 0.06..0.10, getastet auf 0.10. Bei 0.015 war die Nacht EXAKT schwarz (r107-f6/r108-f2: Raw 0.00E+000) — der Schleier skaliert nur die Lichtmenge, die ihn speist (seit r108-f3 speist der Mond den PBS-Himmel). Messung r108-f3 (dim 0.08, Mond-Himmel AN): Nacht-Mittel 6.0, Raw linear in dim -> 0.10 = obere Bandkante. Nachthimmel < 0.02 und Dach-Schatten On bleiben; Mond-Lux bleibt 16000 (Dach blockiert Direktlicht).
        public static float FogMeanFreePath = 140f; // LICHT1c2: 105-Experiment zurueckgenommen — G6-Himmel Max reagiert nicht auf mfp (168@140 vs 167@105), weil J5 den Volumennebel im geteilten Profil abschaltet und der Wert bis zum Urwald-Test im Prozess leakt. 105 machte aber die J5-Nacht 2x heller (Mittel 44.9 -> 85). 140 = empirisch gruener Stand (r104-f5).
        public static float FogDensity = 0.009f; // LICHT1c2: Mittag-Dichte (Dichtekurve-Faktor 1.0 im DayNightCycle). Kriterium LICHT1c2: Mittag bleibt so duenn wie LICHT1c1 (<= 0.009) — NEBEL DICHTER AM MORGEN kommt ueber die Tageszyklus-Kurve (FogFactor-Plateau 2.0x bis 0.3), nicht ueber eine global hoehere Dichte. 0.014 (r103) war ein globaler Notbehelf, um T7s StDev-Delta zu erreichen; das ist jetzt die Kurve. Nahfeld bleibt durch den dunklen Schleier + Schatten 0.92 kontrastreich (Alex: mehr Sonnenlicht).

        // --- Sonne: LICHT1c direct-light push (Alex-Entscheidung 2026-10-28: mehr Sonnenlicht). ---
        public static Color SunColor = new Color(1f, 0.96f, 0.90f); // FARBE1: less warm (farbe_1 looked brown/olive; warmer light amplified brown)
        public static float SunIntensityLux = 100000f; // LICHT1c: was DEAD CODE (effective dayLux was the DayNightCycle default 100000); now wired to dayCycle.dayLux + initial sun. Stayed at 100000: with auto-exposure everywhere, absolute lux only shifts exposure DOWN (G6-Himmel Max sank 210->186->167 at 130k/110k); the direct-light SHARE is raised via fog/ambient levers below — that IS LICHT1c's goal.
        public static float MoonIntensityLux = 32000f; // LICHT1c4: 250 -> 16000 zurueck. Die LICHT1c3-Werte (800, dann 250) waren gegen VERFÄLSCHTE Nachtrahmen kalibriert: der Zyklus kippte mitten in der Nacht auf vollen Tag (dayLengthMinutes-Einfrieren, r107-f2), deshalb war "Nacht" so hell wie Tag (Raw 4,3E-2 = Tag-Raw) und Lux-Senken half nicht. Ohne Drift gemessen: Mond 250 lx -> Nacht-Raw EXAKT 0 (weit unter dem J5-Fenster 10..45). Rechenschema bei fixer EV 10,44 und Test-Tonemap (x20+ACES): Tag-Raw 4,3E-2 -> Mittel 132,8; Nacht-Raw = Tag-Raw x Mond/Tag-Lux. Fenstermitte (~Raw 7E-3) liegt bei 16000 lx (1/6 vom Tag) -> erwartetes Nacht-Mittel ~30. Das ist der im DayNightCycle dokumentierte 16000-lx-Mond-Look (Nacht ~5-10x dunkler als Tag).
        public static float SunShadowStrength = 0.92f; // LICHT1c: 0.85 -> 0.92, deeper canopy shade under stronger sun (kontrastreicher)
        public static int SunColorTemperatureK = 5400;

        // --- Umgebungslicht (Trilight-Fallback; gruenlich-kuehl, NEAR Baseline so the Bildtests keep contrast). ---
        public static Color AmbientSky = new Color(0.30f, 0.46f, 0.40f);    // FARBE1 d3: green over blue (d2 frame went blue-grey: B 61 > G 60)
        public static Color AmbientEquator = new Color(0.30f, 0.50f, 0.32f); // FARBE1: green horizon bounce
        public static Color AmbientGround = new Color(0.12f, 0.22f, 0.10f);  // FARBE1: greener ground bounce (ground strip G only 46 vs R 42)

        // --- Belichtung fest (Auto-Histogram blew out die Scene) und Sattierung senken. ---
        public static float FixedExposureEV = 13.8f; // HDRP Fixed = EV100 (kein Multiplikator!): 1 = voellig ueberbelichtet (flache 164er Bilder, r120). Sonne 75000 lx -> ca. EV 13. Iterate per messung.txt.
        public static float SaturationPercent = -12f; // WALD2a1: mild de-sat of brown earth tones (HDRP percent, baseline was +10)
        public static float ContrastPercent = 6f; // LICHT1c: +6 HDRP-Contrast (kontrastreicher). LICHT1c2-Pruefung: der G6-Himmel-Rahmen laeuft ohne Post-Pipeline (SaveFrameHdr), Kontrast 6->2.5 aenderte Max nicht (168 -> 167, r104-f5/f7) — der 2.5-Versuch war ueberfluessig und bleibt zurueckgenommen. Die J5-Nacht-Streuung war Nichtdeterminismus (Zyklus laeuft weiter), kein Kontrast-Effekt.
        public static float DaySkyMultiplier = 1.0f; // LICHT1c2: 1.35 -> 2.1 (r104-f4) -> 1.0. Messung: G6-Max SINKT mit hoeherem Himmel-Multiplier (181@1.35, 167@2.1) — Auto-Exposure normalisiert den Mittel (~160), der Multiplier hebt den ganzen Himmel inkl. Mittel, nicht nur die Sonne. Die LICHT1c1-Kompensationsidee war falsch; 1.0 laesst den PBS-Himmel unveraendert (HEAD ohne Kontrast war 214). Der Zyklus setzt den Wert erst ab dayF>0.95 (Gate gegen Nacht-Leck, r104-f4 J5 82.6).
        public static float SkyNightMultiplier = 0.018f; // LICHT1c6 (r109): 0.010 -> 0.018, Claude-Obergrenze < 0.02. Messung r108-f5: Nacht-Mittel 6.0 bei 0.010; die Nebel-Zuschreibung (Attributions-Probe) ergibt nur ~0.3 (Nebel AUS: 6.3, Vol-Nebel AN: 3.9) — der Schleier ist NICHT der Hebel, die dynamische PBS-Himmels-Ambiente schon (skaliert linear mit dem Multiplikator): erwartet ~11 bei 0.018. FogNightDimFactor bleibt auf 0.10 (obere Claude-Bandkante 0.06..0.10), Mond-Lux 16000 (Dach blockiert Direktlicht, hochdrehen wirkt laut Auftrag nicht).

        // --- LICHT1c3: Dach-Schatten + Tagesoeffnungen (JungleWorldGenerator.BuildCanopy). ---
        public static bool CanopyShadowOn = true;   // LICHT1c3: Das Blaetterdach wirft Schatten (Lichtflecken am Boden).
        public static float CanopyGapStep = 18f;    // LICHT1c3: Rasterabstand der runden Dach-Oeffnungen in m.
        public static float CanopyGapRadius = 9f;   // LICHT1c3: Radius der Dach-Oeffnungen in m (Kernradius der Lichtflecken ~3-4 m, weicher Rand).

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
