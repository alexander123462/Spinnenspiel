using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Ein Seidenfaden zwischen zwei Ankerpunkten mit parabolischem Durchhang (Sag).
    /// Wird zur Darstellung als LineRenderer gezeichnet; statische Liste <see cref="All"/>
    /// führt alle aktiven Fäden (z. B. zum späteren Klettern darauf).
    /// </summary>
    public class SilkThread : MonoBehaviour
    {
        /// <summary>Alle aktiven Seidenfäden (OnEnable eintragen, OnDisable austragen).</summary>
        public static readonly List<SilkThread> All = new List<SilkThread>();

        public Vector3 anchorA;
        public Vector3 anchorB;

        /// <summary>Durchhang in m, 0 = straff.</summary>
        public float sag;

        public int segments = 24;
        public float width = 0.0025f;

        /// <summary>
        /// Zeichnet nur den vorderen Anteil des Fadens (0..1) – für die Spinn-Animation.
        /// </summary>
        public float Extend01 { get; set; } = 1f;

        private LineRenderer line;
        private static Material sharedMaterial;

        /// <summary>Gerade (unbelastete) Länge zwischen den Ankerpunkten.</summary>
        public float Length => Vector3.Distance(anchorA, anchorB);

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        public void Init(Vector3 a, Vector3 b, float sagValue)
        {
            Init(a, b, sagValue, Vector3.up, Vector3.up);
        }

        /// <summary>
        /// Initialisiert den Faden; <paramref name="normalA"/>/<paramref name="normalB"/>
        /// sind die Normalen der Ankern – dort erscheinen kleine Haftscheiben.
        /// </summary>
        public void Init(Vector3 a, Vector3 b, float sagValue, Vector3 normalA, Vector3 normalB)
        {
            anchorA = a;
            anchorB = b;
            sag = sagValue;
            EnsureLine();
            Refresh();

            foreach (Transform child in transform)
            {
                if (child.name == "AdhesionDisc")
                {
                    Destroy(child.gameObject);
                }
            }

            CreateAdhesionDisc(a, normalA);
            CreateAdhesionDisc(b, normalB);
        }

        /// <summary>Kleine abgeflachte Haftscheibe (Sphere ohne Collider) an einem Anker, ausgerichtet nach der Flächennormale.</summary>
        private void CreateAdhesionDisc(Vector3 center, Vector3 normal)
        {
            Vector3 n = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            disc.name = "AdhesionDisc";
            var collider = disc.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            disc.transform.SetParent(transform, false);
            disc.transform.position = center + n * 0.001f;
            disc.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
            disc.transform.localScale = new Vector3(0.012f, 0.002f, 0.012f);
            var renderer = disc.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = SharedMaterial;
            }
        }

        /// <summary>Punkt auf der Fadenkurve bei t (0..1) mit parabolischem Durchhang nach unten.</summary>
        public Vector3 PointAt(float t)
        {
            t = Mathf.Clamp01(t);
            return Vector3.Lerp(anchorA, anchorB, t) + Vector3.down * sag * 4f * t * (1f - t);
        }

        /// <summary>Kurvenableitung (nicht normalisiert) bei t.</summary>
        public Vector3 TangentAt(float t)
        {
            t = Mathf.Clamp01(t);
            return (anchorB - anchorA) + Vector3.down * sag * 4f * (1f - 2f * t);
        }

        /// <summary>
        /// Parameter t (0..1) des nächstgelegenen Kurvenpunkts zu p.
        /// Über die Segmente in feinen Zwischenproben – für Fadenlängen im Spiel genau genug.
        /// </summary>
        public float ClosestT(Vector3 p)
        {
            int samples = Mathf.Max(1, segments * 8);
            float bestT = 0f;
            float bestDist = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                float d = (PointAt(t) - p).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestT = t;
                }
            }

            return bestT;
        }

        /// <summary>Zeichnet die LineRenderer-Punkte von anchorA bis Extend01 über die Kurve.</summary>
        public void Refresh()
        {
            if (line == null)
            {
                return;
            }

            float ext = Mathf.Clamp01(Extend01);
            int count = Mathf.Max(2, Mathf.FloorToInt(segments * ext) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                line.SetPosition(i, PointAt(i > 0 ? (float)i / (count - 1) * ext : 0f));
            }
        }

        private void EnsureLine()
        {
            if (line == null)
            {
                line = GetComponent<LineRenderer>();
                if (line == null)
                {
                    line = gameObject.AddComponent<LineRenderer>();
                }
            }

            line.positionCount = segments + 1;
            line.startWidth = width;
            line.endWidth = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = SharedMaterial;
        }

        /// <summary>Ein Material für alle Fäden: URP Unlit, weiß, Alpha 0.8, transparent.</summary>
        public static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                {
                    var mat = Spinnenspiel.Rendering.PipelineMaterials.Unlit(new Color(1f, 1f, 1f, 0.8f));
                    Spinnenspiel.Rendering.PipelineMaterials.SetTransparent(mat, true);
                    sharedMaterial = mat;
                }

                return sharedMaterial;
            }
        }
    }
}
