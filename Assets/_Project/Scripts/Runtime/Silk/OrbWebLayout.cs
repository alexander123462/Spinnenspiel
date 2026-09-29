using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Geometrie eines echten Radnetzes (Orb Web) als reine Berechnungslogik:
    /// Rahmen-Polygon, Radien, Hilfsspirale, Fangspirale und Hub-Ring,
    /// alles in einer gemeinsamen Ebene um die Nabe (Hub).
    /// </summary>
    public class OrbWebLayout
    {
        public Vector3[] FrameAnchors { get; }
        public int RadiusCount { get; }
        public float SpiralSpacing { get; }
        public float FreeZoneRadius { get; }

        /// <summary>Nabe des Netzes (leicht oberhalb des Anker-Schwerpunkts).</summary>
        public Vector3 Hub { get; }
        /// <summary>Plannormale (Best-Fit über die Anker).</summary>
        public Vector3 Normal { get; }

        /// <summary>Geschlossenes Rahmen-Polygon (Anker, 8 % Richtung Nabe).</summary>
        public List<Vector3> Frame { get; }
        /// <summary>Endpunkte der radialen Fäden am Rahmen.</summary>
        public List<Vector3> RadiusEnds { get; }
        /// <summary>Hilfsspirale (stabilisierend, optisch normalerweise ausgeblendet).</summary>
        public List<Vector3> AuxSpiral { get; }
        /// <summary>Fangspirale, von außen nach innen zur Nabe.</summary>
        public List<Vector3> CaptureSpiral { get; }
        /// <summary>Kleiner Ring um die Nabe.</summary>
        public List<Vector3> HubRing { get; }

        private readonly Vector2[] framePlane;
        private readonly Vector3 basisU;
        private readonly Vector3 basisV;
        private const int MaxSpiralSteps = 10000;

        public OrbWebLayout(Vector3[] frameAnchors, int radiusCount = 24, float spiralSpacing = 0.025f,
            float freeZoneRadius = 0.04f, int seed = 1)
        {
            if (frameAnchors == null || (frameAnchors.Length != 3 && frameAnchors.Length != 4))
                throw new System.ArgumentException("frameAnchors muss 3 oder 4 Punkte haben.");
            if (radiusCount < 3)
                throw new System.ArgumentException("radiusCount muss >= 3 sein.");

            FrameAnchors = (Vector3[])frameAnchors.Clone();
            RadiusCount = radiusCount;
            SpiralSpacing = spiralSpacing;
            FreeZoneRadius = freeZoneRadius;

            // Best-Fit-Normale: gemittelte Kreuzprodukte der Kanten um den Schwerpunkt.
            Vector3 centroid = Vector3.zero;
            foreach (var a in frameAnchors) centroid += a;
            centroid /= frameAnchors.Length;

            Vector3 n = Vector3.zero;
            for (int i = 0; i < frameAnchors.Length; i++)
            {
                Vector3 v1 = frameAnchors[i] - centroid;
                Vector3 v2 = frameAnchors[(i + 1) % frameAnchors.Length] - centroid;
                n += Vector3.Cross(v1, v2);
            }
            Normal = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;

            // "Nach oben" innerhalb der Ebene (Nabe liegt oberhalb der Mitte).
            Vector3 upInPlane = Vector3.up - Normal * Vector3.Dot(Vector3.up, Normal);
            if (upInPlane.sqrMagnitude < 1e-8f)
                upInPlane = Vector3.right - Normal * Vector3.Dot(Vector3.right, Normal);
            upInPlane = upInPlane.sqrMagnitude > 1e-8f ? upInPlane.normalized : Vector3.forward;

            float avgRadius = 0f;
            foreach (var a in frameAnchors) avgRadius += Vector3.Distance(centroid, a);
            avgRadius /= frameAnchors.Length;

            Hub = centroid + upInPlane * (0.08f * avgRadius);

            // Orthonormale Basis in der Ebene: u, v senkrecht auf Normal.
            basisU = Vector3.Cross(Normal, upInPlane).normalized;
            basisV = Vector3.Cross(Normal, basisU).normalized;

            // Rahmen hängt zwischen den Ankern: 8 % Richtung Nabe.
            Frame = new List<Vector3>(frameAnchors.Length);
            framePlane = new Vector2[frameAnchors.Length];
            for (int i = 0; i < frameAnchors.Length; i++)
            {
                Vector3 a = frameAnchors[i];
                Frame.Add(a + (Hub - a) * 0.08f);
                framePlane[i] = ToPlane(Frame[i]);
            }

            var rng = new System.Random(seed);
            const float jitterRad = 3f * Mathf.Deg2Rad;
            float stepAngle = 2f * Mathf.PI / radiusCount;

            // Radien: Strahlen von der Nabe in gleichmäßigen Winkeln (±3° Zufall).
            RadiusEnds = new List<Vector3>(radiusCount);
            for (int i = 0; i < radiusCount; i++)
            {
                float ang = i * stepAngle + (float)(rng.NextDouble() * 2.0 - 1.0) * jitterRad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float r = FrameRadius(dir);
                RadiusEnds.Add(ToWorld(dir * r));
            }

            // Mindest- und Maximal-Rahmenradius über alle Radien.
            float minFrameR = float.PositiveInfinity;
            float maxFrameR = 0f;
            for (int i = 0; i < radiusCount; i++)
            {
                float ang = i * stepAngle;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float r = FrameRadius(dir);
                minFrameR = Mathf.Min(minFrameR, r);
                maxFrameR = Mathf.Max(maxFrameR, r);
            }

            // Hilfsspirale: von 3*freeZoneRadius nach außen, je Umlauf spiralSpacing*4,
            // ein Punkt pro Radius; die Spirale endet, sobald ein Radius den Rahmen verlässt,
            // damit jeder Umlauf geschlossen bleibt.
            AuxSpiral = new List<Vector3>();
            {
                float r0 = 3f * freeZoneRadius;
                float perTurn = spiralSpacing * 4f;
                for (int t = 0; t < MaxSpiralSteps; t++)
                {
                    float r = r0 + t * perTurn;
                    if (r > minFrameR) break;
                    for (int i = 0; i < radiusCount; i++)
                    {
                        float ang = i * stepAngle;
                        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        if (r > FrameRadius(dir)) continue;
                        AuxSpiral.Add(ToWorld(dir * r));
                    }
                }
            }

            // Fangspirale: je Radius 90 % des Rahmenabstands, je Umlauf spiralSpacing nach innen,
            // bis 2*freeZoneRadius; die Spirale endet mit dem ersten Radius, der zu tief wird,
            // damit jeder Umlauf geschlossen bleibt (keine Sprünge über Lücken).
            CaptureSpiral = new List<Vector3>();
            {
                float inner = 2f * freeZoneRadius;
                for (int t = 0; t < MaxSpiralSteps; t++)
                {
                    if (0.9f * minFrameR - t * spiralSpacing < inner) break;
                    for (int i = 0; i < radiusCount; i++)
                    {
                        float ang = i * stepAngle;
                        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        float r = 0.9f * FrameRadius(dir) - t * spiralSpacing;
                        if (r < inner) continue;
                        CaptureSpiral.Add(ToWorld(dir * r));
                    }
                }
            }

            // Hub-Ring: kleiner Kreis um die Nabe.
            HubRing = new List<Vector3>(12);
            {
                float r = freeZoneRadius * 0.5f;
                for (int i = 0; i < 12; i++)
                {
                    float ang = i * (2f * Mathf.PI / 12f);
                    HubRing.Add(ToWorld(new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r)));
                }
            }
        }

        /// <summary>Ebenenkoordinate (2D) in Weltkoordinate umwandeln.</summary>
        public Vector3 ToWorld(Vector2 p) => Hub + basisU * p.x + basisV * p.y;

        /// <summary>Weltkoordinate in Ebenenkoordinate (2D) umwandeln.</summary>
        public Vector2 ToPlane(Vector3 p)
        {
            Vector3 d = p - Hub;
            return new Vector2(Vector3.Dot(d, basisU), Vector3.Dot(d, basisV));
        }

        /// <summary>Abstand (m) eines Punktes zum Rahmen-Polygon.</summary>
        public float DistanceToFrame(Vector3 point)
        {
            Vector2 p = ToPlane(point);
            float best = float.MaxValue;
            for (int i = 0; i < framePlane.Length; i++)
            {
                Vector2 a = framePlane[i];
                Vector2 b = framePlane[(i + 1) % framePlane.Length];
                best = Mathf.Min(best, SegmentDistance(p, a, b));
            }
            return best;
        }

        /// <summary>Punkt-im-Polygon (2D) für den Rahmen.</summary>
        public bool IsInsideFrame(Vector3 point)
        {
            Vector2 p = ToPlane(point);
            bool inside = false;
            int cnt = framePlane.Length;
            for (int i = 0, j = cnt - 1; i < cnt; j = i++)
            {
                Vector2 a = framePlane[i], b = framePlane[j];
                if ((a.y > p.y) != (b.y > p.y))
                {
                    float xCross = (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x;
                    if (p.x < xCross) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Entfernung des Schnittpunkts des Strahls (2D-Richtung, Ursprung Nabe)
        /// mit dem Rahmen-Polygon, d. h. der Radius in Strahlrichtung.
        /// </summary>
        private float FrameRadius(Vector2 dir)
        {
            float best = float.MaxValue;
            for (int i = 0; i < framePlane.Length; i++)
            {
                Vector2 a = framePlane[i];
                Vector2 b = framePlane[(i + 1) % framePlane.Length];
                Vector2 seg = b - a;
                float den = dir.x * seg.y - dir.y * seg.x;
                if (Mathf.Abs(den) < 1e-9f) continue;
                float tRay = (a.x * seg.y - a.y * seg.x) / den;
                float tSeg = (a.x * dir.y - a.y * dir.x) / den;
                if (tRay > 1e-5f && tSeg >= 0f && tSeg <= 1f && tRay < best)
                    best = tRay;
            }
            return best == float.MaxValue ? float.PositiveInfinity : best;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float sq = ab.sqrMagnitude;
            float t = sq > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / sq) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
