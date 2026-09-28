using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>Gemeinsame Hilfsfunktionen der Editor-Builder: Ordner, Assets ersetzen, Texturen speichern.</summary>
    public static class AssetUtil
    {
        public const string GeneratedRoot = "Assets/_Project/Art/Generated";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int index = path.LastIndexOf('/');
            string parent = path.Substring(0, index);
            string leaf = path.Substring(index + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Speichert ein Asset unter <paramref name="path"/> und ersetzt ein vorhandenes.</summary>
        public static T ReplaceAsset<T>(T asset, string path) where T : Object
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Schreibt eine Textur als PNG und importiert sie mit passenden Einstellungen.</summary>
        public static Texture2D SaveTexture(Texture2D texture, string path, bool normalMap, bool alpha)
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.alphaIsTransparency = alpha;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material CreateLitMaterial(string path, Color color, float smoothness, Texture2D baseMap = null, Texture2D normalMap = null)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (baseMap != null)
            {
                material.SetTexture("_BaseMap", baseMap);
            }

            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            material.enableInstancing = true;
            return ReplaceAsset(material, path);
        }

        public static void SetStaticRecursive(GameObject root)
        {
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            foreach (Transform child in root.transform)
            {
                SetStaticRecursive(child.gameObject);
            }
        }
    }
}
