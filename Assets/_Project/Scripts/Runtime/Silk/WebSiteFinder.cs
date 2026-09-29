using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Findet automatisch einen Netzplatz in der Welt: um einen Mittelpunkt herum
    /// muss in allen vier Quadranten (links/rechts x oben/unten) ein greifbarer
    /// Collider sein, damit das Netz gespannt werden kann.
    /// </summary>
    public static class WebSiteFinder
    {
        private const int DirectionCount = 16;
        private const float MaxDistance = 1.2f;
        private const float MinAnchorDistance = 0.12f;
        private const float MinSpreadX = 0.25f;
        private const float MinSpreadY = 0.25f;

        /// <summary>
        /// Findet vier Ankerpunkte (TL, TR, BR, BL) um den Mittelpunkt in der
        /// Blick-Ebene. Erfolgreich, wenn alle vier Quadranten einen Treffer
        /// haben und der Abstand TL-TR sowie TL-BL jeweils >= 0.25 m betragen.
        /// </summary>
        public static bool TryFind(Vector3 center, Vector3 viewForward, Transform ignore, out Vector3[] anchors)
        {
            anchors = null;

            // Blickrichtung waagrecht, Fallback nach vorne.
            Vector3 n = viewForward;
            n.y = 0f;
            if (n.sqrMagnitude < 0.0001f)
            {
                n = Vector3.forward;
            }
            else
            {
                n = n.normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, n).normalized;
            Vector3 up = Vector3.up;

            Vector3 tl = Vector3.zero, tr = Vector3.zero, br = Vector3.zero, bl = Vector3.zero;
            float bestTl = 0f, bestTr = 0f, bestBr = 0f, bestBl = 0f;

            for (int i = 0; i < DirectionCount; i++)
            {
                // 16 Richtungen, alle 22.5 Grad, Start 11.25 Grad - keine Richtung
                // genau waagrecht oder senkrecht, damit TL/TR/BR/BL eindeutig sind.
                float rad = Mathf.Deg2Rad * (11.25f + i * 22.5f);
                Vector3 dir = right * Mathf.Cos(rad) + up * Mathf.Sin(rad);
                float x = Vector3.Dot(dir, right);
                float y = Vector3.Dot(dir, up);

                // Naechster gültige Treffer dieser Richtung (Hits sortiert nach Distanz).
                RaycastHit[] hits = Physics.RaycastAll(center, dir, MaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                {
                    if (ignore != null && hits[h].collider.transform.IsChildOf(ignore))
                    {
                        continue;
                    }
                    if (hits[h].distance < MinAnchorDistance)
                    {
                        continue;
                    }

                    float d = hits[h].distance;
                    if (x < 0f && y > 0f)
                    {
                        if (d > bestTl)
                        {
                            bestTl = d;
                            tl = hits[h].point;
                        }
                    }
                    else if (x > 0f && y > 0f)
                    {
                        if (d > bestTr)
                        {
                            bestTr = d;
                            tr = hits[h].point;
                        }
                    }
                    else if (x > 0f && y < 0f)
                    {
                        if (d > bestBr)
                        {
                            bestBr = d;
                            br = hits[h].point;
                        }
                    }
                    else if (x < 0f && y < 0f)
                    {
                        if (d > bestBl)
                        {
                            bestBl = d;
                            bl = hits[h].point;
                        }
                    }
                    break;
                }
            }

            bool ok = bestTl > 0f && bestTr > 0f && bestBr > 0f && bestBl > 0f;
            if (!ok)
            {
                return false;
            }

            ok = Vector3.Distance(tl, tr) >= MinSpreadX && Vector3.Distance(tl, bl) >= MinSpreadY;
            if (!ok)
            {
                return false;
            }

            anchors = new[] { tl, tr, br, bl };
            return true;
        }

        /// <summary>
        /// Probiert mehrere Mittelpunkte um die Spinne (vor der Spinne und nach
        /// oben versetzt) und gibt den ersten erfolgreichen zurück.
        /// </summary>
        public static bool TryFindNear(Vector3 spiderPos, Vector3 viewForward, Transform ignore, out Vector3[] anchors, out Vector3 center)
        {
            anchors = null;
            center = Vector3.zero;

            Vector3 n = viewForward;
            n.y = 0f;
            if (n.sqrMagnitude < 0.0001f)
            {
                n = Vector3.forward;
            }
            else
            {
                n = n.normalized;
            }

            float[] distances = { 0.3f, 0.6f };
            float[] heights = { 0.35f, 0.6f, 0.9f };
            foreach (float d in distances)
            {
                foreach (float h in heights)
                {
                    Vector3 c = spiderPos + n * d + Vector3.up * h;
                    if (TryFind(c, viewForward, ignore, out Vector3[] a))
                    {
                        anchors = a;
                        center = c;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
