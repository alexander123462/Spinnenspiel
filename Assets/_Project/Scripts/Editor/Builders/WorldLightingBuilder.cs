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

        public static Light Build()
        {
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowNormalBias = 0.3f;
            sun.shadowBias = 0.02f;
            sun.intensity = 1.35f;
            // HDRP: physikalische Lichtstaerken (Lux) + Volumetrische Dimmung fuer Lichtstrahlen.
            var hd = sun.GetComponent<HDAdditionalLightData>(); if (hd == null) hd = sun.gameObject.AddComponent<HDAdditionalLightData>();
            sun.lightUnit = LightUnit.Lux; sun.intensity = 100000f; sun.useColorTemperature = true; sun.colorTemperature = 5600f; sun.shadows = LightShadows.Soft; hd.volumetricDimmer = 1f;
            sun.color = new Color(1f, 0.93f, 0.82f);
            RenderSettings.sun = sun;

            if (ScanSky.IsAvailable())
            {
                Texture2D hdri = ScanSky.EnsureImported();
                // Sonnenausrichtung per Hand im HDRI vermessen (hellster Pixel),
                // siehe ScanSky.SunRotation.
                sunObject.transform.rotation = ScanSky.SunRotation;

                Shader panoramic = Shader.Find("Skybox/Panoramic");
                if (panoramic != null)
                {
                    Material sky = new Material(panoramic);
                    sky.SetTexture("_MainTex", hdri);
                    sky.SetFloat("_Mapping", 1f); // Latitude-Longitude (Equirect)
                    sky.SetFloat("_ImageType", 0f);
                    sky.SetFloat("_Exposure", 1f);
                    RenderSettings.skybox = AssetUtil.ReplaceAsset(sky, Folder + "/MAT_Sky.mat");
                }
                else
                {
                    Debug.LogWarning("[Spinnenspiel] Shader Skybox/Panoramic nicht gefunden – Skybox bleibt leer.");
                }

                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
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

                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.50f, 0.58f, 0.70f);
                RenderSettings.ambientEquatorColor = new Color(0.52f, 0.54f, 0.50f);
                RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.16f);
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.68f, 0.74f, 0.78f);

            CreateVolume();
            ConfigurePipeline();
            return sun;
        }

        private static void CreateVolume()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetUtil.ReplaceAsset(profile, Folder + "/Testwelt_VolumeProfile.asset");

            var env = profile.Add<VisualEnvironment>(true); env.skyType.Override((int)SkyType.PhysicallyBased);
            profile.Add<PhysicallyBasedSky>(true);
            var fog = profile.Add<Fog>(true); fog.enabled.Override(true); fog.meanFreePath.Override(60f); fog.maximumHeight.Override(25f); fog.enableVolumetricFog.Override(true); fog.albedo.Override(new Color(0.75f, 0.82f, 0.75f));
            var exp = profile.Add<Exposure>(true); exp.mode.Override(ExposureMode.AutomaticHistogram); exp.limitMin.Override(6f); exp.limitMax.Override(15f);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.15f);
            var ca = profile.Add<ColorAdjustments>(true); ca.saturation.Override(10f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.2f);
            var sh = profile.Add<HDShadowSettings>(true); sh.maxShadowDistance.Override(80f);
            var cs = profile.Add<ContactShadows>(true); cs.enable.Override(true);
            var ao = profile.Add<ScreenSpaceAmbientOcclusion>(true); ao.intensity.Override(1f);
            // G4: leichte Makro-Tiefenunschärfe (manueller Fokus, nicht Physikalische-Kamera-Modus).
            var dof = profile.Add<DepthOfField>(true); dof.focusMode.Override(DepthOfFieldMode.Manual); dof.nearFocusStart.Override(0f); dof.nearFocusEnd.Override(0.15f); dof.farFocusStart.Override(2.5f); dof.farFocusEnd.Override(12f);

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
