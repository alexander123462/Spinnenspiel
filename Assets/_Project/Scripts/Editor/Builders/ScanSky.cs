using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Editor-Werkzeug für das Poly-Haven-HDRI (Kloofendal, 4k, puresky):
    /// kopiert es nach Assets/_Project/Art/Scans/HDRI/ und konfiguriert den Import
    /// (linear, max. 4096, BC6H, keine Mipmaps).
    /// </summary>
    public static class ScanSky
    {
        private const string SourceName = "kloofendal_48d_partly_cloudy_puresky_4k.hdr";
        private const string TargetFolder = "Assets/_Project/Art/Scans/HDRI";

        private static string TargetPath => TargetFolder + "/" + SourceName;

        private static string SourcePath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "AssetSource", "polyhaven", "hdri", SourceName);

        /// <summary>
        /// Sonnenausrichtung im HDRI, per Hand vermessen: hellster Pixel bei x=2437, y=479
        /// in 4096×2048 → Elevation ≈ 47.9°, Azimut ≈ -55.8°.
        /// </summary>
        public static readonly Quaternion SunRotation = Quaternion.Euler(47.9f, -55.8f, 0f);

        /// <summary>True, wenn die Quelldatei im Projektordner vorliegt.</summary>
        public static bool IsAvailable()
        {
            return File.Exists(SourcePath);
        }

        /// <summary>
        /// Stellt sicher, dass das HDRI als Asset unter Assets/_Project/Art/Scans/HDRI/
        /// vorliegt, und liefert die importierte Textur.
        /// </summary>
        public static Texture2D EnsureImported()
        {
            if (!File.Exists(TargetPath))
            {
                AssetUtil.EnsureFolder(TargetFolder);
                File.Copy(SourcePath, TargetPath, true);
            }

            AssetDatabase.ImportAsset(TargetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(TargetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Spinnenspiel] Keine TextureImporter-Einstellung möglich: " + TargetPath);
            }
            else
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = false; // linear: HDRI für Skybox + GI
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = false;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TargetPath);
        }
    }
}
