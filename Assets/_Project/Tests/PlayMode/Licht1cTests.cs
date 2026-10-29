using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// LICHT1c1 (Look mehr Sonnenlicht, ohne Dach-Oeffnungen): Sonne fest aus
    /// WaldLook (100000 lx, per UrwaldSceneBuilder auf dayCycle.dayLux verdrahtet; war
    /// toter Code), Schatten staerker (0.92), Volumennebel duenner (meanFreePath 140
    /// statt 95) und kontrastreicher (+6 ColorAdjustments). Die klassische NebelDICHTe
    /// kommt aus WaldLook (0.014) und die Nebelfarbe folgt dem Tagesfaktor: tags volle
    /// Tag-Huellflaeche (G6-Himmel Max > 200), nachts auf FogNightDimFactor gedimmt
    /// (J5-Nacht-Fenster 10..45). Dieser Test pinnt die Verdrahtung im gebauten Urwald.
    /// </summary>
    public class Licht1cTests
    {
        // Referenzwerte aus Assets/_Project/Scripts/Editor/Builders/WaldLook.cs
        // (Editor-Assembly, hier nicht referenzierbar — Werte bewusst doppelt gehalten).
        private const float ExpectedDayLux = 100000f;
        private const float ExpectedFogDensity = 0.009f; // LICHT1c2: Mittag-Dichte (Kriterium <= 0.009)
        private const float MinContrast = 5f; // LICHT1c1: +6 Kontrast (Build-Wert, LICHT1c2 bestaetigt).
        // LICHT1c: der gebaute Nebel (meanFreePath) wird im PlayMode-Betrieb vom
        // DayNightCycle skaliert und das geteilte Profil-Asset ist ueber Tests hinweg
        // kontaminiert — daher gegen die Builder-Ausgabe auf der Platte pruefen.
        private const string ProfileAssetPath =
            "Assets/_Project/Art/Generated/Lighting/Testwelt_VolumeProfile.asset";

        [UnityTest]
        public IEnumerator LICHT1c_LookWiring_InBuiltUrwald()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;

            DayNightCycle cycle = null;
            float timeout = 15f;
            while (timeout > 0f && cycle == null)
            {
                cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(cycle, "No DayNightCycle in Urwald.");

            JungleWorldGenerator worldGen = null;
            timeout = 25f;
            while (timeout > 0f && worldGen == null)
            {
                var g = UnityEngine.Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) worldGen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(worldGen, "Urwald world never ready.");

            // LICHT1c3: Dach-Schatten + Oeffnungen sind aus WaldLook verdrahtet
            // (Schatten An, Raster 18 m, Radius 9 m — Beweisfoto-Kriterium -fotoboden).
            Assert.IsTrue(worldGen.canopyShadowOn,
                "canopyShadowOn not wired (WaldLook.CanopyShadowOn).");
            Assert.AreEqual(18f, worldGen.canopyGapStep, 0.01f,
                "canopyGapStep not wired to WaldLook (18 m).");
            Assert.AreEqual(9f, worldGen.canopyGapRadius, 0.01f,
                "canopyGapRadius not wired to WaldLook (9 m).");

            // LICHT1c: die Sonne haelt ihre 100000 lx aus WaldLook (war: Default auf dem
            // Component; der Wert ist jetzt an einer Stelle definiert und verdrahtet).
            Assert.AreEqual(ExpectedDayLux, cycle.dayLux, 1f,
                "dayCycle.dayLux not wired to WaldLook.SunIntensityLux.");
            cycle.SetDayProgress(0.5f); // Mittag: sun.intensity == dayLux, FogFactor 1.0
            Assert.IsNotNull(cycle.sun, "Sun not set on the cycle.");
            Assert.AreEqual(ExpectedDayLux, cycle.sun.intensity, 2f,
                "Noon sun intensity must equal WaldLook.SunIntensityLux (100000).");
            Assert.GreaterOrEqual(cycle.sun.shadowStrength, 0.9f,
                "LICHT1c: sun shadowStrength should be deeper (0.92).");

            // LICHT1c2: Tages-Himmel-Multiplier 1.0 aus WaldLook — die 1.35/2.1-Kompensation
            // wirkte gegen den G6-Himmel-Max (Auto-Exposure normalisiert; r104-f3/f4) und leckte
            // in die Nacht. Der Zyklus faehrt den Himmel jetzt grundkurvenmaessig auf 1.0 und der
            // Multiplier greift erst ab dayF>0.95 (Gate). Mittag: m == daySkyMultiplier == 1.0.
            float skyMul = cycle.CurrentSkyMultiplier();
            Assert.AreEqual(1.0f, skyMul, 0.06f,
                "Daytime sky multiplier not wired to WaldLook.DaySkyMultiplier (1.0).");

            // Nebel duenner und dunkler: Builder-Parameter gegen die Profil-Datei auf der
            // Platte (Laufzeitwerte werden vom Zyklus skaliert und sind testuebergreifend
            // kontaminiert). meanFreePath 140 (war 95), Fog-Farbe dunkler (sichtbar am
            // albedo-Wert im selben Block nicht pinibar — Farbcheck bleibt dem Bild).
            string profileYaml = System.IO.File.ReadAllText(
                System.IO.Path.Combine(UnityEngine.Application.dataPath,
                    "_Project/Art/Generated/Lighting/Testwelt_VolumeProfile.asset"));
            Assert.IsTrue(profileYaml.Contains("meanFreePath:")
                    && profileYaml.IndexOf("meanFreePath:", StringComparison.Ordinal) >= 0
                    && MatchValueAfter(profileYaml, "meanFreePath:", 140f),
                "Built fog volume meanFreePath is not 140 (LICHT1c thinning).");
            Assert.AreEqual(ExpectedFogDensity, RenderSettings.fogDensity, 0.0005f,
                "RenderSettings.fogDensity not wired to WaldLook.FogDensity (0.009).");

            // Kontrast: ColorAdjustments.contrast im Global-Volume-Profil per Name+Reflexion
            // (PlayMode-Tests kennen keine HDRP-Typen).
            float contrast = FindVolumeParamFloat("ColorAdjustments", "contrast");
            Debug.Log("[LICHT1c] dayLux " + cycle.dayLux + " | sun " + cycle.sun.intensity
                + " | fogDensity " + RenderSettings.fogDensity + " | contrast " + contrast);
            Assert.GreaterOrEqual(contrast, MinContrast,
                "ColorAdjustments.contrast override missing or too low (want +6).");
        }

        /// <summary>LICHT1c1: Die klassische Nebelfarbe folgt dem Tagesfaktor — tags die
        /// volle Tag-Huellflaeche (G6-Himmel Max > 200 lebt davon), nachts auf
        /// WaldLook.FogNightDimFactor (0.12) gedimmt, damit die mondleuchtende Ferne die
        /// J5-Nacht nicht in den Bereich ueber 45 hebt.</summary>
        [UnityTest]
        public IEnumerator LICHT1c_NightFogDim_FollowsDayFactor()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;

            DayNightCycle cycle = null;
            float timeout = 15f;
            while (timeout > 0f && cycle == null)
            {
                cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(cycle, "No DayNightCycle in Urwald.");

            // Mittag: Nebelfarbe == WaldLook.FogAlbedo (0.40/0.58/0.41, hier bewusst doppelt
            // gehalten — Editor-Assembly ist dem PlayMode-Test nicht referenzierbar).
            cycle.SetDayProgress(0.5f);
            Color noonFog = RenderSettings.fogColor;
            Assert.AreEqual(0.58f, noonFog.g, 0.03f,
                "Noon fog colour is not the WaldLook FogAlbedo green (0.58).");

            // Tiefste Nacht: auf FogNightDimFactor 0.08 gedimmt (gruen 0.0464). LICHT1c6
            // (Claude 2026-10-29): 0.015 machte die Nacht EXAKT schwarz (J5-Mittel 0) — der
            // Nebel-Wert weicht, Nachthimmel < 0.02 und Dach-Schatten On bleiben.
            cycle.SetDayProgress(0.0f);
            Color nightFog = RenderSettings.fogColor;
            Assert.AreEqual(0.058f, nightFog.g, 0.004f,
                "Night fog colour not dimmed to FogNightDimFactor (0.10 x 0.58).");

            // LICHT1c3: der SICHTBARE Schleier ist das HDRP-Fog-Volumen (albedo) — auch er
            // muss nachts gedimmt sein (J5-Nacht 164.7 r104-f9 lief mit voller Tag-Albedo).
            // PlayMode-Test ohne HDRP-Referenz: Reflexion auf cycle.fogVolumeComponent.albedo.
            object fogVol = typeof(DayNightCycle).GetField("fogVolumeComponent")
                .GetValue(cycle);
            Assert.IsNotNull(fogVol, "No HDRP Fog volume wired to the DayNightCycle.");
            object albedoParam = fogVol.GetType().GetField("albedo").GetValue(fogVol);
            Color volAlbedo = (Color)albedoParam.GetType().GetProperty("value").GetValue(albedoParam);
            // LICHT1c3 DIM-Diagnose (r105-f3): HDRPs klassischer Nebel blendet mit fog.color,
            // nicht albedo — der unueberschriebene Default 0.5 Grau machte Nachtrahmen genauso
            // hell wie Tagesrahmen (Raw identisch 4.7E-2). Auch color muss nachts dimmen.
            object colorParam = fogVol.GetType().GetField("color").GetValue(fogVol);
            Color volColor = (Color)colorParam.GetType().GetProperty("value").GetValue(colorParam);
            Debug.Log("[LICHT1c-Nebel] noon " + noonFog.ToString("F3")
                + " | night " + nightFog.ToString("F3")
                + " | volumen " + volAlbedo.ToString("F3")
                + " | color " + volColor.ToString("F3"));
            Assert.AreEqual(0.058f, volAlbedo.g, 0.004f,
                "HDRP fog volume albedo not dimmed at night (visible veil stays bright).");
            Assert.AreEqual(0.058f, volColor.g, 0.004f,
                "HDRP classic fog colour (fog.color) not dimmed at night — the grey veil keeps night frames as bright as day.");

            // LICHT1c3: nachts darf die PBS-Himmelsscheibe (Mond) durch die Dach-Oeffnungen
            // nicht als helle Scheibe durchscheinen — der Nachthimmel haelt den Multiplikator
            // aus WaldLook. LICHT1c6 (Claude-Entscheidung 2026-10-29): Obergrenze 0.01 -> 0.02
            // (Band "Nachthimmel < 0.02"). Bei 0.010 mass J5 Nacht-Mittel 6.0 (< 10-Fenster);
            // die dynamische Himmels-Ambiente ist der lineare Hebel, 0.018 landete ~11.
            float nightSky = cycle.CurrentSkyMultiplier();
            Assert.GreaterOrEqual(nightSky, 0f, "Night sky multiplier negative.");
            Assert.LessOrEqual(nightSky, 0.02f,
                "Night sky multiplier above Claude cap 0.02 — moon disc lights the scene through canopy gaps.");
        }

        /// <summary>LICHT1c3: Die Himmelskoerper-Scheibe wird vom Zyklus am Tag auf
        /// Emission (heller PBS-Sonnenkern, G6-Beweis) und nachts auf Reflection
        /// (keine Leuchtscheibe) geschaltet — Backlog: "Nachts duerfen Oeffnungen Mond
        /// nicht als Scheibe durchlassen". HDRP ist im Test-Assembly nicht referenziert,
        /// daher Reflexion auf HDAdditionalLightData.</summary>
        [Test]
        public void LICHT1c3_Himmelscheibe_TagsEmissionNachtsAus()
        {
            Type hdType = Type.GetType(
                "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, Unity.RenderPipelines.HighDefinition.Runtime");
            Assert.IsNotNull(hdType, "HDRP runtime assembly not found.");

            GameObject go = new GameObject("DiskToggleTest");
            Light testSun = go.AddComponent<Light>();
            testSun.type = UnityEngine.LightType.Directional;
            object hd = go.AddComponent(hdType);
            DayNightCycle cycle = go.AddComponent<DayNightCycle>();
            cycle.sun = testSun;

            FieldInfo srcField = hdType.GetField("celestialBodyShadingSource");
            Assert.IsNotNull(srcField, "celestialBodyShadingSource field missing.");
            PropertyInfo skyProp = hdType.GetProperty("interactsWithSky");
            Assert.IsNotNull(skyProp, "interactsWithSky property missing.");

            Assert.IsTrue(cycle.SetCelestialDiskForTests(0.5f),
                "SetCelestialDiskForTests failed (HDAdditionalLightData not found on the light?).");
            string daySrc = srcField.GetValue(hd).ToString();
            bool daySky = (bool)skyProp.GetValue(hd);
            cycle.SetCelestialDiskForTests(0f);
            string nightSrc = srcField.GetValue(hd).ToString();
            bool nightSky = (bool)skyProp.GetValue(hd);

            Assert.AreEqual("Emission", daySrc, "Day disc must emit (bright sun for G6).");
            Assert.AreEqual("ReflectSunLight", nightSrc,
                "Night shading source must not be Emission (moon lux would emit a bright disc).");
            Assert.IsTrue(daySky, "Day light must participate in the sky (sun disc for G6).");
            // LICHT1c6 (Claude 2026-10-29): der Mond muss den PBS-Himmel speisen — ohne
            // Himmelsbeteiligung war die Nacht EXAKT schwarz (r108-f2: Nebelfarbe 0.0464,
            // Raw 0.00E+000; r107: jeder Mond-Lux wirkungslos). Die Scheibe bleibt dunkel
            // (ReflectSunLight, nicht Emission); der Nachthimmel-Multiplikator 0.003 < 0.02
            // begrenzt die Helligkeit ( LICHT1c_NightFogDim pinnt ihn).
            Assert.IsTrue(nightSky,
                "Night: light must feed the sky (lunar atmosphere lights the fog veil) — "
                + "with no sky participation the night frame is exactly black (r108-f2).");

            cycle.sun = null;
            UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>Prueft, ob im YAML-Block nach 'key:' ein VolumeParameter mit
        /// m_OverrideState: 1 und m_Value ~= wanted steht.</summary>
        private static bool MatchValueAfter(string yaml, string key, float wanted)
        {
            int i = yaml.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            string block = yaml.Substring(i, Math.Min(120, yaml.Length - i));
            int v = block.IndexOf("m_Value:", StringComparison.Ordinal);
            if (v < 0 || !float.TryParse(block.Substring(v + 8).TrimStart()
                    .Split('\n')[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float got))
                return false;
            return Math.Abs(got - wanted) < 0.5f;
        }

        /// <summary>Liefert den ueberschriebenen float-Wert eines Volume-Parameters
        /// (z.B. ColorAdjustments.contrast) aus dem globalen Volume-Profil.</summary>
        private static float FindVolumeParamFloat(string componentName, string paramName)
        {
            Volume vol = UnityEngine.Object.FindFirstObjectByType<Volume>();
            Assert.IsNotNull(vol, "No global Volume in Urwald.");
            foreach (VolumeComponent comp in vol.sharedProfile.components)
            {
                if (comp.GetType().Name != componentName) continue;
                FieldInfo fi = comp.GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance);
                if (fi == null) continue;
                object param = fi.GetValue(comp); // VolumeParameter<float> (Core-Assembly)
                PropertyInfo val = param.GetType().GetProperty("value");
                return Convert.ToSingle(val.GetValue(param));
            }
            return -999f;
        }
    }
}