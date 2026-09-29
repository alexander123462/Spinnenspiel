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

        /// <summary>Ergebnis eines Fang-Test-Setups (Iteratoren dürfen keine out-Parameter nehmen).</summary>
        private class WebTestSetup
        {
            public OrbWeb web;
            public FlyingInsect insect;
            public Vector3 start;
        }

        /// <summary>
        /// Bereitet die Fang-Tests vor: Spawner stilllegen, vorhandene Fliegen entfernen,
        /// Netz erzeugen und auf die gewünschte Stufe setzen.
        /// </summary>
        private static IEnumerator SetupWebTest(OrbWeb.WebStage stage, WebTestSetup result)
        {
            result.web = null;
            result.insect = null;
            result.start = Vector3.zero;

            yield return LoadScene();

            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
            }
            else
            {
                Assert.Fail("Spawner 'Insekten' nicht gefunden");
                yield break;
            }

            // Zuvor gespawnte Fliegen entfernen, damit sie nicht stören.
            foreach (FlyingInsect f in Object.FindObjectsOfType<FlyingInsect>())
            {
                f.Remove();
            }

            WebBuildDemo demo = Object.FindFirstObjectByType<WebBuildDemo>();
            Assert.IsNotNull(demo, "WebBuildDemo nicht gefunden");
            result.web = demo.CreateWeb();
            Assert.IsNotNull(result.web, "CreateWeb() lieferte kein Netz");
            result.web.SetProgress(stage, 1f);

            Vector3 n = result.web.PlaneNormal;
            result.start = result.web.Layout.Hub + n * 0.5f;

            GameObject fly = Object.Instantiate(spawner.template, result.start, Quaternion.identity);
            fly.name = "Testfliege";
            fly.SetActive(true);
            result.insect = fly.GetComponent<FlyingInsect>();
            Assert.IsNotNull(result.insect, "Testfliege hat kein FlyingInsect");
            result.insect.home = result.start;
            result.insect.FlyTo(result.web.Layout.Hub - n * 0.5f);
        }

        [UnityTest]
        public IEnumerator Fly_GetsCaughtInFinishedWeb()
        {
            WebTestSetup s = new WebTestSetup();
            yield return SetupWebTest(OrbWeb.WebStage.Done, s);

            float t = 0f;
            while (t < 3f && s.insect.State != FlyingInsect.InsectState.Caught)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(FlyingInsect.InsectState.Caught, s.insect.State, "Fliege wurde vom fertigen Netz nicht gefangen");
            Assert.AreEqual(s.web, s.insect.CaughtIn, "CaughtIn ist nicht das Test-Netz");
            float planeDist = Mathf.Abs(Vector3.Dot(s.insect.CaughtPoint - s.web.Layout.Hub, s.web.PlaneNormal));
            Assert.Less(planeDist, 0.01f, "Fangpunkt nicht in der Netzebene: " + planeDist);

            Object.Destroy(s.insect.gameObject);
        }

        [UnityTest]
        public IEnumerator Fly_PassesUnfinishedWeb()
        {
            WebTestSetup s = new WebTestSetup();
            yield return SetupWebTest(OrbWeb.WebStage.Radii, s);

            float t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(FlyingInsect.InsectState.Flying, s.insect.State,
                "Fliege wurde trotz unvollendetem Netz (Radien) gefangen");

            Object.Destroy(s.insect.gameObject);
        }

        [UnityTest]
        public IEnumerator Fly_EscapesAfterTime()
        {
            WebTestSetup s = new WebTestSetup();
            yield return SetupWebTest(OrbWeb.WebStage.Done, s);

            s.insect.escapeTime = 1f;

            // Fangen abwarten (max. 3 s).
            float t = 0f;
            while (t < 3f && s.insect.State != FlyingInsect.InsectState.Caught)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(FlyingInsect.InsectState.Caught, s.insect.State, "Voraussetzung: Fliege müsste gefangen sein");

            // Nach escapeTime + Puffer muss sie sich befreit haben.
            t = 0f;
            while (t < 1.5f && s.insect.State != FlyingInsect.InsectState.Flying)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(FlyingInsect.InsectState.Flying, s.insect.State,
                "Fliege hat sich nach escapeTime nicht befreit");

            Object.Destroy(s.insect.gameObject);
        }

        [UnityTest]
        public IEnumerator Fly_WrapProgressMakesCocoon()
        {
            yield return LoadScene();

            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(spawner, "Spawner 'Insekten' nicht gefunden");
            spawner.enabled = false;

            // Zuvor gespawnte Fliegen entfernen, damit sie nicht stoeren.
            foreach (FlyingInsect f in Object.FindObjectsOfType<FlyingInsect>())
            {
                f.Remove();
            }

            Vector3 home = new Vector3(5.4f, 0.7f, 1.0f);
            GameObject fly = Object.Instantiate(spawner.template, home, Quaternion.identity);
            fly.name = "Testfliege";
            fly.SetActive(true);
            FlyingInsect insect = fly.GetComponent<FlyingInsect>();
            Assert.IsNotNull(insect, "Testfliege hat kein FlyingInsect");
            insect.home = home;
            insect.Hold();

            // 1 s lang jeden Frame den Einwickel-Fortschritt vorueberschicken.
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                insect.SetWrapProgress(t);
                yield return null;
            }
            insect.SetWrapProgress(1f);
            yield return null;

            Assert.AreEqual(FlyingInsect.InsectState.Wrapped, insect.State, "State ist nicht Wrapped");
            Transform cocoon = fly.transform.Find("Kokon");
            Assert.IsNotNull(cocoon, "Kind 'Kokon' fehlt an der Fliege");
            Assert.Greater(cocoon.localScale.z, 0.04f, "Kokon-LocalScale zu klein: " + cocoon.localScale.z);
            Assert.AreEqual(0, fly.GetComponentsInChildren<Collider>().Length,
                "Fliege oder Kokon hat noch einen Collider");

            Object.Destroy(fly);
        }

        [UnityTest]
        public IEnumerator Predation_HuntsWrapsAndEats()
        {
            yield return LoadScene();

            // Spawner stilllegen und vorhandene Fliegen entfernen (wie in den 7.2-Tests).
            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(spawner, "Spawner 'Insekten' nicht gefunden");
            spawner.enabled = false;
            foreach (FlyingInsect f in Object.FindObjectsOfType<FlyingInsect>())
            {
                f.Remove();
            }

            WebBuildDemo demo = Object.FindFirstObjectByType<WebBuildDemo>();
            Assert.IsNotNull(demo, "WebBuildDemo nicht gefunden");
            OrbWeb web = demo.CreateWeb();
            Assert.IsNotNull(web, "CreateWeb() lieferte kein Netz");
            web.SetProgress(OrbWeb.WebStage.Done, 1f);

            SpiderPredation predation = Object.FindFirstObjectByType<SpiderPredation>();
            Assert.IsNotNull(predation, "SpiderPredation an der Spinne fehlt");
            Transform spider = predation.transform;
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");
            SpiderHunger hunger = spider.GetComponent<SpiderHunger>();
            Assert.IsNotNull(hunger, "SpiderHunger fehlt");
            SpiderSilk silk = spider.GetComponent<SpiderSilk>();
            Assert.IsNotNull(silk, "SpiderSilk fehlt");

            spider.position = new Vector3(5.4f, 0.6f, 1.9f);
            builder.SitAtHub(web);
            Assert.IsTrue(builder.IsSittingInWeb, "Voraussetzung: Spinne sitzt in der Nabe");

            Vector3 n = builder.Normal;
            Vector3 right = Vector3.Cross(Vector3.up, n).normalized;
            Vector3 start = web.Layout.Hub + n * 0.5f + right * 0.2f;

            GameObject fly = Object.Instantiate(spawner.template, start, Quaternion.identity);
            fly.name = "Testfliege";
            fly.SetActive(true);
            FlyingInsect insect = fly.GetComponent<FlyingInsect>();
            Assert.IsNotNull(insect, "Testfliege hat kein FlyingInsect");
            insect.home = start;
            insect.FlyTo(web.Layout.Hub - n * 0.5f + right * 0.2f);

            // Fang abwarten (max. 3 s).
            float t = 0f;
            while (t < 3f && insect.State != FlyingInsect.InsectState.Caught)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(FlyingInsect.InsectState.Caught, insect.State, "Fliege wurde nicht gefangen");
            Assert.AreEqual(web, insect.CaughtIn, "CaughtIn ist nicht das Test-Netz");

            hunger.Hunger = 0.8f;
            predation.durationScale = 0.3f;
            predation.StartHunt(insect);

            // Jagd verfolgen: mindestens einmal Spinnpose, Fliege darf nie frei fliegen.
            bool sawSpinning = false;
            bool neverFlying = true;
            t = 0f;
            while (t < 10f && predation.IsHunting)
            {
                if (silk.IsSpinning)
                {
                    sawSpinning = true;
                }
                if (insect != null && insect.State == FlyingInsect.InsectState.Flying)
                {
                    neverFlying = false;
                }
                t += Time.deltaTime;
                yield return null;
            }

            Assert.IsFalse(predation.IsHunting, "Jagd lief nach 10 s nicht zu Ende");
            Assert.IsTrue(sawSpinning, "IsSpinning war waehrend der Jagd nie true");
            Assert.IsTrue(neverFlying, "Fliege war waehrend der Jagd frei (State Flying)");
            Assert.IsTrue(insect == null, "Fliege wurde nicht entfernt");
            Assert.Less(hunger.Hunger, 0.5f, "Hunger sank nicht beim Fressen: " + hunger.Hunger);
            float distHub = Vector3.Distance(spider.position, web.Layout.Hub);
            Assert.Less(distHub, 0.08f, "Spinne ist nicht zurueck an der Nabe: " + distHub);
            Assert.IsTrue(builder.IsSittingInWeb, "Spinne sitzt nicht wieder in der Nabe");
            Assert.IsFalse(silk.IsSpinning, "Spinnpose wurde nicht beendet");
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }

    }
}
