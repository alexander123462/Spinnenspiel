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
                GetArgFloat(FlagWarte, DefaultWarteS));
        }

        /// <summary>
        /// FOTO1: Aktiviert den Foto-Modus: Seed vor dem Szenenladen setzen, Runner anlegen.
        /// Gibt true zurueck, wenn der Modus aktiv wurde (leerer Pfad -> false, Spiel laeuft normal weiter).
        /// </summary>
        public static bool TryInstall(string pfad, int seed, float pitch, float warteS)
        {
            if (string.IsNullOrEmpty(pfad))
            {
                return false;
            }

            JungleWorldGenerator.NextSeed = seed;

            var go = new GameObject("FotoModus");
            go.AddComponent<FotoModusRunner>().Configure(pfad, pitch, warteS);
            Debug.Log("[FotoModus] aktiv: pfad=" + pfad + " seed=" + seed
                + " pitch=" + pitch + " warte=" + warteS);
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

        private enum Phase { Warten, Einschwingen, Schweissen }

        private string _pfad;
        private float _pitch;
        private float _warteS;
        private float _elapsed;
        private Phase _phase = Phase.Warten;

        public void Configure(string pfad, float pitch, float warteS)
        {
            _pfad = pfad;
            _pitch = pitch;
            _warteS = Mathf.Max(1f, warteS);
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
                _phase = Phase.Schweissen;
                ScreenCapture.CaptureScreenshot(_pfad);
                var c = UnityEngine.Camera.main;
                if (c != null)
                {
                    Debug.Log("[FotoModus] Pose cam=" + c.transform.position.ToString("F3") + " euler=" + c.transform.eulerAngles.ToString("F1"));
                }
                Debug.Log("[FotoModus] Foto gespeichert: " + _pfad);
            }
        }
    }
}
