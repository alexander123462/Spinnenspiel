using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Milestone 2: Fußplatzierung, Gangkoordination und Körperanpassung.</summary>
    public class SpiderLegTests
    {
        private const string SceneName = "M1_Testgelaende";
        private const float GroundTolerance = 0.015f;

        private static float GroundHeightAt(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            Assert.Fail("Kein Boden unter " + position);
            return 0f;
        }

        private static void AssertPlantedFeetOnGround(SpiderLeg[] legs, string phase)
        {
            foreach (SpiderLeg leg in legs)
            {
                if (leg.IsStepping || !leg.IsGrounded)
                {
                    continue;
                }

                float ground = GroundHeightAt(leg.FootPosition);
                float error = leg.FootPosition.y - ground;
                Assert.Less(Mathf.Abs(error), GroundTolerance, phase + ": Fuß " + leg.name + " liegt " + error.ToString("F4") + " m neben dem Boden");
            }
        }

        [UnityTest]
        public IEnumerator Legs_StayOnGround_AlternateAndAdaptBody()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            SpiderGaitController gait = spider.GetComponent<SpiderGaitController>();
            SpiderBodyController body = spider.GetComponent<SpiderBodyController>();
            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.IsNotNull(gait, "SpiderGaitController fehlt");
            Assert.IsNotNull(body, "SpiderBodyController fehlt");
            Assert.AreEqual(8, legs.Length, "Die Spinne braucht 8 Beine");

            foreach (SpiderLeg leg in legs)
            {
                Assert.IsTrue(leg.IsGrounded, "Bein " + leg.name + " hat nach dem Start keinen Boden");
            }

            AssertPlantedFeetOnGround(legs, "Stand");

            int[] stepsBefore = new int[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                stepsBefore[i] = legs[i].StepCount;
            }

            gait.ResetStatistics();
            movement.SetExternalMoveInput(new Vector2(0.3f, 1f));
            float time = 0f;
            while (time < 3f)
            {
                time += Time.deltaTime;
                AssertPlantedFeetOnGround(legs, "Laufen");
                Assert.LessOrEqual(gait.SteppingCount, 5, "Zu viele Beine schwingen gleichzeitig");
                yield return null;
            }

            Assert.LessOrEqual(gait.MaxSteppingObserved, 5, "Zu viele Beine schwingen gleichzeitig");
            for (int i = 0; i < legs.Length; i++)
            {
                Assert.GreaterOrEqual(legs[i].StepCount - stepsBefore[i], 2, "Bein " + legs[i].name + " hat beim Laufen kaum Schritte gemacht");
            }

            movement.SetExternalMoveInput(Vector2.zero);
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(0, gait.SteppingCount, "Beine zappeln im Stand weiter");
            AssertPlantedFeetOnGround(legs, "Stand nach dem Laufen");

            float bodyY = body.Body.localPosition.y - body.AverageFootHeight;
            Assert.That(bodyY, Is.InRange(0.03f, 0.1f), "Körperhöhe über den Füßen unplausibel: " + bodyY);
        }
    }
}
