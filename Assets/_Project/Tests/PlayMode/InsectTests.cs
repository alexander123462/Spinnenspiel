using System.Collections;
using System.Globalization;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Insekten 1: Fliegen fliegen um ihren Heimpunkt, der Spawner hält die Zahl (Szene Klettertest).</summary>
    public class InsectTests
    {
        private const string SceneName = "Klettertest";

        [UnityTest]
        public IEnumerator Fly_FliesAroundHome()
        {
            yield return LoadScene();

            InsectSpawner flySpawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(flySpawner, "Spawner 'Insekten' nicht gefunden");
            GameObject template = flySpawner.template;
            Assert.IsNotNull(template, "Vorlage-Fliege des Spawners fehlt");

            Vector3 home = new Vector3(5.4f, 0.7f, 1.0f);
            GameObject fly = Object.Instantiate(template, home, Quaternion.identity);
            fly.name = "Testfliege";
            fly.GetComponent<FlyingInsect>().home = home;
            fly.SetActive(true);

            // 5 s fliegen lassen und dabei messen: Weg, Abstand zu home, Hoehe ueber Boden.
            float distance = 0f;
            float maxDistHome = 0f;
            float minHeightAboveGround = float.MaxValue;
            Vector3 previous = fly.transform.position;
            float t = 0f;
            while (t < 5f)
            {
                Vector3 current = fly.transform.position;
                distance += Vector3.Distance(current, previous);
                previous = current;
                maxDistHome = Mathf.Max(maxDistHome, Vector3.Distance(current, home));
                // Hoehe ueber Boden wie die Fliege selbst messen (Raycast von der Position nach unten).
                RaycastHit groundHit;
                if (Physics.Raycast(current + Vector3.up * 0.01f, Vector3.down, out groundHit, 10f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    minHeightAboveGround = Mathf.Min(minHeightAboveGround, current.y - groundHit.point.y);
                }
                t += Time.deltaTime;
                yield return null;
            }

            string info = string.Format(CultureInfo.InvariantCulture,
                "weg={0:F2} m, maxAbstandHome={1:F2} m, minHoehe={2:F3} m",
                distance, maxDistHome, minHeightAboveGround);
            Object.Destroy(fly);

            Assert.Greater(distance, 2f, "In 5 s < 2 m Weg: " + info);
            Assert.Less(maxDistHome, 2.5f + 0.6f, "Abstand zu home > wanderRadius + 0.6: " + info);
            Assert.Greater(minHeightAboveGround, 0.1f, "Hoehe ueber Boden < 0.1: " + info);
        }

        [UnityTest]
        public IEnumerator Spawner_KeepsCount()
        {
            yield return LoadScene();

            GameObject insekten = GameObject.Find("Insekten");
            Assert.IsNotNull(insekten, "Spawner 'Insekten' nicht gefunden");
            InsectSpawner spawner = insekten.GetComponent<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner fehlt");

            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(spawner.count, spawner.AliveCount,
                "Anzahl Insekten stimmt nicht: " + spawner.AliveCount + " != " + spawner.count);

            // Ein Insekt entfernen -> der Spawner soll den Verlust auffuellen.
            FlyingInsect one = Object.FindFirstObjectByType<FlyingInsect>();
            Assert.IsNotNull(one, "Keine Fliege gefunden");
            one.Remove();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(spawner.count, spawner.AliveCount,
                "Nach Entfernen wurde nicht nachgespawnt: " + spawner.AliveCount + " != " + spawner.count);
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }

    }
}
