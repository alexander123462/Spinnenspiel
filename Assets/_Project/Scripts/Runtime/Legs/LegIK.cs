using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Analytische Zwei-Knochen-IK (Kosinussatz).
    /// Liefert die Kniepositon fuer eine Kette Wurzel -> Knie -> Ende.
    /// Die Knie-Richtung wird ueber einen Pol-Vektor bestimmt (bei Spinnen: nach oben/aussen).
    /// </summary>
    public static class LegIK
    {
        private const float Epsilon = 1e-4f;

        /// <param name="root">Weltposition des Gelenkursprungs (Hüfte).</param>
        /// <param name="target">Gewünschte Weltposition des Kettenendes.</param>
        /// <param name="poleDirection">Richtung, in die das Knie zeigen soll.</param>
        /// <param name="upperLength">Länge des ersten Segments.</param>
        /// <param name="lowerLength">Länge des zweiten Segments.</param>
        /// <param name="reachedEnd">Tatsächlich erreichtes Kettenende (bei Überdehnung auf maximale Reichweite begrenzt).</param>
        /// <returns>Weltposition des Knies.</returns>
        public static Vector3 SolveTwoBone(Vector3 root, Vector3 target, Vector3 poleDirection,
            float upperLength, float lowerLength, out Vector3 reachedEnd)
        {
            Vector3 toTarget = target - root;
            float distance = toTarget.magnitude;
            Vector3 direction = distance > Epsilon ? toTarget / distance : Vector3.down;

            float maxReach = upperLength + lowerLength - Epsilon;
            float minReach = Mathf.Abs(upperLength - lowerLength) + Epsilon;
            distance = Mathf.Clamp(distance, minReach, maxReach);
            reachedEnd = root + direction * distance;

            // Abstand vom Ursprung bis zum Fußpunkt des Lots vom Knie auf die Wurzel-Ende-Achse.
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));

            Vector3 bend = Vector3.ProjectOnPlane(poleDirection, direction);
            if (bend.sqrMagnitude < Epsilon * Epsilon)
            {
                bend = Vector3.ProjectOnPlane(Vector3.up, direction);
            }

            if (bend.sqrMagnitude < Epsilon * Epsilon)
            {
                bend = Vector3.ProjectOnPlane(Vector3.forward, direction);
            }

            bend.Normalize();
            return root + direction * along + bend * height;
        }
    }
}
