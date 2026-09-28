using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Dünne Koordinationskomponente auf dem Wurzelobjekt der Spinne:
    /// hält Referenzen auf SpiderInput, SpiderMovement und SpiderCamera bereit.
    /// Enthält bewusst keine Spiellogik – die liegt in den Komponenten selbst.
    /// </summary>
    [RequireComponent(typeof(SpiderMovement))]
    [DisallowMultipleComponent]
    public class SpiderController : MonoBehaviour
    {
        [SerializeField] private SpiderInput input;
        [SerializeField] private SpiderCamera cameraRig;

        public SpiderInput Input { get; private set; }

        public SpiderMovement Movement { get; private set; }

        public SpiderCamera CameraRig { get; private set; }

        private void Awake()
        {
            Movement = GetComponent<SpiderMovement>();
            if (Movement == null)
            {
                Debug.LogError("SpiderController: SpiderMovement fehlt – Spinne ist nicht steuerbar.", this);
                return;
            }

            if (input == null)
            {
                input = GetComponent<SpiderInput>();
                if (input == null)
                {
                    var found = FindFirstObjectByType<SpiderInput>();
                    if (found != null)
                    {
                        input = found;
                    }
                }
            }

            Input = input;
            if (Input == null)
            {
                Debug.LogError("SpiderController: SpiderInput fehlt – keine Tastatur-/Gamepad-Eingabe möglich.", this);
            }

            if (cameraRig == null)
            {
                var foundCamera = FindFirstObjectByType<SpiderCamera>();
                if (foundCamera != null)
                {
                    cameraRig = foundCamera;
                }
            }

            CameraRig = cameraRig;
        }
    }
}
