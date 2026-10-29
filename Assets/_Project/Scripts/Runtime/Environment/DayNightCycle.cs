using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.HighDefinition;

namespace Spinnenspiel
{
    /// <summary>
    /// Tag/Nacht-Cycle: DIRECTIONAL-SUN rotates around the world slowly,
    /// adjusts its brightness and Nebel according to the altitude of the sun,
    /// updates the dynamic ambient light (from Skybox) at regular intervals.
    /// The day length is a configuration value (default 20 min per day).
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        /// <summary>The directional light set by the Builder.</summary>
        public Light sun;

        /// <summary>Duration of one full day in minutes.</summary>
        [Tooltip("Duration of one full day/night cycle in minutes")]
        public float dayLengthMinutes = 20f;

        /// <summary>Brightness of the sun at noon (Lux, HDRP).</summary>
        [Tooltip("Sun brightness at noon in Lux")]
        public float dayLux = 100000f;

        /// <summary>Brightness during night/moonlight (Lux).</summary>
        [Tooltip("Moon-like brightness at night in Lux")]
        public float moonLux = 50000f;

        /// <summary>Interval for updating the dynamic ambient light in seconds.</summary>
        [Tooltip("Interval for DynamicGI.UpdateEnvironment in seconds")]
        public float ambientUpdateSeconds = 2f;

        /// <summary>Progress of the day 0..1 (0 = midnight, 0.5 = noon). Read-only for Tests.</summary>
        public float DayProgress;

        /// <summary>J5: Brightness factor 0 (night) .. 1 (noon). Drives light AND insect activity.</summary>
        public float CurrentDayFactor;

        /// <summary>J5: Night factor 1 (deep night) .. 0 (full day). Ramps over transitionMinutes at dawn/dusk.</summary>
        public float CurrentNightFactor;

        /// <summary>S5 Tagesbilanz: fires exactly once when the cycle crosses sunrise (night -> morning).
        /// Initialized explicitly so runtime-created components (tests) can hook listeners.</summary>
        public UnityEvent MorningFired;

        /// <summary>J5: Minutes of the cycle that are dark (night core, ramps excluded).
        /// LICHT1c4-Wirkung: das ist eine ABSOLUTE Minutenzahl. Der Nachtanteil in der
        /// Fortschritts-Skala ist nightMinutes/dayLengthMinutes — wer den Zyklus ueber
        /// dayLengthMinutes "einfriert" (z. B. 10000 min), schrumpft das Nachtfenster auf
        /// +/-0.0003 Fortschritt und die Daemmerungsrampe auf 4e-6: der Zyklus schnappt beim
        /// naechsten Frame von Nacht auf vollen Tag (Tag/Nacht-Proben r107-f2: dayFactor 0 ->
        /// 1, sunLux 250 -> 100000, Himmels-Multiplikator 0.003 -> 1, Nebelfarbe 0.006 -> 0.40
        /// bei Fortschritt 0.0003 = kurz nach Mitternacht). Fuer Messungen deshalb
        /// <see cref="SetHoldDayProgressForTests"/> benutzen, nie dayLengthMinutes.</summary>
        [Tooltip("Minutes per cycle that are dark (night core, ramps excluded)")]
        [SerializeField] private float nightMinutes = 6f;

        /// <summary>LICHT1c4: wenn true, schiebt Update() den Tagesfortschritt nicht mehr weiter
        /// (Licht, Nebel und Himmel werden weiterhin jeden Frame gesetzt). Haelte-Modus fuer
        /// Messungen und Screenshot-Werkzeuge, damit der Zyklus waehrend langer Aufnahmen nicht
        /// aus der Nacht laeuft. Aus = normales Spiel.</summary>
        [Tooltip("Hold the day progress (measurements/screenshots); light and fog keep updating")]
        [SerializeField] private bool holdDayProgress;

        /// <summary>J5: Width in minutes of each dawn/dusk ramp (transition not abrupt).</summary>
        [Tooltip("Width in minutes of each dawn/dusk transition ramp")]
        public float transitionMinutes = 2f;

        /// <summary>Day progress at game start (default morning, 0.3 = just after sunrise).</summary>
        [Tooltip("Day progress at game start (0 = midnight, 0.5 = noon)")]
        [SerializeField] private float startDayFraction = 0.3f;

        private float dayFraction;
        private float ambientTimer;
        private float lastDayFraction; // S5: previous fraction for the sunrise-crossing check.

        // Night color as configuration value (HDRP: Fog-Albedo lives in the Volume,
        // the fog color itself is not changed at runtime).
        [Tooltip("Sun tint at night (moonlight)")]
        private Color moonTint = new Color(0.8f, 0.85f, 1f);

        // WALD2a3: daytime sun color from the Builder (WaldLook.SunColor), so the runtime
        // cycle does not overwrite it with a hardcoded value. Default stays at the old
        // baseline so runtime-created components (tests) keep their behaviour.
        [Tooltip("Sun tint during day (from WaldLook)")]
        public Color daySunColor = new Color(1f, 0.93f, 0.82f);

        // T7 Morgennebel / LICHT1c2: Nebeldichte folgt dem Tageslauf. Der Faktor multipliziert
        // die Mittag-Dichte. Stueckweise lineare Keys halten die Kurve stetig (kein Sprung > 0.05
        // auf feinem Raster). FRUEHER: Plateau nur 0.05..0.2 bei 2.0x — um 0.3 war der Morgennebel
        // schon fast weg, bei gleichem Sonnenstand (0.3 vs. 0.7) blieb kein messbarer Unterschied
        // (StDev-Delta 0.90). JETZT: Plateau bis 0.3 bei 3.0x, Anstieg geramppt von 0 bis 0.15
        // (Der Kurventest misst Stetigkeit: <0.05 Sprung pro 0.002-Schritt — die 0.05-Rampe mit
        // Sprung 1->3 ergab 0.08 und warf den Test), Ausloesen bis 0.45, Mittag/Nachmittag 1.0,
        // Abendbuckel 1.4 bei 0.75.
        private static readonly float[] fogKeysT = { 0f, 0.15f, 0.3f, 0.45f, 0.7f, 0.75f, 0.85f, 0.9f, 1f };
        private static readonly float[] fogKeysV = { 1f, 3f, 3f, 1f, 1f, 1.4f, 1.4f, 1f, 1f };

        /// <summary>T7: Nebelfactor at a day progress (0 = midnight, 0.5 = noon).
        /// Ramps from midnight to 3.0x noon by 0.15, plateau to 0.3, dissolves by late morning
        /// (0.45), noon/afternoon 1.0x, evening 1.4x, night ~1.0x.</summary>
        public static float FogFactor(float dayProgress)
        {
            float t = dayProgress - Mathf.Floor(dayProgress);
            for (int i = 0; i < fogKeysT.Length - 1; i++)
            {
                if (t <= fogKeysT[i + 1])
                {
                    float span = fogKeysT[i + 1] - fogKeysT[i];
                    float u = span > 0f ? (t - fogKeysT[i]) / span : 0f;
                    return Mathf.Lerp(fogKeysV[i], fogKeysV[i + 1], u);
                }
            }
            return fogKeysV[fogKeysV.Length - 1];
        }

        /// <summary>T7: The HDRP Fog volume driven by the cycle (its meanFreePath is
        /// divided by the fog factor: smaller path = denser fog). Set by the Builder.</summary>
        [Tooltip("HDRP Fog volume whose meanFreePath follows the day (set by the Builder)")]
        public Fog fogVolumeComponent;

        private float fogBaseMeanFreePath = -1f; // baseline read once from the volume
        private float fogBaseDensity = -1f; // baseline RenderSettings.fogDensity (visible when volumetric off)
        private bool fogMissingWarned;

        /// <summary>LICHT1c2: Mittag-Dichte (Faktor 1.0 der Nebelkurve) vom Builder gesetzt
        /// (WaldLook.FogDensity). RenderSettings.fogDensity ist ein Prozess-globaler Wert und
        /// bleibt nach vorangelaufenen PlayMode-Tests skaliert zurueck — wer ihn beim Start
        /// abliest, erbt die Kontamination (Voll-Lauf vs. gezielte Auswahl maass dann
        /// Unterschiedliches). Mit Builder-Wert schreibt der Zyklus jede Frame ABSOLUT
        /// (Dichte = noonFogDensity x Kurve) und selbstheilend. -1 = Verhalten wie bisher
        /// (Baseline bei Start aus RenderSettings lesen; Runtime-erzeugte Komponenten).</summary>
        [Tooltip("Noon (factor 1.0) fog density from the builder; -1 reads RenderSettings at start")]
        public float noonFogDensity = -1f;

        /// <summary>LICHT1c2: Tag-Nebelfarbe (Huellflaeche, Faktor 1) vom Builder gesetzt
        /// (WaldLook.FogAlbedo). Der Zyklus dimmt sie nachts pro Frame auf fogNightDimFactor.
        /// Ohne Builder-Wert wurde RenderSettings.fogColor einmalig abgelesen — nach einem
        /// Test, der nachts endete, kam dabei die NACHTLICHE (gedimmte) Farbe als Tag-Baseline
        /// heraus (G6-Himmel Max 214 -> 183 im Voll-Lauf). -1 = altes Verhalten.</summary>
        [Tooltip("Day fog colour from the builder; -1 reads RenderSettings at start")]
        public Color dayFogColor = new Color(-1f, -1f, -1f);

        /// <summary>ROTE1-NEBEL1: Mittag-Lichtpfad des Nebelvolumens (Faktor 1.0 der Kurve)
        /// vom Builder gesetzt (WaldLook.FogMeanFreePath). Wie bei noonFogDensity/dayFogColor
        /// ist der Volume-Override ein Prozess-globaler Wert: Start() las ihn einmal aus dem
        /// geteilten Profil — nach vorangelaufenen PlayMode-Tests kam darin ein bereits
        /// skalierter Wert zurueck, und jede neue Zyklus-Instanz teilte ihn erneut durch die
        /// Kurve. Messung r104: Lichtpfad 0,00 m statt 46,7 (Tagebilder fast schwarz,
        /// StDev noon 12,3). Mit Builder-Wert schreibt der Zyklus den Pfad jede Frame
        /// ABSOLUT (noonMeanFreePath / Kurve) und selbstheilend. -1 = Einmalablesen wie bisher.</summary>
        [Tooltip("Noon (factor 1.0) fog meanFreePath from the builder; -1 reads the volume at start")]
        public float noonFogMeanFreePath = -1f;

        /// <summary>FARBE1a: The HDRP PhysicallyBasedSky built by the Builder. The sky renders a
        /// full-bright DAY sky whenever the main light sits above the horizon — and the cycle
        /// parks the "moon" above the horizon at night (J5). Without this link the night frame
        /// shows a bright day sky (J5 night was even brighter than noon). The cycle drives the
        /// sky's multiplier with the day factor, so the sky goes dark with the night.</summary>
        [Tooltip("HDRP PhysicallyBasedSky whose brightness follows the day factor (set by the Builder)")]
        public PhysicallyBasedSky skyVolume;

        /// <summary>FARBE1a: The HDRP Exposure component of the scene volume. Auto-exposure
        /// (AutomaticHistogram) normalises day AND night to mid-grey, which washes out the
        /// day/night difference in fixed-exposure captures. Tests switch it to Fixed via
        /// SetFixedExposureForTests.</summary>
        [Tooltip("HDRP Exposure component (tests switch it to fixed for reproducible captures)")]
        public Exposure exposureVolume;

        /// <summary>FARBE1a: Sky multiplier at deep night. 0.02 keeps the sky a dim twilight
        /// glow (moon-lit scene reads dark but not pitch black): measured day/night raw ratio
        /// at 0.003 was ~64 (night Mittel 2.2 — below the 10..45 window); the sky (via dynamic
        /// ambient) dominates the frame, so 0.02 lands night at ~20-25 (ratio ~6, matches the
        /// 16000 lx moonlight design where night is ~5-10x darker than day).</summary>
        [Tooltip("Sky brightness multiplier at deep night (0 = pitch black)")]
        public float skyNightMultiplier = 0.018f; // LICHT1c6 (r109): Builder setzt WaldLook.Wert (0.018, Obergrenze < 0.02); Default nur Konsistenz.

        /// <summary>LICHT1c1: Multiplier fuer die klassische Nebelfarbe (RenderSettings.fogColor)
        /// bei tiefster Nacht. Die helle Tag-Huellflaeche (FogAlbedo) macht die Ferne auch nachts
        /// hell (J5-Nacht Mittel 77 > 45-Fenster,mondleuchtende Ferne ueber den Schleier). Der
        /// Zyklus dimmt die Nebelfarbe mit dem Tagesfaktor: tags 1x, nachts dieser Faktor.
        /// Vom Builder aus WaldLook.FogNightDimFactor gesetzt.</summary>
        [Tooltip("Fog colour multiplier at deep night (1 = day veil stays, 0 = black distance)")]
        public float fogNightDimFactor = 0.08f; // LICHT1c6: Builder setzt WaldLook.Wert (0.08); Default nur Konsistenz.

        /// <summary>LICHT1c1: Heller-Multiplikator des PBS-Himmels bei vollem Tageslicht.
        /// Der +6-Kontrast (ColorAdjustments, wirkt im HDR-Pfad der Beweisfotos) dimmt die
        /// Sonnen-Scheibe auf dem G6-Himmel 214 -> 188 (<200-Fenster). Der Himmel wird
        /// ueber seinen Multiplier kompensiert (nur Himmel+Ambient, nicht die
        /// Direktlicht-Geometrie) — vom Builder aus WaldLook.DaySkyMultiplier gesetzt.</summary>
        [Tooltip("Sky brightness multiplier at full day (1 = old behaviour)")]
        public float daySkyMultiplier = 1f;

        private Color fogBaseColor = new Color(-1f, -1f, -1f); // baseline RenderSettings.fogColor (read once)

        // LICHT1c3: Backlog-Forderung "Nachts duerfen Oeffnungen Mond nicht als Scheibe
        // durchlassen (Himmelsscheibe ausblenden)". Die Szenen-Sonne steht nachts als
        // "Mond" UEBER dem Horizont (sonst waere die Nacht pechschwarz) und HDRPs
        // PhysicallyBasedSky malt dazu die Himmelskoerper-Scheibe aus der Lichtfarbe
        // (FillCelestialBodyData). Bei ShadingSource Emission ist das eine Leucht-
        // scheibe mit Radiance = Lux / Raumwinkel — nachts bei 8000 lx immer noch eine
        // weisse Scheibe (r105-Beweis: Nachtrahmen == Tagesrahmen, Max 253). Der Zyklus
        // schaltet die Scheibe deshalb am Tag auf Emission (PBS-Sonnenkern fuer G6) und
        // in der Nacht auf ReflectSunLight (dunkle Scheibe, Mondlicht bleibt als gerichtete
        // Beleuchtung erhalten).
        private HDAdditionalLightData hdSun;
        private bool hdSunFetched;

        /// <summary>LICHT1c3: Applies the celestial body disc mode for the current day
        /// factor: Emission (bright sun disc) at full day, no sky participation at all from
        /// dusk onward. No-op if the light has no HDAdditionalLightData.</summary>
        private void ApplyCelestialDisk()
        {
            if (sun == null) return;
            if (!hdSunFetched)
            {
                hdSun = sun.GetComponent<HDAdditionalLightData>();
                hdSunFetched = true;
            }
            if (hdSun == null) return;
            // LICHT1c3 (r106-f5): ReflectSunLight-Experiment — damals noch MIT
            // dayLengthMinutes-Drift gemessen (Nacht-Rahmen waren versteckte Tagesrahmen,
            // "Schaettigung" durch die helle Tag-Nebelfarbe, nicht die Scheibe). LICHT1c6
            // (Claude 2026-10-29, Entscheidung Nacht-Nebelhelligkeit): der Nachthimmel < 0.02
            // und Dach-Schatten On bleiben, aber der Mond muss den PBS-Himmel speisen —
            // ohne jede Himmelsbeteiligung ist die Nacht EXAKT schwarz (r108-f2: Nebelfarbe
            // 0.0464, alle Proben Raw 0.00E+000), und der Nebel-Schleier skaliert nur die
            // LICHTMENGE, die ihn speist (0.015 -> 0.08 aenderte am Schwarzbild nichts).
            // Nachts daher: Himmelsbeteiligung AN, aber Schattungsquelle ReflectSunLight
            // (dunkle Scheibe statt Leuchtkugel). Der Nachthimmel-Multiplikator (WaldLook
            // 0.003 < 0.02) begrenzt die Helligkeit.
            bool day = CurrentDayFactor >= 0.5f;
            hdSun.interactsWithSky = true;
            hdSun.celestialBodyShadingSource = day
                ? HDAdditionalLightData.CelestialBodyShadingSource.Emission
                : HDAdditionalLightData.CelestialBodyShadingSource.ReflectSunLight;
        }

        /// <summary>LICHT1c3: Tests-only hook: applies the disc mode for a given day
        /// fraction (0 = midnight -> Reflection/dark, 0.5 = noon -> Emission/bright).
        /// Returns false when the light has no HDAdditionalLightData.</summary>
        public bool SetCelestialDiskForTests(float dayFractionTest)
        {
            if (sun == null) return false;
            if (!hdSunFetched)
            {
                hdSun = sun.GetComponent<HDAdditionalLightData>();
                hdSunFetched = true;
            }
            if (hdSun == null) return false;
            float saved = CurrentDayFactor;
            CurrentDayFactor = dayFractionTest;
            ApplyCelestialDisk();
            CurrentDayFactor = saved;
            return true;
        }

        /// <summary>J5: dayFactor (0 = deep night, 1 = full day) for a day fraction with explicit
        /// dawn/dusk ramps. Pure function so EditMode tests can pin the ramp shape (and the
        /// SetDayProgress shortcut uses the SAME curve as Update). nightMinutes is passed in
        /// because the field is private.</summary>
        public static float DayFactorAt(float dayFraction, float lengthMinutes, float transitionMinutes, float nightMinutes)
        {
            float rampFrac = Mathf.Max(transitionMinutes, 0.01f) / lengthMinutes;
            float coreMinutes = Mathf.Min(nightMinutes, Mathf.Max(lengthMinutes - 2f * transitionMinutes, 0f));
            float coreFrac = coreMinutes / lengthMinutes;
            float distFromMidnight = Mathf.Min(dayFraction, 1f - dayFraction);
            float dayFactor = distFromMidnight <= coreFrac * 0.5f ? 0f : 1f;
            if (distFromMidnight > coreFrac * 0.5f && rampFrac > 0f)
            {
                dayFactor = Mathf.Min(1f, (distFromMidnight - coreFrac * 0.5f) / rampFrac);
            }
            return dayFactor;
        }

        /// <summary>T7: The current scaled meanFreePath of the wired fog volume, or -1 when
        /// no fog volume is wired. Exposed as a plain float so Tests (no HDRP dependency)
        /// can assert the curve really drives the scene's fog.</summary>
        public float CurrentFogMeanFreePath()
        {
            if (fogVolumeComponent == null || !fogVolumeComponent.meanFreePath.overrideState)
            {
                return -1f;
            }
            return fogVolumeComponent.meanFreePath.value;
        }

        /// <summary>T7: The baseline (noon) meanFreePath captured from the volume, or -1
        /// when no fog volume is wired.</summary>
        public float CurrentFogBaseline()
        {
            return fogBaseMeanFreePath;
        }

        private void Start()
        {
            dayFraction = startDayFraction - Mathf.Floor(startDayFraction);
            DayProgress = dayFraction;
            lastDayFraction = dayFraction;
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null)
                {
                    Debug.LogWarning("[Spinnenspiel] DayNightCycle: sun not set and RenderSettings.sun empty.");
                }
            }
            // T7: capture the baseline fog distance once so the runtime curve scales it.
            // ROTE1-NEBEL1: nur ohne Builder-Baseline (noonFogMeanFreePath < 0) — das
            // Einmalablesen aus dem geteilten Profil erbt sonst Test-Kontamination.
            if (noonFogMeanFreePath < 0f
                && fogVolumeComponent != null && fogVolumeComponent.meanFreePath.overrideState)
            {
                fogBaseMeanFreePath = fogVolumeComponent.meanFreePath.value;
            }
            if (RenderSettings.fog)
            {
                fogBaseDensity = RenderSettings.fogDensity;
            }
        }

        /// <summary>WALD8c: Tests-only switch of the volumetric fog in the wired volume (plain bool
        /// so the test assembly needs no HDRP reference). False when no volume is wired.</summary>
        public bool SetVolumetricFogForTests(bool on)
        {
            if (fogVolumeComponent == null || !fogVolumeComponent.enableVolumetricFog.overrideState) return false;
            fogVolumeComponent.enableVolumetricFog.Override(on);
            return true;
        }

        /// <summary>FARBE1a: Tests-only switch of the wired HDRP Exposure to Fixed with the given
        /// EV. Auto-exposure normalises night up to day brightness (and adapts over frames),
        /// which destroys the fixed-exposure day/night measurement. False when no volume is wired.</summary>
        public bool SetFixedExposureForTests(float ev)
        {
            if (exposureVolume == null) return false;
            exposureVolume.mode.Override(ExposureMode.Fixed);
            exposureVolume.fixedExposure.Override(ev);
            return true;
        }

        /// <summary>LICHT1c2: zurueck auf Automatik-Histogramm. Der Modus lebt auf dem geteilten
        /// Profil-Component und bleibt im PlayMode-Prozess auch nach Szenenwechsel Fixed stehen —
        /// ein Test, der (wie T7) Auto-Exposure voraussetzt, mass sonst mit gedimmtem dayLux
        /// schwarze Frames (Mittel 0.0), wenn ein frueherer Test (J5) auf Fixed EV kalibriert hat.</summary>
        public bool SetAutoExposureForTests()
        {
            if (exposureVolume == null) return false;
            exposureVolume.mode.Override(ExposureMode.AutomaticHistogram);
            return true;
        }

        /// <summary>FARBE1a: The current sky multiplier (1 = full day sky), or -1 when no sky
        /// volume is wired. Plain float so Tests (no HDRP reference) can log the sky source.</summary>
        public float CurrentSkyMultiplier()
        {
            return skyVolume == null ? -1f : skyVolume.multiplier.value;
        }

        /// <summary>LICHT1c4: Tests-/Foto-Modus — Tagesfortschritt halten (true) oder laufen
        /// lassen (false). Ersetzt den alten Trick, dayLengthMinutes auf 10000 zu stellen:
        /// nightMinutes und transitionMinutes sind ABSOLUTE Minuten, der Nachtkern einer
        /// 10000-Minuten-Tage ist darum nur noch +/-0.0003 Fortschritt breit — der Zyklus
        /// schnappte mitten in der Nacht auf vollen Tag (r107-f2: dayFactor 0 -> 1 bei
        /// Fortschritt 0.0003). Bei unveraenderter Tageslaenge bleibt das Nachtfenster breit.
        /// False beim Zuruecksetzen, damit nichts haengen bleibt.</summary>
        public void SetHoldDayProgressForTests(bool hold)
        {
            holdDayProgress = hold;
        }

        /// <summary>FARBE1a: Drives the PhysicallyBasedSky brightness with the day factor.
        /// The HDRP sky follows the MAIN LIGHT elevation; the cycle parks the moon ABOVE the
        /// horizon at night (J5, otherwise the scene would be pitch black), so without this link
        /// the sky keeps rendering a full-bright day sky at night. Multiplier 1 at noon matches
        /// the previous EV-0 baseline; deep night goes to skyNightMultiplier (near black), which
        /// also darkens the dynamic ambient (SkyAmbientMode.Dynamic derives from the sky).</summary>
        private void ApplySky()
        {
            if (skyVolume == null) return;
            // LICHT1c2: der Tages-Multiplier (Kompensation fuer den Look) darf NICHT schon bei
            // kleinem dayF durch den Lerp lecken — J5-Nacht bei dayFactor 0.025 bekam mit
            // daySkyMultiplier 2.1 ein 1.5x helleren Nachthimmel (Nacht-Mittel 50.7 -> 82.6,
            // r104-f3/f4). Grundkurve bis dayF 1 auf 1.0; der Tages-Boost setzt erst ab
            // echter Tagesspitze (dayF > 0.95) ein.
            float m = Mathf.Lerp(skyNightMultiplier, 1f, CurrentDayFactor);
            float boost = Mathf.InverseLerp(0.95f, 1f, CurrentDayFactor);
            if (boost > 0f)
            {
                m = Mathf.Lerp(m, daySkyMultiplier, boost);
            }
            skyVolume.multiplier.Override(m);
        }

        /// <summary>Tests/Screenshot: Jump to a day progress (0 = midnight, 0.5 = noon).</summary>
        public void SetDayProgress(float progress)
        {
            dayFraction = progress - Mathf.Floor(progress);
            DayProgress = dayFraction;
            // S5: a Test-Jump is not a natural sunrise — no event, only the baseline moves.
            lastDayFraction = dayFraction;
            ApplyFog();
            // FARBE1a: recompute the light/sky state for the jumped fraction immediately,
            // so a capture right after the jump already shows the new time of day.
            float lengthMinutes = dayLengthMinutes > 0.01f ? dayLengthMinutes : 0.01f;
            CurrentDayFactor = DayFactorAt(dayFraction, lengthMinutes, Mathf.Max(transitionMinutes, 0.01f), nightMinutes);
            CurrentNightFactor = 1f - CurrentDayFactor;
            if (sun != null)
            {
                float phase = (dayFraction - 0.25f) * Mathf.PI * 2f;
                float elevDeg = Mathf.Asin(Mathf.Sin(phase)) * Mathf.Rad2Deg;
                float azDeg = phase * Mathf.Rad2Deg + 90f;
                float elevLight = elevDeg >= 0f ? elevDeg : -elevDeg;
                float azLight = elevDeg >= 0f ? azDeg : azDeg + 180f;
                sun.transform.rotation = Quaternion.Euler(elevLight, azLight, 0f);
                sun.intensity = Mathf.Lerp(moonLux, dayLux, CurrentDayFactor);
                sun.color = Color.Lerp(daySunColor, moonTint, 1f - CurrentDayFactor);
                ApplyCelestialDisk(); // LICHT1c3: Himmelscheibe tags Emission, nachts aus.
            }
            ApplySky();
        }

        private void Update()
        {
            float lengthMinutes = dayLengthMinutes > 0.01f ? dayLengthMinutes : 0.01f;
            // LICHT1c4: im Haelte-Modus bleibt der Fortschritt stehen (Messungen, Fotos).
            // Licht/Nebel/Himmel unten werden trotzdem weitergeschrieben, damit ein vorher
            // gesetzter Tag/Sprung sofort wirksam bleibt und bleibt.
            if (!holdDayProgress)
            {
                dayFraction += Time.deltaTime / (lengthMinutes * 60f);
                if (dayFraction >= 1f) dayFraction -= 1f;
                DayProgress = dayFraction;
            }

            // S5: sunrise at 0.25 — the event fires only on a natural forward crossing
            // (not on wrap-around midnight, not on a Test-Jump via SetDayProgress).
            if (lastDayFraction < 0.25f && dayFraction >= 0.25f)
            {
                if (MorningFired != null)
                {
                    MorningFired.Invoke();
                }
            }
            lastDayFraction = dayFraction;

            ApplyFog();

            if (sun == null) return;

            // Phase: t=0 midnight -> -90°, t=0.25 sunrise, t=0.5 noon +90°, t=0.75 sunset.
            float phase = (dayFraction - 0.25f) * Mathf.PI * 2f;
            float elevDeg = Mathf.Asin(Mathf.Sin(phase)) * Mathf.Rad2Deg;
            float azDeg = phase * Mathf.Rad2Deg + 90f;

            // J5: at night the light acts as a MOON above the horizon (a sun below the
            // horizon lights nothing and would leave the scene pitch black).
            float elevLight = elevDeg >= 0f ? elevDeg : -elevDeg;
            float azLight = elevDeg >= 0f ? azDeg : azDeg + 180f;
            sun.transform.rotation = Quaternion.Euler(elevLight, azLight, 0f);

            // J5: dayFactor from the cycle fraction with explicit dawn/dusk ramps (not abrupt):
            // night core = nightMinutes centred at midnight, ramps of transitionMinutes on both sides.
            float dayFactor = DayFactorAt(dayFraction, lengthMinutes, Mathf.Max(transitionMinutes, 0.01f), nightMinutes);
            CurrentDayFactor = dayFactor;
            CurrentNightFactor = 1f - dayFactor;
            sun.intensity = Mathf.Lerp(moonLux, dayLux, dayFactor);
            sun.color = Color.Lerp(
                daySunColor, moonTint, 1f - dayFactor);
            ApplyCelestialDisk(); // LICHT1c3: Himmelscheibe tags Emission, nachts aus.

            // FARBE1a: the sky brightness must follow the day factor (see ApplySky).
            ApplySky();

            ambientTimer += Time.deltaTime;
            if (ambientTimer >= ambientUpdateSeconds)
            {
                ambientTimer = 0f;
                DynamicGI.UpdateEnvironment();
            }
        }

        /// <summary>T7: Scales the HDRP Fog volume's meanFreePath by the fog factor.
        /// A higher factor means denser fog, so the light path gets SHORTER (divided).
        /// No-op when no fog volume is wired (e.g. unit-style setups without volumes).</summary>
        private void ApplyFog()
        {
            float factor = FogFactor(DayProgress);
            // HDRP: the visible fog is either the volumetric Fog volume (meanFreePath) or, when
            // volumetric fog is off (Urwald uses the classic exponential-squared fog for near-field
            // sharpness, see U1), RenderSettings.fogDensity. Scale BOTH so the curve always shows.
            if (fogVolumeComponent != null && fogVolumeComponent.meanFreePath.overrideState)
            {
                // ROTE1-NEBEL1: Builder-Baseline hat Vorrang (prozess-global geteilter
                // Override; Einmalablesen erbte Kontamination — Pfad 0,00 statt 46,7).
                float baseP = noonFogMeanFreePath >= 0f ? noonFogMeanFreePath : fogBaseMeanFreePath;
                if (baseP <= 0f && noonFogMeanFreePath < 0f)
                {
                    fogBaseMeanFreePath = baseP = fogVolumeComponent.meanFreePath.value;
                }
                if (baseP > 0f)
                {
                    float scaled = baseP / Mathf.Max(factor, 0.01f);
                    fogVolumeComponent.meanFreePath.Override(scaled);
                }
            }
            if (RenderSettings.fog)
            {
                // LICHT1c2: Builder-Baseline hat Vorrang; ohne Builder-Wert (runtime-Erzeugung)
                // bleibt das alte Einmalablesen aus RenderSettings.
                float baseD = noonFogDensity >= 0f ? noonFogDensity : fogBaseDensity;
                if (baseD <= 0f && noonFogDensity < 0f)
                {
                    fogBaseDensity = baseD = RenderSettings.fogDensity;
                }
                if (baseD > 0f)
                {
                    RenderSettings.fogDensity = baseD * factor;
                }
            }
            // LICHT1c1/c3: die Nebelfarbe folgt dem Tagesfaktor (tags 1x, tiefste Nacht
            // fogNightDimFactor). SetDayProgress ruft ApplyFog VOR der Neuberechnung von
            // CurrentDayFactor auf — den Faktor hier lokal bestimmen, damit ein Test-Jump
            // sofort die richtige Farbe setzt.
            Color baseC = dayFogColor.r >= 0f ? dayFogColor : fogBaseColor;
            if (baseC.r < 0f)
            {
                fogBaseColor = baseC = RenderSettings.fogColor;
            }
            float lengthMinutes = dayLengthMinutes > 0.01f ? dayLengthMinutes : 0.01f;
            float dayF = DayFactorAt(dayFraction, lengthMinutes,
                Mathf.Max(transitionMinutes, 0.01f), nightMinutes);
            // LICHT1c1 fix (r103-f7): der rohe Tagesfaktor dimmt schon um 16:30 (Faktor
            // 0.875) — das nahm G6-Himmel Max exakt 214 -> 190 (Multiplikator auf die
            // Hüllfläche). Die Tag-Hüllfläche bleibt bis in den Abend bei 1x; gedimmt
            // wird erst ab Daemmerung (dayF < 0.5) bis tief in die Nacht.
            float dim = Mathf.Lerp(fogNightDimFactor, 1f,
                Mathf.InverseLerp(0f, 0.5f, dayF)); // InverseLerp klemmt selbst auf 0..1
            if (RenderSettings.fog)
            {
                RenderSettings.fogColor = new Color(
                    baseC.r * dim, baseC.g * dim, baseC.b * dim);
            }
            // LICHT1c3: der SICHTBARE Schleier in HDRP ist das Fog-Volumen, nicht
            // RenderSettings.fogColor — das dimmte bisher nur den ungenutzten Wert und die
            // J5-Nacht blieb bei der hellen Tag-Huellflaeche (Mittel 164.7, r104-f9).
            // DIM-Test (r105-f3) zeigt: albedo allein reicht nicht — Nachtrahmen-Raw war
            // identisch zum Tagesrahmen (4.7E-2), denn HDRPs klassischer Nebel
            // (enableVolumetricFog aus) blendet mit fog.color, und das Profil hatte dort
            // nirgends einen Override -> Default 0.5 Grau als Dauer-Schleier. Der Zyklus
            // dimmt jetzt color UND albedo: tags Faktor 1 (Builder-Wert), nachts 0.12x.
            if (fogVolumeComponent != null && fogVolumeComponent.albedo.overrideState)
            {
                fogVolumeComponent.albedo.Override(new Color(
                    baseC.r * dim, baseC.g * dim, baseC.b * dim));
                fogVolumeComponent.color.Override(new Color(
                    baseC.r * dim, baseC.g * dim, baseC.b * dim));
            }
        }
    }
}
