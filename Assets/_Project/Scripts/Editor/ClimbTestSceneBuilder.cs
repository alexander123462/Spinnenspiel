using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>Baut die Kletter-Testszene: Boden, Wand, Block, Stamm, Ueberhang, Licht, Kamera, Spinne.</summary>
    public static class ClimbTestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Klettertest.unity";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";

        [MenuItem("Spinnenspiel/Build/Klettertest")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material bodenMat = AssetUtil.CreateLitMaterial(MaterialsFolder + "/MAT_KlettertestBoden.mat", new Color(0.35f, 0.35f, 0.36f), 0.3f);
            Material wandMat = AssetUtil.CreateLitMaterial(MaterialsFolder + "/MAT_KlettertestWand.mat", new Color(0.42f, 0.34f, 0.26f), 0.3f);
            Material blockMat = AssetUtil.CreateLitMaterial(MaterialsFolder + "/MAT_KlettertestBlock.mat", new Color(0.38f, 0.32f, 0.26f), 0.3f);
            Material stammMat = AssetUtil.CreateLitMaterial(MaterialsFolder + "/MAT_KlettertestStamm.mat", new Color(0.32f, 0.23f, 0.15f), 0.3f);
            Material plaetteMat = AssetUtil.CreateLitMaterial(MaterialsFolder + "/MAT_KlettertestUeberhang.mat", new Color(0.40f, 0.37f, 0.32f), 0.3f);
            Material netzstabMat = AssetUtil.CreateLitMaterial(MaterialsFolder + "/MAT_KlettertestNetzstab.mat", new Color(0.30f, 0.26f, 0.20f), 0.3f);

            GameObject boden = CreateBox("Boden", new Vector3(20f, 0.2f, 20f), new Vector3(0f, -0.1f, 0f), bodenMat);
            GameObject wand = CreateBox("Wand", new Vector3(4f, 2f, 0.3f), new Vector3(0f, 1f, 3f), wandMat);
            GameObject block = CreateBox("Block", new Vector3(0.6f, 0.4f, 0.6f), new Vector3(3f, 0.2f, 0f), blockMat);
            GameObject stamm = CreateCylinder("Stamm", new Vector3(0.3f, 1.5f, 0.3f), new Vector3(-3f, 1.5f, 0f), stammMat);
            // CapsuleCollider unten rund (haengt ueber); Stamm unten gerade: MeshCollider mit Zylinder-Mesh.
            CapsuleCollider stammCapsule = stamm.GetComponent<CapsuleCollider>();
            if (stammCapsule != null)
            {
                Object.DestroyImmediate(stammCapsule);
            }

            stamm.AddComponent<MeshCollider>().sharedMesh = stamm.GetComponent<MeshFilter>().sharedMesh;
            GameObject ueberhang = CreateBox("Ueberhang", new Vector3(2f, 0.2f, 1.5f), new Vector3(0f, 2.1f, 2.25f), plaetteMat);

            // Netzplatz: zwei dünne senkrechte Stäbe, zwischen denen das Radnetz gespannt wird.
            CreateBox("NetzStab_L", new Vector3(0.04f, 1.2f, 0.04f), new Vector3(5.0f, 0.6f, 2.0f), netzstabMat);
            CreateBox("NetzStab_R", new Vector3(0.04f, 1.2f, 0.04f), new Vector3(5.8f, 0.6f, 2.0f), netzstabMat);

            GameObject light = new GameObject("Sun");
            Light sun = light.AddComponent<Light>();
            sun.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Camera cam = CameraBuilder.Create();
            GameObject spider = SpiderBuilder.Create(new Vector3(0f, 0.02f, 0f), cam.transform);
            SetLayerRecursive(spider, 2); // Layer 2 = "Ignore Raycast"
            CameraBuilder.Bind(cam, spider.transform);

            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.PoseAtRest();
            }

            EnsureScenesFolder();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            // Vorhandene Build-Szenen behalten, Klettertest nur ergaenzen.
            var scenes = new List<EditorBuildSettingsScene>();
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
            Debug.Log("[Spinnenspiel] Klettertest scene built at " + ScenePath);
        }

        private static GameObject CreateBox(string name, Vector3 scale, Vector3 position, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = scale;
            go.transform.position = position;
            ApplyMaterial(go, material);
            return go;
        }

        private static GameObject CreateCylinder(string name, Vector3 scale, Vector3 position, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.localScale = scale;
            go.transform.position = position;
            ApplyMaterial(go, material);
            return go;
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
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
