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

        /// <summary>Grund, warum aktuell kein Netz gebaut werden kann (leer = keine).</summary>
        public string BlockReason = "";

        /// <summary>Ob die weiße Netz-Vorschau gerade sichtbar ist.</summary>
        public bool PreviewVisible { get; private set; }

        /// <summary>Z6: True, wenn die Vorschau rot anzeigt (kein Platz mehr).</summary>
        public bool PreviewBlocked { get; private set; }

        /// <summary>Anzahl aktuell registrierter Netze.</summary>
        public int WebCount => _webs.Count;

        private List<OrbWeb> _webs = new List<OrbWeb>();
        private Vector3[] _anchors;
        private float _anchorsTime;
        private float _nextCheck;
        private int _counter;
        private bool _nearThread;
        private Vector3[] _anchorPoints;
        private bool _buildFromThread;

        // Vorschau: Objekt in Weltkoordinaten, kein Collider.
        private GameObject _previewGo;
        private LineRenderer _previewFrame;
        private readonly List<LineRenderer> _previewAnchorLines = new List<LineRenderer>();
        private Material _previewMat;
        private Vector3[] _lastPreviewFrame;
        private Vector3[] _lastPreviewAnchors;

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
                        UpdatePreview(_anchors, _anchorPoints);
                    }
                    else
                    {
                        ShowBlockedPreview();
                    }
                }
                else
                {
                    CanBuild = false;
                    HidePreview();
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

            // Z6: Esc waahrend ein Netzbau aborts it (Pause stays on GameMenu).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SpiderWebBuilder escBuilder = FindFirstObjectByType<SpiderWebBuilder>();
                if (escBuilder != null && escBuilder.IsBuilding)
                {
                    escBuilder.CancelBuild();
                    CanBuild = false;
                    HidePreview();
                }
            }

            if (pressed && CanBuild)
            {
                TryPlaceWebNow();
            }
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(BlockReason))
            {
                GUI.Label(new Rect(Screen.width / 2f - 160f, Screen.height - 60f, 320f, 24f),
                    BlockReason);
            }
            if (CanBuild)
            {
                string text = _buildFromThread
                    ? "N / Y: Netz unter diesem Faden bauen"
                    : "N / Y: Netz hier bauen (weiße Vorschau)";
                GUI.Label(new Rect(Screen.width / 2f - 160f, Screen.height - 36f, 320f, 24f),
                    text);
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
            // Ankerfäden: Rahmen-Ecken zu den echten Haltepunkten.
            web.SetAnchorLines(anchors, _anchorPoints);
            RegisterWeb(web);
            builder.StartBuild(web);
            CanBuild = false;
            HidePreview();
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
        /// höchstens 0.35 m entfernt). Unter einem Faden gibt es KEINEN
        /// Rückfall auf die Collider-Suche (BlockReason wird gesetzt).
        /// Sonst die Collider-Suche (TryFindNear) mit begrenztem Rahmen
        /// (FitFrame) – die echten Treffer werden zu Ankerfäden.
        /// </summary>
        private bool TryFindAnchors(out Vector3[] anchors)
        {
            anchors = null;
            _anchorPoints = null;
            _buildFromThread = false;
            BlockReason = "";
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
            if (candidate != null)
            {
                _buildFromThread = true;
                if (WebSiteFinder.TryFindFromThread(candidate, transform.position, transform, out anchors, out _anchorPoints))
                {
                    return true;
                }
                BlockReason = "Kein Halt unter dem Faden";
                return false;
            }
            Vector3[] near;
            if (!WebSiteFinder.TryFindNear(transform.position, View(), transform, out near, out _))
            {
                return false;
            }
            anchors = WebSiteFinder.FitFrame(near, WebSiteFinder.MaxFrameHalf);
            _anchorPoints = near;
            return true;
        }

        /// <summary>
        /// Weiße Vorschau des geplanten Netzes (Rahmen + Ankerfäden) in
        /// Weltkoordinaten, nur wenn gebaut werden kann. Aktualisiert sich
        /// bei jeder periodischen Suche, die neue Anker liefert.
        /// </summary>
        private void UpdatePreview(Vector3[] frame, Vector3[] anchorPoints)
        {
            if (frame == null || frame.Length != 4)
            {
                HidePreview();
                return;
            }
            bool changed = !ReferenceEquals(_lastPreviewFrame, frame) || !ReferenceEquals(_lastPreviewAnchors, anchorPoints);
            if (changed)
            {
                EnsurePreviewObject();
                // Rahmen: TL→TR→BR→BL→TL (5 Punkte).
                if (_previewFrame == null)
                {
                    _previewFrame = AddPreviewLine("Vorschau_Rahmen");
                }
                var pts = new List<Vector3> { frame[0], frame[1], frame[2], frame[3], frame[0] };
                _previewFrame.positionCount = pts.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    _previewFrame.SetPosition(i, pts[i]);
                }
                // Ankerfäden: je einer pro Ecke, nur wenn Abstand > 0.02 m.
                for (int i = 0; i < 4; i++)
                {
                    LineRenderer line = GetPreviewAnchorLine(i);
                    bool show = anchorPoints != null && Vector3.Distance(frame[i], anchorPoints[i]) > 0.02f;
                    if (show)
                    {
                        line.enabled = true;
                        line.positionCount = 2;
                        line.SetPosition(0, frame[i]);
                        line.SetPosition(1, anchorPoints[i]);
                    }
                    else
                    {
                        line.enabled = false;
                    }
                }
                _lastPreviewFrame = frame;
                _lastPreviewAnchors = anchorPoints;
            }
            _previewGo.SetActive(true);
            PreviewVisible = true;
        }

        private LineRenderer GetPreviewAnchorLine(int i)
        {
            while (_previewAnchorLines.Count <= i)
            {
                _previewAnchorLines.Add(AddPreviewLine("Vorschau_Anker_" + _previewAnchorLines.Count));
            }
            return _previewAnchorLines[i];
        }

        private void EnsurePreviewObject()
        {
            if (_previewGo != null)
            {
                return;
            }
            _previewGo = new GameObject("NetzVorschau");
            if (SilkThread.SharedMaterial != null)
            {
                _previewMat = new Material(SilkThread.SharedMaterial);
                _previewMat.color = new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private LineRenderer AddPreviewLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_previewGo.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 0;
            line.startWidth = 0.006f;
            line.endWidth = 0.006f;
            line.useWorldSpace = true;
            line.receiveShadows = false;
            line.material = _previewMat;
            return line;
        }

        private void HidePreview()
        {
            if (_previewGo != null)
            {
                _previewGo.SetActive(false);
            }
            PreviewVisible = false;
            PreviewBlocked = false;
        }

        /// <summary>Z6: Vorschau exists and the spot went bad -> show it red instead of hiding.</summary>
        private void ShowBlockedPreview()
        {
            if (_previewGo == null || _lastPreviewFrame == null)
            {
                HidePreview();
                return;
            }
            SetPreviewColor(new Color(1f, 0.15f, 0.15f, 0.5f));
            _previewGo.SetActive(true);
            PreviewVisible = true;
            PreviewBlocked = true;
        }

        private void SetPreviewColor(Color c)
        {
            if (_previewMat != null)
            {
                _previewMat.color = c;
            }
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
