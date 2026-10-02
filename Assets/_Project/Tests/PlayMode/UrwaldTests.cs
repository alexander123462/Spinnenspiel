using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Prüft, dass die Urwald-Szene bei jedem Start eine neue Welt aus dem Seed erzeugt:
    /// Terrain mit TerrainCollider, Sumpfwasser ohne Collider, genug Bäume, Spinne über Wasser.
    /// </summary>
    public class UrwaldTests
    {
        [UnityTest]
        public IEnumerator Urwald_GeneratesWorldFromSeed()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;

            // Bis zu 15 s auf IsReady warten.
            float timeout = 15f;
            JungleWorldGenerator gen = null;
            while (timeout > 0f)
            {
                gen = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (gen != null && gen.IsReady) break;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(gen, "JungleWorldGenerator in der Urwald-Szene nicht gefunden.");
            Assert.IsTrue(gen.IsReady, "Welt war nach 15 s noch nicht fertig generiert.");
            Assert.AreEqual(4242, gen.Seed, "Seed weicht von NextSeed ab.");
            Assert.Less(gen.GenerationSeconds, 8f, "Generierung dauert zu lange (> 8 s).");
            Assert.GreaterOrEqual(gen.TreeCount, 40, "Zu wenige Bäume (< 40).");

            Assert.IsNotNull(Object.FindFirstObjectByType<TerrainCollider>(),
                "Terrain mit TerrainCollider nicht gefunden.");

            GameObject water = GameObject.Find("Sumpfwasser");
            Assert.IsNotNull(water, "Sumpfwasser nicht gefunden.");
            Assert.IsNull(water.GetComponent<Collider>(), "Sumpfwasser darf keinen Collider haben.");

            SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "Spinne nicht gefunden.");
            Assert.Greater(spider.transform.position.y, JungleWorld.WaterLevel,
                "Spinne sollte über dem Wasser spawnen.");

            yield return null;
        }
    }
}
