using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    public static class Milestone1SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/M1_Testgelaende.unity";

        [MenuItem("Spinnenspiel/Build/Milestone 1 Scene")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Terrain terrain = TerrainBuilder.Create(1337);
            LightingBuilder.Create();

            Camera cam = CameraBuilder.Create();

            Vector3 p = new Vector3(30f, 0f, 30f);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.1f;

            GameObject spider = SpiderBuilder.Create(p, cam.transform);
            SetLayerRecursive(spider, 2); // Layer 2 = "Ignore Raycast"

            CameraBuilder.Bind(cam, spider.transform);
            cam.transform.position = p + new Vector3(0f, 0.4f, -1.0f);
            cam.transform.LookAt(p);

            Physics.SyncTransforms();
            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.PoseAtRest();
            }

            EnsureScenesFolder();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            // Vorhandene Szenen (z. B. Testwelt) behalten, M1 nur ergaenzen.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath && System.IO.File.Exists(existing.path))
                {
                    scenes.Add(existing);
                }
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Spinnenspiel] M1 scene built at " + ScenePath);
        }

        private static void EnsureScenesFolder()
        {
            if (AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }

            AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
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
