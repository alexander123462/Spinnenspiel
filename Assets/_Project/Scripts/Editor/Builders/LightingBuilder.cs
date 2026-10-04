using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut Sun (HDRP: Lux, Farbtemperatur, Volumetrische Dimmung) und ein HDRP Global Volume
    /// (PhysicallyBasedSky, Volumen-Nebel, Auto-Belichtung, ACES, Bloom) fuer die Milestone-1-Szene.
    /// </summary>
    public static class LightingBuilder
    {
        private const string VolumeProfilePath = "Assets/_Project/Settings/M1_VolumeProfile.asset";

        public static void Create()
        {
            CreateSun();
            ApplyEnvironment();
            CreateGlobalVolume();
        }

        private static void CreateSun()
        {
            GameObject go = new GameObject("Sun");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1.2f;
            // HDRP: physikalische Lichtstaerken (Lux) + Volumetrische Dimmung fuer Lichtstrahlen.
            var hd = light.GetComponent<HDAdditionalLightData>(); if (hd == null) hd = light.gameObject.AddComponent<HDAdditionalLightData>();
            light.lightUnit = LightUnit.Lux; light.intensity = 100000f; light.useColorTemperature = true; light.colorTemperature = 5600f; light.shadows = LightShadows.Soft; hd.volumetricDimmer = 1f;
            light.color = new Color(1f, 0.96f, 0.88f); // leicht warm
            go.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void ApplyEnvironment()
        {
            // Unity 6: Properties heißen ambientSkyColor/ambientEquatorColor/ambientGroundColor.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.75f, 0.73f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.24f, 0.2f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = new Color(0.72f, 0.76f, 0.82f); // blass-blaugrau
        }

        private static void CreateGlobalVolume()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

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

            if (AssetDatabase.LoadAssetAtPath<Object>(VolumeProfilePath) != null)
            {
                AssetDatabase.DeleteAsset(VolumeProfilePath);
            }
            // Unity 6: SaveAsAsset exist nicht mehr, Ersatz ist CreateAsset.
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            AssetDatabase.SaveAssets();

            GameObject go = new GameObject("Global Volume");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
