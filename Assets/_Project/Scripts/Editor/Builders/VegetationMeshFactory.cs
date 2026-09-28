using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Prozedurale Vegetations-Meshes. Vertex-Farbe: rgb = Tönung, a = Biegegewicht für den Wind (0 Basis, 1 Spitze).
    /// </summary>
    public static class VegetationMeshFactory
    {
        private const string Folder = AssetUtil.GeneratedRoot + "/Meshes";

        private sealed class Builder
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<int> triangles = new List<int>();

            public int Add(Vector3 position, Vector3 normal, Vector2 uv, Color color)
            {
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
                colors.Add(color);
                return vertices.Count - 1;
            }

            public void Quad(int a, int b, int c, int d)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }

            public Mesh Save(string name)
            {
                Mesh mesh = new Mesh();
                mesh.name = name;
                if (vertices.Count > 65000)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return AssetUtil.ReplaceAsset(mesh, Folder + "/" + name + ".asset");
            }
        }

        /// <summary>Grasbüschel aus gebogenen, spitz zulaufenden Halmen.</summary>
        public static Mesh GrassClump(string name, int blades, float minHeight, float maxHeight, int seed)
        {
            Builder b = new Builder();
            for (int i = 0; i < blades; i++)
            {
                float angle = WorldLayout.Hash01(i, seed, 1) * Mathf.PI * 2f;
                float radius = WorldLayout.Hash01(i, seed, 2) * 0.05f;
                Vector3 root = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                float height = Mathf.Lerp(minHeight, maxHeight, WorldLayout.Hash01(i, seed, 3));
                float facing = WorldLayout.Hash01(i, seed, 4) * Mathf.PI * 2f;
                Vector3 side = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));
                Vector3 lean = new Vector3(-side.z, 0f, side.x) * (0.15f + 0.35f * WorldLayout.Hash01(i, seed, 5));
                float width = 0.006f + 0.004f * WorldLayout.Hash01(i, seed, 6);
                float shade = 0.75f + 0.25f * WorldLayout.Hash01(i, seed, 7);

                const int segments = 4;
                int previousLeft = -1, previousRight = -1;
                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    Vector3 center = root + Vector3.up * (height * t) + lean * (height * t * t);
                    float w = width * (1f - t * 0.92f);
                    Vector3 normal = Vector3.Slerp(Vector3.up, new Vector3(-side.z, 0.3f, side.x).normalized, 0.5f);
                    Color color = new Color(shade * Mathf.Lerp(0.6f, 1f, t), shade * Mathf.Lerp(0.7f, 1f, t), shade * Mathf.Lerp(0.6f, 1f, t), t);
                    int left = b.Add(center - side * w, normal, new Vector2(0f, t), color);
                    int right = b.Add(center + side * w, normal, new Vector2(1f, t), color);
                    if (s > 0)
                    {
                        b.Quad(previousLeft, left, right, previousRight);
                    }

                    previousLeft = left;
                    previousRight = right;
                }
            }

            return b.Save(name);
        }

        /// <summary>Blume: Stängel + Blütenkopf aus Blütenblättern (Farbe über Vertex-Farbe).</summary>
        public static Mesh Flower(string name, Color petalColor, Color centerColor, float height, int petals, int seed)
        {
            Builder b = new Builder();
            Color stem = new Color(0.35f, 0.55f, 0.25f, 0f);
            float width = 0.003f;
            Vector3 lean = new Vector3(WorldLayout.Hash01(seed, 1, 9) - 0.5f, 0f, WorldLayout.Hash01(seed, 2, 9) - 0.5f) * 0.08f;
            int l0 = b.Add(new Vector3(-width, 0f, 0f), Vector3.forward, new Vector2(0, 0), stem);
            int r0 = b.Add(new Vector3(width, 0f, 0f), Vector3.forward, new Vector2(1, 0), stem);
            Vector3 top = Vector3.up * height + lean;
            stem.a = 1f;
            int l1 = b.Add(top + new Vector3(-width, 0f, 0f), Vector3.forward, new Vector2(0, 1), stem);
            int r1 = b.Add(top + new Vector3(width, 0f, 0f), Vector3.forward, new Vector2(1, 1), stem);
            b.Quad(l0, l1, r1, r0);

            Color petal = petalColor;
            petal.a = 1f;
            Color middle = centerColor;
            middle.a = 1f;
            int center = b.Add(top + Vector3.up * 0.004f, Vector3.up, new Vector2(0.5f, 0.5f), middle);
            float petalLength = 0.018f + 0.01f * WorldLayout.Hash01(seed, 3, 9);
            for (int i = 0; i < petals; i++)
            {
                float a0 = (i - 0.35f) / petals * Mathf.PI * 2f;
                float a1 = (i + 0.35f) / petals * Mathf.PI * 2f;
                float am = i / (float)petals * Mathf.PI * 2f;
                Vector3 p0 = top + new Vector3(Mathf.Cos(a0), 0.1f, Mathf.Sin(a0)) * petalLength * 0.55f;
                Vector3 p1 = top + new Vector3(Mathf.Cos(am), 0.25f, Mathf.Sin(am)) * petalLength;
                Vector3 p2 = top + new Vector3(Mathf.Cos(a1), 0.1f, Mathf.Sin(a1)) * petalLength * 0.55f;
                int i0 = b.Add(p0, Vector3.up, new Vector2(0, 0), petal);
                int i1 = b.Add(p1, Vector3.up, new Vector2(0.5f, 1), petal);
                int i2 = b.Add(p2, Vector3.up, new Vector2(1, 0), petal);
                b.triangles.Add(center); b.triangles.Add(i0); b.triangles.Add(i1);
                b.triangles.Add(center); b.triangles.Add(i1); b.triangles.Add(i2);
            }

            return b.Save(name);
        }

        /// <summary>Farn/Kleinpflanze: gebogene Wedel mit gezackter Kontur.</summary>
        public static Mesh Fern(string name, int fronds, float length, int seed)
        {
            Builder b = new Builder();
            for (int f = 0; f < fronds; f++)
            {
                float angle = (f + WorldLayout.Hash01(f, seed, 1) * 0.5f) / fronds * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                float len = length * (0.7f + 0.3f * WorldLayout.Hash01(f, seed, 2));
                const int segments = 6;
                int pl = -1, pr = -1;
                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    Vector3 c = dir * (len * t) + Vector3.up * (len * 0.8f * Mathf.Sin(t * Mathf.PI * 0.75f));
                    float w = len * 0.16f * Mathf.Sin(t * Mathf.PI) * (0.8f + 0.2f * Mathf.Sin(t * 40f));
                    Color color = new Color(Mathf.Lerp(0.7f, 1f, t), Mathf.Lerp(0.8f, 1f, t), Mathf.Lerp(0.7f, 0.9f, t), t);
                    Vector3 n = Vector3.Cross(side, dir).normalized;
                    if (n.y < 0f) n = -n;
                    int l = b.Add(c - side * w, n, new Vector2(0, t), color);
                    int r = b.Add(c + side * w, n, new Vector2(1, t), color);
                    if (s > 0) b.Quad(pl, l, r, pr);
                    pl = l;
                    pr = r;
                }
            }

            return b.Save(name);
        }

        /// <summary>Laubstreu: flach liegende Blätter in Herbstfarben (ohne Wind).</summary>
        public static Mesh LeafLitter(string name, int leaves, int seed)
        {
            Builder b = new Builder();
            for (int i = 0; i < leaves; i++)
            {
                Vector3 c = new Vector3(WorldLayout.Hash01(i, seed, 1) - 0.5f, 0.002f + 0.004f * WorldLayout.Hash01(i, seed, 8), WorldLayout.Hash01(i, seed, 2) - 0.5f) * 0.5f;
                float angle = WorldLayout.Hash01(i, seed, 3) * Mathf.PI * 2f;
                Vector3 along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 across = new Vector3(-along.z, 0f, along.x);
                float size = 0.02f + 0.02f * WorldLayout.Hash01(i, seed, 4);
                float hue = WorldLayout.Hash01(i, seed, 5);
                Color color = Color.Lerp(new Color(0.45f, 0.25f, 0.1f, 0f), new Color(0.65f, 0.45f, 0.15f, 0f), hue);
                color = Color.Lerp(color, new Color(0.3f, 0.22f, 0.12f, 0f), WorldLayout.Hash01(i, seed, 6) * 0.5f);
                Vector3 up = Vector3.up;
                int a = b.Add(c - along * size, up, new Vector2(0, 0.5f), color);
                int d = b.Add(c + across * size * 0.45f, up, new Vector2(0.5f, 1f), color);
                int e = b.Add(c + along * size + Vector3.up * 0.003f, up, new Vector2(1, 0.5f), color);
                int f = b.Add(c - across * size * 0.45f, up, new Vector2(0.5f, 0f), color);
                b.Quad(a, d, e, f);
            }

            return b.Save(name);
        }

        /// <summary>Blattkarten-Cluster in Ellipsoidform (Busch oder Baumkrone). UVs zeigen auf den Blatt-Atlas.</summary>
        public static Mesh LeafCards(string name, int cards, Vector3 radii, Vector3 offset, float cardSize, int seed, List<Vector3> blobCenters = null)
        {
            Builder b = new Builder();
            for (int i = 0; i < cards; i++)
            {
                Vector3 blob = blobCenters != null && blobCenters.Count > 0 ? blobCenters[i % blobCenters.Count] : Vector3.zero;
                Vector3 dir = new Vector3(WorldLayout.Hash01(i, seed, 1) * 2f - 1f, WorldLayout.Hash01(i, seed, 2) * 2f - 1f, WorldLayout.Hash01(i, seed, 3) * 2f - 1f);
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.up;
                float r = Mathf.Pow(WorldLayout.Hash01(i, seed, 4), 0.4f);
                Vector3 local = Vector3.Scale(dir.normalized * r, radii);
                Vector3 center = offset + blob + local;
                Vector3 normal = (local.normalized + Vector3.up * 0.4f).normalized;
                Vector3 tangent = Vector3.Cross(normal, new Vector3(WorldLayout.Hash01(i, seed, 5) - 0.5f, 0.2f, WorldLayout.Hash01(i, seed, 6) - 0.5f)).normalized;
                if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
                Vector3 bitangent = Vector3.Cross(tangent, normal).normalized;
                float size = cardSize * (0.7f + 0.6f * WorldLayout.Hash01(i, seed, 7));
                float bend = Mathf.Clamp01(0.35f + r * 0.65f);
                float shade = 0.7f + 0.3f * Mathf.Clamp01(local.y / Mathf.Max(0.01f, radii.y) * 0.5f + 0.5f);
                Color color = new Color(shade, shade, shade * 0.95f, bend);
                int v0 = b.Add(center + (-tangent - bitangent) * size, normal, new Vector2(0, 0), color);
                int v1 = b.Add(center + (-tangent + bitangent) * size, normal, new Vector2(0, 1), color);
                int v2 = b.Add(center + (tangent + bitangent) * size, normal, new Vector2(1, 1), color);
                int v3 = b.Add(center + (tangent - bitangent) * size, normal, new Vector2(1, 0), color);
                b.Quad(v0, v1, v2, v3);
            }

            return b.Save(name);
        }

        /// <summary>Stehender, verjüngter Stamm mit einigen Ästen (Rinden-UVs), für Bäume.</summary>
        public static Mesh Trunk(string name, float height, float baseRadius, int branches, int seed, out List<Vector3> branchTips)
        {
            Builder b = new Builder();
            branchTips = new List<Vector3>();
            AddCylinder(b, Vector3.zero, Vector3.up * height, baseRadius, baseRadius * 0.35f, 12, 8, seed);
            for (int i = 0; i < branches; i++)
            {
                float t = 0.45f + 0.45f * WorldLayout.Hash01(i, seed, 11);
                float angle = WorldLayout.Hash01(i, seed, 12) * Mathf.PI * 2f;
                Vector3 start = Vector3.up * (height * t);
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0.6f + 0.4f * WorldLayout.Hash01(i, seed, 13), Mathf.Sin(angle)).normalized;
                float len = height * (0.22f + 0.12f * WorldLayout.Hash01(i, seed, 14));
                Vector3 end = start + dir * len;
                AddCylinder(b, start, end, baseRadius * 0.28f * (1.1f - t), baseRadius * 0.05f, 6, 3, seed + i);
                branchTips.Add(end);
            }

            return b.Save(name);
        }

        private static void AddCylinder(Builder b, Vector3 from, Vector3 to, float r0, float r1, int sides, int rings, int seed)
        {
            Vector3 axis = (to - from).normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 side2 = Vector3.Cross(axis, side).normalized;
            int start = b.vertices.Count;
            float length = (to - from).magnitude;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float radius = Mathf.Lerp(r0, r1, t);
                Vector3 center = Vector3.Lerp(from, to, t);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 n = (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)).normalized;
                    float bump = 1f + (Mathf.PerlinNoise(s * 0.7f + seed, t * 5f) - 0.5f) * 0.15f;
                    // Stämme bewegen sich im Wind kaum (a = 0.05 an der Spitze)
                    Color c = new Color(1f, 1f, 1f, t * 0.05f);
                    b.Add(center + n * radius * bump, n, new Vector2(s / (float)sides, t * length * 0.8f), c);
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start + r * (sides + 1) + s, i1 = i0 + 1, i2 = i0 + sides + 1, i3 = i2 + 1;
                    b.triangles.Add(i0); b.triangles.Add(i1); b.triangles.Add(i2);
                    b.triangles.Add(i1); b.triangles.Add(i3); b.triangles.Add(i2);
                }
            }
        }
    }
}
