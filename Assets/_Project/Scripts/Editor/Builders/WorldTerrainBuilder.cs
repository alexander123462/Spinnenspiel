using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut das Terrain der Testwelt aus WorldLayout: Höhen (Hügel, Senken, Bachbett, Teich, Mikro-Unebenheiten)
    /// und Splatmap (Erde, Gras, Schlamm, Fels) samt TerrainSurfaceMap für die Oberflächenerkennung.
    /// </summary>
    public static class WorldTerrainBuilder
    {
        private const string Folder = AssetUtil.GeneratedRoot + "/Terrain";
        private const int HeightmapResolution = 513;
        private const int AlphamapResolution = 512;

        public const int LayerEarth = 0;
        public const int LayerGrass = 1;
        public const int LayerMud = 2;
        public const int LayerRock = 3;

        public static Terrain Create()
        {
            TerrainLayer[] layers =
            {
                // Reihenfolge ist fest: TerrainSurfaceMap hängt von den Layer-Indizes ab.
                ScanLayer("TL_Earth", "forest_ground_04", 2.0f, () => CreateLayer("TL_Earth", TextureFactory.Earth(), null, new Vector2(2.5f, 2.5f), 0.05f)),
                ScanLayer("TL_Grass", "sparse_grass", 2.0f, () => CreateLayer("TL_Grass", TextureFactory.Grass(), null, new Vector2(2f, 2f), 0.08f)),
                ScanLayer("TL_Mud", "brown_mud_leaves_01", 1.6f, () => CreateLayer("TL_Mud", TextureFactory.Mud(), null, new Vector2(2f, 2f), 0.55f)),
                ScanLayer("TL_Rock", "rocky_trail", 2.5f, () => CreateLayer("TL_Rock", TextureFactory.Rock(), TextureFactory.RockNormal(), new Vector2(3f, 3f), 0.2f))
            };

            // Erst als Asset anlegen, DANN Layer/Splatmap setzen – sonst werden die Splat-Texturen
            // nicht als Unterobjekte gespeichert und die Splatmap geht beim Laden verloren.
            TerrainData data = new TerrainData();
            data.heightmapResolution = HeightmapResolution;
            data.alphamapResolution = AlphamapResolution;
            data.size = new Vector3(WorldLayout.Size, WorldLayout.MaxHeight, WorldLayout.Size);
            AssetUtil.ReplaceAsset(data, Folder + "/Testwelt_TerrainData.asset");
            data.SetHeights(0, 0, BuildHeights());
            data.terrainLayers = layers;
            float[,,] alphamaps = BuildAlphamaps(data);
            data.SetAlphamaps(0, 0, alphamaps);
            UnityEditor.EditorUtility.SetDirty(data);
            UnityEditor.AssetDatabase.SaveAssets();
            LogLayerCoverage(alphamaps);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            Terrain terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 80f;
            terrain.drawInstanced = true;
            go.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Ground;
            go.AddComponent<TerrainSurfaceMap>().Configure(new[] { SurfaceType.Ground, SurfaceType.Grass, SurfaceType.Mud, SurfaceType.Rock });
            return terrain;
        }

        private static void LogLayerCoverage(float[,,] maps)
        {
            float[] sum = new float[4];
            int count = 0;
            for (int z = 0; z < AlphamapResolution; z += 8)
            {
                for (int x = 0; x < AlphamapResolution; x += 8)
                {
                    for (int l = 0; l < 4; l++)
                    {
                        sum[l] += maps[z, x, l];
                    }

                    count++;
                }
            }

            Debug.Log(string.Format("[Spinnenspiel] Splatmap-Anteile: Erde {0:P0}, Gras {1:P0}, Schlamm {2:P0}, Fels {3:P0}",
                sum[0] / count, sum[1] / count, sum[2] / count, sum[3] / count));
        }

        /// <summary>
        /// Liefert einen TerrainLayer aus dem passenden Foto-Scan, falls vorhanden;
        /// sonst die prozedurale Fallback-Textur. Scan-Layer ersetzen das gleichnamige Fallback-Asset.
        /// </summary>
        private static TerrainLayer ScanLayer(string name, string scanId, float tileSize, System.Func<TerrainLayer> fallback)
        {
            if (ScanTextures.IsAvailable(scanId))
            {
                ScanTextures.EnsureImported(scanId);
                return ScanTextures.CreateTerrainLayer(scanId, tileSize);
            }

            return fallback();
        }

        private static TerrainLayer CreateLayer(string name, Texture2D diffuse, Texture2D normal, Vector2 tileSize, float smoothness)
        {
            TerrainLayer layer = new TerrainLayer();
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.tileSize = tileSize;
            layer.smoothness = smoothness;
            return AssetUtil.ReplaceAsset(layer, Folder + "/" + name + ".terrainlayer");
        }

        private static float[,] BuildHeights()
        {
            float[,] heights = new float[HeightmapResolution, HeightmapResolution];
            float step = WorldLayout.Size / (HeightmapResolution - 1);
            for (int z = 0; z < HeightmapResolution; z++)
            {
                for (int x = 0; x < HeightmapResolution; x++)
                {
                    // SetHeights erwartet [z, x].
                    heights[z, x] = WorldLayout.Height(x * step, z * step) / WorldLayout.MaxHeight;
                }
            }

            return heights;
        }

        private static float[,,] BuildAlphamaps(TerrainData data)
        {
            float[,,] maps = new float[AlphamapResolution, AlphamapResolution, 4];
            float step = WorldLayout.Size / AlphamapResolution;
            for (int z = 0; z < AlphamapResolution; z++)
            {
                for (int x = 0; x < AlphamapResolution; x++)
                {
                    float wx = (x + 0.5f) * step;
                    float wz = (z + 0.5f) * step;
                    float u = wx / WorldLayout.Size, v = wz / WorldLayout.Size;

                    float steepness = data.GetSteepness(u, v);
                    float forest = WorldLayout.ForestFactor(wx, wz);
                    float wet = WorldLayout.Wetness(wx, wz);
                    float patch = Mathf.PerlinNoise(wx * 0.35f + 4.1f, wz * 0.35f + 8.6f);

                    float rock = Mathf.Clamp01((steepness - 24f) / 12f);
                    float mud = Mathf.Clamp01(wet * 1.4f - 0.2f) * (1f - rock);
                    float grass = (1f - forest) * Mathf.Clamp01(0.55f + patch) * (1f - mud) * (1f - rock);
                    float earth = Mathf.Max(0f, 1f - grass - mud - rock);

                    float sum = earth + grass + mud + rock;
                    maps[z, x, LayerEarth] = earth / sum;
                    maps[z, x, LayerGrass] = grass / sum;
                    maps[z, x, LayerMud] = mud / sum;
                    maps[z, x, LayerRock] = rock / sum;
                }
            }

            return maps;
        }
    }
}
