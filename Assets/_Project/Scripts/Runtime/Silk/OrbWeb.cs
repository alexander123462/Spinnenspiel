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

        /// <summary>Alle aktiven Radnetze im Spiel (Beine greifen darüber Fäden).</summary>
        public static readonly List<OrbWeb> All = new List<OrbWeb>();

        /// <summary>Normale der Netzebene aus dem Layout (vor Build: +Z).</summary>
        public Vector3 PlaneNormal => Layout != null ? Layout.Normal : Vector3.forward;

        /// <summary>
        /// Darf das Netz Insekten fangen? Fangspirale muss weit genug gewachsen sein
        /// (t &gt;= 0.3 in ihrer Phase) oder das Netz ist fertig.
        /// </summary>
        public bool CanCatch => (_stage == WebStage.CaptureSpiral && _t >= 0.3f) || _stage == WebStage.Done;

        private WebStage _stage;
        private float _t;
        private float _vib;
        private bool _localSpace;
        private Vector3 _basePos;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

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
            _stage = WebStage.Frame;
            _t = 0f;
            _vib = 0f;
            _localSpace = false;
            _basePos = transform.position;

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
            _stage = stage;
            _t = t01;

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

        /// <summary>
        /// Schneidet das Segment a→b die Netzebene innerhalb des Rahmens?
        /// Nur wenn <see cref="CanCatch"/>. Liefert den Schnittpunkt in <paramref name="hit"/>.
        /// </summary>
        public bool TryIntersectSegment(Vector3 a, Vector3 b, out Vector3 hit)
        {
            hit = a;
            if (!CanCatch || Layout == null)
                return false;

            Vector3 n = PlaneNormal;
            float da = Vector3.Dot(a - Layout.Hub, n);
            float db = Vector3.Dot(b - Layout.Hub, n);

            // Gleiche Seite der Ebene (oder Segment in der Ebene) → kein Durchgang.
            if ((da > 0f && db > 0f) || (da < 0f && db < 0f) || Mathf.Abs(db) < 1e-9f)
                return false;

            hit = a + (b - a) * (da / (da - db));

            // Nur innerhalb des Rahmen-Polygons fangen (Punkt-in-Polygon, 2D-Ebene).
            return Layout.IsInsideFrame(hit);
        }

        /// <summary>Regt die Netz-Vibration an (stärkste bisherige Komponente zählt).</summary>
        public void Vibrate(float strength)
        {
            _vib = Mathf.Max(_vib, strength);
            EnsureLocalSpace();
        }

        /// <summary>
        /// Dämpft die Vibration (Exp, ~4/s) und schwingt das Netz um seine Basisposition
        /// entlang der Netzebene (Sinus 55 rad/s, Amplitude 0.004 · _vib).
        /// </summary>
        private void LateUpdate()
        {
            if (!_localSpace)
                return;

            _vib *= Mathf.Exp(-4f * Time.deltaTime);
            Vector3 offset = PlaneNormal * (Mathf.Sin(Time.time * 55f) * 0.004f * _vib);
            transform.position = _basePos + offset;
        }

        /// <summary>
        /// Stellt die LineRenderer einmalig auf Lokalraum um (Positionen werden umgerechnet),
        /// damit das Schwingen das ganze Netz-GameObject bewegen kann.
        /// </summary>
        private void EnsureLocalSpace()
        {
            if (_localSpace)
                return;

            _localSpace = true;
            _basePos = transform.position;
            foreach (LineRenderer line in AllLines())
            {
                line.useWorldSpace = false;
                int n = line.positionCount;
                for (int i = 0; i < n; i++)
                {
                    line.SetPosition(i, transform.InverseTransformPoint(line.GetPosition(i)));
                }
            }
        }

        /// <summary>Alle Faden-LineRenderer dieses Netzes.</summary>
        private IEnumerable<LineRenderer> AllLines()
        {
            if (frameLine != null) yield return frameLine;
            if (hubRingLine != null) yield return hubRingLine;
            if (captureLine != null) yield return captureLine;
            if (auxLine != null) yield return auxLine;
            foreach (LineRenderer line in radiusLines)
                yield return line;
        }

        /// <summary>
        /// Nächster Punkt auf einem sichtbaren Faden dieses Netzes (Rahmen, Hub-Ring,
        /// Fangspirale, Hilfsspirale, Radien). True wenn der Abstand &lt;= maxDist.
        /// </summary>
        public bool TryClosestPoint(Vector3 p, float maxDist, out Vector3 point)
        {
            point = p;
            if (Layout == null)
                return false;

            // Vorfilter: Punkt muss in maxDist-Entfernung an der Netzebene liegen.
            if (Mathf.Abs(Vector3.Dot(p - Layout.Hub, PlaneNormal)) > maxDist)
                return false;

            float bestSq = float.MaxValue;
            Vector3 best = p;
            CheckLine(frameLine, p, ref bestSq, ref best);
            CheckLine(hubRingLine, p, ref bestSq, ref best);
            CheckLine(captureLine, p, ref bestSq, ref best);
            CheckLine(auxLine, p, ref bestSq, ref best);
            for (int i = 0; i < radiusLines.Count; i++)
                CheckLine(radiusLines[i], p, ref bestSq, ref best);

            if (bestSq <= maxDist * maxDist)
            {
                point = best;
                return true;
            }
            return false;
        }

        /// <summary>Nächster Punkt auf den Segmenten einer Linie (nur sichtbare Punkte).</summary>
        private void CheckLine(LineRenderer line, Vector3 p, ref float bestSq, ref Vector3 best)
        {
            if (line == null || !line.enabled || line.positionCount < 2)
                return;

            int n = line.positionCount;
            int end = line.loop ? n : n - 1; // loop: auch das schließende Segment
            for (int i = 0; i < end; i++)
            {
                Vector3 a = GetWorldPoint(line, i);
                Vector3 b = GetWorldPoint(line, (i + 1) % n);
                Vector3 ab = b - a;
                float t = Vector3.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-12f);
                t = Mathf.Clamp01(t);
                Vector3 closest = a + ab * t;
                float sq = (closest - p).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = closest;
                }
            }
        }

        private static Vector3 GetWorldPoint(LineRenderer line, int i)
        {
            Vector3 pos = line.GetPosition(i);
            return line.useWorldSpace ? pos : line.transform.TransformPoint(pos);
        }

        /// <summary>
        /// Zeigt anteilig (t01) die ersten Punkte einer Linie, bei 0 keine.
        /// Im Lokalraum (nach Vibration) werden die Weltkoordinaten umgerechnet.
        /// </summary>
        private void SetPoints(LineRenderer line, List<Vector3> full, float t01)
        {
            int n = Mathf.RoundToInt(full.Count * Mathf.Clamp01(t01));
            line.positionCount = n;
            for (int i = 0; i < n; i++)
                line.SetPosition(i, _localSpace ? transform.InverseTransformPoint(full[i]) : full[i]);
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
