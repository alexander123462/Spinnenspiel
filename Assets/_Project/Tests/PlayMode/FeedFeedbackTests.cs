using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>S4 Fress-Rueckmeldung: bar flash 0.4 s, tone once per eat, "+25"/"+10"/"+40 label.</summary>
    public class FeedFeedbackTests
    {
        private const string SceneName = "Klettertest";

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }

        [UnityTest]
        public IEnumerator BarFlashModel_FullAtZeroZeroAtEnd()
        {
            const float duration = 0.4f;

            Assert.That(FeedFeedback.BarFlash(0f, duration) == 1f, "BarFlash(0) != 1");
            Assert.That(FeedFeedback.BarFlash(duration, duration) == 0f, "BarFlash(0.4) != 0");
            Assert.That(FeedFeedback.BarFlash(-0.1f, duration) == 0f);
            Assert.That(FeedFeedback.BarFlash(0.5f, duration) == 0f);

            // Monotone falling in between, never above 1 or below 0.
            float mid = FeedFeedback.BarFlash(duration * 0.5f, duration);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
            Assert.Greater(FeedFeedback.BarFlash(0.1f, duration), mid);

            // BarFlashTime: inside the window returns t, outside -1.
            Assert.That(FeedFeedback.BarFlashTime(0.2f, duration) == 0.2f);
            Assert.That(FeedFeedback.BarFlashTime(0.6f, duration) == -1f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EatenPrey_ToneOnceAndLabelMatchesEnergy()
        {
            yield return LoadScene();

            SpiderPredation pred = Object.FindFirstObjectByType<SpiderPredation>();
            Assert.IsNotNull(pred, "SpiderPredation missing in the scene");
            FeedFeedback feedback = Object.FindFirstObjectByType<FeedFeedback>();
            Assert.IsNotNull(feedback, "FeedFeedback missing on the spider");
            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner missing in the scene");

            Vector3 spiderPos = pred.transform.position;

            // Energies per prey type (Fly 25, Muecke 10, Motte 40).
            float[] energies = { 25f, 10f, 40f };
            string[] expectedLabels = { "+25", "+10", "+40" };

            for (int i = 0; i < energies.Length; i++)
            {
                // Direct-grab cooldown is 3 s: wait before each attempt after the first.
                if (i > 0)
                {
                    yield return new WaitForSeconds(3.2f);
                }

                GameObject fly = Object.Instantiate(spawner.template,
                    spiderPos + new Vector3(0.3f, 0f, 0f), Quaternion.identity);
                fly.name = "TestfliegeFress";
                FlyingInsect insect = fly.GetComponent<FlyingInsect>();
                Assert.IsNotNull(insect, "Testfly has no FlyingInsect");
                insect.home = fly.transform.position;
                insect.webAttraction = 0f;
                insect.energyValue = energies[i];
                fly.SetActive(true);

                int playsBefore = feedback.PlayCount;
                pred.grabRollOverride = 0.1f; // deterministic hit
                bool grabbed = pred.TryDirectGrab(insect);
                Assert.True(grabbed, "Direct grab missed despite roll 0.1");

                // Eating fires asynchronously: poll until the tone played (max ~4 s).
                float waited = 0f;
                while (feedback.PlayCount == playsBefore && waited < 4f)
                {
                    yield return null;
                    waited += 1f / 60f;
                }

                // Tone exactly once per eat.
                Assert.AreEqual(playsBefore + 1, feedback.PlayCount,
                    "Crunch tone not played exactly once for this eat");

                // Inside the flash window: label shows the gained energy.
                yield return null; // one Update pass to refresh the label
                Assert.Greater(feedback.CurrentBarFlash(), 0f,
                    "Bar flash not active right after eating");
                Assert.AreEqual(expectedLabels[i], feedback.FlashLabel,
                    "Label does not match prey energy " + energies[i]);

                yield return new WaitForSeconds(0.6f);

                // After 0.6 s (> 0.4 s window): flash over, label gone.
                Assert.That(feedback.CurrentBarFlash() == 0f,
                    "Flash still active after the window");
                Assert.That(feedback.FlashLabel.Length == 0,
                    "Label still shown after the window");

                Object.Destroy(fly.gameObject);
            }

            Debug.Log(string.Format(
                "S4 eaten: playCount={0} labels +25/+10/+40 ok flashWindow=0.4s",
                feedback.PlayCount));
        }
    }
}
