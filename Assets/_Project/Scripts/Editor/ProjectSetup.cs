using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Spinnenspiel.EditorTools
{
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string RendererPath = SettingsFolder + "/Spinnenspiel_URP_Renderer.asset";
        private const string PipelinePath = SettingsFolder + "/Spinnenspiel_URP.asset";

        [MenuItem("Spinnenspiel/Setup/Configure Project")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Spinnenspiel] ProjectSetup complete: URP assigned to GraphicsSettings and " + QualitySettings.names.Length + " quality levels.");
        }
    }
}
