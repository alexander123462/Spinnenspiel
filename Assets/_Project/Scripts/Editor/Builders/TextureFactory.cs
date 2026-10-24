using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Erzeugt kachelbare Material-Texturen prozedural (Erde, Gras, Schlamm, Fels, Rinde, Blatt).
    /// Rauschen wird auf einem Torus gesampelt, damit die Texturen nahtlos kacheln.
    /// </summary>
    public static class TextureFactory
    {
        private const string Folder = AssetUtil.GeneratedRoot + "/Textures";

        public delegate Color PixelFunction(float u, float v);

        /// <summary>Nahtlos kachelndes fBm-Rauschen 0..1.</summary>
        public static float TileNoise(float u, float v, float frequency, int octaves, float seed)
        {
            float sum = 0f, amplitude = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                float angleU = u * Mathf.PI * 2f, angleV = v * Mathf.PI * 2f;
                float r = frequency / (Mathf.PI * 2f);
                // Torus-Projektion auf zwei 2D-Perlin-Ebenen
                float a = Mathf.PerlinNoise(Mathf.Cos(angleU) * r + seed, Mathf.Sin(angleU) * r + seed * 1.3f);
                float b = Mathf.PerlinNoise(Mathf.Cos(angleV) * r + seed * 2.1f, Mathf.Sin(angleV) * r + seed * 0.7f);
                float c = Mathf.PerlinNoise(Mathf.Cos(angleU) * r + Mathf.Sin(angleV) * r + seed * 3.3f, Mathf.Sin(angleU) * r + Mathf.Cos(angleV) * r + seed * 1.9f);
                sum += amplitude * (a * 0.3f + b * 0.3f + c * 0.4f);
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
                seed += 17.3f;
            }

            return sum / norm;
        }

        public static Texture2D Generate(string name, int size, PixelFunction function, bool alpha)
        {
            Texture2D texture = new Texture2D(size, size, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = function(x / (float)size, y / (float)size);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return AssetUtil.SaveTexture(texture, Folder + "/" + name + ".png", false, alpha);
        }

        /// <summary>Normal-Map aus einer Höhenfunktion (Sobel-artige Differenzen).</summary>
        public static Texture2D GenerateNormal(string name, int size, System.Func<float, float, float> height, float strength)
        {
            float[] h = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    h[y * size + x] = height(x / (float)size, y / (float)size);
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float left = h[y * size + (x - 1 + size) % size];
                    float right = h[y * size + (x + 1) % size];
                    float down = h[((y - 1 + size) % size) * size + x];
                    float up = h[((y + 1) % size) * size + x];
                    Vector3 n = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return AssetUtil.SaveTexture(texture, Folder + "/" + name + ".png", true, false);
        }

        public static Texture2D Earth()
        {
            return Generate("T_Earth", 512, (u, v) =>
            {
                float n = TileNoise(u, v, 6f, 5, 1.1f);
                float grit = TileNoise(u, v, 48f, 2, 9.4f);
                Color dark = new Color(0.20f, 0.15f, 0.10f), light = new Color(0.40f, 0.31f, 0.21f);
                Color c = Color.Lerp(dark, light, n);
                return c * (0.85f + 0.3f * grit);
            }, false);
        }

        public static Texture2D Grass()
        {
            return Generate("T_Grass", 512, (u, v) =>
            {
                float n = TileNoise(u, v, 5f, 4, 3.3f);
                float blades = TileNoise(u * 1f, v * 1f, 64f, 2, 7.7f);
                float dry = TileNoise(u, v, 3f, 3, 12.1f);
                Color green = new Color(0.16f, 0.30f, 0.09f), yellow = new Color(0.36f, 0.38f, 0.14f), soil = new Color(0.25f, 0.20f, 0.12f);
                Color c = Color.Lerp(green, yellow, Mathf.Clamp01(dry * 1.4f - 0.3f));
                c = Color.Lerp(soil, c, Mathf.Clamp01(n * 1.6f - 0.1f));
                return c * (0.75f + 0.5f * blades);
            }, false);
        }

        public static Texture2D Mud()
        {
            return Generate("T_Mud", 512, (u, v) =>
            {
                float n = TileNoise(u, v, 5f, 5, 5.5f);
                float puddle = Mathf.Clamp01((TileNoise(u, v, 3f, 3, 8.8f) - 0.55f) * 6f);
                Color mud = Color.Lerp(new Color(0.12f, 0.09f, 0.06f), new Color(0.24f, 0.19f, 0.13f), n);
                return Color.Lerp(mud, new Color(0.08f, 0.07f, 0.05f), puddle * 0.6f);
            }, false);
        }

        public static Texture2D Rock()
        {
            return Generate("T_Rock", 512, (u, v) =>
            {
                float n = TileNoise(u, v, 4f, 6, 2.2f);
                float speck = TileNoise(u, v, 80f, 1, 4.4f);
                float lichen = Mathf.Clamp01((TileNoise(u, v, 6f, 3, 19f) - 0.6f) * 5f);
                Color c = Color.Lerp(new Color(0.30f, 0.29f, 0.27f), new Color(0.55f, 0.53f, 0.49f), n);
                c = Color.Lerp(c, new Color(0.40f, 0.45f, 0.25f), lichen * 0.5f);
                return c * (0.9f + 0.2f * speck);
            }, false);
        }

        public static Texture2D RockNormal()
        {
            return GenerateNormal("T_Rock_N", 512, (u, v) => TileNoise(u, v, 8f, 5, 2.2f), 6f);
        }

        public static Texture2D Bark()
        {
            return Generate("T_Bark", 512, (u, v) =>
            {
                float furrows = TileNoise(u * 1f, v, 24f, 3, 6.1f);
                float streak = Mathf.Abs(Mathf.Sin((u * 18f + TileNoise(u, v, 3f, 2, 1f) * 4f) * Mathf.PI));
                float moss = Mathf.Clamp01((TileNoise(u, v, 4f, 3, 30f) - 0.58f) * 4f);
                Color c = Color.Lerp(new Color(0.13f, 0.09f, 0.06f), new Color(0.35f, 0.27f, 0.19f), furrows * 0.6f + streak * 0.4f);
                return Color.Lerp(c, new Color(0.22f, 0.32f, 0.11f), moss * 0.6f);
            }, false);
        }

        public static Texture2D BarkNormal()
        {
            return GenerateNormal("T_Bark_N", 512, (u, v) =>
                Mathf.Abs(Mathf.Sin((u * 18f + TileNoise(u, v, 3f, 2, 1f) * 4f) * Mathf.PI)) * 0.6f + TileNoise(u, v, 24f, 3, 6.1f) * 0.4f, 5f);
        }

        /// <summary>WALD7: Moos-Atlas — speckled moss greens (hue ~80..110 deg) with an ALPHA gradient
        /// over v (v=0 solid at the Stamm-Fuss, fading to transparent toward v=1). The cutout shape
        /// comes from the texture (noise speckles + fade), so Alpha-Cutout is the right trick here.</summary>
        public static Texture2D Moos()
        {
            return Generate("T_Moos", 512, (u, v) =>
            {
                float blotch = TileNoise(u, v, 6f, 4, 77f);          // moos clumps
                float fine = TileNoise(u, v, 20f, 3, 91f);           // fine structure
                Color deep = new Color(0.16f, 0.38f, 0.13f);         // hue ~125 desat -> moos dark
                Color bright = new Color(0.42f, 0.66f, 0.24f);       // hellgruen, hue ~97 deg
                Color c = Color.Lerp(deep, bright, blotch * 0.7f + fine * 0.3f);
                float fade = Mathf.Clamp01((1f - v) * 1.5f);         // v=0 solid -> v~0.7 gone (out upward)
                float speck = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.62f, blotch * 0.6f + fine * 0.4f));
                return new Color(c.r, c.g, c.b, fade * speck);
            }, true);
        }

        private static float RedwoodHeight(float u, float v)
        {
            // lange senkrechte Fasern (u hoch, v niedrig) + tiefe Furchen
            float fibre = TileNoise(u, v, 2f, 2, 3.3f) * 0.3f + Mathf.PerlinNoise(u * 64f, v * 3f) * 0.7f;
            float ridge = Mathf.Abs(Mathf.Sin((u * 14f + TileNoise(u, v, 2f, 2, 8f) * 2.5f) * Mathf.PI));
            return Mathf.Clamp01(fibre * 0.55f + ridge * 0.45f);
        }

        /// <summary>WALD4: Mammutbaum-Rinde, rotbraun (Farbton ca. 12..25 Grad), senkrechte Fasern, dunkle Furchen.</summary>
        public static Texture2D RedwoodBark()
        {
            return Generate("T_RedwoodBark", 512, (u, v) =>
            {
                float h = RedwoodHeight(u, v);
                float patch = TileNoise(u, v, 3f, 3, 21f);
                Color dark = new Color(0.10f, 0.06f, 0.045f);
                Color mid = new Color(0.26f, 0.15f, 0.10f);
                Color light = new Color(0.40f, 0.26f, 0.19f);
                Color c = h < 0.5f ? Color.Lerp(dark, mid, h * 2f) : Color.Lerp(mid, light, (h - 0.5f) * 2f);
                c = Color.Lerp(c, new Color(0.28f, 0.25f, 0.20f), Mathf.Clamp01((patch - 0.5f) * 3f) * 0.6f); // graue Flecken
                return Color.Lerp(c, new Color(0.22f, 0.34f, 0.12f), Mathf.Clamp01((TileNoise(u, v, 5f, 3, 50f) - 0.64f) * 4f) * 0.35f); // Moosahnung
            }, false);
        }

        public static Texture2D RedwoodBarkNormal()
        {
            return GenerateNormal("T_RedwoodBark_N", 512, RedwoodHeight, 7f);
        }

        /// <summary>WALD5: Dach-Karte, dichte dunkelgruene Nadel-/Blattbueschel mit Alpha.</summary>
        public static Texture2D CanopyLeaves()
        {
            return Generate("T_CanopyLeaves", 512, (u, v) =>
            {
                float alpha = 0f, shade = 0f;
                for (int i = 0; i < 46; i++)
                {
                    float cx = 0.08f + 0.84f * WorldLayout.Hash01(i, 3, 41);
                    float cy = 0.08f + 0.84f * WorldLayout.Hash01(i, 7, 43);
                    float angle = WorldLayout.Hash01(i, 5, 47) * Mathf.PI * 2f;
                    float dx = u - cx, dy = v - cy;
                    float lx = dx * Mathf.Cos(angle) + dy * Mathf.Sin(angle);
                    float ly = -dx * Mathf.Sin(angle) + dy * Mathf.Cos(angle);
                    float leaf = (lx * lx) / (0.11f * 0.11f) + (ly * ly) / (0.03f * 0.03f);
                    if (leaf < 1f)
                    {
                        alpha = 1f;
                        shade = Mathf.Max(shade, 1f - leaf * 0.4f - Mathf.Abs(ly) * 4f);
                    }
                }
                Color c = Color.Lerp(new Color(0.07f, 0.20f, 0.06f), new Color(0.22f, 0.45f, 0.14f), Mathf.Clamp01(shade));
                c.a = alpha;
                return c;
            }, true);
        }

        /// <summary>KLEE1/KLEE2: helles Kleeblatt (3 Herz-Lappen) mit Alpha fuer den Cutout-Teppich.
        /// Die Mitte (0.28..0.72) ist immer undurchsichtig: CloverFactory-UVs liegen dort.
        /// KLEE2: heller Mittelstreifen mit V-Zeichnung (wie bei echtem Sauerklee), dunklerer Rand.</summary>
        public static Texture2D CloverLeaf()
        {
            return Generate("T_Clover_Leaf", 256, (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.42f;
                // Herz-Kontur in Polarkoordinaten (Kerze nach oben).
                float ang = Mathf.Atan2(dy, dx);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float theta = Mathf.Abs(ang - Mathf.PI * 0.5f);
                if (theta > Mathf.PI) theta = 2f * Mathf.PI - theta;
                float rLeaf = 0.42f * (1f - 0.38f * Mathf.Exp(-(theta * theta) / (0.34f * 0.34f)));
                float a = r < rLeaf ? 1f : 0f;
                // KLEE2: V-Zeichnung (Chevron) als heller Streifen um den Blattmittelpunkt.
                float vLine = Mathf.Abs(dy + 0.55f * Mathf.Abs(dx) - 0.02f);
                float chevron = Mathf.Exp(-(vLine * vLine) / 0.0016f) * (dy > -0.30f ? 1f : 0.2f);
                // Heller Mittelstreifen entlang der Hauptachse.
                float mid = Mathf.Exp(-(dx * dx) / 0.0018f) * (dy > -0.05f ? 1f : 0.35f);
                // KLEE2: dunklerer Rand zur Blattkontur hin.
                float edgeDark = 1f - 0.35f * Mathf.Clamp01((r / rLeaf - 0.6f) / 0.4f);
                float n = Mathf.PerlinNoise(u * 9f, v * 9f);
                Color c = Color.Lerp(new Color(0.13f, 0.32f, 0.08f), new Color(0.45f, 0.68f, 0.30f), n * 0.7f + 0.15f);
                c = Color.Lerp(c, new Color(0.62f, 0.80f, 0.48f), Mathf.Clamp01(chevron * 0.8f + mid * 0.5f));
                c *= edgeDark;
                c.a = a;
                return c;
            }, true);
        }

        /// <summary>Blatt-Atlas: mehrere Blätter mit Alpha auf einer Karte (für Busch- und Baumkarten).</summary>
        public static Texture2D LeafCluster()
        {
            return Generate("T_LeafCluster", 512, (u, v) =>
            {
                float alpha = 0f;
                float shade = 0f;
                for (int i = 0; i < 9; i++)
                {
                    float cx = 0.2f + 0.6f * WorldLayout.Hash01(i, 3, 11);
                    float cy = 0.2f + 0.6f * WorldLayout.Hash01(i, 7, 13);
                    float angle = WorldLayout.Hash01(i, 5, 17) * Mathf.PI * 2f;
                    float dx = u - cx, dy = v - cy;
                    float lx = dx * Mathf.Cos(angle) + dy * Mathf.Sin(angle);
                    float ly = -dx * Mathf.Sin(angle) + dy * Mathf.Cos(angle);
                    float leaf = (lx * lx) / (0.18f * 0.18f) + (ly * ly) / (0.075f * 0.075f);
                    if (leaf < 1f)
                    {
                        alpha = 1f;
                        shade = Mathf.Max(shade, 1f - leaf * 0.5f - Mathf.Abs(ly) * 2f);
                    }
                }

                Color c = Color.Lerp(new Color(0.10f, 0.20f, 0.05f), new Color(0.30f, 0.46f, 0.14f), Mathf.Clamp01(shade));
                c.a = alpha;
                return c;
            }, true);
        }

        /// <summary>Hinterleib-Muster: dunkles Braun mit hellerem Mittelband und Winkelflecken (Wolfsspinnen-ähnlich).</summary>
        public static Texture2D SpiderAbdomen()
        {
            return Generate("T_SpiderAbdomen", 256, (u, v) =>
            {
                float fine = TileNoise(u, v, 32f, 3, 61f);
                float band = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 9f);
                float chevron = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat(v * 5f + Mathf.Abs(u - 0.5f) * 3f, 1f) - 0.5f) * 8f) * Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 4f);
                Color dark = new Color(0.10f, 0.07f, 0.05f), light = new Color(0.42f, 0.33f, 0.22f);
                Color c = Color.Lerp(dark, light, Mathf.Max(band * 0.8f, chevron * 0.6f));
                return c * (0.8f + 0.4f * fine);
            }, false);
        }

        /// <summary>Bein-Ringelung entlang V (Capsule-UV läuft entlang der Länge).</summary>
        public static Texture2D SpiderLegBands()
        {
            return Generate("T_SpiderLegBands", 128, (u, v) =>
            {
                float bands = Mathf.SmoothStep(0f, 1f, Mathf.Abs(Mathf.Sin(v * Mathf.PI * 3f)));
                float fine = TileNoise(u, v, 24f, 2, 71f);
                Color c = Color.Lerp(new Color(0.34f, 0.26f, 0.17f), new Color(0.09f, 0.065f, 0.045f), bands);
                return c * (0.85f + 0.3f * fine);
            }, false);
        }

        public static Texture2D SoftCircle()
        {
            return Generate("T_SoftCircle", 64, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a);
            }, true);
        }

        public static Texture2D WaterNormal()
        {
            return GenerateNormal("T_Water_N", 256, (u, v) => TileNoise(u, v, 6f, 4, 42f), 3f);
        }
    }
}
