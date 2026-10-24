using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>T1: Kaefer (Beetle) — rare, armored day-prey. Criteria: speed 0.6 x fly,
    /// energy 35, net loses 15 % per catch, beetle share in 200 rolls within 5..15 %.</summary>
    public class KaeferTests
    {
        private const string SceneName = "Klettertest";

        /// <summary>Criterion: Anteil Kaefer among 200 species-rolls between 5 and 15 %
        /// (the spawner draws PreyMixModel.IsBeetleRoll over a uniform RNG).</summary>
        [UnityTest]
        public IEnumerator Kaefer_MixIsRare()
        {
            yield return null; // coroutine marker (the mix check itself is synchronous)
            System.Random rng = new System.Random(42); // fixed seed -> reproducible
            int beetles = 0;
            for (int i = 0; i < 200; i++)
            {
                if (PreyMixModel.IsBeetleRoll(rng.NextDouble()))
                {
                    beetles++;
                }
            }

            float share = beetles / 200f;
            Debug.Log("[T1] beetle share in 200 rolls: " + share.ToString("F2")
                + " -> 0.05..0.15 -> " + (share >= 0.05f && share <= 0.15f ? "OK" : "NOK"));
            Assert.GreaterOrEqual(share, 0.05f, "Beetle share below 5 %: " + share);
            Assert.LessOrEqual(share, 0.15f, "Beetle share above 15 %: " + share);
        }

        /// <summary>Criterion: energy 35, speed 0.6 x fly (measured flight distance),
        /// durability cost 15 % per catch, and the template is collider-free.</summary>
        [UnityTest]
        public IEnumerator Kaefer_ArmoredSlowPrey()
        {
            yield return LoadScene();

            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(spawner, "Spawner 'Insekten' nicht gefunden");
            GameObject flyTemplate = spawner.template;
            GameObject beetleTemplate = spawner.beetleTemplate;
            Assert.IsNotNull(flyTemplate, "Fliegen-Vorlage fehlt");
            Assert.IsNotNull(beetleTemplate, "Kaefer-Vorlage (beetleTemplate) des Spawners fehlt");

            FlyingInsect fly = flyTemplate.GetComponent<FlyingInsect>();
            FlyingInsect kaefer = beetleTemplate.GetComponent<FlyingInsect>();
            Assert.IsNotNull(fly, "Fliegen-Vorlage hat kein FlyingInsect");
            Assert.IsNotNull(kaefer, "Kaefer-Vorlage hat kein FlyingInsect");

            // Criterion 1: energy 35.
            Debug.Log("[T1] kaefer.energyValue=" + kaefer.energyValue);
            Assert.AreEqual(35f, kaefer.energyValue, "Kaefer-Energie != 35: " + kaefer.energyValue);

            // Criterion 2: escapeTime 20 s (instead of the fly's 30 s).
            Debug.Log("[T1] kaefer.escapeTime=" + kaefer.escapeTime + " fly.escapeTime=" + fly.escapeTime);
            Assert.AreEqual(20f, kaefer.escapeTime, "Kaefer escapeTime != 20: " + kaefer.escapeTime);

            // Criterion 3: durability cost 15 % per catch (override).
            Debug.Log("[T1] kaefer.durabilityCostOverride=" + kaefer.durabilityCostOverride);
            Assert.AreEqual(15f, kaefer.durabilityCostOverride, "Kaefer durability cost != 15: " + kaefer.durabilityCostOverride);

            // Criterion 4: no colliders (insects are collider-free).
            Assert.AreEqual(0, beetleTemplate.GetComponentsInChildren<Collider>().Length,
                "Kaefer hat einen Collider");

            // Criterion 5: speed — the Kaefer must cover clearly LESS ground than the fly
            // (0.6 x). Both start at their home and fly for a fixed window.
            Vector3 home = new Vector3(5.4f, 0.7f, 1.0f);
            GameObject flyInst = Object.Instantiate(flyTemplate, home, Quaternion.identity);
            flyInst.name = "Testfliege";
            flyInst.GetComponent<FlyingInsect>().home = home;
            flyInst.SetActive(true);
            GameObject kaeferInst = Object.Instantiate(beetleTemplate, home, Quaternion.identity);
            kaeferInst.name = "Testkaefer";
            kaeferInst.GetComponent<FlyingInsect>().home = home;
            kaeferInst.SetActive(true);

            float flyDist = 0f, kaeferDist = 0f;
            Vector3 fp = flyInst.transform.position, kp = kaeferInst.transform.position;
            float t = 0f;
            while (t < 5f)
            {
                Vector3 fc = flyInst.transform.position, kc = kaeferInst.transform.position;
                flyDist += Vector3.Distance(fc, fp);
                kaeferDist += Vector3.Distance(kc, kp);
                fp = fc; kp = kc;
                t += Time.deltaTime;
                yield return null;
            }

            Object.Destroy(flyInst.gameObject);
            Object.Destroy(kaeferInst.gameObject);

            // Criterion 5: the Kaefer must cover clearly LESS ground than the fly (0.6 x).
            // Absolute comparison, like the mosquito test, since the wander path is noisy.
            Debug.Log("[T1] flyDist=" + flyDist + " kaeferDist=" + kaeferDist);
            Assert.Greater(flyDist, 0.01f, "Fliege nicht in flight");
            Assert.Less(kaeferDist, flyDist * 0.85f,
                "Kaefer nicht deutlich langsamer als die Fliege: " + kaeferDist + " vs " + flyDist);
            Assert.Greater(kaeferDist, flyDist * 0.3f,
                "Kaefer nicht in flight: " + kaeferDist);
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
