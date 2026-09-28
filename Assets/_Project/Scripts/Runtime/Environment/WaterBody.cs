using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Markiert eine Wasserfläche (Bach, Teich) und macht sie global abfragbar.
    /// Die Oberfläche selbst ist ein Kollider mit SurfaceType.Water.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterBody : MonoBehaviour
    {
        private static readonly List<WaterBody> Active = new List<WaterBody>();

        [SerializeField] private float speedMultiplier = 0.6f;

        private Collider _collider;

        public static IReadOnlyList<WaterBody> All => Active;

        /// <summary>Geschwindigkeitsfaktor für Lebewesen auf dieser Wasserfläche.</summary>
        public float SpeedMultiplier => speedMultiplier;

        private void OnEnable()
        {
            _collider = GetComponent<Collider>();
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>Liegt der Punkt (horizontal) über dieser Wasserfläche? Liefert dann die Oberflächenhöhe.</summary>
        public bool TryGetSurfaceHeight(Vector3 position, out float height)
        {
            height = 0f;
            if (_collider == null)
            {
                return false;
            }

            Vector3 origin = new Vector3(position.x, _collider.bounds.max.y + 1f, position.z);
            Ray ray = new Ray(origin, Vector3.down);
            RaycastHit hit;
            if (_collider.Raycast(ray, out hit, _collider.bounds.size.y + 2f))
            {
                height = hit.point.y;
                return true;
            }

            return false;
        }
    }
}
