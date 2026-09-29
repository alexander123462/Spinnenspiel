using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// Steuerungshilfe (t82): halbtransparente Box oben rechts.
    /// Beim Start sichtbar, nach 15 s automatisch aus.
    /// F1 (Tastatur) oder Gamepad-Select schaltet sie um.
    /// </summary>
    public class ControlsHelp : MonoBehaviour
    {
        private const float AutoHideSeconds = 15f;

        private static readonly string[] Lines =
        {
            "WASD / Stick – Laufen",
            "Shift / Stick drücken – Sprint",
            "Maus / rechter Stick – Kamera",
            "F / rechte Maustaste / rechte Schulter – Faden schießen (Fadenkreuz) / Faden entlangklettern",
            "Leertaste / B – Loslassen, am Sicherheitsfaden abseilen (am Seil: W/S hoch- oder runterklettern)",
            "N / Y – Netz bauen (wenn „Netz bauen“ angezeigt wird)",
            "E / X – Beute holen (wenn im Netz eine Fliege zappelt)",
            "Aus dem Netz fallen lassen",
        };

        /// <summary>Sichtbarkeit der Box (F1 / Select schaltet um).</summary>
        public bool Visible { get; private set; }

        private void Awake()
        {
            Visible = true;
        }

        private void Start()
        {
            Invoke(nameof(HideAfterDelay), AutoHideSeconds);
        }

        private void Update()
        {
            bool f1 = Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
            bool select = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
            if (f1 || select)
            {
                Visible = !Visible;
            }
        }

        private void HideAfterDelay()
        {
            Visible = false;
        }

        private void OnGUI()
        {
            if (!Visible)
            {
                return;
            }

            const float width = 340f;
            const float pad = 10f;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            var lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };

            // Gesamthöhe berechnen, damit die Box passt.
            float h = 28f + pad;
            foreach (string line in Lines)
            {
                h += lineStyle.CalcHeight(new GUIContent(line), width - 2f * pad) + 4f;
            }
            h += 20f + pad;

            float x = Screen.width - width - 12f;
            float y = 12f;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, width, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float ty = y + pad;
            GUI.Label(new Rect(x + pad, ty, width - 2f * pad, 24f), new GUIContent("Steuerung (F1)"), titleStyle);
            ty += 28f;

            foreach (string line in Lines)
            {
                float lh = lineStyle.CalcHeight(new GUIContent(line), width - 2f * pad) + 4f;
                GUI.Label(new Rect(x + pad, ty, width - 2f * pad, lh), new GUIContent(line), lineStyle);
                ty += lh;
            }

            ty += 6f;
            GUI.Label(new Rect(x + pad, ty, width - 2f * pad, 18f), new GUIContent("Hunger-Balken oben links."), lineStyle);
        }
    }
}
