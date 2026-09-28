using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Ordnet den Terrain-Layern (Splatmap) Oberflächentypen zu.
    /// Die Splatmap wird einmal gecacht; Abfragen sind danach reine Array-Zugriffe.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    [DisallowMultipleComponent]
    public class TerrainSurfaceMap : MonoBehaviour
    {
        [Tooltip("Oberflächentyp je Terrain-Layer-Index.")]
        [SerializeField] private SurfaceType[] layerSurfaces = { SurfaceType.Ground };

        private Terrain _terrain;
        private float[,,] _alphamaps;
        private int _width;
        private int _height;
        private int _layers;

        public void Configure(SurfaceType[] surfaces)
        {
            layerSurfaces = surfaces;
            _alphamaps = null;
        }

        /// <summary>Verwirft den Cache, z. B. nachdem die Splatmap zur Laufzeit geändert wurde.</summary>
        public void Invalidate()
        {
            _alphamaps = null;
        }

        public SurfaceType Sample(Vector3 worldPosition)
        {
            if (!EnsureCache() || layerSurfaces == null || layerSurfaces.Length == 0)
            {
                return SurfaceType.Ground;
            }

            TerrainData data = _terrain.terrainData;
            Vector3 local = worldPosition - _terrain.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * _width), 0, _width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * _height), 0, _height - 1);

            int best = 0;
            float bestWeight = -1f;
            for (int i = 0; i < _layers; i++)
            {
                float weight = _alphamaps[z, x, i];
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = i;
                }
            }

            return best < layerSurfaces.Length ? layerSurfaces[best] : SurfaceType.Ground;
        }

        private bool EnsureCache()
        {
            if (_terrain == null)
            {
                _terrain = GetComponent<Terrain>();
            }

            if (_terrain == null || _terrain.terrainData == null)
            {
                return false;
            }

            if (_alphamaps != null)
            {
                return _layers > 0;
            }

            TerrainData data = _terrain.terrainData;
            _width = data.alphamapWidth;
            _height = data.alphamapHeight;
            _layers = data.alphamapLayers;
            _alphamaps = data.GetAlphamaps(0, 0, _width, _height);
            return _layers > 0;
        }
    }
}
