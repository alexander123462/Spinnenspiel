using Spinnenspiel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut die Urwald-Szene "Urwald" (Milestone 10.1b):
    /// Licht, Weltgenerator (Terrain, Sumpfwasser, Bäume, Spinne aus Seed), Kamera, Spinne, Insekten.
    /// Das Gelände erzeugt die JungleWorldGenerator zur Laufzeit – kein statischer Terrain-Builder.
    /// </summary>
    public static class UrwaldSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Urwald.unity";
        private const string LibraryPath = "Assets/_Project/Art/Generated/JungleAssetLibrary.asset";
        private const string MeshFolder = "Assets/_Project/Art/Generated/Meshes/";
        private const string MatFolder = "Assets/_Project/Art/Generated/Materials/";
        private const string LayerFolder = "Assets/_Project/Art/Generated/Terrain/";

        [MenuItem("Spinnenspiel/Build/Urwald")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            WorldLightingBuilder.Build();

            // Asset-Bibliothek für den Laufzeit-Generator anlegen/ersetzen.
            AssetUtil.EnsureFolder("Assets/_Project/Art/Generated");
            JungleAssetLibrary lib = AssetDatabase.LoadAssetAtPath<JungleAssetLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = CreateAsset<JungleAssetLibrary>(LibraryPath, "JungleAssetLibrary");
            }

            lib.trunks = LoadMeshes(new[] { "VM_Trunk0", "VM_Trunk1", "VM_Trunk2" });
            lib.crownsNear = LoadMeshes(new[] { "VM_Crown0", "VM_Crown1", "VM_Crown2" });
            lib.crownsFar = LoadMeshes(new[] { "VM_CrownFar0", "VM_CrownFar1", "VM_CrownFar2" });
            lib.bush = LoadMesh("VM_Bush");
            lib.fern = LoadMesh("VM_Fern");
            lib.reed = LoadMesh("VM_Reed");
            lib.logs = LoadMeshes(new[] { "M_Log0", "M_Log1", "M_Log2" });
            lib.rocks = LoadMeshes(new[] { "M_Rock0", "M_Rock1", "M_Rock2", "M_Rock3", "M_Rock4" });

            lib.leaves = LoadMaterial("MAT_Leaves");
            lib.reedMat = LoadMaterial("MAT_Reed");
            lib.water = LoadMaterial("MAT_WaterPond");

            // Scan-Assets (Poly Haven) ersetzen die prozeduralen Klötzchen; fehlt einer, bleibt der bisherige Wert.
            lib.bark = ScanBark(LoadMaterial("MAT_TreeBark"));
            lib.logMat = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_dead_tree_trunk.mat", LoadMaterial("MAT_Bark"));
            lib.rockMat = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_mossy_rock.mat", LoadMaterial("MAT_Rock"));
            lib.fern = ScanMesh("Assets/_Project/Art/Scans/Models/fern_02/fern_02_1k.fbx", LoadMesh("VM_Fern"));
            // G4: Scan-Farn/-Strauch wirken mitteleuropäisch-braun → grün getönte Kopien (Originale bleiben unverändert).
            lib.fernMat = TropischMaterial(ScanMaterial("Assets/_Project/Art/Scans/Materials/M_fern_02.mat", LoadMaterial("MAT_Fern")), "MAT_FernTropisch");
            lib.bush = ScanMesh("Assets/_Project/Art/Scans/Models/shrub_02/shrub_02_2k.fbx", LoadMesh("VM_Bush"));
            lib.bushMat = TropischMaterial(ScanMaterial("Assets/_Project/Art/Scans/Materials/M_shrub_02.mat", LoadMaterial("MAT_Grass")), "MAT_BushTropisch");
            // A1: extra Busche-Varianten (shrub_03/04) with matching tropische Materials.
            lib.bushVariants = LoadBushVariantMeshes();
            lib.bushVariantMats = CreateBushVariantMats();

            // Riesenbläter (Blender, 3 Varianten): tropische Pflanze mit Blattschnitt-Mask und Normal.
            lib.giantLeaves = LoadRiesenblattMeshes();
            lib.giantLeafMats = CreateRiesenblattMats();

            // Baumfarne (Blender, 3 Varianten): 2 Submeshes pro FBX (0 = Stamm, 1 = Blatt).
            lib.treeFerns = LoadBaumfarnMeshes();
            var baumfarnMats = CreateBaumfarnMats();
            lib.treeFernLeafMat = baumfarnMats[0];
            lib.treeFernStemMat = baumfarnMats[1];

            // U4: Farn-Scatter-Patch (Blender scatter.py, 245 Instances per 8x8 m Patch).
            LoadFernScatter(lib);

            // U3b: Grundfarne (Blender fern_1..5.fbx, 5 Varianten) in die Library.
            LoadGroundFerns(lib);

            // Bodendecker (Scan-Gras/Kraut): nur Optik, keine Collider; um die Spinne herum.
            // A1: more species (nettle, periwinkle) -> variant per Instance via Hash01 below.
            LoadGroundCover(lib);

            // Poly-Haven-Baume (B3): FBX-Meshes (LOD0/1) + HDRP-Materials + Scales in die Library.
            LoadPHTrees(lib);

            // Boden: Scan-TerrainLayer (TileSize 2 m, ca. 1 Kachel pro 2 m); Fallback: prozedurale Layer.
            // Reihenfolge: Earth, Grass, Mud, Rock.
            lib.terrainLayers = ScanLayers(new[]
            {
                "Assets/_Project/Art/Scans/Layers/TL_forest_ground_04.terrainlayer",
                "Assets/_Project/Art/Scans/Layers/TL_sparse_grass.terrainlayer",
                "Assets/_Project/Art/Scans/Layers/TL_brown_mud_leaves_01.terrainlayer",
                "Assets/_Project/Art/Scans/Layers/TL_rocky_trail.terrainlayer",
            }, new[] { "TL_Earth", "TL_Grass", "TL_Mud", "TL_Rock" });

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            GameObject generator = new GameObject("Weltgenerator");
            var gen = generator.AddComponent<JungleWorldGenerator>();
            gen.library = lib;

            new GameObject("Wind").AddComponent<WindController>();
            // Weather: deterministic dry/rain cycle driving the audio crossfade.
            new GameObject("Weather").AddComponent<WeatherController>();
            // Z4 Tag/Nacht: Sun rotates slowly (one day = 20 min), ambient light adapts.
            GameObject dayNight = new GameObject("Tag/Nacht");
            DayNightCycle dayCycle = dayNight.AddComponent<DayNightCycle>();
            dayCycle.sun = Object.FindFirstObjectByType<Light>();
            new GameObject("Sky Ambient").AddComponent<SkyAmbientUpdater>();
            new GameObject("Jungle Ambience").AddComponent<JungleAmbientAudio>();
            new GameObject("Debug Overlay").AddComponent<DebugOverlay>();

            Camera cam = CameraBuilder.Create();
            UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.fieldOfView = 60f;
            cam.farClipPlane = 300f;

            Vector3 spawn = new Vector3(48f, 20f, 48f);
            GameObject spider = SpiderBuilder.Create(spawn, cam.transform);

            EffectsBuilder.Build(spider);
            SetLayerRecursive(spider, 2); // "Ignore Raycast": Kamera- und Beinstrahlen ignorieren die Spinne

            // Insekten 1: Spawner mit Vorlage (deaktivierte Fliege), Zentrum = Spinne.
            GameObject insekten = new GameObject("Insekten");
            InsectSpawner insektenSpawner = insekten.AddComponent<InsectSpawner>();
            insektenSpawner.count = 8;
            insektenSpawner.center = spider.transform;
            insektenSpawner.cycle = dayCycle; // J5: Flies only during the day.
            GameObject vorlage = InsectBuilder.CreateFly();
            vorlage.name = "Vorlage";
            vorlage.transform.SetParent(insekten.transform, false);
            vorlage.SetActive(false);
            insektenSpawner.template = vorlage;

            // Insekten 2 (Z7): Motte-Spawner — same spawner class, Motte template (slower, bigger).
            GameObject insekten2 = new GameObject("Insekten_Motte");
            InsectSpawner motteSpawner = insekten2.AddComponent<InsectSpawner>();
            motteSpawner.count = 4;
            motteSpawner.center = spider.transform;
            motteSpawner.cycle = dayCycle; // J5: Moths only at night.
            motteSpawner.activeAtNight = true;
            GameObject motteVorlage = InsectBuilder.CreateMoth();
            if (motteVorlage != null)
            {
                motteVorlage.name = "Vorlage_Motte";
                motteVorlage.transform.SetParent(insekten2.transform, false);
                motteVorlage.SetActive(false);
                motteSpawner.template = motteVorlage;
            }

            CameraBuilder.Bind(cam, spider.transform);
            cam.transform.position = spawn + new Vector3(0f, 0.35f, -0.95f);
            cam.transform.LookAt(spawn + Vector3.up * 0.1f);

            // Beine schon im Editor in Ruhepose bringen (sonst erst beim ersten Frame im Play-Modus).
            Physics.SyncTransforms();
            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.PoseAtRest();
            }

            AssetUtil.EnsureFolder("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            // Vorhandene Build-Szenen behalten, Urwald nur ergänzen, falls noch nicht drin.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                scenes.Add(existing);
            }
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Spinnenspiel] Urwald gebaut: " + ScenePath);
        }

        private static T CreateAsset<T>(string path, string label) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Mesh LoadMesh(string name)
        {
            string path = MeshFolder + name + ".asset";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[Urwald] Mesh fehlt: " + path);
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Mesh[] LoadMeshes(string[] names)
        {
            Mesh[] result = new Mesh[names.Length];
            for (int i = 0; i < names.Length; i++)
                result[i] = LoadMesh(names[i]);
            return result;
        }

        private static Material LoadMaterial(string name)
        {
            string path = MatFolder + name + ".mat";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[Urwald] Material fehlt: " + path);
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static TerrainLayer[] LoadLayers(string[] names)
        {
            TerrainLayer[] result = new TerrainLayer[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = LayerFolder + names[i] + ".terrainlayer";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogError("[Urwald] TerrainLayer fehlt: " + path);
                    continue;
                }
                result[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            }
            return result;
        }

        /// <summary>Kopie der Scan-Rinde unter Art/Generated mit Tiling gegen Streckung (Scan-Material bleibt unverändert).</summary>
        private static Material ScanBark(Material fallback)
        {
            const string src = "Assets/_Project/Art/Scans/Materials/M_pine_bark.mat";
            if (!System.IO.File.Exists(src))
            {
                Debug.LogWarning("[Urwald] Scan-Material fehlt, prozedurale Rinde bleibt: " + src);
                return fallback;
            }
            string copyPath = MatFolder + "MAT_ScanBark.mat";
            Material scan = AssetDatabase.LoadAssetAtPath<Material>(src);
            Material copy = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
            if (copy == null)
            {
                copy = new Material(scan) { name = "MAT_ScanBark" };
                AssetDatabase.CreateAsset(copy, copyPath);
            }
            copy.CopyPropertiesFromMaterial(scan);
            copy.mainTextureScale = new Vector2(1f, 0.35f); // Rinde gröber: weniger Wiederholungen, längere Fasern
            if (copy.HasProperty("_NormalMap") && copy.GetTexture("_NormalMap") != null)
                copy.SetFloat("_NormalScale", 1.5f);
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
        }

        private static Mesh[] LoadRiesenblattMeshes()
        {
            const string folder = "Assets/_Project/Art/Plants/Riesenblatt/";
            var result = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                string path = folder + "Riesenblatt_" + (i + 1) + ".fbx";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Urwald] Riesenblatt-FBX fehlt: " + path);
                    continue;
                }
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is Mesh mesh)
                    {
                        result[i] = mesh;
                        break;
                    }
                }
                if (result[i] == null)
                    Debug.LogWarning("[Urwald] Kein Mesh im Riesenblatt: " + path);
            }
            return result;
        }

        /// <summary>
        /// HDRP-Lit-Materialien fü die Riesenbläter: Albedo, Blattschnitt-Maske und Normal aus dem
        /// Blender-Export, Opaque mit Cutout. Werden nur angelegt, falls noch nicht vorhanden.
        /// </summary>
        private static Material[] CreateRiesenblattMats()
        {
            const string tex = "Assets/_Project/Art/Plants/Riesenblatt/Textures/";
            var result = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                string path = MatFolder + "MAT_Riesenblatt_" + (i + 1) + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Shader hdrpLit = Shader.Find("HDRP/Lit");
                    if (hdrpLit == null)
                    {
                        throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");
                    }
                    mat = new Material(hdrpLit);
                    mat.name = "MAT_Riesenblatt_" + (i + 1);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Riesenblatt_Leaf_Albedo.png"));
                mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Riesenblatt_Leaf_Mask.png"));
                mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Riesenblatt_Leaf_Normal.png"));
                mat.SetFloat("_SurfaceType", 1f); // Cutout (Blattschnitt)
                mat.SetFloat("_AlphaCutoffEnable", 1f);
                mat.SetFloat("_AlphaCutoff", 0.5f);
                mat.SetFloat("_Smoothness", 0.35f);
                mat.color = Color.white;
                EditorUtility.SetDirty(mat);
                result[i] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        private static Mesh[] LoadBaumfarnMeshes()
        {
            const string folder = "Assets/_Project/Art/Plants/Baumfarn/";
            var result = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                string path = folder + "Baumfarn_" + (i + 1) + ".fbx";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Urwald] Baumfarn-FBX fehlt: " + path);
                    continue;
                }
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is Mesh mesh)
                    {
                        result[i] = mesh;
                        Debug.Log("[Urwald] Baumfarn subMeshCount " + (i + 1) + ": " + mesh.subMeshCount
                            + " (erwartet 2: 0 = Stamm, 1 = Blatt)");
                        break;
                    }
                }
                if (result[i] == null)
                    Debug.LogWarning("[Urwald] Kein Mesh im Baumfarn: " + path);
            }
            return result;
        }

        /// <summary>
        /// HDRP-Lit-Materialien für die Baumfarne: Blatt (Albedo/Mask/Normal, Cutout 0.5, doppelseitig)
        /// und Stamm (Stamm-Albedo, Smoothness 0.2). Werden nur angelegt, falls noch nicht vorhanden.
        /// </summary>
        private static Material[] CreateBaumfarnMats()
        {
            const string tex = "Assets/_Project/Art/Plants/Baumfarn/Textures/";
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
                throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");

            Material leaf = CreateBaumfarnMat("MAT_Baumfarn_Leaf", hdrpLit,
                tex + "Baumfarn_Leaf_Albedo.png", tex + "Baumfarn_Leaf_Mask.png", tex + "Baumfarn_Leaf_Normal.png");
            // Fiederblaetter sind echte Geometrie -> opak, kein Alpha-Cutout (Cutout schnitt die Wedel weg)
            leaf.SetFloat("_SurfaceType", 0f);
            leaf.SetFloat("_AlphaCutoffEnable", 0f);
            leaf.SetFloat("_Smoothness", 0.35f);
            // F3b: Wedel wieder doppelseitig (Rueckseiten sichtbar, Flip-Normalen).
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_CullMode", 0f);
            leaf.SetFloat("_DoubleSidedNormalMode", 0f);

            Material stem = CreateBaumfarnMat("MAT_Baumfarn_Stem", hdrpLit,
                tex + "Baumfarn_Stem_Albedo.png", null, null);
            stem.SetFloat("_Smoothness", 0.2f);

            HDMaterial.ValidateMaterial(leaf);
            HDMaterial.ValidateMaterial(stem);

            EditorUtility.SetDirty(leaf);
            EditorUtility.SetDirty(stem);
            AssetDatabase.SaveAssets();
            return new[] { leaf, stem };
        }

        /// <summary>
        /// U4: Loads the Blender fern-scatter patch (ArtSource/UrwaldTest/fern/scatter.py ->
        /// Assets/_Project/Art/Plants/UrwaldTestFern/fern_scatter.fbx). Copies the FBX if missing,
        /// imports it, measures the per-instance meshes (2 Submeshes: stem + leaf, order measured
        /// via GetSubMesh(i).bounds) and builds two opaque HDRP materials with the Blender tints.
        /// Fern-Form ist echte Geometrie -> Opaque, KEIN Alpha-Cutout (Blender-Materials Alpha ignorieren).
        /// Fills the library fields: meshes, XZ-offsets (from mesh bounds), count, leaf-submesh index, mats.
        /// </summary>
        private static void LoadFernScatter(JungleAssetLibrary lib)
        {
            const string artFolder = "Assets/_Project/Art/Plants/UrwaldTestFern/";
            const string fbxAsset = artFolder + "fern_scatter.fbx";

            // Copy the FBX from ArtSource into the Unity-Asset-Tree (only if missing).
            if (!System.IO.File.Exists(fbxAsset))
            {
                string srcFbx = "ArtSource/UrwaldTest/fern/fern_scatter.fbx";
                if (!System.IO.File.Exists(srcFbx))
                {
                    Debug.LogWarning("[Urwald] Fern-Scatter-FBX missing: " + srcFbx);
                    return;
                }
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/UrwaldTestFern");
                System.IO.File.Copy(srcFbx, fbxAsset, true);
                AssetDatabase.ImportAsset(fbxAsset, ImportAssetOptions.ForceUpdate);
            }

            // The HDRP materials are built here; FBX material slots are ignored.
            ModelImporter modelImporter = AssetImporter.GetAtPath(fbxAsset) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning("[Urwald] Kein ModelImporter für: " + fbxAsset);
                return;
            }
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.SaveAndReimport();

            // Collect the joined instance meshes (2 Submeshes each, transforms baked).
            var collected = new System.Collections.Generic.List<Mesh>();
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbxAsset))
            {
                if (obj is Mesh mesh && mesh.subMeshCount > 0)
                {
                    collected.Add(mesh);
                }
            }
            if (collected.Count == 0)
            {
                Debug.LogWarning("[Urwald] Kein Mesh im Fern-Scatter-FBX: " + fbxAsset);
                return;
            }

            // Measure the submesh order on the first mesh: leaf = wider crown (XZ-extents),
            // stem = schmal. Never assume the FBX material order (known Blender pitfall).
            Mesh probe = collected[0];
            if (probe.subMeshCount < 2)
            {
                Debug.LogWarning("[Urwald] Fern-Scatter-Mesh has < 2 Submeshes: " + fbxAsset);
                return;
            }
            // Leaf-crown has far more triangles than the schmal stem-cylinder -> measure by index count.
            int idx0 = probe.GetSubMesh(0).indexCount;
            int idx1 = probe.GetSubMesh(1).indexCount;
            lib.fernScatterLeafSubmesh = idx1 > idx0 ? 1 : 0;

            // XZ-Positions: mesh bounds centre (transforms baked into the vertices).
            // Centred around the patch centre so the generator can add them to an anchor point.
            var xz = new System.Collections.Generic.List<Vector3>();
            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 max = new Vector3(-float.MaxValue, 0f, -float.MaxValue);
            foreach (Mesh m in collected)
            {
                var b = m.bounds;
                min.x = System.Math.Min(min.x, b.center.x - b.extents.x);
                min.z = System.Math.Min(min.z, b.center.z - b.extents.z);
                max.x = System.Math.Max(max.x, b.center.x + b.extents.x);
                max.z = System.Math.Max(max.z, b.center.z + b.extents.z);
                xz.Add(new Vector3(b.center.x, 0f, b.center.z));
            }
            Vector3 patchCenter = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
            for (int pi = 0; pi < xz.Count; pi++)
            {
                Vector3 q = xz[pi];
                q.x -= patchCenter.x;
                q.z -= patchCenter.z;
                xz[pi] = q;
            }
            lib.fernScatterPatchSize = System.Math.Max(max.x - min.x, max.z - min.z);

            lib.fernScatterMeshes = collected.ToArray();
            lib.fernScatterXZ = xz.ToArray();
            lib.fernScatterCount = collected.Count;

            // Opaque HDRP materials with the Blender tints (leaf green, stem brown).
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
                throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");

            Material leaf = GetOrCreateMat("MAT_FernScatter_Leaf", hdrpLit, new Color(0.10f, 0.27f, 0.06f));
            leaf.SetFloat("_SurfaceType", 0f);      // Opaque: Form is real geometry, no Cutout
            leaf.SetFloat("_AlphaCutoffEnable", 0f);
            leaf.SetFloat("_Smoothness", 0.15f);
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_DoubleSidedNormalMode", 2f); // Flip: backfaces visible
            leaf.enableInstancing = true;

            Material stem = GetOrCreateMat("MAT_FernScatter_Stem", hdrpLit, new Color(0.13f, 0.11f, 0.05f));
            stem.SetFloat("_SurfaceType", 0f);
            stem.SetFloat("_AlphaCutoffEnable", 0f);
            stem.SetFloat("_Smoothness", 0.2f);
            stem.enableInstancing = true;

            HDMaterial.ValidateMaterial(leaf);
            HDMaterial.ValidateMaterial(stem);
            EditorUtility.SetDirty(leaf);
            EditorUtility.SetDirty(stem);
            AssetDatabase.SaveAssets();

            lib.fernScatterLeafMat = leaf;
            lib.fernScatterStemMat = stem;

            Debug.Log("[Urwald] Fern-Scatter: " + lib.fernScatterCount + " Instances, leaf-submesh "
                + lib.fernScatterLeafSubmesh + " (idx0 " + idx0 + ", idx1 " + idx1 + ")");
        }

        /// <summary>U3b: Grundfarne (Blender fern_1..5.fbx) — 5 Varianten, je 1 Mesh. Der FBX-Importer
        /// merges Stem+Leaf into ONE submesh, so we take the largest-area mesh and paint it with one
        /// opaque leaf material (real geometry -> no Cutout). Fills lib.groundFerns / groundFernLeafMat.</summary>
        private static void LoadGroundFerns(JungleAssetLibrary lib)
        {
            const string artFolder = "Assets/_Project/Art/Plants/UrwaldTestFern/";
            const string srcFolder = "ArtSource/UrwaldTest/fern/";
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
                throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");

            var meshes = new System.Collections.Generic.List<Mesh>();
            for (int v = 1; v <= 5; v++)
            {
                string artPath = artFolder + "fern_" + v + ".fbx";
                if (!System.IO.File.Exists(artPath))
                {
                    string src = srcFolder + "fern_" + v + ".fbx";
                    if (System.IO.File.Exists(src))
                    {
                        AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/UrwaldTestFern");
                        System.IO.File.Copy(src, artPath, true);
                        AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
                    }
                }
                if (!System.IO.File.Exists(artPath))
                {
                    Debug.LogWarning("[Urwald] Grundfarn-FBX fehlt: " + artPath);
                    continue;
                }
                Mesh found = null;
                int bestTris = 0;
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
                {
                    if (obj is Mesh mesh && mesh != null)
                    {
                        int tris = 0;
                        for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                        if (tris > bestTris) { bestTris = tris; found = mesh; }
                    }
                }
                if (found == null || bestTris == 0)
                {
                    Debug.LogWarning("[Urwald] Grundfarn ohne Triangles: " + artPath);
                    continue;
                }
                meshes.Add(found);
            }
            if (meshes.Count == 0) { Debug.LogWarning("[Urwald] Kein Grundfarn-Mesh loaded."); return; }

            lib.groundFerns = meshes.ToArray();

            Material leaf = GetOrCreateMat("MAT_Grundfarn_Leaf", hdrpLit, new Color(0.09f, 0.24f, 0.06f));
            leaf.SetFloat("_SurfaceType", 0f);
            leaf.SetFloat("_AlphaCutoffEnable", 0f);
            leaf.SetFloat("_Smoothness", 0.15f);
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_DoubleSidedNormalMode", 2f);
            leaf.enableInstancing = true;

            HDMaterial.ValidateMaterial(leaf);
            EditorUtility.SetDirty(leaf);
            AssetDatabase.SaveAssets();

            lib.groundFernLeafMat = leaf;
            Debug.Log("[Urwald] Grundfarne: " + lib.groundFerns.Length + " Varianten loaded (opaque, single leaf-mat)");
        }

        /// <summary>U4: load or create an HDRP/Lit material asset in MatFolder with a base color.</summary>
        private static Material GetOrCreateMat(string name, Shader shader, Color color)
        {
            string path = MatFolder + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            return mat;
        }

        private static Material CreateBaumfarnMat(string name, Shader shader, string albedo, string mask, string normal)
        {
            string path = MatFolder + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (albedo != null) mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            if (mask != null) mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mask));
            if (normal != null) mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            mat.color = Color.white;
            return mat;
        }

        /// <summary>
        /// B3: loading the 5 Poly Haven trees (LOD0/LOD1 per FBX) plus their HDRP materials
        /// (wood opaque; leaves Alpha-Cutout 0.5, double-sided) into the asset library.
        /// Submesh order measured via GetSubMesh(i).bounds (leaf = wider crown), scale from
        /// mesh height (Jacaranda approx. 30 m, island trees 3-5x).
        /// </summary>
        private static void LoadPHTrees(JungleAssetLibrary lib)
        {
            const string root = "Assets/_Project/Art/Plants/PH_Baeume/";
            string[] ids = { "jacaranda_tree", "island_tree_01", "island_tree_02", "island_tree_03", "tree_small_02" };

            lib.phTreeLod0 = new Mesh[ids.Length];
            lib.phTreeLod1 = new Mesh[ids.Length];
            lib.phTreeWoodMats = new Material[ids.Length];
            lib.phTreeLeafMats = new Material[ids.Length];
            lib.phTreeScales = new float[ids.Length];
            lib.phTreeSlotNames = new string[ids.Length];

            for (int i = 0; i < ids.Length; i++)
            {
                string fbx = root + ids[i] + "/" + ids[i] + ".fbx";
                var lodMeshes = new Mesh[2];
                var fbxRenderers = new MeshRenderer[8]; // FBX takes: few Renderers per model
                int nRenders = 0;
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                {
                    if (obj is Mesh mesh && mesh.name.Contains("_LOD0")) lodMeshes[0] = mesh;
                    if (obj is Mesh m1 && m1.name.Contains("_LOD1")) lodMeshes[1] = m1;
                    if (obj is MeshRenderer mr && nRenders < fbxRenderers.Length) fbxRenderers[nRenders++] = mr;
                }
                // BUG-1 name ground truth: FBX slot names on the LOD0 renderer, index i =
                // submesh i (Unity sorts slots alphabetically, so order is NOT a contract).
                // MeshRenderer has NO public mesh accessor at all (CS1061 both for .mesh and
                // .sharedMesh) -> select by NAME only: prefer "_LOD0" (case-insensitive), else
                // first collected renderer with a warning (slot-name log stays ground truth).
                MeshRenderer lod0Renderer = null;
                for (int ri = 0; ri < nRenders; ri++)
                {
                    var cand = fbxRenderers[ri];
                    if (lod0Renderer == null && cand.name.ToLower().Contains("_lod0")) lod0Renderer = cand;
                }
                if (lod0Renderer == null && nRenders > 0)
                {
                    lod0Renderer = fbxRenderers[0];
                    Debug.LogWarning("[Urwald] PH-Tree " + ids[i] + ": no renderer named *_LOD0*, using first FBX MeshRenderer for slot names (check the log line below)");
                }

                if (lodMeshes[0] == null || lodMeshes[1] == null)
                {
                    foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                    {
                        if (obj is Mesh badMesh)
                        {
                            Debug.Log("[Urwald] PH-Tree Mesh found: " + badMesh.name + " verts=" + badMesh.vertexCount);
                        }
                    }

                    Debug.LogWarning("[Urwald] PH-Tree FBX without LOD0/LOD1: " + fbx);
                    continue;
                }

                lib.phTreeLod0[i] = lodMeshes[0];
                lib.phTreeLod1[i] = lodMeshes[1];
                // Slot names once per species (ground truth for the runtime name-match rule).
                var slotNames = new System.Collections.Generic.List<string>();
                if (lod0Renderer != null && lod0Renderer.sharedMaterials != null)
                {
                    foreach (var sm in lod0Renderer.sharedMaterials)
                        slotNames.Add(sm != null ? sm.name : "");
                }
                lib.phTreeSlotNames[i] = string.Join("|", slotNames.ToArray());
                if (lod0Renderer == null) Debug.LogWarning("[Urwald] PH-Tree FBX without LOD0 MeshRenderer: " + fbx + " (runtime falls back to geometry rule)");
                else Debug.Log(string.Format("[Urwald] PH-Tree {0} slot names: {1}", ids[i], lib.phTreeSlotNames[i]));
                var mats = CreatePHTreeMats(ids[i]);
                lib.phTreeWoodMats[i] = mats[0];
                lib.phTreeLeafMats[i] = mats[1];

                float height = lodMeshes[0].bounds.size.y;
                float target = ids[i] == "jacaranda_tree" ? 30f : 12f;
                float scale = target / Mathf.Max(0.01f, height);
                if (ids[i] != "jacaranda_tree")
                {
                    scale = Mathf.Clamp(scale, 3f, 5f);
                }

                lib.phTreeScales[i] = scale;
                Debug.Log(string.Format("[Urwald] PH-Tree {0}: Hoehe {1:0.00} m -> Scale {2:0.00} -> Hoehe {3:0.0} m (Kriterium Jacaranda 25-35, else 9-20)",
                    ids[i], height, scale, height * scale));
            }
        }

        /// <summary>
        /// HDRP-Lit-Materials per PH-Tree: [0]=Holz opak (trunk/branch-Textures),
        /// [1]=Blätter Alpha-Cutout 0.5, doppelseitig (leaves_*-Textures). Existing get reused,
        /// afterwards HDMaterial.ValidateMaterial. Texture-Import: Normal non-sRGB, Mask non-sRGB.
        /// </summary>
        private static Material[] CreatePHTreeMats(string id)
        {
            string tex = "Assets/_Project/Art/Plants/PH_Baeume/" + id + "/Textures/";
            // Wood-Base: Jacaranda "trunk", else "<id>_Albedo".
            string woodBase = tex + id + "_" + (id == "jacaranda_tree" ? "trunk_" : "");

            Material wood = GetOrLoadPHTreeMat("MAT_PHTree_" + id + "_Wood");
            SetPHTexture(wood, woodBase + "Albedo.png", true);
            SetPHTexture(wood, woodBase + "Normal.png", false);
            SetPHTexture(wood, woodBase + "Mask.png", false);
            wood.SetFloat("_SurfaceType", 0f); // opaque
            wood.SetFloat("_AlphaCutoffEnable", 0f);
            wood.SetFloat("_Smoothness", 0.15f);

            Material leaf = GetOrLoadPHTreeMat("MAT_PHTree_" + id + "_Leaf");
            SetPHTexture(leaf, tex + id + "_leaves_Albedo.png", true);
            SetPHTexture(leaf, tex + id + "_leaves_Normal.png", false);
            SetPHTexture(leaf, tex + id + "_leaves_Mask.png", false);
            leaf.SetFloat("_SurfaceType", 1f); // Cutout
            leaf.SetFloat("_AlphaCutoffEnable", 1f);
            leaf.SetFloat("_AlphaCutoff", 0.5f);
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_CullMode", 0f);
            leaf.SetFloat("_DoubleSidedNormalMode", 2f); // Reversed (Flip-Normals)
            leaf.enableInstancing = true;

            HDMaterial.ValidateMaterial(wood);
            HDMaterial.ValidateMaterial(leaf);
            EditorUtility.SetDirty(wood);
            EditorUtility.SetDirty(leaf);
            return new[] { wood, leaf };
        }

        private static Material GetOrLoadPHTreeMat(string name)
        {
            string path = MatFolder + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader hdrpLit = Shader.Find("HDRP/Lit");
                if (hdrpLit == null)
                {
                    throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");
                }

                mat = new Material(hdrpLit) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = Color.white;
            return mat;
        }

        /// <summary>Texture-Assignment with Import-Settings: sRGB Albedo (alphaTransparency), Normal non-sRGB.</summary>
        private static void SetPHTexture(Material mat, string texPath, bool isAlbedo)
        {
            if (!System.IO.File.Exists(texPath))
            {
                Debug.LogWarning("[Urwald] PH-Tree-Texture fehlt: " + texPath);
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                bool isNormal = texPath.EndsWith("Normal.png");
                importer.sRGBTexture = isAlbedo;
                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.alphaIsTransparency = isAlbedo;
                importer.SaveAndReimport();
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogWarning("[Urwald] PH-Texture not loadable: " + texPath);
                return;
            }

            string prop = texPath.EndsWith("Normal.png") ? "_NormalMap" : texPath.EndsWith("Mask.png") ? "_MaskMap" : "_BaseColorMap";
            mat.SetTexture(prop, tex);
        }

        /// <summary>Lädt Scan-Bodendecker (Gras/Kraut) als Mesh-/Material-Paare in die Asset-Bibliothek.</summary>
        private static void LoadGroundCover(JungleAssetLibrary lib)
        {
            // A1: more species (nettle, periwinkle) -> the variant-per-Instance Hash01 pick
            // below spreads them; groundCover[0] stays grass_medium_01 so older code keeps working.
            string[] ids = { "grass_medium_01", "grass_medium_02", "weed_plant_02", "nettle_plant", "periwinkle_plant" };
            var meshes = new System.Collections.Generic.List<Mesh>();
            var mats = new System.Collections.Generic.List<Material>();

            foreach (string id in ids)
            {
                if (!ScanModels.IsAvailable(id))
                {
                    Debug.LogWarning("[Urwald] Bodendecker nicht verfügbar, übersprungen: " + id);
                    continue;
                }

                try
                {
                    ScanModels.EnsureImported(id);
                    Mesh[] loaded = ScanModels.LoadMeshes(id);
                    if (loaded.Length == 0)
                    {
                        Debug.LogWarning("[Urwald] Bodendecker ohne Mesh, übersprungen: " + id);
                        continue;
                    }

                    Material mat = ScanModels.CreateFoliageMaterial(id);
                    if (mat == null)
                    {
                        Debug.LogWarning("[Urwald] Bodendecker-Material fehlt, übersprungen: " + id);
                        continue;
                    }
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);

                    meshes.Add(loaded[0]);
                    mats.Add(mat);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Urwald] Bodendecker fehlgeschlagen, übersprungen: " + id + " – " + e.Message);
                }
            }

            lib.groundCover = meshes.ToArray();
            lib.groundCoverMats = mats.ToArray();
        }

        /// <summary>A1: extra Busche-Meshes (shrub_03/shrub_04) via ScanModels; missing ones skipped.</summary>
        private static Mesh[] LoadBushVariantMeshes()
        {
            var meshes = new System.Collections.Generic.List<Mesh>();
            foreach (string id in new[] { "shrub_03", "shrub_04" })
            {
                if (!ScanModels.IsAvailable(id))
                {
                    Debug.LogWarning("[Urwald] Busche-Variante nicht verfügbar: " + id);
                    continue;
                }
                ScanModels.EnsureImported(id);
                Mesh[] loaded = ScanModels.LoadMeshes(id);
                if (loaded.Length == 0)
                {
                    Debug.LogWarning("[Urwald] Busche-Variante ohne Mesh: " + id);
                    continue;
                }
                meshes.Add(loaded[0]);
            }
            return meshes.ToArray();
        }

        /// <summary>A1: tropische Materials for the extra Busche (same green tint as shrub_02).</summary>
        private static Material[] CreateBushVariantMats()
        {
            var mats = new System.Collections.Generic.List<Material>();
            foreach (string id in new[] { "shrub_03", "shrub_04" })
            {
                Material src = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_" + id + ".mat", null);
                if (src == null)
                {
                    Debug.LogWarning("[Urwald] Busche-Variante ohne Scan-Material: " + id);
                    continue;
                }
                mats.Add(TropischMaterial(src, "MAT_" + id + "Tropisch"));
            }
            return mats.ToArray();
        }

        private static Material TropischMaterial(Material src, string name)
        {
            if (src == null)
            {
                return null;
            }
            string path = "Assets/_Project/Art/Generated/Materials/" + name + ".mat";
            AssetUtil.EnsureFolder("Assets/_Project/Art/Generated/Materials");
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Color tint = new Color(0.55f, 0.95f, 0.40f, 1f);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(src);
                existing.color = tint;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            Material copy = new Material(src) { name = name };
            copy.color = tint;
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static Material ScanMaterial(string path, Material fallback)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning("[Urwald] Scan-Material fehlt, prozedurales bleibt: " + path);
                return fallback;
            }
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogWarning("[Urwald] Scan-Material nicht lesbar, prozedurales bleibt: " + path);
                return fallback;
            }
            return mat;
        }

        private static Mesh ScanMesh(string fbxPath, Mesh fallback)
        {
            if (!System.IO.File.Exists(fbxPath))
            {
                Debug.LogWarning("[Urwald] Scan-Modell fehlt, prozedurales bleibt: " + fbxPath);
                return fallback;
            }
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is Mesh mesh)
                    return mesh;
            }
            Debug.LogWarning("[Urwald] Kein Mesh im Scan-Modell: " + fbxPath);
            return fallback;
        }

        private static TerrainLayer[] ScanLayers(string[] paths, string[] fallbackNames)
        {
            TerrainLayer[] result = new TerrainLayer[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                TerrainLayer layer = null;
                if (System.IO.File.Exists(paths[i]))
                    layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(paths[i]);
                if (layer == null)
                {
                    Debug.LogWarning("[Urwald] Scan-TerrainLayer fehlt, prozeduraler bleibt: " + paths[i]);
                    layer = LoadLayers(new[] { fallbackNames[i] })[0];
                }
                result[i] = layer;
            }
            return result;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
