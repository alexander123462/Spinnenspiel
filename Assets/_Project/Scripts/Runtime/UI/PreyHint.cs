using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// K3: Beute-Hinweis. Zeigt am Bildschirm-Rand ein kleine Pfeilchen zum naechsten
    /// Insekt, das im eigenen Netz haengt (State Caught), plus die Entfernung in m.
    /// Im HUD-Stil der Energieanzeige (J2): OnGUI direkt, keine eigene Canvas.
    /// Ohne gefangenem Insekt totally hidden.
    /// </summary>
    public class PreyHint : MonoBehaviour
    {
        private const float MarkerSizePx = 18f;

        private void Update()
        {
            _targetOffset = Vector3.zero;
            HasTarget = false;
            ShownDistanceMeters = 0f;

            FlyingInsect nearest = NearestCaughtInsect();
            if (nearest == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            _camForward = cam.transform.forward;
            _camRight = cam.transform.right;
            _camUp = cam.transform.up;
            _targetOffset = nearest.transform.position - cam.transform.position;
            ShownDistanceMeters = _targetOffset.magnitude;
            HasTarget = true;
        }

        /// <summary>Naechsten gefangenem Insekt (Caught), oder null.</summary>
        private FlyingInsect NearestCaughtInsect()
        {
            FlyingInsect best = null;
            float bestDist = float.MaxValue;
            foreach (FlyingInsect f in Object.FindObjectsByType<FlyingInsect>(FindObjectsSortMode.None))
            {
                if (f.State != FlyingInsect.InsectState.Caught)
                {
                    continue;
                }
                Vector3 delta = f.transform.position - transform.position;
                float d = delta.sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = f;
                }
            }
            return best;
        }

        // Public so the PlayMode-Test can read what OnGUI actually shows this frame.
        public bool HasTarget;
        public float ShownDistanceMeters;

        private Vector3 _camForward;
        private Vector3 _camRight;
        private Vector3 _camUp;
        private Vector3 _targetOffset;

        private void OnGUI()
        {
            if (!HasTarget)
            {
                return;
            }

            // Viewport units [-0.5..0.5] -> GUI coords [0..1], y down.
            Vector2 uv = PreyHintModel.ProjectToCameraSpace(_camForward, _camRight, _camUp, _targetOffset);
            float gx = 0.5f + uv.x;
            float gy = 0.5f - uv.y;
            float px = gx * Screen.width;
            float py = gy * Screen.height;

            // Pfeilchen: small square pointing outward (away from center), HUD-Stil like J2-Bar.
            GUI.color = new Color(1f, 0.9f, 0.3f, 0.95f);
            GUI.DrawTexture(new Rect(px - MarkerSizePx * 0.5f, py - MarkerSizePx * 0.5f, MarkerSizePx, MarkerSizePx), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Entfernungs-Label next to the marker.
            GUI.color = new Color(1f, 1f, 0.75f, 0.9f);
            string label = ((int)Mathf.Round(ShownDistanceMeters)) + " m";
            GUI.Label(new Rect(px + 18f, py - 10f, 96f, 22f), label);
            GUI.color = Color.white;
        }
    }
}
