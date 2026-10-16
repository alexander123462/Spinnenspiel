using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// K3: Pure-Logic-Mathematicalmodel for the Beute-Hint (no MonoBehaviour, testable in EditMode).
    /// The target (a caught insect) is projected to a normalized viewport space (u right, v up),
    /// outside the screen this is clamped to a ring at the edge (the direction stays exact!).
    /// </summary>
    public class PreyHintModel
    {
        /// <summary>Radius of the hint-ring in half-viewport units (0.5 = screen edge).</summary>
        private static readonly float ringRadius = 0.42f;

        /// <summary>Targets closer than this are shown at their real position, not on the ring.</summary>
        private static readonly float insideMargin = 0.30f;

        /// <summary>
        /// Projects the world-offset of the target into camera space.
        /// Returns (u, v, distance): u/v are viewport units [-0.5..0.5], center 0; z is the true distance.
        /// A target exactly behind the camera gets a fallback direction (down).
        /// </summary>
        public static Vector2 ProjectToCameraSpace(Vector3 camForward, Vector3 camRight, Vector3 camUp, Vector3 targetOffsetFromCam)
        {
            float dist = targetOffsetFromCam.magnitude;
            if (dist < 1e-6f)
            {
                return new Vector2(0f, 0f);
            }

            Vector3 d = targetOffsetFromCam / dist;
            float z = Vector3.Dot(d, camForward);
            float x = Vector3.Dot(d, camRight);
            float y = Vector3.Dot(d, camUp);

            // Direction in the image plane (works also for targets behind the camera).
            float uRaw = x;
            float vRaw = y;
            if (z <= 0f && Mathf.Abs(uRaw) + Mathf.Abs(vRaw) < 1e-4f)
            {
                uRaw = 0f;
                vRaw = -1f; // exactly behind -> hint down
            }

            float len = Mathf.Sqrt(uRaw * uRaw + vRaw * vRaw);
            if (len < 1e-6f)
            {
                return new Vector2(0f, 0f);
            }

            float r;
            if (z > 0f && len < insideMargin)
            {
                r = len; // on the screen: real position
            }
            else
            {
                r = ringRadius; // off-screen / behind: clamp to edge-ring, direction preserved
            }

            return new Vector2(uRaw / len * r, vRaw / len * r);
        }

        /// <summary>
        /// Screen angle (degrees, 0 = straight ahead/up in the image plane, +clockwise) of a world
        /// offset as seen from the camera. Targets behind the camera get the opposite projection
        /// direction, exactly like the ring clamp in ProjectToCameraSpace does.
        /// </summary>
        public static float TargetScreenAngleDegrees(Vector3 camForward, Vector3 camRight, Vector3 camUp, Vector3 offsetFromCam)
        {
            float dist = offsetFromCam.magnitude;
            if (dist < 1e-6f)
            {
                return 0f;
            }
            Vector3 d = offsetFromCam / dist;
            float x = Vector3.Dot(d, camRight);
            float y = Vector3.Dot(d, camUp);
            float z = Vector3.Dot(d, camForward);
            if (z <= 0f)
            {
                x = -x;
                y = -y; // behind the camera: hint points to the opposite image direction
            }
            return ScreenAngleOf(x, y);
        }

        /// <summary>Screen angle (degrees, 0 = up in the image plane, +clockwise) of a hint position.</summary>
        public static float HintScreenAngleDegrees(Vector2 uv)
        {
            return ScreenAngleOf(uv.x, uv.y);
        }

        /// <summary>Smallest absolute angle difference (degrees) between two screen angles.</summary>
        private static float AngleDiff(float a, float b)
        {
            float d = Mathf.Abs(a - b) % 360f;
            return d > 180f ? 360f - d : d;
        }

        /// <summary>Deviation (degrees) between the hint direction and the true target direction.</summary>
        public static float DirectionDeviationDegrees(Vector3 camForward, Vector3 camRight, Vector3 camUp, Vector3 offsetFromCam)
        {
            Vector2 uv = ProjectToCameraSpace(camForward, camRight, camUp, offsetFromCam);
            if (uv.magnitude < 1e-4f)
            {
                return 0f; // target straight ahead: hint centered, no direction to deviate
            }
            return AngleDiff(TargetScreenAngleDegrees(camForward, camRight, camUp, offsetFromCam), HintScreenAngleDegrees(uv));
        }

        private static float ScreenAngleOf(float x, float y)
        {
            if (Mathf.Abs(x) + Mathf.Abs(y) < 1e-6f)
            {
                return 0f;
            }
            return Mathf.Atan2(x, y) * Mathf.Rad2Deg;
        }
    }
}
