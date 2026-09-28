using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Markiert ein Objekt/einen Kollider als bestimmte Oberfläche.
    /// Wird später von SpiderSurfaceDetector / SpiderGroundDetector gelesen (Milestone 2) –
    /// hier bewusst nur die Definition, ohne Erkennungslogik.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceTag : MonoBehaviour
    {
        [Tooltip("Welche Oberfläche dieser Kollider darstellt.")]
        public SurfaceType surfaceType = SurfaceType.Ground;
    }
}
