using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Meilenstein 7 – Urwald: Seed-basierte, deterministische Weltbeschreibung.
    /// Reine C#-Klasse (kein MonoBehaviour, kein UnityEngine.Random):
    /// nur System.Random(seed) für den Zufallsversatz und Mathf.PerlinNoise.
    /// </summary>
    public class JungleWorld
    {
        /// <summary>Ein einziger Wasserspiegel für alle Sümpfe.</summary>
        public const float WaterLevel = 1.0f;

        /// <summary>Maximale Gelände-Höhe (Meter).</summary>
        public const float MaxHeight = 12f;

        private const float EdgeWidth = 6f;             // äußere 6 m steigen sanft an
        private const float EdgeRise = 2.0f;            // Höhenanstieg am Rand
        private const float BaseMin = WaterLevel + 0.3f; // außerhalb der Sumpfe immer ≥ 1.3 m
        private const float SwampBand = 0.14f;           // Rampen-Breite der Sumpf-Schwelle
        private const float ClearBand = 0.13f;           // Rampen-Breite der Lichtungs-Schwelle

        public int Seed { get; }
        public float Size { get; }

        private readonly float _offBaseX, _offBaseZ;
        private readonly float _offSwampX, _offSwampZ;
        private readonly float _offIslandX, _offIslandZ;
        private readonly float _offClearX, _offClearZ;

        // Per-Seed kalibrierte Schwellwerte (Konstruktor), damit jede Welt
        // garantierte Sümpfe und Lichtungen hat.
        private readonly float _swampT;
        private readonly float _clearT;

        public JungleWorld(int seed, float size = 96f)
        {
            Seed = seed;
            Size = size;

            // Seed-abhängige, deterministische Perlin-Versatzpunkte
            var rnd = new System.Random(seed);
            _offBaseX = (float)rnd.NextDouble() * 1000f;
            _offBaseZ = (float)rnd.NextDouble() * 1000f;
            _offSwampX = (float)rnd.NextDouble() * 1000f;
            _offSwampZ = (float)rnd.NextDouble() * 1000f;
            _offIslandX = (float)rnd.NextDouble() * 1000f;
            _offIslandZ = (float)rnd.NextDouble() * 1000f;
            _offClearX = (float)rnd.NextDouble() * 1000f;
            _offClearZ = (float)rnd.NextDouble() * 1000f;

            // Kalibrierung: Schwellwerte per Binärsuche so setzen, dass die Welt
            // (auf dem 96x96-Raster) genau ~15 % Unterwasser und ~12 % Lichtung hat.
            // Damit hat JEDER Seed spürbare Sümpfe und Lichtungen.
            _swampT = CalibrateThreshold(t => WaterFractionAt(t), 0.15f);
            _clearT = CalibrateThreshold(t => ClearingFractionAt(t), 0.12f);
        }

        /// <summary>Gelände-Höhe in Metern.</summary>
        public float Height(float x, float z)
        {
            return HeightAt(x, z, _swampT);
        }

        private float HeightAt(float x, float z, float swampT)
        {
            // fBm aus 4 Oktaven Perlin (Grundfrequenz ~1/40 m) → sanfte Hügel
            float sum = 0f, amp = 1f, norm = 0f;
            float freq = 1f / 40f;
            for (int i = 0; i < 4; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + _offBaseX, z * freq + _offBaseZ) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            float n = sum / norm; // ~0..1
            float h = BaseMin + n * (MaxHeight - BaseMin);

            // Sumpf: Gelände senkt sich auf WaterLevel − 0.15 … − 0.5
            float sw = SwampFactorAt(x, z, swampT);
            if (sw > 1e-4f)
            {
                float isl = Mathf.PerlinNoise(x * 0.12f + _offIslandX, z * 0.12f + _offIslandZ);
                float depth = WaterLevel - 0.15f - 0.35f * Mathf.Clamp01((isl - 0.35f) / 0.65f);
                h = Mathf.Lerp(h, depth, sw);

                // Kleine Inseln im Sumpf: hoch genug, wieder über Wasser
                float lift = Mathf.Clamp01((isl - 0.72f) / 0.28f);
                h = Mathf.Lerp(h, WaterLevel + 0.2f + lift * 0.6f, lift * sw);
            }

            // Rand der Welt (äußere 6 m) steigt sanft an
            float half = Size * 0.5f;
            float distEdge = half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float edgeT = 1f - Mathf.Clamp01(distEdge / EdgeWidth);
            h += edgeT * edgeT * EdgeRise;
            return h;
        }

        /// <summary>Sumpffaktor 0..1 (eigenes Perlin, ~1/30 m, weiche Schwelle).</summary>
        public float SwampFactor(float x, float z)
        {
            return SwampFactorAt(x, z, _swampT);
        }

        private float SwampFactorAt(float x, float z, float swampT)
        {
            return Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((SwampCore(x, z) - swampT) / SwampBand));
        }

        /// <summary>True, wenn der Punkt unter dem Wasserspiegel liegt.</summary>
        public bool IsUnderWater(float x, float z)
        {
            return Height(x, z) < WaterLevel;
        }

        /// <summary>Pflanzendichte 0..1: grundsätzlich 0.7–1.0, in Lichtungen 0.1–0.2.</summary>
        public float Density(float x, float z)
        {
            return DensityAt(x, z, _clearT);
        }

        private float DensityAt(float x, float z, float clearT)
        {
            float baseD = 0.7f + 0.3f * Mathf.PerlinNoise(
                x / 12f + _offBaseX + 50f, z / 12f + _offBaseZ + 50f);

            // Lichtungen (Perlin ~1/25 m, kalibrierte Schwelle) senken die Dichte
            float clearing = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((ClearCore(x, z) - clearT) / ClearBand));
            return Mathf.Lerp(baseD, 0.15f, clearing);
        }

        /// <summary>True, wenn der Punkt eine Lichtung ist.</summary>
        public bool IsClearing(float x, float z)
        {
            return Density(x, z) < 0.3f;
        }

        /// <summary>Nässe 0..1: hoch nahe/unter Wasser, sonst niedrig mit leichtem Rauschen.</summary>
        public float Wetness(float x, float z)
        {
            float h = Height(x, z);
            float w = Mathf.Clamp01(1f - (h - WaterLevel));
            float noise = 0.08f * (Mathf.PerlinNoise(
                x * 0.9f + _offIslandX, z * 0.9f + _offIslandZ) - 0.5f);
            return Mathf.Clamp01(w + noise);
        }

        /// <summary>Seed-abhängiger, deterministischer Hash-Wert 0..1.</summary>
        public float Hash01(int x, int z, int salt)
        {
            long n = (long)x * 374761393L
                   + (long)z * 668265263L
                   + (long)salt * 1442695041L
                   + (long)Seed * 977L;
            n = (n ^ (n >> 13)) * 1274126177L;
            n ^= n >> 16;
            return (n & 0x7FFFFFFF) / 2147483647f;
        }

        /// <summary>
        /// Spawnpunkt: Raster-Suche (1 m) ab Weltmitte spiralförmig nach außen –
        /// erster Punkt, der trocken (≥ WaterLevel + 0.4), flach (±1 m &lt; 0.3 m)
        /// und keine Lichtung ist.
        /// </summary>
        public Vector3 FindSpawnPoint()
        {
            int half = Mathf.FloorToInt(Size * 0.5f);
            for (int r = 0; r <= half; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        float x = Size * 0.5f + dx, z = Size * 0.5f + dz;
                        if (!SpawnOk(x, z)) continue;
                        return new Vector3(x, Height(x, z), z);
                    }
                }
            }

            // Fallback (darf praktisch nie vorkommen): Weltmitte
            return new Vector3(Size * 0.5f, Height(Size * 0.5f, Size * 0.5f), Size * 0.5f);
        }

        private bool SpawnOk(float x, float z)
        {
            if (IsClearing(x, z)) return false;

            float h = Height(x, z);
            if (h < WaterLevel + 0.4f) return false;

            float maxD = Mathf.Max(
                Mathf.Abs(h - Height(x + 1f, z)),
                Mathf.Abs(h - Height(x - 1f, z)),
                Mathf.Abs(h - Height(x, z + 1f)),
                Mathf.Abs(h - Height(x, z - 1f)));
            return maxD < 0.3f;
        }

        // ---------- Kalibrierung (einmalig im Konstruktor, deterministisch) ----------

        /// <summary>Roher Sumpf-Wert (Perlin ~1/30 m), ohne Schwellwert.</summary>
        private float SwampCore(float x, float z)
        {
            return Mathf.PerlinNoise(x / 30f + _offSwampX, z / 30f + _offSwampZ);
        }

        /// <summary>Roher Lichtungs-Wert (Perlin ~1/25 m), ohne Schwellwert.</summary>
        private float ClearCore(float x, float z)
        {
            return Mathf.PerlinNoise(x / 25f + _offClearX, z / 25f + _offClearZ);
        }

        /// <summary>
        /// Binärsuche des Schwellwerts, sodass genau <paramref name="target"/> des
        /// Welt-Rasters die Bedingung erfüllt (der Anteil sinkt monoton mit dem
        /// Schwellwert). Bewertung auf dem 96x96-Weltraster.
        /// </summary>
        private static float CalibrateThreshold(System.Func<float, float> fraction, float target)
        {
            float lo = 0.0f, hi = 1.0f; // invariant: fraction(lo) > target > fraction(hi)
            for (int i = 0; i < 22; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (fraction(mid) > target) lo = mid; // zu viel Sumpf/Lichtung -> Schwellwert erhöhen
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>Anteil der Welt-Rasterpunkte unter dem Wasserspiegel (bei gegebenem Sumpf-Schwellwert).</summary>
        private float WaterFractionAt(float swampT)
        {
            int half = Mathf.FloorToInt(Size * 0.5f);
            int total = 0, wet = 0;
            for (int x = -half; x < half; x++)
            {
                for (int z = -half; z < half; z++)
                {
                    total++;
                    if (HeightAt(x, z, swampT) < WaterLevel) wet++;
                }
            }
            return (float)wet / total;
        }

        /// <summary>Anteil der Welt-Rasterpunkte, die Lichtung sind (bei gegebenem Lichtungs-Schwellwert).</summary>
        private float ClearingFractionAt(float clearT)
        {
            int half = Mathf.FloorToInt(Size * 0.5f);
            int total = 0, open = 0;
            for (int x = -half; x < half; x++)
            {
                for (int z = -half; z < half; z++)
                {
                    total++;
                    if (DensityAt(x, z, clearT) < 0.3f) open++;
                }
            }
            return (float)open / total;
        }
    }
}
