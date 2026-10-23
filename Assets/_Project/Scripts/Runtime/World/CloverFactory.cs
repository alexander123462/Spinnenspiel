using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// KLEE1: prozeduraler Klee/Sauerklee (CloverFactory) fuer den Klee-Bodenteppich.
    /// Mesh-Einheit: Hoexhe 1 (Stiel auf y=0, Blattdach bei y~1), Breite ~1.4.
    /// Der Generator skaliert auf 5..12 cm (scale = Zielhoehe). Seed-deterministisch.
    /// Alpha-Cutout-Textur T_Clover_Leaf (ein Kleeblatt zentriert) auf Blatt-Faechern,
    /// Stiel-UVs liegen mitten in der undurchsichtigen Blattflaeche (wird nie weggeschnitten).
    /// </summary>
    public static class CloverFactory
    {
        /// <summary>Baut EIN Klee-Pflaenzchen (nicht indexiert, fur CombineMeshes geeignet).</summary>
        public static Mesh Clover(int seed)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();

            float lean = (Hash(seed, 1) - 0.5f) * 0.3f;   // leichtes Lehnen des Stiels
            float leanZ = (Hash(seed, 2) - 0.5f) * 0.3f;
            float stemH = 0.55f + Hash(seed, 3) * 0.2f;    // Stiel bis dorthin, Blattdach bis ~1

            // Stiel: duennes, vertikales Quad (2 Dreiecke), UV im undurchsichtigen Blattzentrum.
            const float sw = 0.035f;
            Vector3 s0 = new Vector3(-sw, 0f, 0f), s1 = new Vector3(sw, 0f, 0f);
            Vector3 s2 = new Vector3(sw + lean, stemH, leanZ), s3 = new Vector3(-sw + lean, stemH, leanZ);
            Vector2 uvSolid = new Vector2(0.5f, 0.5f);
            AddTri(verts, uvs, s0, s1, s2, uvSolid, uvSolid, uvSolid);
            AddTri(verts, uvs, s0, s2, s3, uvSolid, uvSolid, uvSolid);

            // 3 Herz-Blaettchen obenauf, nach aussen geneigt (Teppich: fast horizontal).
            Vector3 top = new Vector3(lean, stemH, leanZ);
            for (int k = 0; k < 3; k++)
            {
                float yaw = (k / 3f) * Mathf.PI * 2f + Hash(seed, 10 + k) * 0.9f;
                float tilt = Mathf.Deg2Rad * (20f + Hash(seed, 20 + k) * 20f); // 20..40 Grad Aufwaerts
                AddLeaflet(verts, uvs, top, yaw, tilt, 0.5f + Hash(seed, 30 + k) * 0.2f);
            }

            var mesh = new Mesh { name = "Clover_" + seed };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            var idx = new int[verts.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ein Herz-Blaettchen als Faecher: Herzradius R(t)=L*(1-0.38*exp(-(theta/0.34)^2))
        /// mit Kerze zur Spitze. Blattlokal: +X = aussen, +Y = quer; dann um tilt um die
        /// Aussenachse heben und um yaw um Welt-Y drehen.</summary>
        private static void AddLeaflet(List<Vector3> verts, List<Vector2> uvs, Vector3 root, float yaw, float tilt, float L)
        {
            const int seg = 9;                 // 9 Segmente, Kerze mittig
            int baseIdx = verts.Count;
            verts.Add(root);
            uvs.Add(new Vector2(0.5f, 0.42f)); // Blattansatz: undurchsichtige Zone
            for (int i = 0; i <= seg; i++)
            {
                float t = -1f + 2f * i / seg;              // -1..1
                float theta = t * 1.25f;                   // +-72 Grad
                float r = L * (1f - 0.38f * Mathf.Exp(-(theta * theta) / (0.34f * 0.34f)));
                float lx = r * Mathf.Cos(theta);           // aussen
                float ly = r * Mathf.Sin(theta);           // quer
                // Heben (tilt um Aussenachse), dann Gieren um Welt-Y.
                float y = lx * Mathf.Sin(tilt) + 0.04f;
                float xz = lx * Mathf.Cos(tilt);
                Vector3 p = root + new Vector3(xz * Mathf.Cos(yaw), y, xz * Mathf.Sin(yaw))
                    + new Vector3(-ly * Mathf.Sin(yaw), 0f, ly * Mathf.Cos(yaw));
                verts.Add(p);
                // UV: Herzblatt mittig in der Blattflaeche der Textur (fest, nie Cutout-Rand).
                uvs.Add(new Vector2(0.5f + 0.22f * t, 0.5f + 0.2f * Mathf.Abs(t)));
            }
            for (int i = 0; i < seg; i++)
            {
                AddTri(verts, uvs,
                    verts[baseIdx], verts[baseIdx + 1 + i], verts[baseIdx + 2 + i],
                    uvs[baseIdx], uvs[baseIdx + 1 + i], uvs[baseIdx + 2 + i]);
            }
        }

        private static void AddTri(List<Vector3> verts, List<Vector2> uvs,
            Vector3 a, Vector3 b, Vector3 c, Vector2 ta, Vector2 tb, Vector2 tc)
        {
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(ta); uvs.Add(tb); uvs.Add(tc);
        }

        private static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 2654435761u ^ (uint)b * 40503u ^ 0x9E3779B9u;
                h ^= h >> 16; h *= 0x7EB35B29u; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
