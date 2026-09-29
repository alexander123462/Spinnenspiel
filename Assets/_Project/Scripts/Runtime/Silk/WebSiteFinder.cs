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
        /// Blick-Ebene. Pro Quadrant gewinnt der Treffer mit der größten
        /// Bewertung score = min(|dx|, |dy|) – Ecken schlagen Seitenpunkte.
        /// Erfolgreich, wenn alle vier Quadranten einen Treffer haben und
        /// die Spannweiten dx(TR)-dx(TL), dx(BR)-dx(BL), dy(TL)-dy(BL),
        /// dy(TR)-dy(BR) jeweils >= 0.25 m betragen.
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
            float bestTl = -1f, bestTr = -1f, bestBr = -1f, bestBl = -1f;

            for (int i = 0; i < DirectionCount; i++)
            {
                // 16 Richtungen, alle 22.5 Grad, Start 11.25 Grad - keine Richtung
                // genau waagrecht oder senkrecht, damit TL/TR/BR/BL eindeutig sind.
                float rad = Mathf.Deg2Rad * (11.25f + i * 22.5f);
                Vector3 dir = right * Mathf.Cos(rad) + up * Mathf.Sin(rad);
                float x = Vector3.Dot(dir, right);
                float y = Vector3.Dot(dir, up);

                // Naechster gültige Treffer dieser Richtung (Hits sortiert nach Distanz).
                RaycastHit[] hits = Physics.SphereCastAll(center, 0.04f, dir, MaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                {
                    if (ignore != null && hits[h].collider.transform.IsChildOf(ignore))
                    {
                        continue;
                    }
                    if (hits[h].distance <= 0f)
                    {
                        continue; // Ueberlappung am Start, z.B. eigener Kollider
                    }
                    if (hits[h].distance < MinAnchorDistance)
                    {
                        continue;
                    }

                    // Trefferpunkt auf die Netzebene (durch center, Normale n)
                    // projizieren, damit alle vier Anker in einer Ebene liegen.
                    Vector3 p = hits[h].point - n * Vector3.Dot(hits[h].point - center, n);
                    // Bewertung: Ecken (beide Komponenten groß) schlagen
                    // weit abseits gelegene Seitenpunkte.
                    float score = Mathf.Min(
                        Mathf.Abs(Vector3.Dot(p - center, right)),
                        Mathf.Abs(Vector3.Dot(p - center, up)));
                    if (x < 0f && y > 0f)
                    {
                        if (score > bestTl)
                        {
                            bestTl = score;
                            tl = p;
                        }
                    }
                    else if (x > 0f && y > 0f)
                    {
                        if (score > bestTr)
                        {
                            bestTr = score;
                            tr = p;
                        }
                    }
                    else if (x > 0f && y < 0f)
                    {
                        if (score > bestBr)
                        {
                            bestBr = score;
                            br = p;
                        }
                    }
                    else if (x < 0f && y < 0f)
                    {
                        if (score > bestBl)
                        {
                            bestBl = score;
                            bl = p;
                        }
                    }
                    break;
                }
            }

            bool ok = bestTl >= 0f && bestTr >= 0f && bestBr >= 0f && bestBl >= 0f;
            if (!ok)
            {
                return false;
            }

            // Spannweiten als Komponenten-Abstände prüfen (quadratischere Rahmen).
            float dxtl = Vector3.Dot(tl - center, right);
            float dxtr = Vector3.Dot(tr - center, right);
            float dxbr = Vector3.Dot(br - center, right);
            float dxbl = Vector3.Dot(bl - center, right);
            float dytl = Vector3.Dot(tl - center, up);
            float dytr = Vector3.Dot(tr - center, up);
            float dybr = Vector3.Dot(br - center, up);
            float dybl = Vector3.Dot(bl - center, up);
            ok = dxtr - dxtl >= MinSpreadX
                && dxbr - dxbl >= MinSpreadX
                && dytl - dybl >= MinSpreadY
                && dytr - dybr >= MinSpreadY;
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

            float[] distances = { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f };
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
