using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut das Milestone-1-Testgelaende: TerrainData (Perlin-Hoehen),
    /// Erd-Textur als PNG, TerrainLayer und Terrain-GameObject.
    /// </summary>
    public static class TerrainBuilder
    {
        private const string TerrainFolder = "Assets/_Project/Art/Terrain";
        private const string TerrainDataPath = "Assets/_Project/Art/Terrain/M1_TerrainData.asset";
        private const string GroundTexturePath = "Assets/_Project/Art/Terrain/M1_Ground.png";
        private const string GroundLayerPath = "Assets/_Project/Art/Terrain/M1_Ground.terrainlayer";

        private static readonly Vector2 TerrainSize = new Vector2(60f, 60f);
        private const float TerrainMaxHeight = 6f;
        private const int HeightmapResolution = 257;
        private const int TextureSize = 256;

        public static Terrain Create(int seed)
        {
            EnsureFolder(TerrainFolder);

            TerrainLayer layer = CreateTerrainLayer();
            TerrainData terrainData = CreateTerrainData(seed);
            terrainData.terrainLayers = new[] { layer };
            ReplaceAsset(terrainData, TerrainDataPath);
            AssetDatabase.SaveAssets();

            GameObject go = Terrain.CreateTerrainGameObject(terrainData);
            go.name = "Terrain";
            go.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Ground;
            return go.GetComponent<Terrain>();
        }

        private static TerrainData CreateTerrainData(int seed)
        {
            // Unity 6: CreateDefaultPatch/CreateDefaultTerrainData exist nicht mehr.
            TerrainData data = new TerrainData();
            data.size = new Vector3(TerrainSize.x, TerrainMaxHeight, TerrainSize.y);
            data.heightmapResolution = HeightmapResolution;
            data.SetHeights(0, 0, GenerateHeights(seed));
            return data;
        }

        private static float[,] GenerateHeights(int seed)
        {
            float[,] heights = new float[HeightmapResolution, HeightmapResolution];
            float ox = seed * 17.17f;
            float oy = seed * 91.31f;

            // 4 Oktaven: grobe Huelgel + feine Unebenheit, Amplituden 1 + 0.5 + 0.25 + 0.125
            for (int z = 0; z < HeightmapResolution; z++)
            {
                for (int x = 0; x < HeightmapResolution; x++)
                {
                    float u = x / (float)(HeightmapResolution - 1);
                    float v = z / (float)(HeightmapResolution - 1);
                    float h = 0f;
                    h += Mathf.PerlinNoise(u * 1f + ox, v * 1f + oy) * 1f;
                    h += Mathf.PerlinNoise(u * 2f + ox * 2f, v * 2f + oy * 2f) * 0.5f;
                    h += Mathf.PerlinNoise(u * 4f + ox * 4f, v * 4f + oy * 4f) * 0.25f;
                    h += Mathf.PerlinNoise(u * 8f + ox * 8f, v * 8f + oy * 8f) * 0.125f;
                    heights[x, z] = Mathf.Clamp01(h / 1.875f);
                }
            }
            return heights;
        }

        private static TerrainLayer CreateTerrainLayer()
        {
            Texture2D texture = CreateGroundTexture();
            TerrainLayer layer = new TerrainLayer();
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(4f, 4f);
            ReplaceAsset(layer, GroundLayerPath);
            return layer;
        }

        private static Texture2D CreateGroundTexture()
        {
            // Erdbraun und Grasgruen, ueber Perlin gemischt
            Color brown = new Color(0.36f, 0.28f, 0.18f);
            Color grass = new Color(0.24f, 0.38f, 0.16f);
            Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = x / (float)TextureSize;
                    float v = y / (float)TextureSize;
                    float n = Mathf.PerlinNoise(u * 8f + 3.7f, v * 8f + 8.1f);
                    Color c = Color.Lerp(brown, grass, n);
                    // leichte Helligkeitsvariation fuer Struktur
                    float shade = 0.9f + 0.2f * Mathf.PerlinNoise(u * 24f + 1.2f, v * 24f + 5.5f);
                    tex.SetPixel(x, y, new Color(c.r * shade, c.g * shade, c.b * shade));
                }
            }
            tex.Apply();

            File.WriteAllBytes(GroundTexturePath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(GroundTexturePath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            int idx = path.LastIndexOf('/');
            string parent = path.Substring(0, idx);
            string leaf = path.Substring(idx + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void ReplaceAsset(Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            // Unity 6: SaveAsAsset exist nicht mehr, Ersatz ist CreateAsset.
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
