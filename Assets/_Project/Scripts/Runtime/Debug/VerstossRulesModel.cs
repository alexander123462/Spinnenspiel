using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// P1 Waechter: reine Regel-Modell (no Unity scene access). Every rule is a
    /// pure function (inputs -> yes/no) so it can be unit-tested without a scene.
    /// The SpielWaechter component reads the live values and calls these.
    /// </summary>
    public class VerstossRulesModel
    {
        /// <summary>R1: Body centre more than tolerance under terrain height.</summary>
        public const float R1Tolerance = 0.05f;

        public static bool R1_BelowTerrain(float spiderY, float terrainY)
        {
            return spiderY < terrainY - R1Tolerance;
        }

        /// <summary>R2: Up-vector vs surface normal tilted beyond maxAngleDeg without a climb state (thread/web/rope/grounded).</summary>
        public const float R2MaxAngleDeg = 100f;

        public static bool R2_BadSlope(float upDotNormal, bool climbAllowed)
        {
            if (climbAllowed)
            {
                return false;
            }

            float dot = Mathf.Clamp(upDotNormal, -1f, 1f);
            float angleDeg = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return angleDeg > R2MaxAngleDeg;
        }

        // ---- R3: input active for longer than idleTime seconds but moved less than moveEps meters ----
        public const float R3IdleTimeS = 5f;
        public const float R3MoveEps = 0.05f;

        public static bool R3_NoProgress(float inputMagnitude, float travelledMeters, float elapsedS)
        {
            if (inputMagnitude <= 0.001f)
            {
                return false;
            }

            return elapsedS >= R3IdleTimeS && travelledMeters < R3MoveEps;
        }

        // ---- R4: position NaN/inf or fall speed over 30 m/s ----
        public const float R4MaxFallSpeed = 30f;

        public static bool R4_PositionOrSpeedInvalid(float x, float y, float z, float fallSpeed)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                return true;
            }

            return fallSpeed > R4MaxFallSpeed;
        }

        private static bool IsFinite(float v)
        {
            return !(v != v) && v != float.PositiveInfinity && v != float.NegativeInfinity;
        }

        // ---- R5: frame time spike over 100 ms ----
        public const float R5MaxFrameTimeMs = 100f;

        public static bool R5_FrameSpike(float frameTimeMs)
        {
            return frameTimeMs > R5MaxFrameTimeMs;
        }

        // ---- R6: body centre inside a tree collider (queried by the component) ----
        public static bool R6_InsideTreeCollider(bool bodyCentreInsideTree)
        {
            return bodyCentreInsideTree;
        }
    }
}

