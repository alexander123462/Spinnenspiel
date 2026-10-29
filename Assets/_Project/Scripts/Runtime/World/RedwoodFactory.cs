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

        /// <summary>STAMM1: Die Rinden-Textur ist in v geteilt: die untersten BarkMossBandV
        /// der Kachel sind Mooszone (nur der unterste Stamm-Meter), der Rest ist normale Rinde.
        /// Trunk-UVs (BarkV) und TextureFactory (RedwoodHeight/RedwoodBark) benutzen denselben Wert.</summary>
        public const float BarkMossBandV = 0.2f;

        /// <summary>STAMM1: eine Rinden-Kachel deckt BarkTileH Meter Stammhoehe ab.</summary>
        public const float BarkTileH = 3.2f;

        /// <summary>STAMM1: v-Koordinate der Rinde in Stammhoehe y. Der unterste Meter liegt
        /// ganz in der Mooszone, alles darueber kachelt innerhalb [BarkMossBandV..1] — die
        /// Mooszone wird von der hohen Rinde nie getroffen. Die Rindenhoehenfunktion ist in v
        /// mit Periode (1 - BarkMossBandV) periodisch, deshalb bleibt der Sprung unsichtbar.</summary>
        public static float BarkV(float y)
        {
            if (y < 0f) return 0f;                                  // unter der Erde: gleichgueltig
            if (y < 1f) return y * BarkMossBandV;                   // unterster Meter: Mooszone
            return BarkMossBandV + Mathf.Repeat((y - 1f) / BarkTileH, 1f) * (1f - BarkMossBandV);
        }

        /// <summary>
        /// Rindenrelief des Stamms in Abhaengigkeit vom Umfangswinkel a und der Hoehe y.
        /// STAMM1: flache Platten statt gleichmaessiger Sinusstreifen — Grundfrequenz 5 pro
        /// Umlauf, zweite Frequenz 1.7x davon, und deren Phase springt an jeder Furche
        /// (Hash ueb den Furchen-Index). Dadurch enden und versetzen sich Furchen entlang
        /// des Stamms; der Abstand ist nicht mehr vorhangartig gleich. Alt: Sinus 5 + Sinus 11
        /// = 8 Furchen pro Umlauf, Plateau-Anteil 22 %. Neu: ~26 Furchen, Plateau ~70 %.
        /// </summary>
        public static float TrunkFlute(float a, float y, float phase)
        {
            float x1 = a * 5f + phase + y * 0.09f;
            float x2 = x1 * 1.7f + FurrowJitter(x1, 10);
            float plate = Mathf.Min(1f, Mathf.Abs(Mathf.Sin(x1)) / 0.5f);
            float plate2 = Mathf.Min(1f, Mathf.Abs(Mathf.Sin(x2)) / 0.5f);
            return 1f + 0.05f * (plate + plate2 - 1f)
                       + 0.12f * Mathf.Exp(-Mathf.Max(0f, y) / 1.5f) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 3.5f + phase)), 2f);
        }

        /// <summary>STAMM1: Phasen-Versatz pro Furche, zwischen den Furchen stetig interpoliert
        /// (keine Knicke im Mesh). Der Index laeuft modulo der Halbperioden pro Umlauf — bei
        /// Grundfrequenz 5 sind das 10 — damit TrunkFlute fuer a und a+2*pi denselben Wert liefert
        /// (r103-f3: mit modulo 8 klaffte die Naht am Stamm, Mesh-Radius-Fehler 3,15 %).</summary>
        private static float FurrowJitter(float x, int furrows)
        {
            float t = x / Mathf.PI;
            int k = Mathf.FloorToInt(t);
            float f = Mathf.SmoothStep(0f, 1f, t - (float)k);
            int i0 = ((k % furrows) + furrows) % furrows;
            int i1 = (i0 + 1) % furrows;
            float h = Mathf.Lerp(WorldLayout.Hash01(i0, 3, 41), WorldLayout.Hash01(i1, 3, 41), f);
            return (h - 0.5f) * 1.1f;
        }

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
                    float rr = rad * TrunkFlute(a, y, phase);
                    verts.Add(new Vector3(cx + Mathf.Cos(a) * rr, y, cz + Mathf.Sin(a) * rr));
                    // senkrechte Fasern: viele Wiederholungen um den Stamm; in v sitzt der
                    // unterste Meter in der Mooszone, alles darueber kachelt (BarkV).
                    uvs.Add(new Vector2((float)s / Sides * Mathf.Max(3f, circ / 1.2f), BarkV(y)));
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
