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
        /// <summary>Bau-Phase des Netzes, für schrittweises Aufdecken (SetProgress).</summary>
        public enum WebStage
        {
            Frame,
            Radii,
            AuxSpiral,
            CaptureSpiral,
            Done
        }

        /// <summary>Zuletzt gebaute Geometrie.</summary>
        public OrbWebLayout Layout;
        /// <summary>Wird die Hilfsspirale angezeigt (Standard: aus)?</summary>
        public bool showAux = false;
        /// <summary>Linienbreite aller Seidenfäden (m).</summary>
        public float threadWidth = 0.0012f;

        private LineRenderer frameLine;
        private LineRenderer hubRingLine;
        private LineRenderer captureLine;
        private LineRenderer auxLine;
        private readonly List<LineRenderer> radiusLines = new List<LineRenderer>();
        private readonly List<List<Vector3>> fullRadii = new List<List<Vector3>>();
        private List<Vector3> fullFrame = new List<Vector3>();
        private List<Vector3> fullCapture = new List<Vector3>();
        private List<Vector3> fullAux = new List<Vector3>();

        /// <summary>Erzeugt die LineRenderer-Fäden für die gegebene Geometrie.</summary>
        public void Build(OrbWebLayout layout)
        {
            Layout = layout;

            // Build ist idempotent: frühere Fäden entfernen.
            foreach (Transform child in transform)
                Object.DestroyImmediate(child.gameObject);

            frameLine = null;
            hubRingLine = null;
            captureLine = null;
            auxLine = null;
            radiusLines.Clear();
            fullRadii.Clear();
            fullFrame = layout.Frame;
            fullCapture = layout.CaptureSpiral;
            fullAux = layout.AuxSpiral;

            Material mat = SilkThread.SharedMaterial;

            // Rahmen: geschlossene Linie.
            frameLine = AddLine("Frame", layout.Frame, true, mat);

            // Radien: Nabe zu jedem Endpunkt.
            for (int i = 0; i < layout.RadiusEnds.Count; i++)
            {
                var pts = new List<Vector3> { layout.Hub, layout.RadiusEnds[i] };
                radiusLines.Add(AddLine("Radius" + i, pts, false, mat));
                fullRadii.Add(pts);
            }

            // Hub-Ring: geschlossener kleiner Kreis.
            hubRingLine = AddLine("HubRing", layout.HubRing, true, mat);

            // Fangspirale: eine Linie, leicht glänzend-weiß, Alpha 0.9.
            var capMat = new Material(mat);
            capMat.color = new Color(1f, 1f, 1f, 0.9f);
            captureLine = AddLine("CaptureSpiral", layout.CaptureSpiral, false, capMat);

            // Hilfsspirale: ausgeblendet, außer showAux.
            auxLine = AddLine("AuxSpiral", layout.AuxSpiral, false, mat);
            auxLine.enabled = showAux;
        }

        /// <summary>
        /// Setzt den Bau-Fortschritt: alles vor <paramref name="stage"/> ist voll sichtbar,
        /// die Stage selbst anteilig (t01 = 0..1), alles danach unsichtbar.
        /// Während CaptureSpiral wird die Hilfsspirale im gleichen Maß abgebaut,
        /// wie die Fangspirale wächst (echte Spinnen fressen die Hilfsspirale auf);
        /// bei Done ist sie weg.
        /// </summary>
        public void SetProgress(WebStage stage, float t01)
        {
            if (frameLine == null)
                throw new System.InvalidOperationException("OrbWeb.SetProgress(): zuerst Build() aufrufen.");
            t01 = Mathf.Clamp01(t01);

            // Rahmen und Hub-Ring gehören zur ersten Phase.
            SetPoints(frameLine, fullFrame, stage == WebStage.Frame ? t01 : 1f);

            // Radien: Anzahl der sichtbaren Radien.
            int visible = stage < WebStage.Radii ? 0
                       : stage > WebStage.Radii ? radiusLines.Count
                       : Mathf.RoundToInt(t01 * radiusLines.Count);
            for (int i = 0; i < radiusLines.Count; i++)
                SetPoints(radiusLines[i], fullRadii[i], i < visible ? 1f : 0f);

            // Fangspirale: wächst in ihrer Phase; Hilfsspirale: wächst in ihrer Phase
            // und wird beim Fangspiralen-Wachsen proportional wieder abgebaut.
            float captureT = stage < WebStage.CaptureSpiral ? 0f
                          : stage == WebStage.CaptureSpiral ? t01 : 1f;
            float auxT = stage < WebStage.AuxSpiral ? 0f
                     : stage == WebStage.AuxSpiral ? t01
                     : stage == WebStage.CaptureSpiral ? 1f - t01 : 0f;
            SetPoints(captureLine, fullCapture, captureT);
            SetPoints(auxLine, fullAux, auxT);
        }

        /// <summary>
        /// Liefert die Punktfolge, die die Spinne in der Phase
        /// <paramref name="stage"/> ablaufen würde (Rahmen: Polygon, Radien: Hub → Ende → Hub …,
        /// Spiralen: deren Punktfolgen, Done: leer).
        /// </summary>
        public Vector3[] ConstructionPath(WebStage stage)
        {
            if (Layout == null)
                throw new System.InvalidOperationException("OrbWeb.ConstructionPath(): zuerst Build() aufrufen.");

            switch (stage)
            {
                case WebStage.Frame:
                    return fullFrame.ToArray();

                case WebStage.Radii:
                    var path = new List<Vector3> { Layout.Hub };
                    foreach (Vector3 end in Layout.RadiusEnds)
                    {
                        path.Add(end);
                        path.Add(Layout.Hub);
                    }

                    return path.ToArray();

                case WebStage.AuxSpiral:
                    return fullAux.ToArray();

                case WebStage.CaptureSpiral:
                    return fullCapture.ToArray();

                default:
                    return new Vector3[0];
            }
        }

        /// <summary>Zeigt anteilig (t01) die ersten Punkte einer Linie, bei 0 keine.</summary>
        private static void SetPoints(LineRenderer line, List<Vector3> full, float t01)
        {
            int n = Mathf.RoundToInt(full.Count * Mathf.Clamp01(t01));
            line.positionCount = n;
            for (int i = 0; i < n; i++)
                line.SetPosition(i, full[i]);
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
