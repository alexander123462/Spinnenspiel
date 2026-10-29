using UnityEngine;
using System.Linq;
using UnityEditor;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Erzeugt die acht Beine der Spinne: Hüften am Kopf-Brust-Stück, Ruhepositionen am Root,
    /// je drei Segment-Visuals (Femur, Tibia, Tarsus) sowie Gang- und Körpercontroller.
    /// </summary>
    public static class SpiderLegBuilder
    {
        // Winkel der Ruhepositionen von vorne gemessen (Grad), je Beinpaar 1..4.
        private static readonly float[] RestAngles = { 26f, 68f, 112f, 150f };
        // Hüft-Z auf dem Körper (lokal), von vorne nach hinten.
        private static readonly float[] HipZ = { 0.045f, 0.033f, 0.021f, 0.009f };
        // Längenfaktor je Paar: vorne lang, drittes Paar kurz, viertes Paar am längsten (Wolfsspinne).
        private static readonly float[] LengthScale = { 1.12f, 1.0f, 0.95f, 1.2f };
        // Abstand des Ruhepunkts je Paar (Faktor auf RestRadius): vorne weiter vor, hinten näher am Körper.
        private static readonly float[] RestReach = { 1.05f, 1.0f, 0.92f, 0.88f };

        private const float RestRadius = 0.135f;
        private const float HipSideOffset = 0.024f;
        private const float HipHeight = 0.004f;
        private const float UpperLength = 0.085f;
        private const float LowerLength = 0.095f;
        private const float TarsusLength = 0.028f;
        // L2: Vogelspinnen-Beine kräftiger
        private const float Thickness = 0.015f;

        private const string LegFbx = "Assets/_Project/Art/Spider/Spinne_Beine.fbx";

        public static void Build(GameObject spider, Transform body, Material legMaterial)
        {
            Transform targets = new GameObject("LegTargets").transform;
            targets.SetParent(spider.transform, false);
            Transform visuals = new GameObject("LegVisuals").transform;
            visuals.SetParent(spider.transform, false);

            for (int pair = 0; pair < 4; pair++)
            {
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    string legName = (s == 0 ? "L" : "R") + (pair + 1);
                    // Tetrapoden-Gang: L1 R2 L3 R4 = Gruppe 0, R1 L2 R3 L4 = Gruppe 1.
                    int group = (pair % 2 == 0) == (s == 0) ? 0 : 1;
                    float scale = LengthScale[pair];

                    Transform hip = new GameObject("Leg_" + legName).transform;
                    hip.SetParent(body, false);
                    hip.localPosition = new Vector3(side * HipSideOffset, HipHeight, HipZ[pair]);

                    float angle = RestAngles[pair] * Mathf.Deg2Rad;
                    float radius = RestRadius * scale * RestReach[pair];
                    Transform rest = new GameObject("Rest_" + legName).transform;
                    rest.SetParent(targets, false);
                    rest.localPosition = new Vector3(side * Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);

                    Transform femur = CreateSegment(visuals, legName + "_Femur", legMaterial, "Femur");
                    Transform tibia = CreateSegment(visuals, legName + "_Tibia", legMaterial, "Tibia");
                    Transform tarsus = CreateSegment(visuals, legName + "_Tarsus", legMaterial, "Tarsus");

                    SpiderLeg leg = hip.gameObject.AddComponent<SpiderLeg>();
                    leg.Configure(rest, body, group, UpperLength * scale, LowerLength * scale, TarsusLength * scale,
                        Thickness, femur, tibia, tarsus, pair);
                }
            }

            spider.AddComponent<SpiderGaitController>();
            SpiderBodyController bodyController = spider.AddComponent<SpiderBodyController>();
            bodyController.Configure(body, body.localPosition.y);
        }

        private static Transform CreateSegment(Transform parent, string name, Material material, string meshKey)
        {
            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(LegFbx).OfType<Mesh>().FirstOrDefault(m => m.name.Contains(meshKey));
            if (mesh != null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                return go.transform;
            }
            Debug.LogWarning("[Spider] Bein-Mesh fehlt: " + meshKey + " in " + LegFbx + " – Kapsel bleibt");
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = name;
            capsule.transform.SetParent(parent, false);
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            MeshRenderer capsuleRenderer = capsule.GetComponent<MeshRenderer>();
            capsuleRenderer.sharedMaterial = material;
            capsuleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return capsule.transform;
        }
    }
}
