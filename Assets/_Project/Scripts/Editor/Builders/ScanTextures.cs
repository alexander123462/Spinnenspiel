using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Editor-Werkzeug für Poly-Haven-Foto-Scan-Texturen:
    /// kopiert diffuse/normal, baut eine Mask-Map (R=Metal, G=AO, B=Höhe, A=1−Roughness),
    /// legt TerrainLayer und URP-Lit-Materialien an.
    /// Quelle: AssetSource/polyhaven/textures/&lt;id&gt; (im Projektordner, nicht in git).
    /// </summary>
    public static class ScanTextures
    {
        private static readonly string[] AllIds =
        {
            "forest_ground_04", "sparse_grass", "brown_mud_leaves_01", "mud_forest",
            "rocky_trail", "mossy_rock", "river_small_rocks", "forest_leaves_04", "pine_bark"
        };

        private const string TextureRoot = "Assets/_Project/Art/Scans/Textures";
        private const string LayerRoot = "Assets/_Project/Art/Scans/Layers";
        private const string MaterialRoot = "Assets/_Project/Art/Scans/Materials";

        /// <summary>Quellordner relativ zum Projektordner (nicht unter Assets/).</summary>
        private static string SourceRoot =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "AssetSource", "polyhaven", "textures");

        private static string SourceFile(string id, string suffix) =>
            Path.Combine(SourceRoot, id, id + "_" + suffix + "_2k." + (suffix == "diff" || suffix == "arm" ? "jpg" : "png"));

        private static string TargetFolder(string id) => TextureRoot + "/" + id;

        private static string TargetDiff(string id) => TargetFolder(id) + "/" + id + "_diff_2k.jpg";

        private static string TargetNormal(string id) => TargetFolder(id) + "/" + id + "_nor_gl_2k.png";

        private static string TargetMask(string id) => TargetFolder(id) + "/" + id + "_mask_2k.png";

        /// <summary>True, wenn alle vier Quelldateien (diff, nor_gl, arm, disp) vorhanden sind.</summary>
        public static bool IsAvailable(string id)
        {
            return File.Exists(SourceFile(id, "diff"))
                && File.Exists(SourceFile(id, "nor_gl"))
                && File.Exists(SourceFile(id, "arm"))
                && File.Exists(SourceFile(id, "disp"));
        }

        /// <summary>
        /// Stellt sicher, dass diffuse, normal und Mask-Map als Assets vorliegen.
        /// Liefert sofort, wenn der Zielordner schon vollständig ist.
        /// </summary>
        public static void EnsureImported(string id)
        {
            if (File.Exists(TargetDiff(id)) && File.Exists(TargetNormal(id)) && File.Exists(TargetMask(id)))
            {
                return; // Zielordner schon vollständig
            }

            AssetUtil.EnsureFolder(TargetFolder(id));
            File.Copy(SourceFile(id, "diff"), TargetDiff(id), true);
            File.Copy(SourceFile(id, "nor_gl"), TargetNormal(id), true);
            File.WriteAllBytes(TargetMask(id), BuildMask(id));

            AssetDatabase.ImportAsset(TargetDiff(id), ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TargetNormal(id), ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TargetMask(id), ImportAssetOptions.ForceUpdate);

            ConfigureImporter(TargetDiff(id), srgb: true, type: TextureImporterType.Default, aniso: 8);
            ConfigureImporter(TargetNormal(id), srgb: false, type: TextureImporterType.NormalMap, aniso: 8);
            ConfigureImporter(TargetMask(id), srgb: false, type: TextureImporterType.Default, aniso: 1);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Erzeugt einen TerrainLayer (diffuse/normal/mask) als Asset unter
        /// Assets/_Project/Art/Scans/Layers/TL_&lt;id&gt;.terrainlayer (vorhandenes wird ersetzt).
        /// </summary>
        public static TerrainLayer CreateTerrainLayer(string id, float tileSize)
        {
            AssetUtil.EnsureFolder(LayerRoot);
            string layerPath = LayerRoot + "/TL_" + id + ".terrainlayer";
            if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath) != null)
            {
                AssetDatabase.DeleteAsset(layerPath);
            }

            // Erst als Asset speichern, dann Felder setzen. In dieser Unity-Version ist
            // TerrainLayer kein ScriptableObject – CreateInstance<T>() geht nicht, daher new.
            TerrainLayer layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, layerPath);

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetDiff(id));
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetNormal(id));
            layer.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetMask(id));
            layer.tileSize = new Vector2(tileSize, tileSize);
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssets();
            return layer;
        }

        /// <summary>
        /// Erzeugt ein URP/Lit-Material (diffuse, Normal, Mask als Metallic+Occlusion) als Asset
        /// unter Assets/_Project/Art/Scans/Materials/M_&lt;id&gt;.mat (vorhandenes wird ersetzt).
        /// </summary>
        public static Material CreateLitMaterial(string id, Vector2 tiling)
        {
            AssetUtil.EnsureFolder(MaterialRoot);
            string materialPath = MaterialRoot + "/M_" + id + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
            {
                AssetDatabase.DeleteAsset(materialPath);
            }

            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TargetDiff(id)));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TargetNormal(id)));
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TargetMask(id)));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TargetMask(id)));
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_Smoothness", 1f);
            material.mainTextureScale = tiling;
            material.enableInstancing = true;

            AssetDatabase.CreateAsset(material, materialPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Importiert alle verfügbaren Scan-Texturen (Menü: Spinnenspiel/Scans/Texturen importieren).</summary>
        [MenuItem("Spinnenspiel/Scans/Texturen importieren")]
        public static void ImportAll()
        {
            foreach (string id in AllIds)
            {
                AssetUtil.EnsureFolder(TargetFolder(id));
                if (IsAvailable(id))
                {
                    EnsureImported(id);
                    Debug.Log("[Spinnenspiel] Scan-Textur bereit: " + id);
                }
                else
                {
                    Debug.LogWarning("[Spinnenspiel] Scan-Textur nicht verfügbar, Quelldateien fehlen: " + id);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Baut die Mask-Map (Unity/HDRP-Layout):
        /// R = Metal (arm.B), G = AO (arm.R), B = Höhe (disp, Graustufe), A = 1 − Roughness (arm.G).
        /// Fehlt disp oder hat es die falsche Größe, bleibt die Höhe bei 0.5.
        /// </summary>
        private static byte[] BuildMask(string id)
        {
            Color[] arm = LoadPixels(SourceFile(id, "arm"), out int width, out int height);

            Color[] disp = null;
            try
            {
                Color[] candidate = LoadPixels(SourceFile(id, "disp"), out int dw, out int dh);
                if (dw == width && dh == height)
                {
                    disp = candidate;
                }
                else
                {
                    Debug.LogWarning("[Spinnenspiel] disp-Map andere Größe (" + dw + "x" + dh +
                        " statt " + width + "x" + height + "), Höhe bleibt 0.5: " + id);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Spinnenspiel] disp-Map nicht lesbar, Höhe bleibt 0.5: " + id + " – " + e.Message);
            }

            Color[] mask = new Color[arm.Length];
            for (int i = 0; i < arm.Length; i++)
            {
                float h = disp != null
                    ? 0.2126f * disp[i].r + 0.7152f * disp[i].g + 0.0722f * disp[i].b
                    : 0.5f;
                mask[i] = new Color(arm[i].b, arm[i].r, h, 1f - arm[i].g);
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            tex.SetPixels(mask);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        /// <summary>Lädt ein Bild per Texture2D.LoadImage und liefert die Pixel als Color[].</summary>
        private static Color[] LoadPixels(string file, out int width, out int height)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(file));
            tex.filterMode = FilterMode.Point;
            width = tex.width;
            height = tex.height;
            Color[] pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return pixels;
        }

        /// <summary>Setzt Import-Einstellungen einer Textur und triggert den Reimport.</summary>
        private static void ConfigureImporter(string path, bool srgb, TextureImporterType type, int aniso)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Spinnenspiel] Keine TextureImporter-Einstellung möglich: " + path);
                return;
            }

            importer.sRGBTexture = srgb;
            importer.textureType = type;
            importer.anisoLevel = aniso;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }
}
