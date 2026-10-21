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
            sun.lightUnit = LightUnit.Lux; sun.intensity = 100000f; sun.useColorTemperature = true; sun.colorTemperature = 5600f; sun.shadows = LightShadows.Soft; hd.volumetricDimmer = 1f; // WALD2a3: Lux stays at 100000 (Lux-dimming without fixed exposure is ineffective / belongs to WALD2b)
            sun.color = WaldLook.SunColor; // WALD2a3: only the sun color moves, from WaldLook (not baseline 1/0.93/0.82)
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
            RenderSettings.fogDensity = 0.012f; // A2: dezent Nebel, nicht flaechendeckend
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
            profile.Add<PhysicallyBasedSky>(true);
            // U1: VolumetricFog AUS - der Nebel dies Dunnelesier vor allem Nahaufnahmen (Nahkampfschaerfe)
            // und macht die Kontrast (luma-StDev) in den Abnahaengemeter laeuermisstaen fall.
            // WALD2a2: only fog.albedo via WaldLook.FogAlbedo (meanFreePath/Density stay at baseline).
            var fog = profile.Add<Fog>(true); fog.enabled.Override(true); fog.meanFreePath.Override(WaldLook.FogMeanFreePath); fog.maximumHeight.Override(25f); fog.enableVolumetricFog.Override(WaldLook.VolumetricFogOn); fog.albedo.Override(WaldLook.FogAlbedo); fog.anisotropy.Override(WaldLook.FogAnisotropy); // WALD8c: Volumetric-Fog ON + Streuung toward the sun -> Lichtbahnen (was off per U1)
            LastBuiltFog = fog; // T7: expose the Fog volume so the DayNightCycle can drive it.
            var exp = profile.Add<Exposure>(true); exp.mode.Override(ExposureMode.AutomaticHistogram); exp.limitMin.Override(0f); exp.limitMax.Override(15f);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.15f);
            // WALD2a1: only saturation via WaldLook (Nebel, Sonne, Exposure stay at baseline).
            var ca = profile.Add<ColorAdjustments>(true); ca.saturation.Override(WaldLook.SaturationPercent);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.2f);
            var sh = profile.Add<HDShadowSettings>(true); sh.maxShadowDistance.Override(80f);
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
            volume.sharedProfile = profile;
        }

        private static void ConfigurePipeline()
        {
            // URP-Konfig unter HDRP hinfällig – Schattendistanz/MSAA stehen im HDRP-Asset bzw. im Volume (HDShadowSettings).
            return;
        }
    }
}
