using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Zentrale, deterministische Beschreibung der Testwelt (Bachverlauf, Teich, Wald-/Wiesengrenze, Höhenprofil).
    /// Wird vom Editor-Weltbau UND zur Laufzeit (Vegetation, Wasser, Audio) genutzt, damit alle Systeme
    /// dieselbe Welt kennen, ohne Daten doppelt zu speichern.
    /// </summary>
    public static class WorldLayout
    {
        public const float Size = 64f;
        public const float MaxHeight = 10f;

        public static readonly Vector3 SpawnPoint = new Vector3(32f, 0f, 18f);

        // Bach: verläuft entlang X, leicht gewunden.
        public const float StreamBedHalfWidth = 0.6f;
        public const float StreamBankWidth = 1.6f;
        public const float StreamDepth = 0.34f;

        // Teich
        public static readonly Vector2 PondCenter = new Vector2(13f, 20f);
        public const float PondRadius = 3.2f;
        public const float PondDepth = 0.45f;

        /// <summary>Z-Koordinate der Bachmitte an Position x.</summary>
        public static float StreamCenterZ(float x)
        {
            return 31f + 3.2f * Mathf.Sin(x * 0.085f + 0.6f) + 1.4f * Mathf.Sin(x * 0.21f + 1.9f);
        }

        /// <summary>Uferhöhe des Bachs an Position x (Bach fällt leicht nach +X ab).</summary>
        public static float StreamBankHeight(float x)
        {
            return 3.3f - 0.012f * x;
        }

        /// <summary>Wasserspiegel des Bachs an Position x.</summary>
        public static float StreamWaterHeight(float x)
        {
            return StreamBankHeight(x) - StreamDepth * 0.45f;
        }

        public static float PondBankHeight => 3.05f;
        public static float PondWaterHeight => PondBankHeight - 0.16f;

        /// <summary>Horizontaler Abstand eines Punkts zur Bachmitte (Näherung über Z-Abstand und Steigung).</summary>
        public static float DistanceToStream(float x, float z)
        {
            float dz = z - StreamCenterZ(x);
            float slope = (StreamCenterZ(x + 0.5f) - StreamCenterZ(x - 0.5f));
            return Mathf.Abs(dz) / Mathf.Sqrt(1f + slope * slope);
        }

        public static float DistanceToPond(float x, float z)
        {
            return new Vector2(x - PondCenter.x, z - PondCenter.y).magnitude;
        }

        /// <summary>0 = Wiese, 1 = Wald (weicher, verrauschter Übergang nördlich des Bachs).</summary>
        public static float ForestFactor(float x, float z)
        {
            float edge = 38f + 3f * (Mathf.PerlinNoise(x * 0.08f + 11.3f, 4.2f) - 0.5f) * 2f;
            return Mathf.Clamp01(Mathf.InverseLerp(edge - 2.5f, edge + 2.5f, z));
        }

        /// <summary>Nässe 0..1: nahe am Wasser feucht/schlammig.</summary>
        public static float Wetness(float x, float z)
        {
            float stream = 1f - Mathf.Clamp01(Mathf.InverseLerp(StreamBedHalfWidth, StreamBedHalfWidth + StreamBankWidth * 1.1f, DistanceToStream(x, z)));
            float pond = 1f - Mathf.Clamp01(Mathf.InverseLerp(PondRadius * 0.8f, PondRadius + 1.4f, DistanceToPond(x, z)));
            return Mathf.Max(stream, pond);
        }

        /// <summary>Terrainhöhe in Metern (Weltraum, Terrain bei y = 0).</summary>
        public static float Height(float x, float z)
        {
            float h = 3.2f;
            h += 1.8f * (Mathf.PerlinNoise(x / 26f + 3.1f, z / 26f + 7.7f) - 0.5f) * 2f;
            h += 0.7f * (Mathf.PerlinNoise(x / 10f + 13.4f, z / 10f + 1.3f) - 0.5f) * 2f;
            h += 0.12f * (Mathf.PerlinNoise(x / 2.7f + 5.2f, z / 2.7f + 9.9f) - 0.5f) * 2f;
            h += 0.035f * (Mathf.PerlinNoise(x / 0.8f + 1.7f, z / 0.8f + 2.8f) - 0.5f) * 2f;

            // Wald etwas hügeliger
            h += ForestFactor(x, z) * 0.9f * Mathf.PerlinNoise(x / 7f + 21f, z / 7f + 3f);

            // Spawnbereich: sanfte, gut lesbare Wiese
            float spawnDistance = new Vector2(x - SpawnPoint.x, z - SpawnPoint.z).magnitude;
            float spawnFlatten = 1f - Mathf.Clamp01(Mathf.InverseLerp(2f, 7f, spawnDistance));
            h = Mathf.Lerp(h, 3.35f + 0.035f * (Mathf.PerlinNoise(x / 0.8f + 1.7f, z / 0.8f + 2.8f) - 0.5f), spawnFlatten * 0.85f);

            // Bachtal: Umgebung auf Uferhöhe ziehen, dann Bett ausheben
            float streamDistance = DistanceToStream(x, z);
            float valley = 1f - Mathf.Clamp01(Mathf.InverseLerp(StreamBedHalfWidth, StreamBedHalfWidth + StreamBankWidth + 3.5f, streamDistance));
            h = Mathf.Lerp(h, StreamBankHeight(x), valley * valley * (3f - 2f * valley));
            float bed = 1f - Mathf.Clamp01(Mathf.InverseLerp(StreamBedHalfWidth, StreamBedHalfWidth + StreamBankWidth, streamDistance));
            h -= StreamDepth * bed * bed * (3f - 2f * bed);

            // Teich
            float pondDistance = DistanceToPond(x, z);
            float pondFlatten = 1f - Mathf.Clamp01(Mathf.InverseLerp(PondRadius, PondRadius + 4f, pondDistance));
            h = Mathf.Lerp(h, PondBankHeight, pondFlatten * pondFlatten * (3f - 2f * pondFlatten));
            float pondBowl = 1f - Mathf.Clamp01(pondDistance / (PondRadius + 0.6f));
            h -= PondDepth * Mathf.Sqrt(pondBowl);

            return Mathf.Clamp(h, 0.2f, MaxHeight - 0.2f);
        }

        /// <summary>Deterministischer Hash 0..1 für Rasterzellen (Vegetation, Streuung).</summary>
        public static float Hash01(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)z * 668265263u + (uint)salt * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777216f;
            }
        }
    }
}
