using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>S3 Fang-Rueckmeldung: at the web -> tone + camera shake; away from the web -> tone only.</summary>
    public class FangFeedbackTests
    {
        private const string SceneName = "Klettertest";

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// Deterministisch ein Testfliege directchord durch das Netz flown (like the green Predation/WorldTests).
        /// Necessary because with the spider sitting in the web (SitAtHub) natural wandering would get chased
        /// away by the flee-logic — so a FlyTo-web-run is used instead of webAttraction.
        /// </summary>
        private static FlyingInsect SpawnFlyThroughWeb(OrbWeb web)
        {
            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner missing in the scene");

            Vector3 hub = web.Layout.Hub;
            Vector3 n = web.PlaneNormal;
            Vector3 right = Vector3.Cross(Vector3.up, n).normalized;
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.right;
            }

            float off = web.CaptureRadius * 0.8f;
            Vector3 start = hub + n * 0.5f + right * off;
            Vector3 ziel = hub - n * 0.5f + right * off;

            GameObject fly = Object.Instantiate(spawner.template, start, Quaternion.identity);
            fly.SetActive(true);
            FlyingInsect insect = fly.GetComponent<FlyingInsect>();
            Assert.IsNotNull(insect, "Testfly has no FlyingInsect");
            insect.home = start;
            insect.FlyTo(ziel, true);
            insect.SetInitialDirection(ziel - start);
            return insect;
        }

        [UnityTest]
        public IEnumerator ShakeModel_PeaksInMiddleAndEndsZero()
        {
            const float duration = 0.15f;
            const float strength = 0.02f;

            Assert.That(ShakeModel.OffsetAt(0f, duration, strength) == 0f);
            Assert.GreaterOrEqual(ShakeModel.OffsetAt(duration * 0.5f, duration, strength), strength - 1e-6f);
            Assert.LessOrEqual(ShakeModel.OffsetAt(duration * 0.5f, duration, strength), strength);
            Assert.That(ShakeModel.OffsetAt(duration, duration, strength) == 0f);
            Assert.That(ShakeModel.OffsetAt(-0.1f, duration, strength) == 0f);
            Assert.That(ShakeModel.OffsetAt(0.3f, duration, strength) == 0f);

            // Bigger prey -> bigger offset at the same time.
            Assert.Greater(ShakeModel.OffsetAt(duration * 0.5f, duration, strength * 2f),
                ShakeModel.OffsetAt(duration * 0.5f, duration, strength));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CatchAtWeb_ToneAndCameraShake()
        {
            yield return LoadScene();

            CameraShake shake = Object.FindFirstObjectByType<CameraShake>();
            Assert.IsNotNull(shake, "CameraShake missing on the camera");
            FangFeedback feedback = Object.FindFirstObjectByType<FangFeedback>();
            Assert.IsNotNull(feedback, "FangFeedback missing on the spider");
            SpiderWebBuilder builder = Object.FindFirstObjectByType<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder missing");

            WebBuildDemo demo = Object.FindFirstObjectByType<WebBuildDemo>();
            Assert.IsNotNull(demo, "WebBuildDemo not found");
            OrbWeb web = demo.CreateWeb();
            Assert.IsNotNull(web, "CreateWeb() returned nothing");
            web.SetProgress(OrbWeb.WebStage.Done, 1f);

            // Spinne IN its own Net -> Wackeln expected.
            builder.SitAtHub(web);

            int caught = 0;
            System.Action<FlyingInsect, OrbWeb> handler = (f, w) =>
            {
                if (w == web)
                {
                    caught++;
                }
            };
            FlyingInsect.AnyCaught += handler;

            SpawnFlyThroughWeb(web);

            float t = 0f;
            while (t < 20f && caught < 1)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.GreaterOrEqual(caught, 1, "No fly caught in 20 s");

            // After the catch: tone played and shake armed.
            yield return null;
            Assert.GreaterOrEqual(feedback.PlayCount, 1, "No Zupfton played at the web");
            Assert.Greater(shake.LastStrength, 0f, "Camera shake not triggered at the web");

            // The camera actually deviates shortly, then returns.
            float maxOffset = 0f;
            for (int i = 0; i < 30; i++)
            {
                maxOffset = Mathf.Max(maxOffset, shake.CurrentOffset.magnitude);
                yield return null;
            }
            FlyingInsect.AnyCaught -= handler;

            Assert.Greater(maxOffset, 0.0005f, "Camera never deviated (shake invisible)");
            Assert.Less(shake.LastStrength, 0.2f, "Shake strength implausibly large");
            Debug.Log(string.Format("S3 at web: playCount={0} maxOffset={1:F4} m", feedback.PlayCount, maxOffset));
        }

        [UnityTest]
        public IEnumerator CatchAtWeb_ShakeSwitchedOff_OffsetStaysZero()
        {
            yield return LoadScene();

            CameraShake shake = Object.FindFirstObjectByType<CameraShake>();
            Assert.IsNotNull(shake, "CameraShake missing on the camera");
            FangFeedback feedback = Object.FindFirstObjectByType<FangFeedback>();
            Assert.IsNotNull(feedback, "FangFeedback missing on the spider");
            SpiderWebBuilder builder = Object.FindFirstObjectByType<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder missing");
            GameSettings settings = Object.FindFirstObjectByType<GameSettings>();
            Assert.IsNotNull(settings, "GameSettings missing in the scene");

            settings.SetCameraShake(false);
            Assert.That(!GameSettings.cameraShakeEnabled);

            WebBuildDemo demo = Object.FindFirstObjectByType<WebBuildDemo>();
            Assert.IsNotNull(demo, "WebBuildDemo not found");
            OrbWeb web = demo.CreateWeb();
            Assert.IsNotNull(web, "CreateWeb() returned nothing");
            web.SetProgress(OrbWeb.WebStage.Done, 1f);

            builder.SitAtHub(web);

            int caught = 0;
            System.Action<FlyingInsect, OrbWeb> handler = (f, w) =>
            {
                if (w == web)
                {
                    caught++;
                }
            };
            FlyingInsect.AnyCaught += handler;

            SpawnFlyThroughWeb(web);

            float t = 0f;
            while (t < 20f && caught < 1)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.GreaterOrEqual(caught, 1, "No fly caught in 20 s");

            // Tone still plays, but the camera must never deviate.
            yield return null;
            Assert.GreaterOrEqual(feedback.PlayCount, 1, "Tone must still play when shake is off");

            float maxOffset = 0f;
            for (int i = 0; i < 30; i++)
            {
                maxOffset = Mathf.Max(maxOffset, shake.CurrentOffset.magnitude);
                yield return null;
            }
            FlyingInsect.AnyCaught -= handler;

            Assert.That(maxOffset == 0f, "Camera deviated despite shake switched off");
            Assert.That(shake.LastStrength == 0f);
            settings.SetCameraShake(true);
            Debug.Log(string.Format("S3 shake off: playCount={0} maxOffset=0", feedback.PlayCount));
        }

        [UnityTest]
        public IEnumerator CatchAwayFromWeb_ToneOnlyNoShake()
        {
            yield return LoadScene();

            CameraShake shake = Object.FindFirstObjectByType<CameraShake>();
            Assert.IsNotNull(shake, "CameraShake missing on the camera");
            FangFeedback feedback = Object.FindFirstObjectByType<FangFeedback>();
            Assert.IsNotNull(feedback, "FangFeedback missing on the spider");

            WebBuildDemo demo = Object.FindFirstObjectByType<WebBuildDemo>();
            Assert.IsNotNull(demo, "WebBuildDemo not found");
            OrbWeb web = demo.CreateWeb();
            Assert.IsNotNull(web, "CreateWeb() returned nothing");
            web.SetProgress(OrbWeb.WebStage.Done, 1f);

            // Spider NOT in the web (stays at its spawn point) -> only the tone.
            foreach (FlyingInsect f in Object.FindObjectsByType<FlyingInsect>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                f.webAttraction = 1f;
            }

            int caught = 0;
            System.Action<FlyingInsect, OrbWeb> handler = (f, w) =>
            {
                if (w == web)
                {
                    caught++;
                }
            };
            FlyingInsect.AnyCaught += handler;

            float t = 0f;
            while (t < 20f && caught < 1)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.GreaterOrEqual(caught, 1, "No fly caught in 20 s");

            yield return new WaitForSeconds(0.5f);
            FlyingInsect.AnyCaught -= handler;

            Assert.GreaterOrEqual(feedback.PlayCount, 1, "Tone must still play away from the web");
            Assert.That(shake.LastStrength == 0f);
            Assert.That(shake.CurrentOffset == Vector3.zero);
            Debug.Log(string.Format("S3 away from web: playCount={0} shake=0", feedback.PlayCount));
        }
    }
}
