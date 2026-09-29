using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    public static class SpiderBuilder
    {
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string BodyMaterialPath = "Assets/_Project/Art/Materials/M_SpiderBody.mat";

        public static GameObject Create(Vector3 position, Transform cameraTransform)
        {
            var root = new GameObject("Spider");
            root.transform.position = position;

            // Bewegung ohne CharacterController: kinematisches Rigidbody + Körperkugel.
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var bodyCollider = root.AddComponent<SphereCollider>();
            bodyCollider.radius = 0.05f;
            bodyCollider.center = new Vector3(0f, 0.05f, 0f);

            root.AddComponent<Spinnenspiel.SpiderInput>();
            var movement = root.AddComponent<Spinnenspiel.SpiderMovement>();
            root.AddComponent<Spinnenspiel.SpiderController>();

            var so = new SerializedObject(movement);
            so.FindProperty("cameraTransform").objectReferenceValue = cameraTransform;
            so.ApplyModifiedPropertiesWithoutUndo();


            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            // Milestone 6: Materialien mit Muster (Hinterleib), Ringelung (Beine), glänzende Augen.
            string folder = AssetUtil.GeneratedRoot + "/Materials";
            Material abdomenMat = AssetUtil.CreateLitMaterial(folder + "/MAT_SpiderAbdomen.mat", Color.white, 0.42f, TextureFactory.SpiderAbdomen());
            Material thoraxMat = AssetUtil.CreateLitMaterial(folder + "/MAT_SpiderThorax.mat", new Color(0.16f, 0.11f, 0.075f), 0.5f);
            Material legMat = AssetUtil.CreateLitMaterial(folder + "/MAT_SpiderLeg.mat", Color.white, 0.35f, TextureFactory.SpiderLegBands());
            Material eyeMat = AssetUtil.CreateLitMaterial(folder + "/MAT_SpiderEye.mat", new Color(0.02f, 0.02f, 0.025f), 0.95f);

            AddPart(body.transform, "Abdomen", new Vector3(0f, 0.008f, -0.052f), new Vector3(0.085f, 0.068f, 0.11f), abdomenMat);
            AddPart(body.transform, "Pedicel", new Vector3(0f, 0.002f, -0.006f), new Vector3(0.022f, 0.02f, 0.03f), thoraxMat);
            AddPart(body.transform, "Cephalothorax", new Vector3(0f, 0f, 0.025f), new Vector3(0.058f, 0.04f, 0.065f), thoraxMat);

            // Augen: zwei große vordere Mittelaugen, zwei kleinere darüber, zwei seitliche.
            AddPart(body.transform, "Eye_AL", new Vector3(-0.007f, 0.011f, 0.055f), Vector3.one * 0.009f, eyeMat);
            AddPart(body.transform, "Eye_AR", new Vector3(0.007f, 0.011f, 0.055f), Vector3.one * 0.009f, eyeMat);
            AddPart(body.transform, "Eye_PL", new Vector3(-0.009f, 0.018f, 0.046f), Vector3.one * 0.0065f, eyeMat);
            AddPart(body.transform, "Eye_PR", new Vector3(0.009f, 0.018f, 0.046f), Vector3.one * 0.0065f, eyeMat);
            AddPart(body.transform, "Eye_LL", new Vector3(-0.016f, 0.012f, 0.047f), Vector3.one * 0.005f, eyeMat);
            AddPart(body.transform, "Eye_LR", new Vector3(0.016f, 0.012f, 0.047f), Vector3.one * 0.005f, eyeMat);

            // Kieferklauen (Cheliceren) und Taster (Pedipalpen)
            AddPart(body.transform, "Chelicera_L", new Vector3(-0.006f, -0.006f, 0.058f), new Vector3(0.009f, 0.014f, 0.009f), thoraxMat);
            AddPart(body.transform, "Chelicera_R", new Vector3(0.006f, -0.006f, 0.058f), new Vector3(0.009f, 0.014f, 0.009f), thoraxMat);
            AddPalp(body.transform, "Palp_L", new Vector3(-0.012f, -0.002f, 0.056f), new Vector3(-0.02f, -0.03f, 0.085f), legMat);
            AddPalp(body.transform, "Palp_R", new Vector3(0.012f, -0.002f, 0.056f), new Vector3(0.02f, -0.03f, 0.085f), legMat);

            // Milestone 2: acht Beine mit IK, Gang- und Koerpercontroller.
            SpiderLegBuilder.Build(root, body.transform, legMat);

            // Milestone 5/6: Oberflaechenerkennung und Trittgeraeusche.
            root.AddComponent<SpiderSurfaceDetector>();
            root.AddComponent<AudioSource>();
            root.AddComponent<SpiderFootstepAudio>();

            // Seide (Milestone 7): Spinnwarzen an der Spitze des Hinterleibs.
            // Kind des (skalierten) Abdomens: lokal (0, -0.1, -0.5) = untere Spitze.
            Transform abdomen = body.transform.Find("Abdomen");
            var spinnerets = new GameObject("Spinnerets");
            spinnerets.transform.SetParent(abdomen, false);
            spinnerets.transform.localPosition = new Vector3(0f, -0.1f, -0.5f);

            var silk = root.AddComponent<SpiderSilk>();
            silk.spinnerets = spinnerets.transform;

            // Seiden-Steuerung (t53): Faden spinnen / loslassen + Fadenkreuz. Referenzen füllt Awake.
            root.AddComponent<SilkAiming>();

            // Sicherheitsfaden (t55): Dragline mit Anker + Haftscheiben beim Laufen.
            root.AddComponent<SpiderDragline>();

            // Netzbau (t62b2): Spinne baut das Radnetz in der echten Reihenfolge.
            root.AddComponent<SpiderWebBuilder>();
            root.AddComponent<SpiderWebPlacer>();

            // Hunger (t73a1): steigt langsam, sinkt beim Fressen.
            root.AddComponent<SpiderHunger>();

            // Beute 2 (t73a3): zur Beute laufen, einwickeln, zurücktragen, fressen.
            root.AddComponent<SpiderPredation>();

            // Steuerungshilfe (t82): Box oben rechts, F1 schaltet um.
            root.AddComponent<ControlsHelp>();

            return root;
        }

        private static Material EnsureBodyMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            if (mat != null)
            {
                return mat;
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                string parent = "Assets/_Project/Art";
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                }
                AssetDatabase.CreateFolder(parent, "Materials");
            }

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.18f, 0.12f, 0.08f));
            mat.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(mat, BodyMaterialPath);
            return mat;
        }

        private static void AddPalp(Transform parent, string name, Vector3 from, Vector3 to, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            Vector3 delta = to - from;
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            go.transform.localScale = new Vector3(0.008f, delta.magnitude * 0.5f, 0.008f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void AddPart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
