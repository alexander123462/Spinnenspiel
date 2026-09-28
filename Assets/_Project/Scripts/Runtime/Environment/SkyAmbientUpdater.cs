using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Aktualisiert einmalig nach dem Start das dynamische Umgebungslicht,
    /// damit das aus der Skybox (HDRI) abgeleitete Ambient zur Laufzeit stimmt.
    /// </summary>
    public sealed class SkyAmbientUpdater : MonoBehaviour
    {
        private void Start()
        {
            DynamicGI.UpdateEnvironment();
        }
    }
}
