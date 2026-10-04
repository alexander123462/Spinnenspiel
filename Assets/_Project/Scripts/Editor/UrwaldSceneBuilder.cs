using Spinnenspiel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            lib.fernMat = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_fern_02.mat", LoadMaterial("MAT_Fern"));
            lib.bush = ScanMesh("Assets/_Project/Art/Scans/Models/shrub_02/shrub_02_2k.fbx", LoadMesh("VM_Bush"));
            lib.bushMat = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_shrub_02.mat", LoadMaterial("MAT_Grass"));

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
            new GameObject("Sky Ambient").AddComponent<SkyAmbientUpdater>();
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
            GameObject vorlage = InsectBuilder.CreateFly();
            vorlage.name = "Vorlage";
            vorlage.transform.SetParent(insekten.transform, false);
            vorlage.SetActive(false);
            insektenSpawner.template = vorlage;

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
            copy.mainTextureScale = new Vector2(2f, 1f); // Rinde um den Stamm dichter, Vertikalfasern bleiben
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
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
