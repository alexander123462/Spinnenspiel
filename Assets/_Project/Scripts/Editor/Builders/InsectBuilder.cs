using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut eine Fliege (Insekten 1) als Szenenobjekt: Root mit <see cref="FlyingInsect"/>,
    /// Kapselkoerper, Kopf, Augen und zwei Quad-Fluegel. Bewusst ~0.035 m gross,
    /// damit sie zur Spinne passt. Materialien werden als Assets gespeichert.
    /// </summary>
    public static class InsectBuilder
    {
        private const string MaterialsFolder = AssetUtil.GeneratedRoot + "/Materials";

        /// <summary>Erzeugt die Fliege (Kapselkoerper + Kopf + Augen + Fluegel) mit FlyingInsect.</summary>
        public static GameObject CreateFly()
        {
            Material body = CreateBodyMaterial();
            Material wing = CreateWingMaterial();
            Material eye = CreateEyeMaterial();

            GameObject root = new GameObject("Fliege");
            root.AddComponent<FlyingInsect>();

            // Kapselkoerper: Längsachse = lokale Z (um X 90° gedreht), ~0.044 m lang.
            GameObject koerper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            koerper.name = "Koerper";
            SetNoCollider(koerper);
            koerper.transform.SetParent(root.transform, false);
            koerper.transform.localPosition = Vector3.zero;
            koerper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            koerper.transform.localScale = new Vector3(0.012f, 0.018f, 0.012f);
            SetMaterial(koerper, body);

            // Kopf: Kugel bei z=+0.02 (vorne).
            GameObject kopf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            kopf.name = "Kopf";
            SetNoCollider(kopf);
            kopf.transform.SetParent(root.transform, false);
            kopf.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            kopf.transform.localScale = Vector3.one * 0.012f;
            SetMaterial(kopf, body);

            // Augen: zwei kleine rotbraune Kugeln seitlich am Kopf.
            CreateEye(root.transform, "Auge_L", new Vector3(-0.006f, 0.003f, 0.024f), eye);
            CreateEye(root.transform, "Auge_R", new Vector3(0.006f, 0.003f, 0.024f), eye);

            // Fluegel: liegende Quads, schlagen um die lokale Z-Achse (Flugrichtung).
            CreateWing(root.transform, "Wing_L", new Vector3(-0.009f, 0.006f, -0.004f), wing);
            CreateWing(root.transform, "Wing_R", new Vector3(0.009f, 0.006f, -0.004f), wing);

            return root;
        }

        /// <summary>Z7: Motte (Insekten 2) from ArtSource/Insekten/motte.py FBX; bigger + slower than the fly.</summary>
        public static GameObject CreateMoth()
        {
            const string fbxPath = "Assets/_Project/Art/Insekten/Motte.fbx";
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            List<Mesh> meshes = new List<Mesh>();
            foreach (Object asset in assets)
            {
                if (asset is Mesh)
                {
                    meshes.Add((Mesh)asset);
                }
            }
            if (meshes.Count == 0)
            {
                Debug.LogError("[Insect] Motte FBX has no mesh: " + fbxPath);
                return null;
            }

            GameObject root = new GameObject("Motte");
            FlyingInsect insect = root.AddComponent<FlyingInsect>();
            insect.SetFlightParams(0.45f, 1.8f); // slower than the fly (0.8)

            Material mothMaterial = CreateMothMaterial();
            int index = 0;
            foreach (Mesh mesh in meshes)
            {
                GameObject part = new GameObject("Motte_" + index++);
                part.transform.SetParent(root.transform, false);
                MeshFilter mf = part.AddComponent<MeshFilter>();
                MeshRenderer mr = part.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = mothMaterial;
            }
            // Blender-Export: ~0.10 m body length; scale up so it reads bigger than the fly (~0.044 m).
            root.transform.localScale = Vector3.one * 1.6f;

            return root;
        }

        /// <summary>Z7: Dreckig-beige Motte-Body (HDRP Lit) als Asset MAT_MothBody.</summary>
        private static Material CreateMothMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.55f, 0.45f, 0.32f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.15f);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_MothBody.mat");
        }

        private static void CreateEye(Transform parent, string name, Vector3 localPos, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            SetNoCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.007f;
            SetMaterial(go, material);
        }

        private static void CreateWing(Transform parent, string name, Vector3 localPos, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            SetNoCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // liegend
            go.transform.localScale = new Vector3(0.014f, 0.028f, 1f);
            SetMaterial(go, material);
        }

        private static void SetMaterial(GameObject go, Material material)
        {
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void SetNoCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        /// <summary>Dunkelgrau-metallisch (URP Lit) als Asset MAT_FlyBody.</summary>
        private static Material CreateBodyMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.22f, 0.23f, 0.25f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.7f);
            material.SetFloat("_Metallic", 0.6f);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_FlyBody.mat");
        }

        /// <summary>URP Unlit Transparent, hellgrau, Alpha 0.35 (wie SilkThread) als Asset MAT_FlyWing.</summary>
        private static Material CreateWingMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Unlit(new Color(0.85f, 0.87f, 0.9f, 0.35f));
            Spinnenspiel.Rendering.PipelineMaterials.SetTransparent(material, true);
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_FlyWing.mat");
        }

        /// <summary>Rotbraun (URP Lit) als Asset MAT_FlyEye.</summary>
        private static Material CreateEyeMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.32f, 0.16f, 0.10f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.8f);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_FlyEye.mat");
        }
    }
}
