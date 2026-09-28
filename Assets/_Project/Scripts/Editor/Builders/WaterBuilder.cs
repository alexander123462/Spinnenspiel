using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Milestone 5: Bach und Teich. Die Wasseroberflächen folgen exakt dem Bachbett aus WorldLayout;
    /// Vertex-Farbe Rot = relative Wassertiefe (für Uferverlauf und Tiefenfarbe im Shader).
    /// Die Oberflächen haben Kollider mit SurfaceType.Water – die Spinne kann (wie eine Jagdspinne)
    /// auf dem Wasser laufen und erkennt den Untergrund.
    /// </summary>
    public static class WaterBuilder
    {
        private const string Folder = AssetUtil.GeneratedRoot;

        public static void Build()
        {
            Shader shader = Shader.Find("Spinnenspiel/Water");
            if (shader == null)
            {
                Debug.LogError("[Spinnenspiel] Shader 'Spinnenspiel/Water' nicht gefunden – Wasser wird nicht gebaut.");
                return;
            }

            Texture2D normal = TextureFactory.WaterNormal();
            Material stream = CreateMaterial("MAT_WaterStream", shader, normal, new Vector4(1f, 0f, 0.14f, 0f));
            Material pond = CreateMaterial("MAT_WaterPond", shader, normal, new Vector4(0.6f, 0.4f, 0.015f, 0f));

            Transform root = new GameObject("Water").transform;
            CreateWaterObject(root, "Stream", BuildStreamMesh(), stream);
            CreateWaterObject(root, "Pond", BuildPondMesh(), pond);
        }

        private static Material CreateMaterial(string name, Shader shader, Texture2D normal, Vector4 flow)
        {
            Material material = new Material(shader);
            material.SetTexture("_NormalMap", normal);
            material.SetVector("_FlowDirection", flow);
            return AssetUtil.ReplaceAsset(material, Folder + "/Materials/" + name + ".mat");
        }

        private static void CreateWaterObject(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            go.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Water;
            go.AddComponent<WaterBody>();
        }

        private static Mesh BuildStreamMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            const float step = 0.4f;
            const int across = 9;
            const float halfWidth = 1.9f;
            int rows = 0;
            for (float x = -1f; x <= WorldLayout.Size + 1f; x += step)
            {
                float z = WorldLayout.StreamCenterZ(x);
                float dzdx = (WorldLayout.StreamCenterZ(x + 0.05f) - WorldLayout.StreamCenterZ(x - 0.05f)) / 0.1f;
                Vector2 tangent = new Vector2(1f, dzdx).normalized;
                Vector2 side = new Vector2(-tangent.y, tangent.x);
                float waterY = WorldLayout.StreamWaterHeight(x);
                for (int i = 0; i < across; i++)
                {
                    float t = i / (float)(across - 1) * 2f - 1f;
                    Vector2 p = new Vector2(x, z) + side * (t * halfWidth);
                    vertices.Add(new Vector3(p.x, waterY, p.y));
                    colors.Add(new Color(DepthFactor(p, waterY), 0f, 0f, 1f));
                    uvs.Add(new Vector2(t, x));
                }

                rows++;
            }

            for (int r = 0; r < rows - 1; r++)
            {
                for (int i = 0; i < across - 1; i++)
                {
                    int a = r * across + i, b = a + 1, c = a + across, d = c + 1;
                    // Oberseite zeigt nach oben (Unity: Cross(b-a, c-a) = Vorderseite)
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            return SaveMesh("WM_Stream", vertices, colors, uvs, triangles);
        }

        private static Mesh BuildPondMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            const int rings = 8;
            const int segments = 40;
            float radius = WorldLayout.PondRadius + 1.4f;
            float waterY = WorldLayout.PondWaterHeight;
            Vector2 center = WorldLayout.PondCenter;

            vertices.Add(new Vector3(center.x, waterY, center.y));
            colors.Add(new Color(DepthFactor(center, waterY), 0f, 0f, 1f));
            uvs.Add(Vector2.zero);
            for (int r = 1; r <= rings; r++)
            {
                float rr = radius * r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    vertices.Add(new Vector3(p.x, waterY, p.y));
                    colors.Add(new Color(DepthFactor(p, waterY), 0f, 0f, 1f));
                    uvs.Add(p - center);
                }
            }

            for (int s = 0; s < segments; s++)
            {
                int a = 1 + s, b = 1 + (s + 1) % segments;
                triangles.Add(0); triangles.Add(b); triangles.Add(a);
            }

            for (int r = 1; r < rings; r++)
            {
                int inner = 1 + (r - 1) * segments, outer = 1 + r * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    int a = inner + s, b = inner + s1, c = outer + s, d = outer + s1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            return SaveMesh("WM_Pond", vertices, colors, uvs, triangles);
        }

        /// <summary>Relative Wassertiefe 0..1 aus der exakten Geländehöhe.</summary>
        private static float DepthFactor(Vector2 p, float waterY)
        {
            float ground = WorldLayout.Height(p.x, p.y);
            return Mathf.Clamp01((waterY - ground) / 0.18f);
        }

        private static Mesh SaveMesh(string name, List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return AssetUtil.ReplaceAsset(mesh, Folder + "/Meshes/" + name + ".asset");
        }
    }
}
