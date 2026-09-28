using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Editor-Werkzeug für Poly-Haven-Foto-Scan-3D-Modelle (Pflanzen, Steine, Stämme):
    /// kopiert FBX + Texturen, verschmilzt diff + alpha zu einer Albedo (RGBA32),
    /// importiert mit passenden Einstellungen und legt Foliage-/Solid-Materialien an.
    /// Quelle: AssetSource/polyhaven/models/&lt;id&gt; (im Projektordner, nicht in git).
    /// </summary>
    public static class ScanModels
    {
        private static readonly string[] AllIds =
        {
            "fern_02", "weed_plant_02", "shrub_02", "shrub_03", "shrub_04",
            "nettle_plant", "dandelion_01", "periwinkle_plant", "dry_branches_medium_01",
            "dead_tree_trunk", "dead_tree_trunk_02", "tree_stump_01", "tree_stump_02",
            "rock_07", "rock_09", "stone_01", "rock_moss_set_01", "rock_moss_set_02",
            "boulder_01", "root_cluster_01"
        };

        private const string ModelRoot = "Assets/_Project/Art/Scans/Models";
        private const string MaterialRoot = "Assets/_Project/Art/Scans/Materials";

        /// <summary>Quellordner relativ zum Projektordner (nicht unter Assets/).</summary>
        private static string SourceRoot =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "AssetSource", "polyhaven", "models");

        private static string TargetFolder(string id) => ModelRoot + "/" + id;

        private static string TargetFbx(string id, string res) => TargetFolder(id) + "/" + id + "_" + res + ".fbx";

        private static string TargetDiff(string id, string res) => TargetFolder(id) + "/" + id + "_diff_" + res + ".jpg";

        private static string TargetNormal(string id, string res) => TargetFolder(id) + "/" + id + "_nor_gl_" + res + ".exr";

        private static string TargetAlbedo(string id) => TargetFolder(id) + "/" + id + "_albedo.png";

        /// <summary>Liefert die im FBX-Dateinamen vorliegende Auflösung (2k vor 1k), sonst null.</summary>
        private static string ResolveResolution(string id)
        {
            foreach (string res in new[] { "2k", "1k" })
            {
                if (File.Exists(Path.Combine(SourceRoot, id, id + "_" + res + ".fbx")))
                {
                    return res;
                }
            }

            return null;
        }

        /// <summary>True, wenn FBX, diff und nor_gl im Quellordner vorhanden sind.</summary>
        public static bool IsAvailable(string id)
        {
            string res = ResolveResolution(id);
            if (res == null)
            {
                return false;
            }

            return File.Exists(Path.Combine(SourceRoot, id, "textures", id + "_diff_" + res + ".jpg"))
                && File.Exists(Path.Combine(SourceRoot, id, "textures", id + "_nor_gl_" + res + ".exr"));
        }

        /// <summary>
        /// Stellt sicher, dass FBX, diffuse/normal (und ggf. verschmolzene Albedo) als Assets
        /// unter Assets/_Project/Art/Scans/Models/&lt;id&gt;/ vorliegen. Liefert sofort,
        /// wenn das Modell schon importiert ist.
        /// </summary>
        public static void EnsureImported(string id)
        {
            string res = ResolveResolution(id);
            if (res == null)
            {
                throw new FileNotFoundException("[Spinnenspiel] Kein FBX gefunden für Modell: " + id);
            }

            string fbxTarget = TargetFbx(id, res);
            bool hasAlpha = File.Exists(Path.Combine(SourceRoot, id, "textures", id + "_alpha_" + res + ".png"));

            if (AssetDatabase.LoadMainAssetAtPath(fbxTarget) == null)
            {
                AssetUtil.EnsureFolder(TargetFolder(id));
                File.Copy(Path.Combine(SourceRoot, id, id + "_" + res + ".fbx"), fbxTarget, true);
                File.Copy(Path.Combine(SourceRoot, id, "textures", id + "_diff_" + res + ".jpg"), TargetDiff(id, res), true);
                File.Copy(Path.Combine(SourceRoot, id, "textures", id + "_nor_gl_" + res + ".exr"), TargetNormal(id, res), true);

                if (hasAlpha)
                {
                    File.WriteAllBytes(TargetAlbedo(id), MergeAlpha(id, res));
                }

                AssetDatabase.ImportAsset(fbxTarget, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(TargetDiff(id, res), ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(TargetNormal(id, res), ImportAssetOptions.ForceUpdate);
                if (hasAlpha)
                {
                    AssetDatabase.ImportAsset(TargetAlbedo(id), ImportAssetOptions.ForceUpdate);
                }
            }

            // Import-Einstellungen immer (neu) anwenden, damit geänderte Werte
            // (z. B. useFileScale) auch auf bereits importierte Modelle eingreifen.
            ModelImporter modelImporter = AssetImporter.GetAtPath(fbxTarget) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning("[Spinnenspiel] Kein ModelImporter für: " + fbxTarget);
            }
            else
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                modelImporter.meshCompression = ModelImporterMeshCompression.Medium;
                modelImporter.useFileScale = false; // FBX-Einheit ignorieren, Vertices im Maßstab der Datei
                modelImporter.SaveAndReimport();
            }

            ConfigureTextureImporter(TargetDiff(id, res), srgb: true, normalMap: false, alphaTransparency: false);
            ConfigureTextureImporter(TargetNormal(id, res), srgb: false, normalMap: true, alphaTransparency: false);
            if (hasAlpha)
            {
                ConfigureTextureImporter(TargetAlbedo(id), srgb: true, normalMap: false, alphaTransparency: true);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Lädt alle Mesh-Unterassets des FBX-Modells. Liefert leer, wenn das Modell fehlt.</summary>
        public static Mesh[] LoadMeshes(string id)
        {
            List<Mesh> meshes = new List<Mesh>();
            foreach (string res in new[] { "2k", "1k" })
            {
                string path = TargetFbx(id, res);
                if (!File.Exists(path))
                {
                    continue;
                }

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is Mesh mesh)
                    {
                        meshes.Add(mesh);
                    }
                }

                break;
            }

            return meshes.ToArray();
        }

        /// <summary>Varianten-Suffix: Buchstabe, optional mit _LOD0 (fern_02_a, weed_plant_02_b_LOD0).</summary>
        private static readonly System.Text.RegularExpressions.Regex VariantSuffix =
            new System.Text.RegularExpressions.Regex(@"_[a-z](_LOD\d+)?$");

        /// <summary>
        /// Lädt alle Varianten-Meshes des FBX (mit Buchstaben-Suffix), ohne die Sammelgruppe,
        /// sortiert nach Name. Hat das Modell gar keine Varianten-Suffixe, zählen alle Meshes.
        /// </summary>
        public static Mesh[] LoadVariants(string id)
        {
            List<Mesh> all = new List<Mesh>(LoadMeshes(id));
            List<Mesh> variants = new List<Mesh>();
            foreach (Mesh mesh in all)
            {
                if (VariantSuffix.IsMatch(mesh.name))
                {
                    variants.Add(mesh);
                }
            }

            if (variants.Count == 0)
            {
                variants = all;
            }

            variants.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return variants.ToArray();
        }

        /// <summary>
        /// Erzeugt ein Foliage-Material (Spinnenspiel/Foliage, Alphatest) als Asset unter
        /// Assets/_Project/Art/Scans/Materials/M_&lt;id&gt;.mat (vorhandenes wird ersetzt).
        /// _BaseMap: verschmolzene Albedo (mit Alpha) oder die diffuse, falls keine alpha existiert.
        /// </summary>
        public static Material CreateFoliageMaterial(string id)
        {
            Shader shader = Shader.Find("Spinnenspiel/Foliage");
            if (shader == null)
            {
                throw new InvalidOperationException("[Spinnenspiel] Shader nicht gefunden: Spinnenspiel/Foliage");
            }

            Material material = new Material(shader);
            material.SetTexture("_BaseMap", BaseTexture(id));
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cutoff", 0.4f);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_TipColor", Color.white);
            material.SetFloat("_VariationAmount", 0.1f);
            material.SetFloat("_Smoothness", 0.25f);
            material.enableInstancing = true;

            AssetUtil.ReplaceAsset(material, MaterialRoot + "/M_" + id + ".mat");
            return material;
        }

        /// <summary>
        /// Erzeugt ein URP/Lit-Material (diffuse, normal) als Asset unter
        /// Assets/_Project/Art/Scans/Materials/M_&lt;id&gt;.mat (vorhandenes wird ersetzt).
        /// </summary>
        public static Material CreateSolidMaterial(string id)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", BaseTexture(id));
            material.SetTexture("_BumpMap", TextureOf(id, "nor_gl"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", 0.2f);
            material.enableInstancing = true;

            AssetUtil.ReplaceAsset(material, MaterialRoot + "/M_" + id + ".mat");
            return material;
        }

        /// <summary>
        /// Importiert alle Scan-Modelle (Menü: Spinnenspiel/Scans/Modelle importieren),
        /// legt je Modell das passende Material an und loggt Mesh-/Dreieckszahlen.
        /// </summary>
        [MenuItem("Spinnenspiel/Scans/Modelle importieren")]
        public static void ImportAll()
        {
            foreach (string id in AllIds)
            {
                if (!IsAvailable(id))
                {
                    Debug.LogWarning("[Spinnenspiel] Scan-Modell nicht verfügbar, Quelldateien fehlen: " + id);
                    continue;
                }

                try
                {
                    EnsureImported(id);
                    Mesh[] variants = LoadVariants(id);

                    int min = int.MaxValue;
                    int max = 0;
                    foreach (Mesh mesh in variants)
                    {
                        int tris = mesh.triangles.Length / 3;
                        if (tris < min)
                        {
                            min = tris;
                        }
                        if (tris > max)
                        {
                            max = tris;
                        }
                    }

                    if (HasAlphaSource(id))
                    {
                        CreateFoliageMaterial(id);
                    }
                    else
                    {
                        CreateSolidMaterial(id);
                    }

                    Vector3 size = variants.Length > 0 ? variants[0].bounds.size : Vector3.zero;
                    Debug.Log(string.Format(
                        "[Spinnenspiel] Scan-Modell {0}: {1} Varianten, Dreiecke je Variante {2}–{3}, Größe Variante 1 {4:0.00} × {5:0.00} × {6:0.00} m",
                        id, variants.Length,
                        variants.Length > 0 ? min : 0,
                        max,
                        size.x, size.y, size.z));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Spinnenspiel] Scan-Modell fehlgeschlagen: " + id + " – " + e.Message);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>True, wenn im Quellordner eine alpha-Textur existiert (Pflanze → Foliage-Material).</summary>
        private static bool HasAlphaSource(string id)
        {
            string res = ResolveResolution(id);
            return res != null
                && File.Exists(Path.Combine(SourceRoot, id, "textures", id + "_alpha_" + res + ".png"));
        }

        /// <summary>Lädt die diffuse (oder Albedo, falls vorhanden) des Modells als Texture2D.</summary>
        private static Texture2D BaseTexture(string id)
        {
            string path = File.Exists(TargetAlbedo(id)) ? TargetAlbedo(id) : null;
            if (path == null)
            {
                path = TexturePath(id, "diff");
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Lädt eine importierte Textur (diff oder nor_gl) des Modells.</summary>
        private static Texture2D TextureOf(string id, string kind)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(id, kind));
        }

        /// <summary>Pfad der importierten Textur einer Art (diff/nor_gl), unabhängig von der Auflösung.</summary>
        private static string TexturePath(string id, string kind)
        {
            foreach (string res in new[] { "2k", "1k" })
            {
                string path = TargetFolder(id) + "/" + id + "_" + kind + "_" + res + (kind == "diff" ? ".jpg" : ".exr");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("[Spinnenspiel] Importierte Textur fehlt: " + id + " (" + kind + ")");
        }

        /// <summary>Verschmilzt diff (RGB) und alpha (Graustufe → A) zu einem RGBA32-PNG. Liefert die PNG-Bytes.</summary>
        private static byte[] MergeAlpha(string id, string res)
        {
            string dir = Path.Combine(SourceRoot, id, "textures");
            Color[] diff = LoadPixels(Path.Combine(dir, id + "_diff_" + res + ".jpg"), out int width, out int height);
            Color[] alpha = LoadPixels(Path.Combine(dir, id + "_alpha_" + res + ".png"), out int aw, out int ah);

            Color[] pixels;
            if (aw != width || ah != height)
            {
                Debug.LogWarning("[Spinnenspiel] alpha-Map andere Größe (" + aw + "x" + ah +
                    " statt " + width + "x" + height + "), Alpha wird ignoriert: " + id);
                pixels = diff;
            }
            else
            {
                pixels = new Color[diff.Length];
                for (int i = 0; i < diff.Length; i++)
                {
                    float gray = (alpha[i].r + alpha[i].g + alpha[i].b) / 3f;
                    pixels[i] = new Color(diff[i].r, diff[i].g, diff[i].b, gray);
                }
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        /// <summary>Lädt ein Bild per Texture2D.LoadImage und liefert die Pixel als Color[].</summary>
        private static Color[] LoadPixels(string file, out int width, out int height)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(file));
            width = tex.width;
            height = tex.height;
            Color[] pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return pixels;
        }

        /// <summary>Setzt die Import-Einstellungen einer Textur und triggert den Reimport.</summary>
        private static void ConfigureTextureImporter(string path, bool srgb, bool normalMap, bool alphaTransparency)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Spinnenspiel] Keine TextureImporter-Einstellung möglich: " + path);
                return;
            }

            importer.sRGBTexture = srgb;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.alphaIsTransparency = alphaTransparency;
            importer.SaveAndReimport();
        }
    }
}
