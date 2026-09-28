using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Prozedurale Meshes für Steine und Holz. Werden einmal erzeugt und als Assets gespeichert,
    /// damit alle Instanzen dieselben Meshes teilen (Batching/Instancing).
    /// </summary>
    public static class MeshFactory
    {
        private const string Folder = AssetUtil.GeneratedRoot + "/Meshes";

        /// <summary>Unregelmäßiger Stein: unterteilter Ikosaeder, per Rauschen verformt, Unterseite abgeflacht.</summary>
        public static Mesh Rock(string name, int seed, int subdivisions, float roughness, float flatten)
        {
            List<Vector3> vertices;
            List<int> triangles;
            Icosphere(subdivisions, out vertices, out triangles);

            float sx = 0.8f + 0.5f * WorldLayout.Hash01(seed, 1, 3);
            float sz = 0.8f + 0.5f * WorldLayout.Hash01(seed, 2, 3);
            float sy = 0.55f + 0.3f * WorldLayout.Hash01(seed, 3, 3);
            List<Vector2> uvs = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float n = Noise3(p * 1.7f, seed) * 0.6f + Noise3(p * 4.1f, seed + 7) * 0.3f + Noise3(p * 9f, seed + 13) * 0.1f;
                float r = 1f + (n - 0.5f) * 2f * roughness;
                Vector3 q = new Vector3(p.x * sx, p.y * sy, p.z * sz) * r * 0.5f;
                if (q.y < -0.1f)
                {
                    q.y = Mathf.Lerp(q.y, -0.1f, flatten);
                }

                vertices[i] = q;
                uvs.Add(new Vector2(Mathf.Atan2(p.z, p.x) / (2f * Mathf.PI) + 0.5f, p.y * 0.5f + 0.5f) * 2f);
            }

            return Save(name, vertices, triangles, uvs, null);
        }

        /// <summary>Liegender Stamm/Ast entlang +Z, Länge 1, Radius 1 an der Basis, verjüngt, mit Rinden-UVs und Kappen.</summary>
        public static Mesh Log(string name, int seed, int sides, int rings, float taper, float bumpiness)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float radius = Mathf.Lerp(1f, taper, t);
                float bend = Mathf.Sin(t * Mathf.PI) * 0.15f * (WorldLayout.Hash01(seed, 9, 1) - 0.5f);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    float bump = 1f + (Mathf.PerlinNoise(s * 0.9f + seed, t * 6f) - 0.5f) * bumpiness;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius * bump + bend, Mathf.Sin(a) * radius * bump, t - 0.5f));
                    uvs.Add(new Vector2(s / (float)sides, t * 4f));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int i0 = r * (sides + 1) + s, i1 = i0 + 1, i2 = i0 + sides + 1, i3 = i2 + 1;
                    triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                    triangles.Add(i1); triangles.Add(i3); triangles.Add(i2);
                }
            }

            AddCap(vertices, uvs, triangles, 0, sides, -0.5f, false);
            AddCap(vertices, uvs, triangles, rings * (sides + 1), sides, 0.5f, true);
            return Save(name, vertices, triangles, uvs, null);
        }

        private static void AddCap(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int ringStart, int sides, float z, bool front)
        {
            int center = vertices.Count;
            Vector3 c = Vector3.zero;
            for (int s = 0; s < sides; s++)
            {
                c += vertices[ringStart + s];
            }

            vertices.Add(c / sides);
            uvs.Add(new Vector2(0.5f, 0.5f));
            int first = vertices.Count;
            for (int s = 0; s <= sides; s++)
            {
                Vector3 p = vertices[ringStart + s];
                vertices.Add(p);
                uvs.Add(new Vector2(p.x * 0.25f + 0.5f, p.y * 0.25f + 0.5f));
            }

            for (int s = 0; s < sides; s++)
            {
                if (front)
                {
                    triangles.Add(center); triangles.Add(first + s); triangles.Add(first + s + 1);
                }
                else
                {
                    triangles.Add(center); triangles.Add(first + s + 1); triangles.Add(first + s);
                }
            }
        }

        public static Mesh Save(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (uvs != null)
            {
                mesh.SetUVs(0, uvs);
            }

            if (colors != null)
            {
                mesh.SetColors(colors);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return AssetUtil.ReplaceAsset(mesh, Folder + "/" + name + ".asset");
        }

        /// <summary>Einfaches 3D-Wertrauschen 0..1 aus drei Perlin-Ebenen.</summary>
        public static float Noise3(Vector3 p, int seed)
        {
            float s = seed * 3.17f;
            float xy = Mathf.PerlinNoise(p.x + s, p.y + s * 0.7f);
            float yz = Mathf.PerlinNoise(p.y + s * 1.3f, p.z + s);
            float zx = Mathf.PerlinNoise(p.z + s * 0.5f, p.x + s * 1.9f);
            return (xy + yz + zx) / 3f;
        }

        private static void Icosphere(int subdivisions, out List<Vector3> vertices, out List<int> triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = vertices[i].normalized;
            }

            triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            for (int level = 0; level < subdivisions; level++)
            {
                Dictionary<long, int> cache = new Dictionary<long, int>();
                List<int> next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(vertices, cache, a, b);
                    int bc = Midpoint(vertices, cache, b, c);
                    int ca = Midpoint(vertices, cache, c, a);
                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }

                triangles = next;
            }

            // Unity: Vorderseite ist dort, wohin Cross(b - a, c - a) zeigt -> alle Dreiecke nach außen drehen.
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 va = vertices[triangles[i]], vb = vertices[triangles[i + 1]], vc = vertices[triangles[i + 2]];
                if (Vector3.Dot(Vector3.Cross(vb - va, vc - va), va + vb + vc) < 0f)
                {
                    int swap = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = swap;
                }
            }
        }

        private static int Midpoint(List<Vector3> vertices, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int index;
            if (cache.TryGetValue(key, out index))
            {
                return index;
            }

            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            index = vertices.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
