using Spinnenspiel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut die komplette Testwelt (Milestone 3–6) als Szene "Testwelt":
    /// Gelände mit Bach und Teich, Hindernisse, Vegetation, Wasser, Licht, Wind, Audio, Spinne und Kamera.
    /// Alles ist deterministisch – derselbe Aufruf erzeugt immer dieselbe Welt.
    /// </summary>
    public static class TestWorldBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Testwelt.unity";
        private const string M1ScenePath = "Assets/_Project/Scenes/M1_Testgelaende.unity";

        [MenuItem("Spinnenspiel/Build/Testwelt (Milestone 3-6)")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Terrain terrain = WorldTerrainBuilder.Create();
            Debug.Log("[Spinnenspiel] Terrain gebaut.");

            HorizonBuilder.Build(terrain);
            WorldLightingBuilder.Build();
            EnvironmentBuilder.Build(terrain, 7);
            Debug.Log("[Spinnenspiel] Hindernisse gebaut.");

            VegetationBuilder.Build(terrain, 11);
            Debug.Log("[Spinnenspiel] Vegetation gebaut.");

            WaterBuilder.Build();
            Debug.Log("[Spinnenspiel] Wasser gebaut.");

            new GameObject("Wind").AddComponent<WindController>();
            new GameObject("Sky Ambient").AddComponent<SkyAmbientUpdater>();
            new GameObject("Ambient Audio").AddComponent<AmbientAudio>();
            new GameObject("Debug Overlay").AddComponent<DebugOverlay>();

            Camera cam = CameraBuilder.Create();
            UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.fieldOfView = 60f;
            cam.farClipPlane = 300f;

            Vector3 spawn = WorldLayout.SpawnPoint;
            spawn.y = terrain.SampleHeight(spawn) + terrain.transform.position.y + 0.02f;
            GameObject spider = SpiderBuilder.Create(spawn, cam.transform);

            // Weltgrenzen (XZ-Klemmung), damit die Spinne die Welt nicht verlässt.
            var boundsSo = new SerializedObject(spider.GetComponent<Spinnenspiel.SpiderMovement>());
            boundsSo.FindProperty("useWorldBounds").boolValue = true;
            boundsSo.FindProperty("worldBoundsMin").vector3Value = new Vector3(0.5f, -100f, 0.5f);
            boundsSo.FindProperty("worldBoundsMax").vector3Value = new Vector3(WorldLayout.Size - 0.5f, 100f, WorldLayout.Size - 0.5f);
            boundsSo.ApplyModifiedPropertiesWithoutUndo();

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
            // Vorhandene Build-Szenen behalten, Testwelt und M1 nur ergaenzen.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (System.IO.File.Exists(existing.path) &&
                    existing.path != ScenePath && existing.path != M1ScenePath)
                {
                    scenes.Add(existing);
                }
            }
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(M1ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Spinnenspiel] Testwelt gebaut: " + ScenePath);
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
