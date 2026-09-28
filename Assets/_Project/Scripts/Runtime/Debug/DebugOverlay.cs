using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// Diagnose-Anzeige (F3): FPS, Geschwindigkeit, Oberfläche, schwingende Beine, gerenderte Vegetation.
    /// Hilft beim Testen von Bewegung und Performance.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible;

        private float _smoothedDelta = 1f / 60f;
        private SpiderMovement _movement;
        private SpiderGaitController _gait;
        private SpiderSurfaceDetector _surface;
        private VegetationSystem _vegetation;
        private GUIStyle _style;

        private void Start()
        {
            _movement = FindFirstObjectByType<SpiderMovement>();
            if (_movement != null)
            {
                _gait = _movement.GetComponent<SpiderGaitController>();
                _surface = _movement.GetComponent<SpiderSurfaceDetector>();
            }

            _vegetation = FindFirstObjectByType<VegetationSystem>();
        }

        private void Update()
        {
            _smoothedDelta = Mathf.Lerp(_smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            }

            string text = string.Format("FPS: {0:0}  ({1:0.0} ms)", 1f / Mathf.Max(0.0001f, _smoothedDelta), _smoothedDelta * 1000f);
            if (_movement != null)
            {
                text += string.Format("\nTempo: {0:0.00} m/s{1}", _movement.Speed, _movement.IsSprinting ? "  (Sprint)" : "");
            }

            if (_surface != null)
            {
                text += "\nOberfläche: " + _surface.CurrentSurface + string.Format("  (Faktor {0:0.00})", _surface.SpeedMultiplier);
            }

            if (_gait != null)
            {
                text += string.Format("\nSchwingende Beine: {0}  Schrittdauer: {1:0.000} s", _gait.SteppingCount, _gait.CurrentStepDuration);
            }

            if (_vegetation != null)
            {
                text += string.Format("\nVegetation: {0} Instanzen, {1} Chunks", _vegetation.RenderedInstancesLastFrame, _vegetation.LoadedChunkCount);
            }

            text += "\n[F3] ausblenden";
            GUI.Box(new Rect(10, 10, 360, 130), text, _style);
        }
    }
}
