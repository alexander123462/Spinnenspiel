using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Schaltet HDRP als aktive Render-Pipeline ein (HDRP-Umstieg, Schritt 1).
    /// Erzeugt bei Bedarf das Pipeline-Asset unter Assets/_Project/Settings/HDRP.
    /// URP bleibt installiert; Materialien kommen in Schritt 2.
    /// </summary>
    public static class HdrpSetup
    {
        const string AssetPath = "Assets/_Project/Settings/HDRP/HDRP_Asset.asset";

        [MenuItem("Spinnenspiel/HDRP einrichten")]
        public static void Run()
        {
            // Ordner Assets/_Project/Settings/HDRP anlegen, falls noch nicht vorhanden.
            string folder = "Assets/_Project/Settings/HDRP";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = "Assets/_Project/Settings";
                if (!AssetDatabase.IsValidFolder(parent))
                    parent = AssetDatabase.CreateFolder("Assets/_Project", "Settings");
                AssetDatabase.CreateFolder(parent, "HDRP");
            }

            // Pipeline-Asset laden oder neu anlegen.
            HDRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            // Pipeline global und für alle Qualitätsstufen aktivieren.
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }

            PlayerSettings.colorSpace = ColorSpace.Linear;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HDRP] aktiv: " + GraphicsSettings.currentRenderPipeline);
        }
    }
}
