using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Milestone 3–6: Testwelt mit Hindernissen, Vegetation, Wasser, Audio und Effekten.</summary>
    public class WorldTests
    {
        private const string SceneName = "Testwelt";

        private static GameObject LoadedSpider()
        {
            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            return spider;
        }

        private static IEnumerator LoadWorld()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);
        }

        [UnityTest]
        public IEnumerator Spider_WalksOverObstacleCourse_FeetFindRockAndWood()
        {
            yield return LoadWorld();
            GameObject spider = LoadedSpider();
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length);

            foreach (SpiderLeg leg in legs)
            {
                Assert.IsTrue(leg.IsGrounded, "Bein " + leg.name + " ohne Boden nach dem Start");
            }

            Vector3 start = spider.transform.position;
            bool touchedObstacle = false;
            movement.SetExternalMoveInput(new Vector2(0f, 1f));
            float time = 0f;
            while (time < 5f)
            {
                time += Time.deltaTime;
                foreach (SpiderLeg leg in legs)
                {
                    if (!leg.IsStepping && leg.IsGrounded && (leg.FootSurface == SurfaceType.Rock || leg.FootSurface == SurfaceType.Wood))
                    {
                        touchedObstacle = true;
                    }

                    if (!leg.IsStepping && leg.IsGrounded)
                    {
                        // Ein aufgesetzter Fuß darf nicht in einem Kollider stecken (Berühren eines Hindernisses ist erlaubt).
                        Vector3 probe = leg.FootPosition + leg.FootNormal * 0.004f;
                        Assert.IsFalse(Physics.CheckSphere(probe, 0.0015f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                            "Fuß " + leg.name + " steckt in einem Hindernis bei " + leg.FootPosition);
                    }
                }

                yield return null;
            }

            movement.SetExternalMoveInput(Vector2.zero);
            Vector3 travelled = spider.transform.position - start;
            travelled.y = 0f;
            Assert.Greater(travelled.magnitude, 1.5f, "Spinne kommt im Hindernisparcours nicht voran");
            Assert.IsTrue(touchedObstacle, "Kein Fuß wurde auf einen Stein oder Ast gesetzt");
        }

        [UnityTest]
        public IEnumerator Vegetation_IsStreamedAndRendered()
        {
            yield return LoadWorld();
            VegetationSystem vegetation = Object.FindFirstObjectByType<VegetationSystem>();
            Assert.IsNotNull(vegetation, "VegetationSystem fehlt");
            yield return null;
            Assert.Greater(vegetation.LoadedChunkCount, 4, "Keine Vegetations-Chunks geladen");
            Assert.Greater(vegetation.RenderedInstancesLastFrame, 500, "Kaum Vegetation gerendert");
            Assert.Greater(Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length, 10, "Zu wenige Bäume");
            Assert.IsNotNull(Object.FindFirstObjectByType<WindController>(), "WindController fehlt");
        }

        [UnityTest]
        public IEnumerator Spider_OnStream_DetectsWaterAndSlowsDown()
        {
            yield return LoadWorld();
            GameObject spider = LoadedSpider();
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            SpiderSurfaceDetector surface = spider.GetComponent<SpiderSurfaceDetector>();
            SpiderFootEffects effects = spider.GetComponent<SpiderFootEffects>();
            Assert.IsNotNull(surface, "SpiderSurfaceDetector fehlt");

            float x = 44f;
            Vector3 target = new Vector3(x, WorldLayout.StreamWaterHeight(x) + 0.05f, WorldLayout.StreamCenterZ(x));
            movement.Teleport(target, spider.transform.rotation);
            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.SnapToGround();
            }

            yield return new WaitForSeconds(1f);
            Assert.IsTrue(surface.IsOnWater, "Spinne erkennt das Wasser nicht, Oberfläche: " + surface.CurrentSurface);
            Assert.Less(surface.SpeedMultiplier, 0.8f, "Wasser bremst die Spinne nicht");

            movement.SetExternalMoveInput(new Vector2(1f, 0f));
            yield return new WaitForSeconds(1.5f);
            movement.SetExternalMoveInput(Vector2.zero);
            Assert.Greater(effects.RipplesEmitted, 0, "Keine Wellenringe beim Laufen auf Wasser");
        }

        [UnityTest]
        public IEnumerator Audio_AmbienceAndFootstepsRun()
        {
            yield return LoadWorld();
            GameObject spider = LoadedSpider();
            AmbientAudio ambient = Object.FindFirstObjectByType<AmbientAudio>();
            Assert.IsNotNull(ambient, "AmbientAudio fehlt");
            Assert.IsTrue(ambient.IsRunning, "Ambient-Audio läuft nicht");

            SpiderFootstepAudio steps = spider.GetComponent<SpiderFootstepAudio>();
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            movement.SetExternalMoveInput(new Vector2(-1f, 0f));
            yield return new WaitForSeconds(1.5f);
            movement.SetExternalMoveInput(Vector2.zero);
            Assert.Greater(steps.StepsPlayed, 4, "Keine Trittgeräusche");
        }

        [UnityTest]
        public IEnumerator Spider_ClimbsOverLog()
        {
            yield return LoadWorld();
            GameObject spider = LoadedSpider();
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();

            // Stamm suchen: nah am Spawnpunkt, Richtung (-1.6, 0, 2.4).
            Vector3 reference = WorldLayout.SpawnPoint + new Vector3(-1.6f, 0f, 2.4f);
            CapsuleCollider log = null;
            float bestDist = float.MaxValue;
            foreach (CapsuleCollider col in Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None))
            {
                if (col.gameObject.name != "Log")
                {
                    continue;
                }

                Vector3 d = col.transform.position - reference;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    log = col;
                }
            }

            Assert.IsNotNull(log, "Baumstamm 'Log' nicht gefunden");
            Vector3 hd = log.transform.position - reference;
            hd.y = 0f;
            Assert.Less(hd.magnitude, 0.5f, "Baumstamm zu weit vom erwarteten Punkt: " + hd.magnitude);

            Vector3 axis = log.transform.forward;
            axis.y = 0f;
            axis = axis.normalized;
            Vector3 perp = Vector3.Cross(Vector3.up, axis).normalized;

            // Startpunkt quer vor dem Stamm, auf Terrain-Höhe.
            Vector3 start = log.transform.position - perp * 0.45f;
            start.y = Terrain.activeTerrain.SampleHeight(start) + Terrain.activeTerrain.transform.position.y + 0.02f;
            movement.Teleport(start, Quaternion.LookRotation(perp, Vector3.up));
            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.SnapToGround();
            }

            yield return new WaitForSeconds(0.5f);

            // Eingabe aus Kameraperspektive, damit die Spinne quer über den Stamm läuft.
            float dot = Vector3.Dot(spider.transform.position - log.transform.position, perp);
            float time = 0f;
            while (time < 8f && dot <= 0.4f)
            {
                time += Time.deltaTime;
                Vector3 camF = Camera.main.transform.forward;
                Vector3 camR = Camera.main.transform.right;
                camF.y = 0f;
                camR.y = 0f;
                camF = camF.normalized;
                camR = camR.normalized;
                movement.SetExternalMoveInput(new Vector2(Vector3.Dot(perp, camR), Vector3.Dot(perp, camF)));
                dot = Vector3.Dot(spider.transform.position - log.transform.position, perp);
                yield return null;
            }

            movement.SetExternalMoveInput(Vector2.zero);
            Assert.Greater(dot, 0.4f, "Spinne kommt nicht über den Baumstamm, Abstand hinter der Achse: " + dot);
        }
    }
}
