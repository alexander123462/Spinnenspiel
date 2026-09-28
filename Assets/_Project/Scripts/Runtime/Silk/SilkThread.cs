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
            anchorA = a;
            anchorB = b;
            sag = sagValue;
            EnsureLine();
            Refresh();
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
        private static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = new Color(1f, 1f, 1f, 0.8f);
                    // URP: Surface Type Transparent + Blend-Einstellungen
                    mat.SetFloat("_Surface", 1f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);        // 5
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); // 10
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = 3000;
                    sharedMaterial = mat;
                }

                return sharedMaterial;
            }
        }
    }
}
