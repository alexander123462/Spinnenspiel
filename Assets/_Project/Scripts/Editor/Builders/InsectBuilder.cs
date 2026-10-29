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

        // MODELL19: MAT_FlyBody/Eye/Wing wurden frueher pro Aufruf geloescht und neu
        // angelegt. Da CreateMosquito dieselbe Augen-Methode nutzt, zeigte der
        // Fliegen-Kopf danach auf ein zerstoertes Material (Slot 1 = null im Scene-File).
        // Cache: jedes Material wird pro Builder-Lauf nur einmal erzeugt.
        private static Material _flyBodyCache;
        private static Material _flyWingCache;
        private static Material _flyEyeCache;

        /// <summary>MODELL19: Fliege aus dem Blender-Neubau (ArtSource/Insekten/
        /// fliege_geometrie.py, FBX wie bei der Motte). Kinder behalten ihre Blender-
        /// Namen (Koerper/Kopf/Beine/Wing_L/Wing_R) - FlyingInsect findet die Fluegel
        /// ueber transform.Find. real 8 mm Koerper, in Spielskalierung ~39 mm.</summary>
        public static GameObject CreateFly()
        {
            const string fbxPath = "Assets/_Project/Art/Insekten/Fliege.fbx";
            Material body = CreateBodyMaterial();
            Material wing = CreateWingMaterial();
            Material eye = CreateEyeMaterial();

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            string[] teile = { "Koerper", "Kopf", "Beine", "Wing_L", "Wing_R" };

            GameObject root = new GameObject("Fliege");
            root.AddComponent<FlyingInsect>();

            foreach (string name in teile)
            {
                Mesh mesh = null;
                foreach (Object asset in assets)
                {
                    if (asset is Mesh && asset.name == name)
                    {
                        mesh = (Mesh)asset;
                    }
                }
                if (mesh == null)
                {
                    Debug.LogError("[Insect] Fliege-FBX fehlt Teil: " + name + " in " + fbxPath);
                    continue;
                }

                GameObject part = new GameObject(name);
                part.transform.SetParent(root.transform, false);
                MeshFilter mf = part.AddComponent<MeshFilter>();
                MeshRenderer mr = part.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                if (name == "Kopf" && mesh.subMeshCount >= 2)
                {
                    // Slot 0 = Koerper, Slot 1 = Auge (Blender-Slot-Reihenfolge).
                    mr.sharedMaterials = new Material[] { body, eye };
                }
                else if (name.StartsWith("Wing"))
                {
                    mr.sharedMaterial = wing;
                }
                else
                {
                    mr.sharedMaterial = body;
                }
            }

            return root;
        }

        /// <summary>
        /// MODELL21: Muecke aus dem Blender-Neubau (ArtSource/Insekten/muecke_geometrie.py
        /// + muecke_textur.py, FBX wie bei Fliege/Motte): segmentierter Thorax+Leib
        /// ~24.5 mm (63 % der Fliegen-Koerperlaenge 39 mm), 6 lange gegliederte Beine,
        /// Stechruessel, plumose Antennen, zwei schmale Fluegel mit Schuppenfransen.
        /// Kinder behalten Blender-Namen - FlyingInsect findet die Fluegel ueber
        /// transform.Find. Flugparameter wie bisher (1.6x Fliegenspeed, Energie 10).
        /// </summary>
        public static GameObject CreateMosquito()
        {
            const string fbxPath = "Assets/_Project/Art/Insekten/Muecke.fbx";
            Material body = CreateMosquitoBodyMaterial();
            Material wing = CreateMosquitoWingMaterial();

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            string[] teile = { "Koerper", "Kopf", "Schnabel", "Beine",
                               "Fuehler_L", "Fuehler_R", "Wing_L", "Wing_R" };

            GameObject root = new GameObject("Muecke");
            FlyingInsect insect = root.AddComponent<FlyingInsect>();
            // 1.6 x the fly speed (default 0.8) -> 1.28; same wander radius as the fly.
            insect.SetFlightParams(1.28f, 2.5f);
            insect.energyValue = 10f;

            foreach (string name in teile)
            {
                Mesh mesh = null;
                foreach (Object asset in assets)
                {
                    if (asset is Mesh && asset.name == name)
                    {
                        mesh = (Mesh)asset;
                    }
                }
                if (mesh == null)
                {
                    Debug.LogError("[Insect] Muecke-FBX fehlt Teil: " + name + " in " + fbxPath);
                    continue;
                }

                GameObject part = new GameObject(name);
                part.transform.SetParent(root.transform, false);
                MeshFilter mf = part.AddComponent<MeshFilter>();
                MeshRenderer mr = part.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = name.StartsWith("Wing") ? wing : body;
            }

            return root;
        }

        /// <summary>
        /// T1: Kaefer (Beetle) — armored, slow day-prey built from primitives (no Blender):
        /// oval dark body, wing covers (elytra). Energy 35, speed 0.6 x fly (0.48),
        /// escapeTime 20 s, durability cost 15 % per catch (via durabilityCostOverride).
        /// </summary>
        public static GameObject CreateBeetle()
        {
            Material body = CreateBeetleBodyMaterial();
            Material elytra = CreateBeetleElytraMaterial();

            GameObject root = new GameObject("Kaefer");
            FlyingInsect insect = root.AddComponent<FlyingInsect>();
            // 0.6 x the fly speed (default 0.8) -> 0.48; same wander radius as the fly.
            insect.SetFlightParams(0.48f, 2.5f);
            insect.energyValue = 35f;
            insect.escapeTime = 20f;
            insect.durabilityCostOverride = BeetleDurabilityCost;

            // Ovaler dunkler Koerper: gedreht Kapsule, ~1.4 x der Fliege (armored, bigger).
            GameObject koerper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            koerper.name = "Koerper";
            SetNoCollider(koerper);
            koerper.transform.SetParent(root.transform, false);
            koerper.transform.localPosition = Vector3.zero;
            koerper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            koerper.transform.localScale = new Vector3(0.017f, 0.022f, 0.014f);
            SetMaterial(koerper, body);

            // Kopf: Kugel bei z=+0.026 (vorne).
            GameObject kopf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            kopf.name = "Kopf";
            SetNoCollider(kopf);
            kopf.transform.SetParent(root.transform, false);
            kopf.transform.localPosition = new Vector3(0f, 0f, 0.026f);
            kopf.transform.localScale = Vector3.one * 0.014f;
            SetMaterial(kopf, body);

            // Fluegeldecken (Elytra): two glossy dark Quads lying on the back.
            CreateBeetleElytra(root.transform, "Wing_L", new Vector3(-0.011f, 0.009f, -0.006f), elytra);
            CreateBeetleElytra(root.transform, "Wing_R", new Vector3(0.011f, 0.009f, -0.006f), elytra);

            return root;
        }

        /// <summary>T1: Durability cost of a beetle catch (Prozent).</summary>
        private const float BeetleDurabilityCost = 15f;

        /// <summary>
        /// T2: Libelle (Dragonfly) — big, fast, risky day-prey built from primitives (no Blender):
        /// long slim body + head + 4 wings. Energy 60, speed 2.0 x fly (1.6), hangs 15 s in an
        /// intact net, and with 40 % chance it tears the net (durability to 0) and frees itself
        /// (via netTearChance). Very rare: mixed in at 3 % (LibelleModel.libelleShare).
        /// </summary>
        public static GameObject CreateLibelle()
        {
            Material body = CreateLibelleBodyMaterial();
            Material wing = CreateLibelleWingMaterial();

            GameObject root = new GameObject("Libelle");
            FlyingInsect insect = root.AddComponent<FlyingInsect>();
            // 2.0 x the fly speed (default 0.8) -> 1.6; bigger wander radius for a big flyer.
            insect.SetFlightParams(1.6f, 3f);
            insect.energyValue = 60f;
            insect.escapeTime = LibelleModel.hangTime; // 15 s
            insect.netTearChance = LibelleModel.netTearChance; // 40 %

            // Long slim body: a stretched capsule, ~2.2 x the fly length.
            GameObject koerper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            koerper.name = "Koerper";
            SetNoCollider(koerper);
            koerper.transform.SetParent(root.transform, false);
            koerper.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            koerper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            koerper.transform.localScale = new Vector3(0.007f, 0.038f, 0.007f);
            SetMaterial(koerper, body);

            // Head: small sphere at the front (z=+0.028).
            GameObject kopf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            kopf.name = "Kopf";
            SetNoCollider(kopf);
            kopf.transform.SetParent(root.transform, false);
            kopf.transform.localPosition = new Vector3(0f, 0f, 0.028f);
            kopf.transform.localScale = Vector3.one * 0.011f;
            SetMaterial(kopf, body);

            // 4 Wings: two long front Quads (Wing_L/Wing_R animate) + two rear Quads, clearly
            // separated along the body so all four read as a dragonfly.
            CreateLibelleWing(root.transform, "Wing_L", new Vector3(-0.024f, 0.007f, 0.014f), wing);
            CreateLibelleWing(root.transform, "Wing_R", new Vector3(0.024f, 0.007f, 0.014f), wing);
            CreateLibelleWing(root.transform, "HindWing_L", new Vector3(-0.023f, 0.007f, -0.020f), wing);
            CreateLibelleWing(root.transform, "HindWing_R", new Vector3(0.023f, 0.007f, -0.020f), wing);

            return root;
        }

        /// <summary>
        /// MODELL20: Motte neu aus Blender (ArtSource/Insekten/motte_geometrie.py +
        /// motte_textur.py): pelziger Koerper (real 15 mm), gefiederte Fuehler,
        /// Fluegel mit Schuppenmuster und Fransensaum (Spannweite real 30 mm).
        /// FBX liegt bereits in Spielskalierung (~4.5x wie die Fliege).
        /// </summary>
        public static GameObject CreateMoth()
        {
            const string fbxPath = "Assets/_Project/Art/Insekten/Motte.fbx";
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            string[] teile = { "Koerper", "Fuehler_L", "Fuehler_R", "Beine", "Wing_L", "Wing_R" };

            GameObject root = new GameObject("Motte");
            FlyingInsect insect = root.AddComponent<FlyingInsect>();
            insect.SetFlightParams(0.45f, 1.8f); // slower than the fly (0.8)

            Material body = CreateMothBodyMaterial();
            Material wing = CreateMothWingMaterial();
            foreach (string name in teile)
            {
                Mesh mesh = null;
                foreach (Object asset in assets)
                {
                    if (asset is Mesh && asset.name == name)
                    {
                        mesh = (Mesh)asset;
                    }
                }
                if (mesh == null)
                {
                    Debug.LogError("[Insect] Motte-FBX fehlt Teil: " + name + " in " + fbxPath);
                    continue;
                }

                GameObject part = new GameObject(name);
                part.transform.SetParent(root.transform, false);
                MeshFilter mf = part.AddComponent<MeshFilter>();
                MeshRenderer mr = part.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = name.StartsWith("Wing") ? wing : body;
            }

            return root;
        }

        /// <summary>MODELL20: Pelz-Koerper mit gebackener Albedo als Asset MAT_MothBody.</summary>
        private static Material CreateMothBodyMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white);
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.2f);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Art/Insekten/Motte_body_albedo.png");
            if (albedo != null)
            {
                material.SetTexture("_BaseColorMap", albedo);
            }
            else
            {
                Debug.LogWarning("[Insect] Motte_body_albedo.png fehlt – MAT_MothBody wird weiss.");
                material.color = new Color(0.42f, 0.34f, 0.24f);
            }
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_MothBody.mat");
        }

        /// <summary>MODELL20: Fluegel mit Schuppen-Albedo als Asset MAT_MothWing.
        /// DoubleSided (Mirror): die Fluegelflaeche ist eine duenne Platte, deren
        /// Kanten in Unity sonst einseitig weggeculled werden (Draufsicht leer).</summary>
        private static Material CreateMothWingMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white);
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.3f);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Art/Insekten/Motte_wing_albedo.png");
            if (albedo != null)
            {
                material.SetTexture("_BaseColorMap", albedo);
            }
            else
            {
                Debug.LogWarning("[Insect] Motte_wing_albedo.png fehlt – MAT_MothWing wird weiss.");
                material.color = new Color(0.5f, 0.42f, 0.3f);
            }
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_DoubleSidedNormalMode", 2f); // Mirror
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_MothWing.mat");
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

        /// <summary>S2: Augen-Sphere mit geaendem Radius (Muecke = smaller than the fly).</summary>
        private static void CreateEyeScaled(Transform parent, string name, Vector3 localPos, float size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            SetNoCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            SetMaterial(go, material);
        }

        /// <summary>S2: Fluegel-Quad, schlanke (narrower than the fly) for the Muecke.</summary>
        private static void CreateWingSlim(Transform parent, string name, Vector3 localPos, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            SetNoCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // liegend
            go.transform.localScale = new Vector3(0.008f, 0.020f, 1f);
            SetMaterial(go, material);
        }

        /// <summary>T1: Fluegeldecke (Elytra) — a glossy dark Quad lying on the Kaefer's back.</summary>
        private static void CreateBeetleElytra(Transform parent, string name, Vector3 localPos, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            SetNoCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // liegend
            go.transform.localScale = new Vector3(0.012f, 0.030f, 1f);
            SetMaterial(go, material);
        }

        /// <summary>T2: Libelle-Fluegel — a long slim Quad sticking out sideways beside the body (4x per dragonfly).</summary>
        private static void CreateLibelleWing(Transform parent, string name, Vector3 localPos, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            SetNoCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // liegend (horizontal)
            go.transform.localScale = new Vector3(0.034f, 0.010f, 1f); // long axis along X (sticks out sideways)
            SetMaterial(go, material);
        }

        /// <summary>T1: Dark, shiny armored Kaefer-Body (HDRP Lit) als Asset MAT_BeetleBody.</summary>
        private static Material CreateBeetleBodyMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.10f, 0.11f, 0.12f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.75f);
            material.SetFloat("_Metallic", 0.5f);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_BeetleBody.mat");
        }

        /// <summary>T2: Steel-blue, slender Libelle-Body (HDRP Lit) als Asset MAT_LibelleBody.</summary>
        private static Material CreateLibelleBodyMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.15f, 0.45f, 0.55f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.6f);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_LibelleBody.mat");
        }

        /// <summary>T2: Pale, translucent Libelle-Wing (HDRP Lit) als Asset MAT_LibelleWing.</summary>
        private static Material CreateLibelleWingMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.85f, 0.90f, 0.92f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.35f);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_LibelleWing.mat");
        }

        /// <summary>T1: Glossy dark Fluegeldecke (HDRP Lit) als Asset MAT_BeetleElytra.</summary>
        private static Material CreateBeetleElytraMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.14f, 0.13f, 0.10f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.85f);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_BeetleElytra.mat");
        }

        /// <summary>MODELL21: Chitin-Koerper mit gebackener Albedo als MAT_MosquitoBody.</summary>
        private static Material CreateMosquitoBodyMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white);
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.35f);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Art/Insekten/Muecke_body_albedo.png");
            if (albedo != null)
            {
                material.SetTexture("_BaseColorMap", albedo);
            }
            else
            {
                Debug.LogWarning("[Insect] Muecke_body_albedo.png fehlt – MAT_MosquitoBody wird grau.");
                material.color = new Color(0.3f, 0.26f, 0.21f);
            }
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_MosquitoBody.mat");
        }

        /// <summary>MODELL21: Fluegelmembran mit Schuppen-Albedo, DoubleSided (duenne
        /// Platte, Kanten sonst einseitig weggeculled) als MAT_MosquitoWing.</summary>
        private static Material CreateMosquitoWingMaterial()
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white);
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.4f);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Art/Insekten/Muecke_wing_albedo.png");
            if (albedo != null)
            {
                material.SetTexture("_BaseColorMap", albedo);
            }
            else
            {
                Debug.LogWarning("[Insect] Muecke_wing_albedo.png fehlt – MAT_MosquitoWing wird hell.");
                material.color = new Color(0.7f, 0.72f, 0.75f);
            }
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_DoubleSidedNormalMode", 2f); // Mirror
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_MosquitoWing.mat");
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

        /// <summary>MODELL19: dunkler Fallfliegen-Koerper mit eingebrannter
        /// Albedo-Textur (Fliege_albedo.png) als Asset MAT_FlyBody.</summary>
        private static Material CreateBodyMaterial()
        {
            if (_flyBodyCache != null) return _flyBodyCache;
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white);
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.35f);
            material.SetFloat("_Metallic", 0.05f);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Art/Insekten/Fliege_albedo.png");
            if (albedo != null)
            {
                material.SetTexture("_BaseColorMap", albedo);
            }
            else
            {
                Debug.LogWarning("[Insect] Fliege_albedo.png fehlt – MAT_FlyBody wird weiss.");
                material.color = new Color(0.22f, 0.23f, 0.25f);
            }
            material.enableInstancing = true;
            _flyBodyCache = AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_FlyBody.mat");
            return _flyBodyCache;
        }

        /// <summary>URP Unlit Transparent, hellgrau, Alpha 0.35 (wie SilkThread) als Asset MAT_FlyWing.</summary>
        private static Material CreateWingMaterial()
        {
            if (_flyWingCache != null) return _flyWingCache;
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Unlit(new Color(0.85f, 0.87f, 0.9f, 0.35f));
            Spinnenspiel.Rendering.PipelineMaterials.SetTransparent(material, true);
            _flyWingCache = AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_FlyWing.mat");
            return _flyWingCache;
        }

        /// <summary>Rotbraun (URP Lit) als Asset MAT_FlyEye.</summary>
        private static Material CreateEyeMaterial()
        {
            if (_flyEyeCache != null) return _flyEyeCache;
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.32f, 0.16f, 0.10f));
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(material, 0.8f);
            material.enableInstancing = true;
            _flyEyeCache = AssetUtil.ReplaceAsset(material, MaterialsFolder + "/MAT_FlyEye.mat");
            return _flyEyeCache;
        }
    }
}
