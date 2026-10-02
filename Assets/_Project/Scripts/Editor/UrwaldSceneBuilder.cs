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

            lib.bark = LoadMaterial("MAT_TreeBark");
            lib.leaves = LoadMaterial("MAT_Leaves");
            lib.bushMat = LoadMaterial("MAT_Grass");
            lib.fernMat = LoadMaterial("MAT_Fern");
            lib.reedMat = LoadMaterial("MAT_Reed");
            lib.logMat = LoadMaterial("MAT_Bark");
            lib.rockMat = LoadMaterial("MAT_Rock");
            lib.water = LoadMaterial("MAT_WaterPond");

            // Reihenfolge: Earth, Grass, Mud, Rock.
            lib.terrainLayers = LoadLayers(new[] { "TL_Earth", "TL_Grass", "TL_Mud", "TL_Rock" });

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
