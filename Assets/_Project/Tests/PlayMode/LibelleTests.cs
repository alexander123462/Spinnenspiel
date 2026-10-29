using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>T2: Libelle (Dragonfly) — big, fast, risky day-prey. Criteria: speed 2.0 x fly,
    /// energy 60, escapeTime 15 s, net-tear chance 40 % (fixed seed: 30..50 tears in 100),
    /// libelle share in 500 rolls within 1..6 %.</summary>
    public class LibelleTests
    {
        private const string SceneName = "Klettertest";

        /// <summary>Criterion: Anteil Libelle among 500 species-rolls between 1 and 6 %
        /// (the spawner draws LibelleModel.IsLibelleRoll over a uniform RNG).</summary>
        [UnityTest]
        public IEnumerator Libelle_MixIsVeryRare()
        {
            yield return null; // coroutine marker (the mix check itself is synchronous)
            System.Random rng = new System.Random(1); // fixed seed -> reproducible
            int libelles = 0;
            for (int i = 0; i < 500; i++)
            {
                if (LibelleModel.IsLibelleRoll(rng.NextDouble()))
                {
                    libelles++;
                }
            }

            float share = libelles / 500f;
            Debug.Log("[T2] libelle share in 500 rolls: " + share.ToString("F3")
                + " -> 0.01..0.06 -> " + (share >= 0.01f && share <= 0.06f ? "OK" : "NOK"));
            Assert.GreaterOrEqual(share, 0.01f, "Libelle share below 1 %: " + share);
            Assert.LessOrEqual(share, 0.06f, "Libelle share above 6 %: " + share);
        }

        /// <summary>Criterion: net-tear chance 40 % — with a fixed seed, 30..50 of 100 catches
        /// tear the net (the spawner/insect draw LibelleModel.IsTearRoll over a uniform RNG).</summary>
        [UnityTest]
        public IEnumerator Libelle_TearChance()
        {
            yield return null; // coroutine marker (the chance check itself is synchronous)
            System.Random rng = new System.Random(1001); // fixed seed -> reproducible
            int tears = 0;
            for (int i = 0; i < 100; i++)
            {
                if (LibelleModel.IsTearRoll(rng.NextDouble()))
                {
                    tears++;
                }
            }

            Debug.Log("[T2] net tears in 100 catches: " + tears + " -> 30..50 -> "
                + (tears >= 30 && tears <= 50 ? "OK" : "NOK"));
            Assert.GreaterOrEqual(tears, 30, "Libelle tears below 30 of 100: " + tears);
            Assert.LessOrEqual(tears, 50, "Libelle tears above 50 of 100: " + tears);
        }

        /// <summary>Criterion: energy 60, escapeTime 15 s, netTearChance 0.4, speed 2.0 x fly,
        /// no colliders, and the model has 4 wings (long slim body).</summary>
        [UnityTest]
        public IEnumerator Libelle_BigFastRiskyPrey()
        {
            yield return LoadScene();

            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(spawner, "Spawner 'Insekten' nicht gefunden");
            GameObject flyTemplate = spawner.template;
            GameObject libelleTemplate = spawner.libelleTemplate;
            Assert.IsNotNull(flyTemplate, "Fliegen-Vorlage fehlt");
            Assert.IsNotNull(libelleTemplate, "Libelle-Vorlage (libelleTemplate) des Spawners fehlt");

            FlyingInsect fly = flyTemplate.GetComponent<FlyingInsect>();
            FlyingInsect libelle = libelleTemplate.GetComponent<FlyingInsect>();
            Assert.IsNotNull(fly, "Fliegen-Vorlage hat kein FlyingInsect");
            Assert.IsNotNull(libelle, "Libelle-Vorlage hat kein FlyingInsect");

            // Criterion 1: energy 60.
            Debug.Log("[T2] libelle.energyValue=" + libelle.energyValue);
            Assert.AreEqual(60f, libelle.energyValue, "Libelle-Energie != 60: " + libelle.energyValue);

            // Criterion 2: escapeTime 15 s (hangs in an intact net).
            Debug.Log("[T2] libelle.escapeTime=" + libelle.escapeTime + " fly.escapeTime=" + fly.escapeTime);
            Assert.AreEqual(15f, libelle.escapeTime, "Libelle escapeTime != 15: " + libelle.escapeTime);

            // Criterion 3: net-tear chance 0.4 (override on the insect).
            Debug.Log("[T2] libelle.netTearChance=" + libelle.netTearChance);
            Assert.AreEqual(0.4f, libelle.netTearChance, "Libelle netTearChance != 0.4: " + libelle.netTearChance);

            // Criterion 4: no colliders (insects are collider-free).
            Assert.AreEqual(0, libelleTemplate.GetComponentsInChildren<Collider>().Length,
                "Libelle hat einen Collider");

            // Criterion 5: exactly 4 wings (Wing_L/Wing_R + HindWing_L/HindWing_R).
            int wings = CountWings(libelleTemplate);
            Debug.Log("[T2] libelle wing count=" + wings);
            Assert.AreEqual(4, wings, "Libelle hat nicht 4 Fluegel: " + wings);

            // Criterion 6: speed — the Libelle must cover clearly MORE ground than the fly (2.0 x).
            Vector3 home = new Vector3(5.4f, 0.7f, 1.0f);
            GameObject flyInst = Object.Instantiate(flyTemplate, home, Quaternion.identity);
            flyInst.name = "Testfliege";
            flyInst.GetComponent<FlyingInsect>().home = home;
            flyInst.SetActive(true);
            GameObject libelleInst = Object.Instantiate(libelleTemplate, home, Quaternion.identity);
            libelleInst.name = "Testlibelle";
            libelleInst.GetComponent<FlyingInsect>().home = home;
            libelleInst.SetActive(true);

            float flyDist = 0f, libelleDist = 0f;
            Vector3 fp = flyInst.transform.position, lp = libelleInst.transform.position;
            float t = 0f;
            while (t < 5f)
            {
                Vector3 fc = flyInst.transform.position, lc = libelleInst.transform.position;
                flyDist += Vector3.Distance(fc, fp);
                libelleDist += Vector3.Distance(lc, lp);
                fp = fc; lp = lc;
                t += Time.deltaTime;
                yield return null;
            }

            Object.Destroy(flyInst.gameObject);
            Object.Destroy(libelleInst.gameObject);

            Debug.Log("[T2] flyDist=" + flyDist + " libelleDist=" + libelleDist);
            Assert.Greater(flyDist, 0.01f, "Fliege nicht in flight");
            Assert.Greater(libelleDist, flyDist * 1.3f,
                "Libelle nicht deutlich schneller als die Fliege: " + libelleDist + " vs " + flyDist);
        }

        /// <summary>Counts the wing parts (Quads named Wing_* / HindWing_*) of a template.</summary>
        private static int CountWings(GameObject root)
        {
            int wings = 0;
            Transform rootTransform = root.transform;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                string name = rootTransform.GetChild(i).name;
                if (name.StartsWith("Wing") || name.StartsWith("HindWing"))
                {
                    wings++;
                }
            }

            return wings;
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
