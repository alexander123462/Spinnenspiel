using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Prozedurale Mammutbaum-Teile (Referenz Docs/referenz/WALD-ZIEL.md): hohe, gerade, nach oben
    /// schlanker werdende Staemme mit Wurzelanlauf und Rindenfurchen, dazu Blattbuescheldach-Karten.
    /// Reiner Code, deterministisch pro Seed, keine Assets noetig (Material kommt aus der Library).
    /// </summary>
    public static class RedwoodFactory
    {
        public const int Sides = 20;

        /// <summary>Stammradius auf Hoehe y (ohne Rippen), gleiche Formel wie im Mesh.</summary>
        public static float RadiusAt(float y, float height, float baseRadius, float topRadius)
        {
            float t = Mathf.Clamp01(y / height);
            return Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(t, 0.75f)) + baseRadius * 0.3f * Mathf.Exp(-Mathf.Max(0f, y) / 1.4f);
        }

        /// <summary>Stamm-Mesh, Ursprung am Fuss (y=0 = Boden, Mesh reicht 4 m darunter (Hang)).</summary>
        public static Mesh Trunk(int seed, float height, float baseRadius, float topRadius)
        {
            var rnd = new System.Random(seed);
            float phase = (float)rnd.NextDouble() * 10f;
            float lean = ((float)rnd.NextDouble() - 0.5f) * 0.02f; // fast senkrecht
            int rings = Mathf.CeilToInt(height / 2f) + 1;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float circ = Mathf.PI * 2f * baseRadius;
            for (int r = 0; r <= rings; r++)
            {
                float y = -4f + (height + 4f) * r / rings;
                float t = Mathf.Clamp01(y / height);
                float rad = Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(t, 0.75f));
                // Wurzelanlauf: unten bis zu +70 % breiter, klingt in ca. 3 m ab
                rad += baseRadius * 0.3f * Mathf.Exp(-Mathf.Max(0f, y) / 1.4f);
                float cx = lean * y * 10f, cz = lean * y * 7f;
                for (int s = 0; s <= Sides; s++)
                {
                    float a = (float)s / Sides * Mathf.PI * 2f;
                    // Furchen/Rippen der Rinde + Wurzelrippen unten
                    float flute = 1f + 0.035f * Mathf.Sin(a * 5f + phase + y * 0.09f)
                                     + 0.02f * Mathf.Sin(a * 11f + phase * 2f + y * 0.2f)
                                     + 0.12f * Mathf.Exp(-Mathf.Max(0f, y) / 1.5f) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 3.5f + phase)), 2f);
                    float rr = rad * flute;
                    verts.Add(new Vector3(cx + Mathf.Cos(a) * rr, y, cz + Mathf.Sin(a) * rr));
                    // senkrechte Fasern: viele Wiederholungen um den Stamm, lang gestreckt in v
                    uvs.Add(new Vector2((float)s / Sides * Mathf.Max(3f, circ / 1.2f), y / 3.2f));
                }
            }
            int row = Sides + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int i0 = r * row + s, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }
            var m = new Mesh { name = "RW_Trunk" + seed };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Blattbueschel fuers Kronendach: 14 geneigte, leicht gebogene Karten um einen Mittelpunkt (ca. 6 m).</summary>
        public static Mesh CanopyCluster(int seed)
        {
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                float yaw = (float)rnd.NextDouble() * 360f;
                float pitch = -25f + (float)rnd.NextDouble() * 50f;
                float size = 4.5f + (float)rnd.NextDouble() * 2.5f;
                Vector3 off = new Vector3(((float)rnd.NextDouble() - 0.5f) * 5f, ((float)rnd.NextDouble() - 0.5f) * 2.5f, ((float)rnd.NextDouble() - 0.5f) * 5f);
                Quaternion q = Quaternion.Euler(pitch, yaw, 0f);
                // Karte liegt ungefaehr waagrecht (Normale +Y), Neigung per pitch
                Quaternion flat = q * Quaternion.Euler(90f, 0f, 0f);
                int b = verts.Count;
                float h = size * 0.5f;
                verts.Add(off + flat * new Vector3(-h, -h, 0f));
                verts.Add(off + flat * new Vector3(h, -h, 0f));
                verts.Add(off + flat * new Vector3(-h, h, 0f));
                verts.Add(off + flat * new Vector3(h, h, 0f));
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
            var m = new Mesh { name = "RW_Canopy" + seed };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
