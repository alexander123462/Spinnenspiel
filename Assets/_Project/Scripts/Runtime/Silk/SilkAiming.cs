using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Steuerung der Seide (t53): Faden per Taste spinnen (Silk) und per Taste loslassen (Release).
    /// Zielen über die Bildschirmmitte (Kamera-Raycast, 15 m, ohne Layer 2), gültig bei Treffer
    /// in maxRange, während die Spinne haftet und nicht am Faden ist. Zeigt Fadenkreuz + Tasten-Hinweis per OnGUI.
    /// </summary>
    public class SilkAiming : MonoBehaviour
    {
        [Tooltip("Seiden-System (ShootThread, maxRange, spinnerets); bei leer wird automatisch gefunden.")]
        public SpiderSilk silk;

        [Tooltip("Spieler-Input (Silk/Release); bei leer wird automatisch gefunden.")]
        public SpiderInput input;

        [Tooltip("Bewegung (CurrentThread, IsAttached, LetGo); bei leer wird automatisch gefunden.")]
        public SpiderMovement movement;

        [Tooltip("Kamera fürs Zielen; bei leer wird Camera.main verwendet.")]
        public Camera aimCamera;

        private const float RayLength = 15f;

        /// <summary>True, wenn die Bildschirmmitte auf einen gültigen Spinn-Treffer zeigt.</summary>
        public bool HasValidTarget { get; private set; }

        /// <summary>Trefferpunkt in der Welt; nur bei <see cref="HasValidTarget"/> gültig.</summary>
        public Vector3 TargetPoint { get; private set; }

        private void Awake()
        {
            if (movement == null)
            {
                movement = GetComponent<SpiderMovement>();
            }

            if (silk == null)
            {
                silk = GetComponent<SpiderSilk>();
            }

            if (input == null)
            {
                input = GetComponent<SpiderInput>();
            }
        }

        private void Update()
        {
            UpdateAim();

            // Faden spinnen: Taste + gültiges Ziel.
            if (input != null && input.SilkPressed && HasValidTarget && silk != null && silk.spinnerets != null)
            {
                Vector3 dir = (TargetPoint - silk.spinnerets.position).normalized;
                silk.ShootThread(dir);
            }

            // Loslassen: Taste + Spinne aktuell am Faden oder im Netz.
            if (input != null && input.ReleasePressed && movement != null
                && (movement.CurrentThread != null || movement.CurrentWeb != null))
            {
                movement.LetGo();
            }
            // B4c: Am Seil (Dragline, neither thread nor web) Leertaste = fallen lassen too —
            // otherwise the rope was a dead state where space did nothing.
            else if (input != null && input.ReleasePressed && movement != null && movement.IsOnRope)
            {
                movement.DropFromRope();
            }
        }

        private void UpdateAim()
        {
            HasValidTarget = false;
            TargetPoint = Vector3.zero;

            if (silk == null || silk.spinnerets == null || movement == null)
            {
                return;
            }

            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            // Spinne muss haften und nicht am Faden sein (sonst kein neu Spinnen).
            if (!movement.IsAttached || movement.CurrentThread != null)
            {
                return;
            }

            Ray ray = cam.ViewportPointToRay(new Vector2(0.5f, 0.5f));
            // Layer 2 = Spinne, bewusst ausgenommen (wie bei SpiderSilk).
            if (!Physics.Raycast(ray, out RaycastHit hit, RayLength,
                    Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if ((hit.point - silk.spinnerets.position).magnitude > silk.maxRange)
            {
                return;
            }

            // Treffer muss auch als Faden-Anker gültig sein (keine zu nahe / eigene Ebene).
            Vector3 dir = hit.point - silk.spinnerets.position;
            if (!Physics.Raycast(silk.spinnerets.position, dir.normalized, out RaycastHit thatHit,
                    silk.maxRange, Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore)
                || !silk.CanAnchor(silk.spinnerets.position, dir, thatHit, movement.SurfaceNormal))
            {
                return;
            }

            TargetPoint = hit.point;
            HasValidTarget = true;
        }

        private void OnGUI()
        {
            float cx = Screen.width * 0.5f;

            // Fadenkreuz: kleiner Punkt in der Bildschirmmitte, weiß bei gültigem Ziel, sonst grau-transparent.
            GUI.color = HasValidTarget
                ? new Color(1f, 1f, 1f, 0.9f)
                : new Color(0.6f, 0.6f, 0.6f, 0.4f);
            GUI.DrawTexture(new Rect(cx - 4f, Screen.height * 0.5f - 4f, 8f, 8f), Texture2D.whiteTexture);

            // Tasten-Hinweis unten Mitte.
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            string hint = (movement != null && movement.CurrentThread != null)
                ? "[Leertaste] loslassen"
                : "[F] Faden spinnen";
            Vector2 size = GUI.skin.label.CalcSize(new GUIContent(hint));
            GUI.Label(new Rect(cx - size.x * 0.5f, Screen.height - size.y - 12f, size.x, size.y), hint);
        }
    }
}
