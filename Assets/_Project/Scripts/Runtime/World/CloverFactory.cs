using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// KLEE1/KLEE2: prozeduraler Klee/Sauerklee (CloverFactory) fuer den Klee-Bodenteppich.
    /// Mesh-Einheit: Hoehe 1 (Stiel auf y=0, Blattdach bei y~1), Breite ~0.95.
    /// Der Generator skaliert auf 5..9 cm (scale = Zielhoehe) -> Blaetter 4..9 cm breit.
    /// KLEE2: jedes der 3 Teilblaetter ist eine gewoelbte Herzflaeche aus zwei Ringen
    /// (21 Dreiecke je Teilblatt, Mitte gegenueber dem Rand angehoben) statt eines
    /// flachen Faechers — das nimmt den "flache kantige Platten"-Look vom Player-Foto.
    /// Alpha-Cutout-Textur T_Clover_Leaf auf Blatt-Faechern; alle Blatt-UVs liegen in der
    /// undurchsichtigen Mitte (0.28..0.72), werden also nie weggeschnitten.
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

        /// <summary>KLEE2: ein Herz-Teilblatt als gewoelbte Flaeche aus zwei Ringen.
        /// Herzkontur R(t)=L*(1-0.38*exp(-(theta/0.34)^2)) mit Kerze zur Spitze; der innere
        /// Ring liegt bei 55 % des Konturradius, die Blattmitte ist um 'cup' ueber dem Rand
        /// angehoben (Schuessel- Woelbung). Blattlokal: +X = aussen, +Y = quer; dann um tilt
        /// um die Aussenachse heben und um yaw um Welt-Y drehen. 21 Dreiecke je Teilblatt.</summary>
        private static void AddLeaflet(List<Vector3> verts, List<Vector2> uvs, Vector3 root, float yaw, float tilt, float L)
        {
            const int seg = 9;          // Aeussere Ringe mit 9 Punkten (Kerze mittig)
            const int inner = 7;        // Innerer Ring mit 7 Punkten
            const float cup = 0.16f;    // Woelbung: Mitte ueber dem Rand
            Vector3 up = new Vector3(0f, 1f, 0f);

            // Blattlokal berechnen (u = Konturanteil 0..1, t = Winkelparameter -1..1),
            // am Ende gemeinsam um tilt (Aussenachse) und yaw (Welt-Y) drehen.
            var lp = new List<Vector3>();     // blaetterlokale Positionen
            var lv = new List<Vector2>();     // UVs (alle garantiert in der undurchsichtigen Blattmitte)

            Vector3 center = LocalPoint(L, 0.3f, 0f, cup);
            lp.Add(center); lv.Add(LeafUv(0.3f, 0f));

            for (int i = 0; i < inner; i++)
            {
                float t = -1f + 2f * i / (inner - 1);
                Vector3 p = LocalPoint(L, 0.55f, t, cup * Dome(0.55f));
                lp.Add(p); lv.Add(LeafUv(0.55f, t));
            }
            for (int i = 0; i <= seg; i++)
            {
                float t = -1f + 2f * i / seg;
                Vector3 p = LocalPoint(L, 1f, t, 0f);
                lp.Add(p); lv.Add(LeafUv(1f, t));
            }

            // In Weltlage bringen: tilt hebt die Blattspitze an (Rechts um Aussenachse), dann Gieren um Welt-Y.
            Vector3 axis = Vector3.Cross(new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)), up);
            var wp = new List<Vector3>(lp.Count);
            foreach (Vector3 q in lp)
                wp.Add(root + Quaternion.AngleAxis(tilt * Mathf.Rad2Deg, axis) * q);

            // Dreiecke: Mitte -> innerer Faecher, innerer Ring -> Randstreifen.
            void V(int i) { verts.Add(wp[i]); uvs.Add(lv[i]); }
            for (int i = 0; i < inner; i++)
            {
                V(0); V(1 + i); V(1 + (i + 1) % inner);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = 1 + i % inner, b = 1 + (i + 1) % inner;
                int oa = 1 + inner + i, ob = 1 + inner + i + 1;
                V(a); V(b); V(oa);
                V(b); V(ob); V(oa);
            }
        }

        /// <summary>Blattlokaler Punkt der Herzkontur: u = Konturanteil (0.55 innen, 1 Rand),
        /// t = Winkelparameter (-1..1, Kerze bei t=0), lift = Hoehe ueber der Blattebene.</summary>
        private static Vector3 LocalPoint(float L, float u, float t, float lift)
        {
            float theta = t * 1.25f;
            float rContour = L * (1f - 0.38f * Mathf.Exp(-(theta * theta) / (0.34f * 0.34f)));
            float r = u * rContour;
            float lx = r * Mathf.Cos(theta);   // aussen
            float ly = r * Mathf.Sin(theta);   // quer
            return new Vector3(lx, lift, ly);
        }

        /// <summary>Schuesselprofil 1 (Mitte) .. 0 (Rand): quadratischer Abfall.</summary>
        private static float Dome(float u) { return (1f - u) * (1f - u); }

        /// <summary>UV eines Punkts bei Konturanteil u und Winkel t, in der undurchsichtigen
        /// Blattmitte der Textur (0.40..0.60, safe gegenueber dem Cutout-Bereich ab 0.28/0.72).</summary>
        private static Vector2 LeafUv(float u, float t)
        {
            return new Vector2(0.5f + 0.18f * u, 0.5f + 0.18f * t);
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
