using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Natürliche Beleuchtung der Testwelt: tiefstehende Nachmittagssonne mit weichen Schatten,
    /// prozeduraler Himmel, Trilight-Umgebungslicht, Distanznebel und Post-Processing
    /// (ACES-Tonemapping, Bloom, Farbabstimmung, Vignette). Stellt außerdem Schattendistanz/MSAA der Pipeline ein.
    /// </summary>
    public static class WorldLightingBuilder
    {
        private const string Folder = AssetUtil.GeneratedRoot + "/Lighting";

        /// <summary>T7: The Fog volume component of the last-built Volume profile, so
        /// the DayNightCycle can scale its meanFreePath (Volume-Override, no new scene).</summary>
        public static Fog LastBuiltFog;

        /// <summary>FARBE1a: The PhysicallyBasedSky and Exposure components of the last-built
        /// profile, so the DayNightCycle can drive sky brightness (day factor) and the tests
        /// can pin a fixed exposure.</summary>
        public static PhysicallyBasedSky LastBuiltSky;
        public static Exposure LastBuiltExposure;

        private static float G6_FIXED_EV = 6f; // Fixed-Exposure EV (HDRP: hoeher = dunkler; r117 EV=-1 -> Mittel 255 blendout)

        public static Light Build()
        {
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = WaldLook.SunShadowStrength; // WALD2a3: shadow strength from one place (0.78, not black)
            sun.shadowNormalBias = 0.3f;
            sun.shadowBias = 0.02f;
            sun.intensity = 1.35f;
            // HDRP: physikalische Lichtstaerken (Lux) + Volumetrische Dimmung fuer Lichtstrahlen.
            var hd = sun.GetComponent<HDAdditionalLightData>(); if (hd == null) hd = sun.gameObject.AddComponent<HDAdditionalLightData>();
            sun.lightUnit = LightUnit.Lux; sun.intensity = WaldLook.SunIntensityLux; sun.useColorTemperature = true; sun.colorTemperature = 5600f; sun.shadows = LightShadows.Soft; hd.volumetricDimmer = 1f; // LICHT1c: initial lux from WaldLook (was hardcoded 100000); DayNightCycle.dayLux carries the same value at runtime
            sun.color = WaldLook.SunColor; // WALD2a3: only the sun color moves, from WaldLook (not baseline 1/0.93/0.82)
            // LICHT1c3 (PBS-Paketcode PhysicallyBasedSkyRenderer.FillCelestialBodyData): die
            // Szenen-Sonne hatte celestialBodyShadingSource=Reflection — die Himmelskoerper-
            // Scheibe wird von der Richtungslicht-Selbst verschattet, Oberflaeche nur ~1.0
            // (deshalb im Bild nie als helle Scheibe sichtbar, G6-Max blieb bei der Nebel-
            // Huellflaeche ~169). Emission nutzt color x 1/SolidAngle des Lichts: echte
            // Sonnenscheibe (~120000 lx -> HDR), nachts mit Mond-Lux (800) und
            // Multiplikator 0.003 gedimmt — keine helle Mond-Scheibe (Backlog-Forderung).
            hd.celestialBodyShadingSource = HDAdditionalLightData.CelestialBodyShadingSource.Emission;
            RenderSettings.sun = sun;

            if (ScanSky.IsAvailable())
            {
                Texture2D hdri = ScanSky.EnsureImported();
                // Sonnenausrichtung per Hand im HDRI vermessen (hellster Pixel),
                // siehe ScanSky.SunRotation.
                sunObject.transform.rotation = ScanSky.SunRotation;

                // G6/G1: kein Legacy-Skybox mehr (Built-In-Shaders magenta in HDRP).
                // HDRP rendert den PhysicallyBased-Sky per Volume (CreateVolume), skybox leer.
                RenderSettings.skybox = null;
            }
            else
            {
                sunObject.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

                Shader skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    Material sky = new Material(skyShader);
                    sky.SetFloat("_SunSize", 0.035f);
                    sky.SetFloat("_AtmosphereThickness", 0.9f);
                    sky.SetColor("_SkyTint", new Color(0.52f, 0.6f, 0.72f));
                    sky.SetColor("_GroundColor", new Color(0.35f, 0.33f, 0.3f));
                    sky.SetFloat("_Exposure", 1.15f);
                    RenderSettings.skybox = AssetUtil.ReplaceAsset(sky, Folder + "/MAT_Sky.mat");
                }

                // WALD2a1: Umgebungslicht (Trilight-Fallback) gruenlich-kuehl ueber WaldLook.
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = WaldLook.AmbientSky;
                RenderSettings.ambientEquatorColor = WaldLook.AmbientEquator;
                RenderSettings.ambientGroundColor = WaldLook.AmbientGround;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = WaldLook.FogDensity; // LICHT1c: from WaldLook (was hardcoded 0.012); DayNightCycle scales it with the T7 fog factor
            RenderSettings.fogColor = WaldLook.FogAlbedo; // WALD2a2: RenderSettings.fogColor follows the same WaldLook value

            CreateVolume();
            ConfigurePipeline();
            return sun;
        }

        private static void CreateVolume()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetUtil.ReplaceAsset(profile, Folder + "/Testwelt_VolumeProfile.asset");

            var env = profile.Add<VisualEnvironment>(true); env.skyType.Override((int)SkyType.PhysicallyBased); env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            LastBuiltSky = profile.Add<PhysicallyBasedSky>(true); // FARBE1a: DayNightCycle faehrt die Himmelshelligkeit mit dem Tag-Faktor.
            // U1: VolumetricFog AUS - der Nebel dies Dunnelesier vor allem Nahaufnahmen (Nahkampfschaerfe)
            // und macht die Kontrast (luma-StDev) in den Abnahaengemeter laeuermisstaen fall.
            // WALD2a2: only fog.albedo via WaldLook.FogAlbedo (meanFreePath/Density stay at baseline).
            // LICHT1c3 (Wurzel-Diagnose r105-f3): HDRP rendert klassischen Nebel
            // (enableVolumetricFog-Aus wie in Tests/J5) mit fog.color — dessen Default 0.5 Grau
            // war der Dauer-Schleier: Nachtrahmen-Raw == Tagesrahmen-Raw (4.7E-2 identisch),
            // G6 Max konstant 167 = ACES auf normalisiertes 0.5-Grau. Farbe jetzt HIER auf
            // Waldgruen gesetzt; DayNightCycle ueberschreibt color + albedo tag-/nachtabhaengig.
            var fog = profile.Add<Fog>(true); fog.enabled.Override(true); fog.meanFreePath.Override(WaldLook.FogMeanFreePath); fog.maximumHeight.Override(25f); fog.enableVolumetricFog.Override(WaldLook.VolumetricFogOn); fog.color.Override(WaldLook.FogAlbedo); fog.albedo.Override(WaldLook.FogAlbedo); fog.anisotropy.Override(WaldLook.FogAnisotropy); // WALD8c: Volumetric-Fog ON + Streuung toward the sun -> Lichtbahnen (was off per U1)
            LastBuiltFog = fog; // T7: expose the Fog volume so the DayNightCycle can drive it.
            var exp = profile.Add<Exposure>(true); LastBuiltExposure = exp; exp.mode.Override(ExposureMode.AutomaticHistogram); exp.limitMin.Override(0f); exp.limitMax.Override(15f); // FARBE1a: verdrahtet, damit Tests sie auf Fixed stellen koennen (Look bleibt Auto).
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.15f);
            // WALD2a1: only saturation via WaldLook (Nebel, Sonne, Exposure stay at baseline).
            var ca = profile.Add<ColorAdjustments>(true); ca.saturation.Override(WaldLook.SaturationPercent); ca.contrast.Override(WaldLook.ContrastPercent); // LICHT1c: +Contrast (kontrastreicher Look)
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.2f);
            var sh = profile.Add<HDShadowSettings>(true); sh.maxShadowDistance.Override(80f);
            // LICHT1c3: Standard-Kaskaden (4; Splits 0.05/0.15/0.3 -> Kaskade 0 endet bei 4 m)
            // liessen das 38-46 m hohe Dach nie in die Nahkaskaden fallen -> kein Schatten am
            // Boden. 2 Kaskaden mit Split0 0.5: 0-40 m / 40-80 m (LICHT1b-Messung r117).
            sh.cascadeShadowSplitCount.Override(2); sh.cascadeShadowSplit0.Override(0.5f);
            var cs = profile.Add<ContactShadows>(true); cs.enable.Override(true);
            var ao = profile.Add<ScreenSpaceAmbientOcclusion>(true); ao.intensity.Override(1f);
            // G4: leichte Makro-Tiefenunschärfe (manueller Fokus, nicht Physikalische-Kamera-Modus).
            var dof = profile.Add<DepthOfField>(true); dof.focusMode.Override(DepthOfFieldMode.Manual); dof.nearFocusStart.Override(0f); dof.nearFocusEnd.Override(0.15f); dof.farFocusStart.Override(6f); dof.farFocusEnd.Override(20f); // A2: dezent DoF, nicht flaechendeckend

            foreach (VolumeComponent component in profile.components)
            {
                UnityEditor.AssetDatabase.AddObjectToAsset(component, profile);
            }

            UnityEditor.EditorUtility.SetDirty(profile);
            UnityEditor.AssetDatabase.SaveAssets();

            GameObject go = new GameObject("Global Volume");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            // LICHT1c3: Priority erhoehen. Die HDRP-GlobalSettings referenzieren ein
            // DefaultSettingsVolumeProfile mit AKTIVEN Overrides (Exposure AutomaticHistogram,
            // PhysicallyBasedSky multiplier 1). Bei Priority-Bindung (0) gewinnt dieses im
            // Stack gegen unser Profil — der Zyklus pinnte Fixed-Exposure und dimmte den
            // Himmel, gerendert wurde trotzdem taghell (r106 f4-f14: Nachtrahmen ==
            // Tagesrahmen). Mit 100 gewinnen alle Testwelt-Parameter eindeutig.
            volume.priority = 100f;
            volume.sharedProfile = profile;
        }

        private static void ConfigurePipeline()
        {
            // URP-Konfig unter HDRP hinfällig – Schattendistanz/MSAA stehen im HDRP-Asset bzw. im Volume (HDShadowSettings).
            return;
        }
    }
}
