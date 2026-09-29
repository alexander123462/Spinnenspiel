using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// Lässt die Spinne überall ein Netz bauen: Taste N (Tastatur) oder Y
    /// (Gamepad) startet den Bau an dem nächsten gültigen Netzplatz um die
    /// Spinne. Es sind höchstens maxWebs Netze gleichzeitig vorhanden, das
    /// älteste wird entfernt. Wenn diese Komponente existiert, übernimmt sie
    /// die Netzbau-Auslösung von WebBuildDemo.
    /// </summary>
    public class SpiderWebPlacer : MonoBehaviour
    {
        /// <summary>Maximale Anzahl gleichzeitiger Netze.</summary>
        public int maxWebs = 3;

        /// <summary>Testhilfe: feste Blickrichtung statt der Main-Kamera.</summary>
        public Vector3? viewOverride;

        /// <summary>Ob aktuell an einem gültigen Netzplatz gebaut werden kann.</summary>
        public bool CanBuild { get; private set; }

        /// <summary>Anzahl aktuell registrierter Netze.</summary>
        public int WebCount => _webs.Count;

        private List<OrbWeb> _webs = new List<OrbWeb>();
        private Vector3[] _anchors;
        private float _nextCheck;
        private int _counter;

        private void Update()
        {
            // Netzplatz-Suche alle 0,25 s, nur wenn der Builder gerade frei ist.
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 0.25f;
                SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
                if (builder != null && !builder.IsBuilding && !builder.IsSittingInWeb)
                {
                    CanBuild = WebSiteFinder.TryFindNear(transform.position, View(), transform, out _anchors, out _);
                }
                else
                {
                    CanBuild = false;
                }
            }

            bool pressed = false;
            if (Keyboard.current != null)
            {
                pressed = Keyboard.current.nKey.wasPressedThisFrame;
            }
            if (!pressed && Gamepad.current != null)
            {
                pressed = Gamepad.current.buttonNorth.wasPressedThisFrame;
            }
            if (pressed && CanBuild)
            {
                TryPlaceWebNow();
            }
        }

        private void OnGUI()
        {
            if (CanBuild)
            {
                GUI.Label(new Rect(Screen.width / 2f - 80f, Screen.height - 36f, 160f, 24f),
                    "N / Y: Netz bauen");
            }
        }

        /// <summary>Blickrichtung: viewOverride, sonst Main-Kamera, sonst eigene.</summary>
        private Vector3 View()
        {
            if (viewOverride.HasValue)
            {
                return viewOverride.Value;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                return cam.transform.forward;
            }
            return transform.forward;
        }

        /// <summary>
        /// Baut sofort ein Netz am nächsten gültigen Platz und startet den
        /// Bau. true bei Erfolg.
        /// </summary>
        public bool TryPlaceWebNow()
        {
            if (!WebSiteFinder.TryFindNear(transform.position, View(), transform, out Vector3[] anchors, out _))
            {
                return false;
            }
            OrbWeb web = WebBuildDemo.CreateWebAt(anchors, null, "Netz_" + (++_counter));
            if (web == null)
            {
                return false;
            }
            RegisterWeb(web);
            SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
            if (builder != null)
            {
                builder.StartBuild(web);
            }
            CanBuild = false;
            return true;
        }

        /// <summary>
        /// Registriert ein Netz; entfernt bei Überschreitung von maxWebs das
        /// älteste Netz (das gerade aktive des Builders bleibt bestehen).
        /// </summary>
        public void RegisterWeb(OrbWeb web)
        {
            if (web == null)
            {
                return;
            }
            _webs.Add(web);
            while (_webs.Count > maxWebs)
            {
                SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
                OrbWeb active = (builder != null) ? builder.Web : null;
                OrbWeb victim = null;
                for (int i = 0; i < _webs.Count; i++)
                {
                    if (_webs[i] != null && _webs[i] != active)
                    {
                        victim = _webs[i];
                        break;
                    }
                }
                if (victim == null)
                {
                    return; // nur aktive Netze übrig, nichts zu entfernen
                }
                Destroy(victim.gameObject);
                _webs.Remove(victim);
            }
        }
    }
}
