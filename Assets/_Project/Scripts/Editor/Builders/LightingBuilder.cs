using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut Sun, Ambient/Fog-Settings und ein URP Global Volume
    /// (ACES-Tonemapping + Bloom) fuer die Milestone-1-Szene.
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

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.1f);

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
