using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Umgebung außerhalb der spielbaren Welt: ein niedrig aufgelöstes Hügelland, das nahtlos am Terrainrand ansetzt
    /// (kein "Weltende" sichtbar, der Nebel übernimmt die Ferne), plus unsichtbare Randwände, damit die Spinne
    /// nicht von der Welt fällt.
    /// </summary>
    public static class HorizonBuilder
    {
        private const float Step = 2f;
        private const float Extent = 90f;

        public static void Build(Terrain terrain)
        {
            Transform root = new GameObject("Horizon").transform;
            Mesh mesh = BuildMesh(terrain);
            GameObject hills = new GameObject("Distant Hills");
            hills.transform.SetParent(root, false);
            hills.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = hills.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetUtil.CreateLitMaterial(AssetUtil.GeneratedRoot + "/Materials/MAT_Horizon.mat",
                new Color(0.55f, 0.62f, 0.45f), 0.05f, TextureFactory.Grass());
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float size = WorldLayout.Size;
            AddWall(root, new Vector3(size * 0.5f, 5f, -0.5f), new Vector3(size + 2f, 20f, 1f));
            AddWall(root, new Vector3(size * 0.5f, 5f, size + 0.5f), new Vector3(size + 2f, 20f, 1f));
            AddWall(root, new Vector3(-0.5f, 5f, size * 0.5f), new Vector3(1f, 20f, size + 2f));
            AddWall(root, new Vector3(size + 0.5f, 5f, size * 0.5f), new Vector3(1f, 20f, size + 2f));
        }

        private static void AddWall(Transform parent, Vector3 center, Vector3 size)
        {
            GameObject wall = new GameObject("Boundary");
            wall.transform.SetParent(parent, false);
            wall.transform.position = center;
            wall.layer = 2; // Ignore Raycast: Kamera und Beine sehen die Wand nicht, der CharacterController schon.
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = size;
        }

        private static Mesh BuildMesh(Terrain terrain)
        {
            float size = WorldLayout.Size;
            float min = -Extent, max = size + Extent;
            int count = Mathf.RoundToInt((max - min) / Step) + 1;
            List<Vector3> vertices = new List<Vector3>(count * count);
            List<Vector2> uvs = new List<Vector2>(count * count);
            for (int z = 0; z < count; z++)
            {
                for (int x = 0; x < count; x++)
                {
                    float wx = min + x * Step, wz = min + z * Step;
                    vertices.Add(new Vector3(wx, HeightAt(terrain, wx, wz), wz));
                    uvs.Add(new Vector2(wx, wz) * 0.5f);
                }
            }

            List<int> triangles = new List<int>();
            for (int z = 0; z < count - 1; z++)
            {
                for (int x = 0; x < count - 1; x++)
                {
                    float cx = min + (x + 0.5f) * Step, cz = min + (z + 0.5f) * Step;
                    // Zellen, die komplett im Terrain liegen, auslassen.
                    if (cx > Step && cx < size - Step && cz > Step && cz < size - Step)
                    {
                        continue;
                    }

                    int a = z * count + x, b = a + 1, c = a + count, d = c + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            return MeshFactory.Save("M_Horizon", vertices, triangles, uvs, null);
        }

        private static float HeightAt(Terrain terrain, float x, float z)
        {
            float size = WorldLayout.Size;
            float ex = Mathf.Clamp(x, 0f, size), ez = Mathf.Clamp(z, 0f, size);
            float edgeHeight = terrain.SampleHeight(new Vector3(ex, 0f, ez)) + terrain.transform.position.y;
            float distance = new Vector2(x - ex, z - ez).magnitude;
            if (distance < 0.01f)
            {
                return edgeHeight - 0.08f;
            }

            float hills = 3.5f + 5f * Mathf.PerlinNoise(x * 0.025f + 3f, z * 0.025f + 7f) + distance * 0.06f;
            float t = Mathf.Clamp01(distance / 25f);
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(edgeHeight - 0.08f, hills, t);
        }
    }
}
