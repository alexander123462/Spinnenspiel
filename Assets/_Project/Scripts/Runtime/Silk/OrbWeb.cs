using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Zeichnet ein Radnetz (OrbWebLayout) als LineRenderer-Kinder:
    /// Rahmen, Radien, Hub-Ring, Fangspirale und (ausblendbar) Hilfsspirale.
    /// </summary>
    public class OrbWeb : MonoBehaviour
    {
        /// <summary>Zuletzt gebaute Geometrie.</summary>
        public OrbWebLayout Layout;
        /// <summary>Wird die Hilfsspirale angezeigt (Standard: aus)?</summary>
        public bool showAux = false;
        /// <summary>Linienbreite aller Seidenfäden (m).</summary>
        public float threadWidth = 0.0012f;

        /// <summary>Erzeugt die LineRenderer-Fäden für die gegebene Geometrie.</summary>
        public void Build(OrbWebLayout layout)
        {
            Layout = layout;

            // Build ist idempotent: frühere Fäden entfernen.
            foreach (Transform child in transform)
                Object.DestroyImmediate(child.gameObject);

            Material mat = SilkThread.SharedMaterial;

            // Rahmen: geschlossene Linie.
            AddLine("Frame", layout.Frame, true, mat);

            // Radien: Nabe zu jedem Endpunkt.
            for (int i = 0; i < layout.RadiusEnds.Count; i++)
            {
                var pts = new List<Vector3> { layout.Hub, layout.RadiusEnds[i] };
                AddLine("Radius" + i, pts, false, mat);
            }

            // Hub-Ring: geschlossener kleiner Kreis.
            AddLine("HubRing", layout.HubRing, true, mat);

            // Fangspirale: eine Linie, leicht glänzend-weiß, Alpha 0.9.
            var capMat = new Material(mat);
            capMat.color = new Color(1f, 1f, 1f, 0.9f);
            AddLine("CaptureSpiral", layout.CaptureSpiral, false, capMat);

            // Hilfsspirale: ausgeblendet, außer showAux.
            LineRenderer aux = AddLine("AuxSpiral", layout.AuxSpiral, false, mat);
            aux.enabled = showAux;
        }

        private LineRenderer AddLine(string name, List<Vector3> points, bool loop, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                line.SetPosition(i, points[i]);
            line.loop = loop;
            line.startWidth = threadWidth;
            line.endWidth = threadWidth;
            line.useWorldSpace = true;
            line.receiveShadows = false;
            line.material = mat;
            return line;
        }
    }
}
