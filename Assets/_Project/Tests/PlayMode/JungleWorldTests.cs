using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;

namespace Spinnenspiel.Tests
{
    /// <summary>M7: Zufällige Weltbeschreibung JungleWorld – deterministisch, Sümpfe, Lichtungen, Spawn.</summary>
    public class JungleWorldTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4242 };

        [Test]
        public void SameSeed_SameWorld()
        {
            var a = new JungleWorld(1234);
            var b = new JungleWorld(1234);

            for (int i = 0; i < 200; i++)
            {
                float x = -48f + (i % 20) * 5f;
                float z = -48f + (i / 20) * 10f;
                Assert.AreEqual(a.Height(x, z), b.Height(x, z), 1e-6f, $"Height weicht ab bei ({x},{z})");
                Assert.AreEqual(a.SwampFactor(x, z), b.SwampFactor(x, z), 1e-6f, $"SwampFactor weicht ab bei ({x},{z})");
                Assert.AreEqual(a.Density(x, z), b.Density(x, z), 1e-6f, $"Density weicht ab bei ({x},{z})");
            }
        }

        [Test]
        public void DifferentSeeds_DifferentWorlds()
        {
            var a = new JungleWorld(1);
            var b = new JungleWorld(2);

            int different = 0;
            for (int i = 0; i < 200; i++)
            {
                float x = -48f + (i % 20) * 5f;
                float z = -48f + (i / 20) * 10f;
                if (Mathf.Abs(a.Height(x, z) - b.Height(x, z)) > 0.05f)
                    different++;
            }

            Assert.GreaterOrEqual(different, 100,
                $"Seeds 1 und 2: nur {different}/200 Punkte unterscheiden sich um > 0.05 m");
        }

        [Test]
        public void World_HasSwampsAndClearings()
        {
            foreach (int seed in Seeds)
            {
                var w = new JungleWorld(seed);
                int total = 0, water = 0, clearing = 0, dryViolation = 0;

                for (int x = -48; x < 48; x++)
                {
                    for (int z = -48; z < 48; z++)
                    {
                        total++;
                        if (w.IsUnderWater(x, z)) water++;
                        if (w.IsClearing(x, z)) clearing++;
                        if (w.SwampFactor(x, z) < 0.01f
                            && w.Height(x, z) < JungleWorld.WaterLevel + 0.3f - 0.01f)
                            dryViolation++;
                    }
                }

                float waterPct = 100f * water / total;
                float clearingPct = 100f * clearing / total;

                Assert.GreaterOrEqual(waterPct, 3f, $"Seed {seed}: Unterwasser-Anteil {waterPct:P2} < 3 %");
                Assert.LessOrEqual(waterPct, 30f, $"Seed {seed}: Unterwasser-Anteil {waterPct:P2} > 30 %");
                Assert.GreaterOrEqual(clearingPct, 3f, $"Seed {seed}: Lichtungs-Anteil {clearingPct:P2} < 3 %");
                Assert.AreEqual(0, dryViolation,
                    $"Seed {seed}: {dryViolation} Punkte außerhalb der Sumpfe unter WaterLevel + 0.3");
            }
        }

        [Test]
        public void SpawnPoint_IsDryAndFlat()
        {
            foreach (int seed in Seeds)
            {
                var w = new JungleWorld(seed);
                Vector3 p = w.FindSpawnPoint();
                float half = w.Size * 0.5f;

                Assert.LessOrEqual(Mathf.Abs(p.x), half, $"Seed {seed}: Spawnpunkt x={p.x} außerhalb der Welt");
                Assert.LessOrEqual(Mathf.Abs(p.z), half, $"Seed {seed}: Spawnpunkt z={p.z} außerhalb der Welt");
                Assert.GreaterOrEqual(p.y, JungleWorld.WaterLevel + 0.4f,
                    $"Seed {seed}: Spawnpunkt zu nass (y={p.y})");
                Assert.False(w.IsClearing(p.x, p.z), $"Seed {seed}: Spawnpunkt liegt in einer Lichtung");
                Assert.AreEqual(w.Height(p.x, p.z), p.y, 1e-4f, $"Seed {seed}: Spawn-Höhe passt nicht zum Gelände");
            }
        }
    }
}
