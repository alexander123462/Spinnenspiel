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

        // W1: Bachlauf – ein Meander-Polyline per Seed, die eem Terrain einkallet
        public const float CreekCore = 2.2f;             // Halbwatie der Bach-Grundkoerper (m)
        private const float CreekRamp = 1.6f;            // Rampen-Breite zum Ufer (m)
        private const float CreekBedDepth = 0.35f;       // Bachgrund unter WaterLevel (m)

        public int Seed { get; }
        public float Size { get; }

        private readonly float _offBaseX, _offBaseZ;
        private readonly float _offSwampX, _offSwampZ;
        private readonly float _offIslandX, _offIslandZ;
        private readonly float _offClearX, _offClearZ;

        // W1: Bachlauf as Meander-Polyline across the world (built in ctor from the seed)
        private readonly Vector2[] _creekPts;
        public Vector2[] CreekPath { get => _creekPts; }

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

            // W1: Bachlauef-Polyline per Seed (Meander across the world, x = 0..Size)
            _creekPts = BuildCreek(rnd);

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

            // W1: Bachlauef einkallet – aber NOT within the outer 6 m border band,
            // so the rim rise (and tree/liana anchors there) stay stable. Use TRUE
            // distance to the world border (world spans x,z in [0,Size]).
            float distBorder = Mathf.Min(Mathf.Min(x, z), Mathf.Min(Size - x, Size - z));
            if (distBorder > EdgeWidth)
                h = CreekModulate(h, CreekDist(x, z));
            return h;
        }

        /// <summary>Sumpffaktor 0..1 (eigenes Perlin, ~1/30 m, weiche Schwelle).</summary>
        public float SwampFactor(float x, float z)
        {
            // W1: ein Bachlauef ist Feuchtland too -> near the creek the ground is
            // intentionally below WaterLevel, so it must count as swamp/wetland and
            // not as a "dry point that is unexpectedly wet".
            float creekWet = 1f - Mathf.Clamp01((CreekDist(x, z) - CreekCore) / CreekRamp);
            return Mathf.Max(SwampFactorAt(x, z, _swampT), creekWet);
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
            if (CreekDist(x, z) < CreekCore + 0.8f) return false; // no spawn in/on the creek

            float h = Height(x, z);
            if (h < WaterLevel + 0.4f) return false;

            float maxD = Mathf.Max(
                Mathf.Abs(h - Height(x + 1f, z)),
                Mathf.Abs(h - Height(x - 1f, z)),
                Mathf.Abs(h - Height(x, z + 1f)),
                Mathf.Abs(h - Height(x, z - 1f)));
            return maxD < 0.3f;
        }

        // ---------- W1: Bachlauef ----------

        /// <summary>
        /// Meander-Polyline per Seed: ~8 Waypoints across the world (x = 0..Size),
        /// z jittered. Deterministic from the System.Random stream of the Seed.
        /// </summary>
        private Vector2[] BuildCreek(System.Random rnd)
        {
            const int pts = 9;
            var path = new Vector2[pts];
            float z = Size * (0.3f + 0.4f * (float)rnd.NextDouble()); // Start-z zufaellig in the middle band
            for (int i = 0; i < pts; i++)
            {
                float x = Size * i / (pts - 1);
                if (i > 0 && i < pts - 1)
                    z += (float)rnd.NextDouble() * 2f - 1f;            // slight drift (m)
                z = Mathf.Clamp(z, Size * 0.15f, Size * 0.85f);        // stay inside the world
                path[i] = new Vector2(x, z);
            }
            return path;
        }

        /// <summary>Distance from (x,z) to the creek polyline (m).</summary>
        private float CreekDist(float x, float z)
        {
            float best = 1e9f;
            for (int i = 0; i + 1 < _creekPts.Length; i++)
            {
                Vector2 a = _creekPts[i], b = _creekPts[i + 1];
                Vector2 ab = b - a;
                float lenSq = ab.x * ab.x + ab.y * ab.y;
                float t = lenSq > 1e-6f ? ((x - a.x) * ab.x + (z - a.y) * ab.y) / lenSq : 0f;
                t = Mathf.Clamp01(t);
                float dx = x - (a.x + ab.x * t), dz = z - (a.y + ab.y * t);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        /// <summary>True within the creek bed (core half-width).</summary>
        public bool IsInCreek(float x, float z)
        {
            return CreekDist(x, z) <= CreekCore;
        }

        /// <summary>Carve the creek bed into a height: core below WaterLevel, ramp to the bank.</summary>
        private static float CreekModulate(float h, float dist)
        {
            if (dist > CreekCore + CreekRamp) return h;
            float bed = JungleWorld.WaterLevel - CreekBedDepth;
            if (dist <= CreekCore) return Mathf.Min(h, bed);
            float t = (dist - CreekCore) / CreekRamp; // 0 at core -> 1 at bank
            return Mathf.Lerp(Mathf.Min(h, bed), h, t);
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
