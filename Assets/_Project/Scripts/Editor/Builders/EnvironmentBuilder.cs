using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Milestone 3: begehbare Hindernisse der Testwelt – Felsen, Steine, Baumstämme, Äste, Baumstümpfe.
    /// Alle Objekte haben passende Kollider und SurfaceTags, damit das Beinsystem darauf Halt findet.
    /// Meshes und Materialien werden geteilt (ein Mesh/Material je Variante).
    /// </summary>
    public static class EnvironmentBuilder
    {
        private const string MaterialFolder = AssetUtil.GeneratedRoot + "/Materials";

        private static Mesh[] _rockMeshes;
        private static Mesh[] _logMeshes;
        private static Material _rockMaterial;
        private static Material _barkMaterial;
        private static Terrain _terrain;
        private static System.Random _random;

        public static void Build(Terrain terrain, int seed)
        {
            _terrain = terrain;
            _random = new System.Random(seed);
            CreateSharedAssets();

            Transform root = new GameObject("Environment").transform;
            Transform rocks = Child(root, "Rocks");
            Transform wood = Child(root, "Wood");

            BuildTestCourse(rocks, wood);

            Scatter(12, p => WorldLayout.ForestFactor(p.x, p.y) > 0.6f && ClearOfWater(p, 2.5f), p => PlaceRock(rocks, p, Range(0.6f, 1.4f), true));
            Scatter(80, p => ClearOfWater(p, 0.2f), p => PlaceRock(rocks, p, Range(0.05f, 0.22f), false));
            Scatter(25, p => WorldLayout.DistanceToStream(p.x, p.y) < 2.4f && ClearOfWater(p, 0.1f), p => PlaceRock(rocks, p, Range(0.08f, 0.3f), false));
            Scatter(10, p => WorldLayout.ForestFactor(p.x, p.y) > 0.5f && ClearOfWater(p, 2f), p => PlaceLog(wood, p, Range(1.6f, 3.4f), Range(0.07f, 0.16f), Range(0f, 360f), "Log"));
            Scatter(70, p => (WorldLayout.ForestFactor(p.x, p.y) > 0.3f || Chance(0.15f)) && ClearOfWater(p, 0.5f), p => PlaceLog(wood, p, Range(0.3f, 1.1f), Range(0.008f, 0.022f), Range(0f, 360f), "Branch"));
            Scatter(5, p => WorldLayout.ForestFactor(p.x, p.y) > 0.8f && ClearOfWater(p, 2f), p => PlaceStump(wood, p, Range(0.14f, 0.24f), Range(0.18f, 0.4f)));

            AssetUtil.SetStaticRecursive(root.gameObject);
        }

        private static void CreateSharedAssets()
        {
            _rockMeshes = new Mesh[5];
            for (int i = 0; i < _rockMeshes.Length; i++)
            {
                _rockMeshes[i] = MeshFactory.Rock("M_Rock" + i, 11 + i * 7, i < 2 ? 3 : 2, 0.28f, 0.7f);
            }

            _logMeshes = new Mesh[3];
            for (int i = 0; i < _logMeshes.Length; i++)
            {
                _logMeshes[i] = MeshFactory.Log("M_Log" + i, 5 + i * 3, 14, 10, 0.7f + 0.1f * i, 0.18f);
            }

            // Foto-Scans bevorzugen, bei fehlenden Quelldateien die prozedurale Fallback-Textur.
            if (ScanTextures.IsAvailable("mossy_rock"))
            {
                ScanTextures.EnsureImported("mossy_rock");
                _rockMaterial = ScanTextures.CreateLitMaterial("mossy_rock", new Vector2(1f, 1f));
            }
            else
            {
                _rockMaterial = AssetUtil.CreateLitMaterial(MaterialFolder + "/MAT_Rock.mat", new Color(0.62f, 0.6f, 0.56f), 0.18f, TextureFactory.Rock(), TextureFactory.RockNormal());
            }

            if (ScanTextures.IsAvailable("pine_bark"))
            {
                ScanTextures.EnsureImported("pine_bark");
                _barkMaterial = ScanTextures.CreateLitMaterial("pine_bark", new Vector2(1f, 2f));
            }
            else
            {
                _barkMaterial = AssetUtil.CreateLitMaterial(MaterialFolder + "/MAT_Bark.mat", Color.white, 0.1f, TextureFactory.Bark(), TextureFactory.BarkNormal());
            }
        }

        /// <summary>Hindernisparcours direkt am Spawn, damit das Beinsystem sofort sichtbar gefordert wird.</summary>
        private static void BuildTestCourse(Transform rocks, Transform wood)
        {
            // Direkt vor der Spinne (Blickrichtung +Z): Steinreihe, querliegende Äste, ein Felsblock etwas seitlich.
            Vector3 s = WorldLayout.SpawnPoint;
            for (int i = 0; i < 7; i++)
            {
                float side = (i % 3 - 1) * 0.12f;
                PlaceRock(rocks, new Vector2(s.x + side, s.z + 0.9f + i * 0.28f), 0.05f + i * 0.012f, false);
            }

            PlaceLog(wood, new Vector2(s.x, s.z + 3.2f), 0.9f, 0.018f, 90f, "Branch");
            PlaceLog(wood, new Vector2(s.x + 0.1f, s.z + 3.9f), 0.7f, 0.014f, 70f, "Branch");
            PlaceLog(wood, new Vector2(s.x - 1.6f, s.z + 2.4f), 2.2f, 0.11f, 15f, "Log");
            PlaceRock(rocks, new Vector2(s.x + 2.2f, s.z + 3.2f), 0.9f, true);
            PlaceStump(wood, new Vector2(s.x - 2.4f, s.z + 0.6f), 0.18f, 0.25f);
            for (int i = 0; i < 5; i++)
            {
                PlaceRock(rocks, new Vector2(s.x + 0.35f + i * 0.2f, s.z + 4.6f + (i % 2) * 0.15f), 0.12f + (i % 3) * 0.04f, false);
            }
        }

        private static void Scatter(int count, System.Func<Vector2, bool> accept, System.Action<Vector2> place)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                Vector2 p = new Vector2(Range(2f, WorldLayout.Size - 2f), Range(2f, WorldLayout.Size - 2f));
                Vector2 spawn = new Vector2(WorldLayout.SpawnPoint.x, WorldLayout.SpawnPoint.z);
                if ((p - spawn).magnitude < 1.4f || !accept(p))
                {
                    continue;
                }

                place(p);
                placed++;
            }
        }

        private static void PlaceRock(Transform parent, Vector2 p, float size, bool boulder)
        {
            Mesh mesh = _rockMeshes[boulder ? _random.Next(0, 2) : _random.Next(0, _rockMeshes.Length)];
            GameObject go = new GameObject(boulder ? "Boulder" : "Stone");
            go.transform.SetParent(parent, false);
            float ground = GroundHeight(p);
            go.transform.position = new Vector3(p.x, ground - size * 0.12f, p.y);
            go.transform.rotation = Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f));
            go.transform.localScale = new Vector3(size * Range(0.85f, 1.2f), size * Range(0.8f, 1.1f), size * Range(0.85f, 1.2f));
            AddRenderer(go, mesh, _rockMaterial);
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false; // statisch: exakte Oberfläche für die Fußplatzierung
            go.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Rock;
        }

        private static void PlaceLog(Transform parent, Vector2 p, float length, float radius, float yaw, string name)
        {
            Mesh mesh = _logMeshes[_random.Next(0, _logMeshes.Length)];
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 axis = yawRotation * Vector3.forward;
            Vector2 a = p - new Vector2(axis.x, axis.z) * (length * 0.5f);
            Vector2 b = p + new Vector2(axis.x, axis.z) * (length * 0.5f);
            float ha = GroundHeight(a), hb = GroundHeight(b);
            float pitch = -Mathf.Atan2(hb - ha, length) * Mathf.Rad2Deg;
            float sink = radius * 0.4f;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(p.x, Mathf.Min(ha, hb) + (Mathf.Abs(hb - ha) * 0.5f) + radius - sink, p.y);
            go.transform.rotation = Quaternion.Euler(pitch, yaw, Range(0f, 360f));
            go.transform.localScale = new Vector3(radius, radius, length);
            AddRenderer(go, mesh, _barkMaterial);

            CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.85f;
            collider.height = 1f;
            go.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;
        }

        private static void PlaceStump(Transform parent, Vector2 p, float radius, float height)
        {
            GameObject go = new GameObject("Stump");
            go.transform.SetParent(parent, false);
            float ground = GroundHeight(p);
            go.transform.position = new Vector3(p.x, ground + height * 0.5f - 0.03f, p.y);
            go.transform.rotation = Quaternion.Euler(90f, Range(0f, 360f), 0f);
            go.transform.localScale = new Vector3(radius, radius, height);
            AddRenderer(go, _logMeshes[0], _barkMaterial);
            CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.9f;
            collider.height = 1f;
            go.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;
        }

        private static void AddRenderer(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        private static bool ClearOfWater(Vector2 p, float margin)
        {
            return WorldLayout.DistanceToStream(p.x, p.y) > WorldLayout.StreamBedHalfWidth + margin
                && WorldLayout.DistanceToPond(p.x, p.y) > WorldLayout.PondRadius + margin;
        }

        private static float GroundHeight(Vector2 p)
        {
            return _terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + _terrain.transform.position.y;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static float Range(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private static bool Chance(float probability)
        {
            return _random.NextDouble() < probability;
        }

        /// <summary>Positionen aller platzierten Felsen (für Vegetation, die Felsen meidet).</summary>
        public static List<Vector3> CollectObstaclePositions(Transform environmentRoot)
        {
            List<Vector3> list = new List<Vector3>();
            foreach (Collider c in environmentRoot.GetComponentsInChildren<Collider>())
            {
                list.Add(c.bounds.center);
            }

            return list;
        }
    }
}
