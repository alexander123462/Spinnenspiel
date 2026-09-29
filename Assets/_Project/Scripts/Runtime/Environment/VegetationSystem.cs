using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Spinnenspiel
{
    public enum VegetationBiome
    {
        Any,
        Meadow,
        Forest,
        WaterBank
    }

    /// <summary>Eine Vegetationsart (Gras, Blumen, Farn, Laub, Büsche ...).</summary>
    [Serializable]
    public class VegetationLayer
    {
        public string name = "Layer";
        public Mesh mesh;
        public Material material;
        [Tooltip("Instanzen pro Quadratmeter bei voller Biom-Gewichtung.")]
        public float density = 1f;
        public VegetationBiome biome = VegetationBiome.Any;
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
        public float cullDistance = 25f;
        public bool castShadows;
        public bool alignToNormal = true;
        public float maxSteepness = 35f;
        public float sinkDepth = 0.01f;
        [Tooltip("Instanzen näher als dieser Abstand zu Wasser werden verworfen (m).")]
        public float waterClearance = 0.2f;
        public int salt = 1;
    }

    /// <summary>
    /// Prozedurale, gestreamte Vegetation mit GPU-Instancing.
    /// Die Welt ist in Chunks aufgeteilt; Chunks in Reichweite der Kamera werden deterministisch erzeugt
    /// (gleiche Welt bei jedem Start), entfernte wieder freigegeben. Gerendert wird per
    /// Graphics.RenderMeshInstanced mit Frustum- und Distanz-Culling – keine GameObjects pro Pflanze.
    /// </summary>
    [DisallowMultipleComponent]
    public class VegetationSystem : MonoBehaviour
    {
        private const int MaxInstancesPerCall = 1023;

        [SerializeField] private Terrain terrain;
        [SerializeField] private float chunkSize = 8f;
        [SerializeField] private int maxChunksGeneratedPerFrame = 4;
        [SerializeField] private List<VegetationLayer> layers = new List<VegetationLayer>();

        private sealed class Chunk
        {
            public Bounds bounds;
            public Matrix4x4[][] matrices;
        }

        private readonly Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();
        private readonly List<Vector2Int> _toRemove = new List<Vector2Int>();
        private readonly Plane[] _frustum = new Plane[6];
        private Matrix4x4[] _batch = new Matrix4x4[MaxInstancesPerCall];
        private float _maxCullDistance;

        public int LoadedChunkCount => _chunks.Count;
        public int RenderedInstancesLastFrame { get; private set; }

        public void Configure(Terrain targetTerrain, List<VegetationLayer> vegetationLayers)
        {
            terrain = targetTerrain;
            layers = vegetationLayers;
        }

        private void Awake()
        {
            _maxCullDistance = 0f;
            foreach (VegetationLayer layer in layers)
            {
                _maxCullDistance = Mathf.Max(_maxCullDistance, layer.cullDistance);
            }
        }

        private void Start()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Umgebung der Startposition sofort erzeugen, damit nichts sichtbar "aufploppt".
                UpdateChunks(cam.transform.position, int.MaxValue);
            }
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || terrain == null)
            {
                return;
            }

            Vector3 cameraPosition = cam.transform.position;
            UpdateChunks(cameraPosition, maxChunksGeneratedPerFrame);
            Render(cam, cameraPosition);
        }

        /// <summary>
        /// Rendert die Vegetation sofort für eine bestimmte Kamera (z. B. Editor-Screenshots außerhalb des Play-Modus).
        /// </summary>
        public void RenderForCamera(Camera cam)
        {
            if (cam == null || terrain == null)
            {
                return;
            }

            if (_maxCullDistance <= 0f)
            {
                Awake();
            }

            UpdateChunks(cam.transform.position, int.MaxValue);
            Render(cam, cam.transform.position);
        }

        private void UpdateChunks(Vector3 center, int generationBudget)
        {
            int radius = Mathf.CeilToInt(_maxCullDistance / chunkSize) + 1;
            Vector2Int centerChunk = new Vector2Int(Mathf.FloorToInt(center.x / chunkSize), Mathf.FloorToInt(center.z / chunkSize));

            int generated = 0;
            for (int dz = -radius; dz <= radius && generated < generationBudget; dz++)
            {
                for (int dx = -radius; dx <= radius && generated < generationBudget; dx++)
                {
                    Vector2Int key = new Vector2Int(centerChunk.x + dx, centerChunk.y + dz);
                    if (_chunks.ContainsKey(key) || !ChunkInsideTerrain(key))
                    {
                        continue;
                    }

                    _chunks[key] = GenerateChunk(key);
                    generated++;
                }
            }

            // Streaming: weit entfernte Chunks freigeben.
            _toRemove.Clear();
            foreach (KeyValuePair<Vector2Int, Chunk> pair in _chunks)
            {
                if (Mathf.Abs(pair.Key.x - centerChunk.x) > radius + 1 || Mathf.Abs(pair.Key.y - centerChunk.y) > radius + 1)
                {
                    _toRemove.Add(pair.Key);
                }
            }

            foreach (Vector2Int key in _toRemove)
            {
                _chunks.Remove(key);
            }
        }

        private bool ChunkInsideTerrain(Vector2Int key)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float x = key.x * chunkSize, z = key.y * chunkSize;
            return x + chunkSize > origin.x && z + chunkSize > origin.z && x < origin.x + size.x && z < origin.z + size.z;
        }

        private Chunk GenerateChunk(Vector2Int key)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Chunk chunk = new Chunk { matrices = new Matrix4x4[layers.Count][] };
            float minY = float.MaxValue, maxY = float.MinValue;
            List<Matrix4x4> buffer = new List<Matrix4x4>(1024);

            for (int l = 0; l < layers.Count; l++)
            {
                VegetationLayer layer = layers[l];
                buffer.Clear();
                if (layer.mesh == null || layer.material == null || layer.density <= 0f)
                {
                    chunk.matrices[l] = new Matrix4x4[0];
                    continue;
                }

                float cell = 1f / Mathf.Sqrt(layer.density);
                int cells = Mathf.Max(1, Mathf.RoundToInt(chunkSize / cell));
                cell = chunkSize / cells;
                for (int cz = 0; cz < cells; cz++)
                {
                    for (int cx = 0; cx < cells; cx++)
                    {
                        int gx = key.x * cells + cx, gz = key.y * cells + cz;
                        float x = key.x * chunkSize + (cx + WorldLayout.Hash01(gx, gz, layer.salt)) * cell;
                        float z = key.y * chunkSize + (cz + WorldLayout.Hash01(gx, gz, layer.salt + 101)) * cell;
                        float u = (x - origin.x) / data.size.x, v = (z - origin.z) / data.size.z;
                        if (u < 0f || v < 0f || u > 1f || v > 1f)
                        {
                            continue;
                        }

                        float weight = BiomeWeight(layer, x, z);
                        if (weight <= WorldLayout.Hash01(gx, gz, layer.salt + 202) || data.GetSteepness(u, v) > layer.maxSteepness)
                        {
                            continue;
                        }

                        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y - layer.sinkDepth;
                        float yaw = WorldLayout.Hash01(gx, gz, layer.salt + 303) * 360f;
                        float scale = Mathf.Lerp(layer.minScale, layer.maxScale, WorldLayout.Hash01(gx, gz, layer.salt + 404));
                        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                        if (layer.alignToNormal)
                        {
                            Vector3 normal = data.GetInterpolatedNormal(u, v);
                            rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.7f)) * rotation;
                        }

                        buffer.Add(Matrix4x4.TRS(new Vector3(x, y, z), rotation, Vector3.one * scale));
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y + layer.mesh.bounds.max.y * scale);
                    }
                }

                chunk.matrices[l] = buffer.ToArray();
            }

            if (minY > maxY)
            {
                minY = maxY = origin.y;
            }

            Vector3 center = new Vector3((key.x + 0.5f) * chunkSize, (minY + maxY) * 0.5f, (key.y + 0.5f) * chunkSize);
            chunk.bounds = new Bounds(center, new Vector3(chunkSize + 2f, (maxY - minY) + 2f, chunkSize + 2f));
            return chunk;
        }

        private static float BiomeWeight(VegetationLayer layer, float x, float z)
        {
            float wet = WorldLayout.Wetness(x, z);
            float streamDistance = WorldLayout.DistanceToStream(x, z) - WorldLayout.StreamBedHalfWidth;
            float pondDistance = WorldLayout.DistanceToPond(x, z) - WorldLayout.PondRadius;
            if (streamDistance < layer.waterClearance || pondDistance < layer.waterClearance)
            {
                return 0f;
            }

            float forest = WorldLayout.ForestFactor(x, z);
            switch (layer.biome)
            {
                case VegetationBiome.Meadow:
                    return (1f - forest) * (1f - wet * 0.7f);
                case VegetationBiome.Forest:
                    return forest;
                case VegetationBiome.WaterBank:
                    return Mathf.Clamp01(wet * 1.5f);
                default:
                    return 1f;
            }
        }

        private void Render(Camera cam, Vector3 cameraPosition)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, _frustum);
            int rendered = 0;
            for (int l = 0; l < layers.Count; l++)
            {
                VegetationLayer layer = layers[l];
                if (layer.mesh == null || layer.material == null)
                {
                    continue;
                }

                RenderParams renderParams = new RenderParams(layer.material)
                {
                    shadowCastingMode = layer.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    layer = gameObject.layer,
                    camera = Application.isPlaying ? null : cam
                };

                float cullSqr = layer.cullDistance * layer.cullDistance;
                int count = 0;
                foreach (Chunk chunk in _chunks.Values)
                {
                    if (chunk.bounds.SqrDistance(cameraPosition) > cullSqr || !GeometryUtility.TestPlanesAABB(_frustum, chunk.bounds))
                    {
                        continue;
                    }

                    Matrix4x4[] matrices = chunk.matrices[l];
                    float farthest = (chunk.bounds.center - cameraPosition).magnitude + chunk.bounds.extents.magnitude;
                    bool fullyInside = farthest * farthest <= cullSqr;
                    for (int i = 0; i < matrices.Length; i++)
                    {
                        // Distanz-Culling pro Instanz nur für Chunks am Rand der Sichtweite
                        if (!fullyInside && (matrices[i].GetPosition() - cameraPosition).sqrMagnitude > cullSqr)
                        {
                            continue;
                        }

                        _batch[count++] = matrices[i];
                        if (count == MaxInstancesPerCall)
                        {
                            Flush(renderParams, layer.mesh, count);
                            rendered += count;
                            count = 0;
                        }
                    }
                }

                if (count > 0)
                {
                    Flush(renderParams, layer.mesh, count);
                    rendered += count;
                }
            }

            RenderedInstancesLastFrame = rendered;
        }

        private void Flush(RenderParams renderParams, Mesh mesh, int count)
        {
            Graphics.RenderMeshInstanced(renderParams, mesh, 0, _batch, count, 0);
        }

        /// <summary>Liefert alle Pflanzeninstanzen innerhalb der Kugel, die hoch genug sind (für Kollider).</summary>
        public int QueryInstances(Vector3 center, float radius, float minHeight, List<Matrix4x4> outMatrices, List<Bounds> outMeshBounds)
        {
            outMatrices.Clear();
            outMeshBounds.Clear();
            float radiusSqr = radius * radius;
            foreach (Chunk chunk in _chunks.Values)
            {
                if (chunk.bounds.SqrDistance(center) > radiusSqr)
                {
                    continue;
                }
                for (int l = 0; l < layers.Count; l++)
                {
                    VegetationLayer layer = layers[l];
                    if (layer.mesh == null)
                    {
                        continue;
                    }
                    Matrix4x4[] matrices = chunk.matrices[l];
                    for (int i = 0; i < matrices.Length; i++)
                    {
                        Matrix4x4 m = matrices[i];
                        Vector3 pos = m.GetColumn(3);
                        if ((pos - center).sqrMagnitude > radiusSqr)
                        {
                            continue;
                        }
                        if (layer.mesh.bounds.size.y * m.GetColumn(1).magnitude < minHeight)
                        {
                            continue;
                        }
                        outMatrices.Add(m);
                        outMeshBounds.Add(layer.mesh.bounds);
                    }
                }
            }
            return outMatrices.Count;
        }
    }
}
