using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(6f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);

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
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogWarning("[Spinnenspiel] Keine URP-Pipeline aktiv – Schatten/MSAA nicht angepasst.");
                return;
            }

            pipeline.shadowDistance = 45f;
            pipeline.shadowCascadeCount = 3;
            pipeline.msaaSampleCount = 4;
            UnityEditor.EditorUtility.SetDirty(pipeline);
        }
    }
}
