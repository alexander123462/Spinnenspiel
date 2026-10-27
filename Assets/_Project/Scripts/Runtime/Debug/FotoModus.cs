using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// FOTO1: Foto-Modus im Player. Nur aktiv, wenn die Kommandozeile -fotopfad <Datei> traegt.
    /// Setzt den Welt-Seed (-fotoseed, Standard 4242) VOR dem Szenenladen, schwenkt die Kamera
    /// um -fotopitch Grad nach oben, wartet -fotowarte Sekunden (Standard 25) plus Welt-Bereitschaft
    /// und speichert dann ein Screenshot nach -fotopfad; danach beendet Application.Quit das Spiel.
    /// </summary>
    public static class FotoModus
    {
        public const string FlagPfad = "-fotopfad";
        public const string FlagSeed = "-fotoseed";
        public const string FlagPitch = "-fotopitch";
        public const string FlagWarte = "-fotowarte";
        // FOTO2 (TEXTUR2): Kamera vor dem Schweissen neben das naechste Objekt mit diesem
        // Materialnamen (Teilstring) stellen. Nur aktiv zusammen mit -fotopfad; damit
        // Player-Fotos von Asset-Typen weit weg vom Spawnpunkt ueberhaupt gelingen.
        public const string FlagNear = "-fotonear";
        // FOTO2: Tele-FOV fuer Makro-Fotos (z. B. 25 statt 60 Grad). Nur zusammen mit -fotonear sinnvoll.
        public const string FlagZoom = "-fotozoom";
        // MODELL12: Riesenblaetter trotz WALD6 ins Bild holen (Player-Foto fuer Asset-Nachweis).
        public const string FlagRiesen = "-fotoriesen";

        public const int DefaultSeed = 4242;
        public const float DefaultPitch = 0f;
        public const float DefaultWarteS = 25f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            string pfad = GetArgValue(FlagPfad);
            if (string.IsNullOrEmpty(pfad))
            {
                return;
            }

            TryInstall(pfad,
                GetArgInt(FlagSeed, DefaultSeed),
                GetArgFloat(FlagPitch, DefaultPitch),
                GetArgFloat(FlagWarte, DefaultWarteS),
                GetArgValue(FlagNear),
                GetArgFloat(FlagZoom, 0f),
                HasArgFlag(FlagRiesen));
        }

        /// <summary>
        /// FOTO1: Aktiviert den Foto-Modus: Seed vor dem Szenenladen setzen, Runner anlegen.
        /// Gibt true zurueck, wenn der Modus aktiv wurde (leerer Pfad -> false, Spiel laeuft normal weiter).
        /// </summary>
        public static bool TryInstall(string pfad, int seed, float pitch, float warteS, string nearMat = null, float zoomFov = 0f, bool riesenblaetter = false)
        {
            if (string.IsNullOrEmpty(pfad))
            {
                return false;
            }

            JungleWorldGenerator.NextSeed = seed;
            if (riesenblaetter)
            {
                JungleWorldGenerator.ForceGiantLeaves = true;   // MODELL12: Einmal-Override
            }

            var go = new GameObject("FotoModus");
            go.AddComponent<FotoModusRunner>().Configure(pfad, pitch, warteS, nearMat, zoomFov);
            Debug.Log("[FotoModus] aktiv: pfad=" + pfad + " seed=" + seed
                + " pitch=" + pitch + " warte=" + warteS + " near=" + (nearMat ?? "-")
                + " zoom=" + zoomFov);
            return true;
        }

        /// <summary>Wert hinter einem Flag aus der Kommandozeile ("--flag" wird als "-flag" gelesen).</summary>
        public static string GetArgValue(string flag)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string alt = flag.StartsWith("--") ? flag.Substring(1) : "-" + flag;
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == flag || args[i] == alt) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        public static int GetArgInt(string flag, int fallback)
        {
            string v = GetArgValue(flag);
            return int.TryParse(v, out int n) ? n : fallback;
        }

        public static float GetArgFloat(string flag, float fallback)
        {
            string v = GetArgValue(flag);
            return float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : fallback;
        }

        /// <summary>true, wenn ein Schalter-Flag (ohne Wert) in der Kommandozeile steht.</summary>
        public static bool HasArgFlag(string flag)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string alt = flag.StartsWith("--") ? flag.Substring(1) : "-" + flag;
            foreach (string a in args) if (a == flag || a == alt) return true;
            return false;
        }
    }

    /// <summary>
    /// FOTO1: Ausfuehrender Teil des Foto-Modus. Zaehlt die Wartezeit, wartet zusaetzlich auf
    /// IsReady der Welt, setzt die Kamera-Pitch, laesst die Kamera-Rueckfuehrung einschiwingen
    /// (SettleS) und speichert dann den Screenshot. Dadurch sind zwei Fotos mit gleichem Seed
    /// in Bildmittelwert und KameraPose reproduzierbar.
    /// </summary>
    public class FotoModusRunner : MonoBehaviour
    {
        /// <summary>Sekunden, die die Kamera nach dem Pitch-Setzen einschwingen darf.</summary>
        public const float SettleS = 3f;

        private enum Phase { Warten, Einschwingen, Positionieren, Schweissen }

        private string _pfad;
        private float _pitch;
        private float _warteS;
        private string _nearMat;
        private float _zoomFov;
        private float _elapsed;
        private Phase _phase = Phase.Warten;

        public void Configure(string pfad, float pitch, float warteS, string nearMat = null, float zoomFov = 0f)
        {
            _pfad = pfad;
            _pitch = pitch;
            _warteS = Mathf.Max(1f, warteS);
            _nearMat = nearMat;
            _zoomFov = zoomFov;
        }

        private void Update()
        {
            if (_phase == Phase.Schweissen)
            {
                // Ein Frame nach dem Screenshot beenden: die PNG-Datei wird am Ende des
                // Capture-Frames geschrieben; zu frueshes Quit wuerde sie abschneiden.
                Application.Quit();
                return;
            }

            _elapsed += Time.deltaTime;

            if (_phase == Phase.Warten)
            {
                if (_elapsed < _warteS)
                {
                    return;
                }

                // Welt muss fertig generiert sein, sonst Foto von einer halben Welt.
                var gen = FindFirstObjectByType<JungleWorldGenerator>();
                if (gen != null && !gen.IsReady)
                {
                    return;
                }

                var cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    var orb = cam.GetComponent<SpiderCamera>();
                    if (orb != null)
                    {
                        orb.SetPitch(_pitch);
                    }
                    else
                    {
                        Vector3 e = cam.transform.eulerAngles;
                        cam.transform.rotation = Quaternion.Euler(_pitch, e.y, e.z);
                    }
                }

                // Kamera-Rueckfuehrung (SmoothDamp/Slerp) einschwingen lassen: erst dann ist
                // die Pose unabhaengig vom Frame-Timing und Fotos mit gleichem Seed vergelijchbar.
                _elapsed = 0f;
                _phase = Phase.Einschwingen;
                return;
            }

            if (_phase == Phase.Einschwingen && _elapsed >= SettleS)
            {
                if (string.IsNullOrEmpty(_nearMat))
                {
                    Capture();
                    return;
                }
                var cam = UnityEngine.Camera.main;
                if (cam != null && TryGetNearPose(_nearMat, cam.transform.position, _pitch, out Vector3 pos, out Quaternion rot))
                {
                    var orb = cam.GetComponent<SpiderCamera>();
                    if (orb != null) orb.enabled = false;
                    // FOTO2: alle Renderer ohne Zielmaterial ausblenden — sonst steht
                    // oft eine andere Pflanze naher an der Kamera im Bild (TEXTUR2-Beweis).
                    int hidden = 0;
                    foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        bool keep = false;
                        foreach (var m in mr.sharedMaterials)
                            if (m != null && m.name.Contains(_nearMat)) { keep = true; break; }
                        if (!keep) { mr.enabled = false; hidden++; }
                    }
                    Debug.Log("[FotoModus] Nicht-Ziel-Renderer ausgeblendet: " + hidden);
                    cam.nearClipPlane = 0.05f;
                    if (_zoomFov >= 5f) cam.fieldOfView = _zoomFov;
                    cam.transform.SetPositionAndRotation(pos, rot);
                    Debug.Log("[FotoModus] Positioniert vor '" + _nearMat + "' cam=" + pos.ToString("F2"));
                    _elapsed = 0f;
                    _phase = Phase.Positionieren; // HDRP-Belichtung/DOF einschiwingen lassen
                    return;
                }
                Debug.LogWarning("[FotoModus] kein Objekt mit Material '" + _nearMat + "' gefunden.");
                Capture();
                return;
            }

            if (_phase == Phase.Positionieren && _elapsed >= 1.5f)
            {
                Capture();
            }
        }

        private void Capture()
        {
            _phase = Phase.Schweissen;
            ScreenCapture.CaptureScreenshot(_pfad);
            var c = UnityEngine.Camera.main;
            if (c != null)
            {
                Debug.Log("[FotoModus] Pose cam=" + c.transform.position.ToString("F3") + " euler=" + c.transform.eulerAngles.ToString("F1"));
            }
            Debug.Log("[FotoModus] Foto gespeichert: " + _pfad);
        }

        /// <summary>FOTO2: Kamera-Pose neben dem naechsten Renderer mit einem Material, dessen
        /// Name matName enthaelt. Abstand 1,2 m oder 2,2x Halbmesser (DOF scharf), Blick von der
        /// bisherigen Kameraseite zum Objektzentrum, dann -pitch Grad um die lokale X-Achse.</summary>
        public static bool TryGetNearPose(string matName, Vector3 from, float pitch, out Vector3 pos, out Quaternion rot)
        {
            MeshRenderer best = null;
            float bestD = float.MaxValue;
            foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!mr.enabled || mr.bounds.size.magnitude > 150f) continue;
                bool hit = false;
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name.Contains(matName)) { hit = true; break; }
                if (!hit) continue;
                float d = (mr.bounds.center - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = mr; }
            }
            pos = Vector3.zero; rot = Quaternion.identity;
            if (best == null) return false;

            Bounds b = best.bounds;
            Vector3 dir = from - b.center; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            float dist = Mathf.Max(0.9f, b.extents.magnitude * 1.8f);
            pos = b.center + dir * dist + Vector3.up * (b.size.y * 0.5f - b.extents.y);
            Debug.Log("[FotoModus] Ziel '" + matName + "' center=" + b.center.ToString("F2")
                + " size=" + b.size.ToString("F2") + " dist=" + dist.ToString("F2"));
            Vector3 aim = (b.center + Vector3.up * b.size.y * 0.1f) - pos;
            rot = Quaternion.LookRotation(aim.normalized);
            rot = rot * Quaternion.Euler(pitch, 0f, 0f);
            return true;
        }
    }
}
