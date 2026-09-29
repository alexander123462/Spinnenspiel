using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// Löst im Spiel den Netzbau aus: Taste N (Tastatur) oder Y (Gamepad)
    /// startet den Bau des Netzes an den Ankerpunkten dieses Objekts,
    /// wenn die Spinne in Reichweite ist.
    /// </summary>
    public class WebBuildDemo : MonoBehaviour
    {
        /// <summary>Vier Ankerpunkte für das Netz (exakt 4).</summary>
        public Vector3[] anchors;
        /// <summary>Maximaler Abstand Spinne → Anker-Mittelpunkt für die Auslösung.</summary>
        public float triggerDistance = 1.5f;

        private void Update()
        {
            if (FindFirstObjectByType<SpiderWebPlacer>() != null) return;
            bool pressed = false;
            if (Keyboard.current != null)
            {
                pressed = Keyboard.current.nKey.wasPressedThisFrame;
            }
            if (!pressed && Gamepad.current != null)
            {
                pressed = Gamepad.current.buttonNorth.wasPressedThisFrame;
            }
            if (!pressed)
            {
                return;
            }

            SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
            if (builder == null || builder.IsBuilding)
            {
                return;
            }
            if (Vector3.Distance(builder.transform.position, Center) > triggerDistance)
            {
                return;
            }

            // Altes Netz ersetzen: vorhandenes "Netz"-Kind zerstören, neues erzeugen.
            Transform old = transform.Find("Netz");
            if (old != null)
            {
                Destroy(old.gameObject);
            }
            OrbWeb web = CreateWebAt(anchors, transform, "Netz");
            if (web != null)
            {
                builder.StartBuild(web);
            }
        }

        private void OnGUI()
        {
            if (FindFirstObjectByType<SpiderWebPlacer>() != null) return;
            SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
            if (builder == null || builder.IsBuilding)
            {
                return;
            }
            if (Vector3.Distance(builder.transform.position, Center) > triggerDistance)
            {
                return;
            }
            GUI.Label(new Rect(Screen.width / 2f - 80f, Screen.height - 36f, 160f, 24f),
                "N / Y: Netz bauen");
        }

        /// <summary>Mittelpunkt der vier Anker.</summary>
        private Vector3 Center
        {
            get
            {
                if (anchors == null || anchors.Length == 0)
                {
                    return Vector3.zero;
                }
                Vector3 sum = Vector3.zero;
                foreach (Vector3 a in anchors)
                {
                    sum += a;
                }
                return sum / anchors.Length;
            }
        }

        /// <summary>
        /// Erzeugt ein neues OrbWeb an den vier Ankerpunkten (freie Methode,
        /// z. B. auch für automatisch gefundene Netzplätze per WebSiteFinder).
        /// </summary>
        public static OrbWeb CreateWebAt(Vector3[] anchors, Transform parent, string name)
        {
            if (anchors == null || anchors.Length != 4)
            {
                Debug.LogWarning("[WebBuildDemo] CreateWebAt: exakt 4 Anker nötig.");
                return null;
            }
            GameObject go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            OrbWeb web = go.AddComponent<OrbWeb>();
            web.Build(new OrbWebLayout(anchors, 16, 0.02f, 0.03f, 7));
            return web;
        }

        /// <summary>
        /// Erzeugt ein neues Netz-GameObject "Netz" (Kind dieses Objekts) mit
        /// demselben Layout wie im Test Builder_BuildsWebInOrder.
        /// </summary>
        public OrbWeb CreateWeb()
        {
            return CreateWebAt(anchors, transform, "Netz");
        }
    }
}
