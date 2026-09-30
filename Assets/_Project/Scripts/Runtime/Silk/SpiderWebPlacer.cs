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
        private float _anchorsTime;
        private float _nextCheck;
        private int _counter;
        private bool _nearThread;

        private void Update()
        {
            // Netzplatz-Suche alle 0,25 s, nur wenn der Builder gerade frei ist.
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 0.25f;
                SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
                if (builder != null && !builder.IsBuilding && !builder.IsSittingInWeb)
                {
                    CanBuild = TryFindAnchors(out _anchors);
                    if (CanBuild)
                    {
                        _anchorsTime = Time.time;
                    }
                }
                else
                {
                    CanBuild = false;
                }

                // Hinweis "auf den Faden steigen" alle 0,25 s aktualisieren.
                SpiderMovement m = Movement;
                if (m != null && m.CurrentThread == null)
                {
                    _nearThread = false;
                    foreach (SilkThread th in SilkThread.All)
                    {
                        if (th == null)
                        {
                            continue;
                        }
                        Vector3 cp = th.PointAt(th.ClosestT(transform.position));
                        if (Vector3.Distance(cp, transform.position) <= 0.12f)
                        {
                            _nearThread = true;
                            break;
                        }
                    }
                }
                else
                {
                    _nearThread = false;
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
            else if (_nearThread)
            {
                GUI.Label(new Rect(Screen.width / 2f - 160f, Screen.height - 36f, 320f, 24f),
                    "In Fadenrichtung laufen: auf den Faden steigen");
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
        /// Baut ein Netz an den übergebenen 4 Ankern und startet den Bau.
        /// true bei Erfolg.
        /// </summary>
        public bool PlaceWeb(Vector3[] anchors)
        {
            if (anchors == null || anchors.Length != 4)
            {
                return false;
            }
            SpiderWebBuilder builder = FindFirstObjectByType<SpiderWebBuilder>();
            if (builder == null || builder.IsBuilding)
            {
                return false;
            }
            OrbWeb web = WebBuildDemo.CreateWebAt(anchors, null, "Netz_" + (++_counter));
            if (web == null)
            {
                return false;
            }
            RegisterWeb(web);
            builder.StartBuild(web);
            CanBuild = false;
            return true;
        }

        /// <summary>
        /// Spinne am Faden (wenn an einem hängend), sonst null.
        /// </summary>
        private SpiderMovement Movement
        {
            get
            {
                if (_movement == null)
                {
                    _movement = GetComponent<SpiderMovement>();
                }
                return _movement;
            }
        }

        private SpiderMovement _movement;

        /// <summary>
        /// Netzplatz suchen: ZUERST unter einem gespannten Faden
        /// (Brückenfaden, wie bei echten Radnetzspinnen) – der Faden, auf dem
        /// die Spinne hängt, sonst der nächste Faden (nächster Punkt
        /// höchstens 0.35 m entfernt). Falls das nicht geht, die bisherige
        /// Collider-Suche (TryFindNear).
        /// </summary>
        private bool TryFindAnchors(out Vector3[] anchors)
        {
            anchors = null;
            SilkThread candidate = null;
            SpiderMovement movement = Movement;
            if (movement != null)
            {
                if (movement.CurrentThread != null)
                {
                    candidate = movement.CurrentThread;
                }
                else
                {
                    foreach (SilkThread th in SilkThread.All)
                    {
                        if (th == null)
                        {
                            continue;
                        }
                        Vector3 cp = th.PointAt(th.ClosestT(transform.position));
                        if (Vector3.Distance(cp, transform.position) <= 0.35f)
                        {
                            candidate = th;
                            break;
                        }
                    }
                }
            }
            if (candidate != null && WebSiteFinder.TryFindFromThread(candidate, transform.position, transform, out anchors))
            {
                return true;
            }
            return WebSiteFinder.TryFindNear(transform.position, View(), transform, out anchors, out _);
        }

        /// <summary>
        /// Baut sofort ein Netz am zuletzt gefundenen (frischen) Platz oder
        /// macht eine frische Suche und baut dort. true bei Erfolg.
        /// </summary>
        public bool TryPlaceWebNow()
        {
            // Frische gemerkte Anker (weniger als 1 s alt) bevorzugen: bauen
            // dort, wo die Suche sie gefunden hat, nicht an der aktuellen
            // (ggf. veränderten) Position der Spinne.
            if (CanBuild && _anchors != null && Time.time - _anchorsTime < 1f)
            {
                return PlaceWeb(_anchors);
            }

            if (!TryFindAnchors(out Vector3[] anchors))
            {
                return false;
            }
            _anchors = anchors;
            _anchorsTime = Time.time;
            return PlaceWeb(anchors);
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
