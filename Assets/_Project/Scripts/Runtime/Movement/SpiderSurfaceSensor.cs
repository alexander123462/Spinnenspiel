using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Oberflaechen-Sensor fuer das Klettern: findet die waehrste Unterlage unter der Spinne
    /// (Boden, Wand, Kante) und meldet, ob eine Wand voraus ist. Reine Klasse, kein MonoBehaviour.
    /// </summary>
    [System.Serializable]
    public class SpiderSurfaceSensor
    {
        /// <summary>Hoehe der Probe ueber der Spinne (z. B. Beinlaenge).</summary>
        public float probeLift = 0.08f;
        /// <summary>Wie tief Strahl A (vertikal) sucht.</summary>
        public float probeDepth = 0.2f;
        /// <summary>Reichweite der Wand-Probe nach vorn.</summary>
        public float wallProbe = 0.14f;
        /// <summary>Raycast-Maske ohne Layer 2 (Ignore Raycast / Spinne) und ohne Pflanzen (Layer 8).</summary>
        public int mask = Physics.DefaultRaycastLayers & ~(1 << 2) & ~GameLayers.PlantMask;

        private const float TiltDegrees = 40f;

        /// <summary>
        /// Sucht die Oberflaeche unter der Spinne.
        /// </summary>
        /// <param name="position">Standort der Spinne.</param>
        /// <param name="up">Eigene Oberseite der Spinne (nicht zwingend Welt-up).</param>
        /// <param name="forward">Blickrichtung der Spinne.</param>
        /// <param name="point">Trefferpunkt der waehrsten Oberflaeche.</param>
        /// <param name="normal">Normalenvektor der Oberflaeche.</param>
        /// <param name="wallAhead">true, wenn kurz vor der Spinne eine Wand steht.</param>
        public bool Sense(Vector3 position, Vector3 up, Vector3 forward, out Vector3 point, out Vector3 normal, out bool wallAhead)
        {
            up = up.normalized;
            forward = forward.normalized;
            Vector3 right = Vector3.Cross(up, forward);

            Vector3 origin = position + up * probeLift;
            float lenAB = probeLift + probeDepth;
            Vector3 down = -up;

            point = Vector3.zero;
            normal = Vector3.zero;
            wallAhead = false;

            // Strahl C: Wand voraus.
            Vector3 pointC = Vector3.zero;
            Vector3 nC = Vector3.zero;
            bool wallHit = false;
            if (Physics.Raycast(origin, forward, out RaycastHit hitC, wallProbe, mask, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Angle(hitC.normal, up) > 35f)
                {
                    wallHit = true;
                    pointC = hitC.point;
                    nC = hitC.normal;
                }
            }

            // Strahl A: senkrecht nach unten.
            RaycastHit hitA;
            bool hasA = Physics.Raycast(origin, down, out hitA, lenAB, mask, QueryTriggerInteraction.Ignore);

            // Strahlen B: nach unten 40 Grad zu den vier Seiten gekippt.
            RaycastHit hitB0, hitB1, hitB2, hitB3;
            bool hb0 = RayB(origin, down, forward, lenAB * 1.3f, out hitB0);
            bool hb1 = RayB(origin, down, -forward, lenAB * 1.3f, out hitB1);
            bool hb2 = RayB(origin, down, right, lenAB * 1.3f, out hitB2);
            bool hb3 = RayB(origin, down, -right, lenAB * 1.3f, out hitB3);

            if (wallHit)
            {
                wallAhead = true;
                point = pointC;
                Vector3 sum = Vector3.zero;
                int count = 0;
                if (hasA) { sum += hitA.normal; count++; }
                if (hb0) { sum += hitB0.normal; count++; }
                if (hb1) { sum += hitB1.normal; count++; }
                if (hb2) { sum += hitB2.normal; count++; }
                if (hb3) { sum += hitB3.normal; count++; }

                if (count > 0)
                {
                    normal = (nC * 0.7f + (sum / count) * 0.3f).normalized;
                }
                else
                {
                    normal = nC;
                }

                return true;
            }

            // Strahl D: nur wenn A nichts getroffen hat (Außenkante hinter der Kante).
            RaycastHit hitD = default;
            bool hasD = false;
            if (!hasA)
            {
                hasD = Physics.Raycast(origin + forward * 0.1f - up * (probeLift + 0.12f), -forward, out hitD, 0.25f, mask, QueryTriggerInteraction.Ignore);
            }

            // Kein Treffer aus A, B, D => keine Oberflaeche.
            bool any = hasA || hb0 || hb1 || hb2 || hb3 || hasD;
            if (!any)
            {
                return false;
            }

            float weightSum = 0f;
            Vector3 weighted = Vector3.zero;
            float bestDist = float.MaxValue;
            Vector3 bestPoint = Vector3.zero;
            if (hasA) { AddWeighted(ref weighted, ref weightSum, hitA, ref bestDist, ref bestPoint); }
            if (hb0) { AddWeighted(ref weighted, ref weightSum, hitB0, ref bestDist, ref bestPoint); }
            if (hb1) { AddWeighted(ref weighted, ref weightSum, hitB1, ref bestDist, ref bestPoint); }
            if (hb2) { AddWeighted(ref weighted, ref weightSum, hitB2, ref bestDist, ref bestPoint); }
            if (hb3) { AddWeighted(ref weighted, ref weightSum, hitB3, ref bestDist, ref bestPoint); }
            if (hasD) { AddWeighted(ref weighted, ref weightSum, hitD, ref bestDist, ref bestPoint); }

            normal = (weighted / weightSum).normalized;
            point = hasA ? hitA.point : bestPoint;
            return true;
        }

        private bool RayB(Vector3 origin, Vector3 down, Vector3 tiltDir, float length, out RaycastHit hit)
        {
            Vector3 dir = (Quaternion.AngleAxis(TiltDegrees, Vector3.Cross(down, tiltDir)) * down).normalized;
            return Physics.Raycast(origin, dir, out hit, length, mask, QueryTriggerInteraction.Ignore);
        }

        private static void AddWeighted(ref Vector3 weighted, ref float weightSum, RaycastHit hit, ref float bestDist, ref Vector3 bestPoint)
        {
            float w = 1f / (hit.distance + 0.01f);
            weighted += hit.normal * w;
            weightSum += w;
            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestPoint = hit.point;
            }
        }
    }
}
