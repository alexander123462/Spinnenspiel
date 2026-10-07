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

        [UnityTest]
        public IEnumerator ControlsHelp_AutoHidesAndToggles()
        {
            yield return LoadWorld();
            GameObject spider = LoadedSpider();
            ControlsHelp help = spider.GetComponent<ControlsHelp>();
            Assert.IsNotNull(help, "ControlsHelp fehlt");
            Assert.IsTrue(help.Visible, "ControlsHelp must be visible at start.");

            // After the auto-hide delay it must be gone (AutoHideSeconds = 15).
            float t = 0f;
            while (t < 17f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(help.Visible, "ControlsHelp still visible after 17 s.");

            // F1 toggles it back on.
            help.Toggle();
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(help.Visible, "Toggle must show ControlsHelp again.");
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
                        Assert.IsFalse(Physics.CheckSphere(probe, 0.0015f, Physics.DefaultRaycastLayers & ~GameLayers.PlantMask, QueryTriggerInteraction.Ignore),
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

        [UnityTest]
        public IEnumerator PlantColliders_NearSpider()
        {
            yield return LoadWorld();
            VegetationColliderPool pool = Object.FindFirstObjectByType<VegetationColliderPool>();
            Assert.IsNotNull(pool, "VegetationColliderPool fehlt");
            Assert.Greater(pool.ActiveCount, 0, "Keine aktiven Pflanzen-Collider in Spinennähe");
            foreach (Transform child in pool.transform)
            {
                if (child.gameObject.activeSelf)
                {
                    Assert.AreEqual(GameLayers.Plants, child.gameObject.layer, "Falscher Layer: " + child.name);
                }
            }
        }

        [UnityTest]
        public IEnumerator WorldWeb_BuildsBetweenPlants()
        {
            yield return LoadWorld();
            GameObject spider = LoadedSpider();
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            SpiderWebPlacer placer = spider.GetComponent<SpiderWebPlacer>();
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(placer, "SpiderWebPlacer fehlt");
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");

            // Pflanzen-Collider vor der Netzplatz-Suche aktivieren (Auftrag 6.3d2).
            VegetationColliderPool pool = Object.FindFirstObjectByType<VegetationColliderPool>();
            Assert.IsNotNull(pool, "VegetationColliderPool fehlt");
            pool.radius = 12f;
            pool.maxColliders = 800;
            pool.focus = spider.transform;
            pool.ForceRefresh();
            Physics.SyncTransforms();

            // 4 Blickrichtungen relativ zum Start (waagrecht).
            Vector3 start = spider.transform.position;
            Vector3 fwd = spider.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd = fwd.normalized;
            Vector3 rightDir = Vector3.Cross(Vector3.up, fwd);
            Vector3[] blicke = { fwd, rightDir, -fwd, -rightDir };

            // Raster -10..10 m um den Start, Bodenpunkt pro Spalte, dann Netzplatz-Suche.
            int found = 0;
            Vector3 firstPoint = Vector3.zero;
            Vector3 firstBlick = fwd;
            Vector3[] firstAnker = null;
            bool haveFirst = false;
            for (int gx = -10; gx <= 10; gx++)
            {
                for (int gz = -10; gz <= 10; gz++)
                {
                    Vector3 col = new Vector3(start.x + gx, start.y + 20f, start.z + gz);
                    RaycastHit[] hits = Physics.RaycastAll(col, Vector3.down, 40f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    Vector3 ground = Vector3.zero;
                    bool groundFound = false;
                    for (int h = 0; h < hits.Length; h++)
                    {
                        if (hits[h].collider.transform == spider.transform ||
                            hits[h].collider.transform.IsChildOf(spider.transform))
                        {
                            continue; // eigene Kollider ignorieren
                        }
                        ground = hits[h].point;
                        groundFound = true;
                        break;
                    }
                    if (!groundFound)
                    {
                        continue;
                    }

                    foreach (Vector3 blick in blicke)
                    {
                        if (WebSiteFinder.TryFindNear(ground, blick, spider.transform, out Vector3[] anchors, out Vector3 center))
                        {
                            found++;
                            if (!haveFirst)
                            {
                                firstPoint = ground;
                                firstBlick = blick;
                                firstAnker = (Vector3[])anchors.Clone(); // Anker des ersten Treffers merken
                                haveFirst = true;
                            }
                        }
                    }
                }
            }

            Debug.Log("[WELTNETZ] Plaetze gefunden: " + found);
            Assert.GreaterOrEqual(found, 5, "Zu wenige Netzplaetze in der Testwelt: " + found);

            // Spinne auf den ersten Treffer teleportieren und Netz bauen lassen.
            movement.SetScripted(true);
            movement.Teleport(firstPoint + Vector3.up * 0.05f, Quaternion.LookRotation(firstBlick, Vector3.up));
            yield return new WaitForSeconds(0.5f);
            placer.viewOverride = firstBlick;
            builder.durationScale = 0.1f;
            Assert.IsNotNull(firstAnker, "Keine Anker des ersten Treffers gemerkt");
            Assert.IsTrue(placer.PlaceWeb(firstAnker), "PlaceWeb am ersten Platz fehlgeschlagen");

            float wait = 0f;
            while (builder.CurrentStage != OrbWeb.WebStage.Done && wait < 20f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "Netzbau nach 20 s nicht fertig");

            // Kontrollbilder: nah (0,9 m) und weit (2,5 m) aus Netznormale-Perspektive.
            Vector3 hub = builder.Web.Layout.Hub;
            Vector3 n = builder.Normal;
            GameObject camGo = new GameObject("WeltNetzCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            RenderTexture rt = new RenderTexture(1280, 720, 24);
            rt.Create();
            cam.targetTexture = rt;

            string dir = System.IO.Path.Combine(Application.dataPath, "../Logs/ci");
            System.IO.Directory.CreateDirectory(dir);

            // Wie in OrbWebTests.Builder_RecordsFrames: LookRotation ohne Up-Vektor
            // (n liegt fast in Blickrichtung - als Up-Vektor kippt das Bild).
            cam.transform.position = hub + n * 0.9f + Vector3.up * 0.15f;
            cam.transform.rotation = Quaternion.LookRotation(hub - cam.transform.position);
            SaveFrame(cam, rt, System.IO.Path.Combine(dir, "weltnetz.png"));

            cam.transform.position = hub + n * 2.5f + Vector3.up * 0.8f;
            cam.transform.rotation = Quaternion.LookRotation(hub - cam.transform.position);
            SaveFrame(cam, rt, System.IO.Path.Combine(dir, "weltnetz_weit.png"));

            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "weltnetz.png")), "weltnetz.png fehlt");
            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "weltnetz_weit.png")), "weltnetz_weit.png fehlt");
            Assert.IsTrue(builder.IsSittingInWeb, "Spinne sitzt nach dem Bau nicht in der Nabe");

            // Aufräumen: Kamera + RenderTexture.
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
        }

        [UnityTest]
        public IEnumerator Showcase_WebAndHunt()
        {
            yield return LoadWorld();
            GameObject spider = LoadedSpider();
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            SpiderWebPlacer placer = spider.GetComponent<SpiderWebPlacer>();
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(placer, "SpiderWebPlacer fehlt");
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");

            // Spawner stilllegen und vorhandene Fliegen entfernen (wie in den Predation-Tests).
            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner fehlt");
            Assert.IsNotNull(spawner.template, "Spawner-Vorlage fehlt");
            spawner.enabled = false;
            foreach (FlyingInsect f in Object.FindObjectsOfType<FlyingInsect>())
            {
                f.Remove();
            }

            // Pflanzen-Collider vor der Netzplatz-Suche aktivieren (wie in WorldWeb_BuildsBetweenPlants).
            VegetationColliderPool pool = Object.FindFirstObjectByType<VegetationColliderPool>();
            Assert.IsNotNull(pool, "VegetationColliderPool fehlt");
            pool.radius = 12f;
            pool.maxColliders = 800;
            pool.focus = spider.transform;
            pool.ForceRefresh();
            Physics.SyncTransforms();

            // Ersten Netzplatz suchen (gleiche Rasterlogik wie in WorldWeb_BuildsBetweenPlants).
            Vector3 start = spider.transform.position;
            Vector3 fwd = spider.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd = fwd.normalized;
            Vector3 rightDir = Vector3.Cross(Vector3.up, fwd);
            Vector3[] blicke = { fwd, rightDir, -fwd, -rightDir };
            bool haveFirst = false;
            Vector3 firstPoint = Vector3.zero;
            Vector3 firstBlick = fwd;
            Vector3[] firstAnker = null;
            for (int gx = -10; gx <= 10 && !haveFirst; gx++)
            {
                for (int gz = -10; gz <= 10 && !haveFirst; gz++)
                {
                    Vector3 col = new Vector3(start.x + gx, start.y + 20f, start.z + gz);
                    RaycastHit[] hits = Physics.RaycastAll(col, Vector3.down, 40f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    Vector3 ground = Vector3.zero;
                    bool groundFound = false;
                    for (int h = 0; h < hits.Length; h++)
                    {
                        if (hits[h].collider.transform == spider.transform ||
                            hits[h].collider.transform.IsChildOf(spider.transform))
                        {
                            continue; // eigene Kollider ignorieren
                        }
                        ground = hits[h].point;
                        groundFound = true;
                        break;
                    }
                    if (!groundFound)
                    {
                        continue;
                    }

                    foreach (Vector3 blick in blicke)
                    {
                        if (WebSiteFinder.TryFindNear(ground, blick, spider.transform, out Vector3[] anchors, out Vector3 center))
                        {
                            firstPoint = ground;
                            firstBlick = blick;
                            firstAnker = (Vector3[])anchors.Clone();
                            haveFirst = true;
                            break;
                        }
                    }
                }
            }
            Assert.IsTrue(haveFirst, "Kein Netzplatz in der Testwelt gefunden");

            // Spinne hinteleportieren und Netz bauen lassen.
            movement.SetScripted(true);
            movement.Teleport(firstPoint + Vector3.up * 0.05f, Quaternion.LookRotation(firstBlick, Vector3.up));
            yield return new WaitForSeconds(0.5f);
            placer.viewOverride = firstBlick;
            builder.durationScale = 0.35f;
            Assert.IsTrue(placer.PlaceWeb(firstAnker), "PlaceWeb am ersten Platz fehlgeschlagen");

            // Aufnahmekamera (1280x720) - wie in WorldWeb_BuildsBetweenPlants.
            GameObject camGo = new GameObject("ShowcaseCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            RenderTexture rt = new RenderTexture(1280, 720, 24);
            rt.Create();
            cam.targetTexture = rt;
            cam.transform.position = spider.transform.position + Vector3.up * 1f;
            cam.transform.rotation = Quaternion.LookRotation(spider.transform.position - cam.transform.position);
            string dir = System.IO.Path.Combine(Application.dataPath, "../Logs/ci");
            System.IO.Directory.CreateDirectory(dir);

            // show_0..show_3: je ein Bild pro Bausstufe, jeweils ~40 % in die Stufe hinein.
            // Stufendauern 5/12/5/20 s * durationScale 0.35 -> Wartezeit ~40 % davon.
            OrbWeb.WebStage[] aufnahmen =
            {
                OrbWeb.WebStage.Frame,
                OrbWeb.WebStage.Radii,
                OrbWeb.WebStage.AuxSpiral,
                OrbWeb.WebStage.CaptureSpiral
            };
            float[] warte = { 0.7f, 1.7f, 0.7f, 2.8f };
            float t = 0f;
            for (int s = 0; s < aufnahmen.Length; s++)
            {
                t = 0f;
                while (builder.CurrentStage < aufnahmen[s] && t < 30f)
                {
                    t += Time.deltaTime;
                    ShowCamera(cam, builder); // Kamera jeden Frame neu setzen (n wird erst mit dem Bau bekannt)
                    yield return null;
                }
                t = 0f;
                while (t < warte[s])
                {
                    t += Time.deltaTime;
                    ShowCamera(cam, builder);
                    yield return null;
                }
                ShowCamera(cam, builder);
                SaveFrame(cam, rt, System.IO.Path.Combine(dir, "show_" + s + ".png"));
            }

            // Bis Done warten.
            t = 0f;
            while (builder.CurrentStage != OrbWeb.WebStage.Done && t < 60f)
            {
                t += Time.deltaTime;
                ShowCamera(cam, builder);
                yield return null;
            }
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "Netzbau nach 60 s nicht fertig");
            Assert.IsTrue(builder.IsSittingInWeb, "Spinne sitzt nach dem Bau nicht in der Nabe");

            // show_4: Spinne in der Nabe.
            ShowCamera(cam, builder);
            SaveFrame(cam, rt, System.IO.Path.Combine(dir, "show_4.png"));

            // Fliege seitlich versetzt durch das Netz (wie in Predation_HuntsWrapsAndEats).
            Vector3 hub = builder.Web.Layout.Hub;
            Vector3 n = builder.Normal;
            Vector3 right = Vector3.Cross(Vector3.up, n).normalized;
            float seitVersatz = builder.Web.CaptureRadius * 0.8f;
            Vector3 flyStart = hub + n * 0.5f + right * seitVersatz;
            Vector3 ziel = hub - n * 0.5f + right * seitVersatz;
            GameObject fly = Object.Instantiate(spawner.template, flyStart, Quaternion.identity);
            fly.name = "ShowcaseFliege";
            fly.SetActive(true);
            FlyingInsect insect = fly.GetComponent<FlyingInsect>();
            Assert.IsNotNull(insect, "Fliege hat kein FlyingInsect");
            insect.home = flyStart;
            insect.FlyTo(ziel, true);
            insect.SetInitialDirection(ziel - flyStart);

            t = 0f;
            while (t < 5f && insect.State != FlyingInsect.InsectState.Caught)
            {
                t += Time.deltaTime;
                ShowCamera(cam, builder);
                yield return null;
            }
            Assert.AreEqual(FlyingInsect.InsectState.Caught, insect.State, "Fliege wurde nicht gefangen");

            // show_5: Fliege im Netz.
            ShowCamera(cam, builder);
            SaveFrame(cam, rt, System.IO.Path.Combine(dir, "show_5.png"));

            // Beute-Position und Startabstand der Spinne zur Nabe (für die Jagd-Schwellen).
            Vector3 beute = insect.CaughtPoint;
            // Relativschwelle: die Spinne muss sich mindestens 0.03 m auf die Beute
            // zubewegen (Startabstand ist bereits ~0.10 m, absolute Schranke zu knapp).
            float startZurBeute = Vector3.Distance(spider.transform.position, beute);
            float minZurBeute = startZurBeute;

            // Jagd: show_6 beim Einwickeln, show_7 beim Fressen in der Nabe.
            SpiderPredation predation = spider.GetComponent<SpiderPredation>();
            Assert.IsNotNull(predation, "SpiderPredation fehlt");
            SpiderHunger hunger = spider.GetComponent<SpiderHunger>();
            Assert.IsNotNull(hunger, "SpiderHunger fehlt");
            SpiderSilk silk = spider.GetComponent<SpiderSilk>();
            Assert.IsNotNull(silk, "SpiderSilk fehlt");
            hunger.Hunger = 0.8f;
            predation.durationScale = 0.5f;
            predation.StartHunt(insect);
            minZurBeute = Mathf.Min(minZurBeute, Vector3.Distance(spider.transform.position, beute));

            // Spinne jagt die Beute und kehrt zur Nabe zurueck; show_6,
            // sobald das Einwickeln losgeht (in derselben Schleife).
            bool beiBeute = false;
            bool show6 = false;
            t = 0f;
            while (t < 20f)
            {
                minZurBeute = Mathf.Min(minZurBeute, Vector3.Distance(spider.transform.position, beute));
                beiBeute = minZurBeute < startZurBeute - 0.03f;
                if (silk.IsSpinning && !show6)
                {
                    ShowCamera(cam, builder);
                    SaveFrame(cam, rt, System.IO.Path.Combine(dir, "show_6.png"));
                    show6 = true;
                }
                if (beiBeute && Vector3.Distance(spider.transform.position, hub) < 0.06f) break;
                t += Time.deltaTime;
                ShowCamera(cam, builder);
                yield return null;
            }
            Assert.IsTrue(beiBeute, "Spinne ist nicht zur Beute gelaufen: start=" + startZurBeute.ToString("F3") + " min=" + minZurBeute.ToString("F3"));
            yield return new WaitForSeconds(1f); // ca. 1 s nach Rueckkehr: Fressen in der Nabe
            ShowCamera(cam, builder);
            SaveFrame(cam, rt, System.IO.Path.Combine(dir, "show_7.png"));

            // Jagd zu Ende laufen lassen (Fliege wird gefressen).
            t = 0f;
            while (t < 30f && predation.IsHunting)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(predation.IsHunting, "Jagd lief nach 30 s nicht zu Ende");
            yield return null;

            // show_8: weite Ansicht.
            Vector3 weit = hub + n * 2.5f + Vector3.up * 0.8f;
            cam.transform.position = weit;
            cam.transform.rotation = Quaternion.LookRotation(hub - weit);
            SaveFrame(cam, rt, System.IO.Path.Combine(dir, "show_8.png"));

            // 9 Bilder vorhanden, Fliege am Ende gefressen (Unity-FakeNull: destroyed == null).
            for (int i = 0; i <= 8; i++)
            {
                string file = System.IO.Path.Combine(dir, "show_" + i + ".png");
                Assert.IsTrue(System.IO.File.Exists(file), "Bild fehlt: show_" + i + ".png");
            }
            Assert.IsTrue(insect == null, "Fliege wurde nicht gefressen (existiert noch)");

            // Aufräumen: Kamera + RenderTexture.
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
        }

        /// <summary>Showcase-Kamera aus der Netznormale (n ist erst nach Baubeginn bekannt).</summary>
        private static void ShowCamera(Camera cam, SpiderWebBuilder builder)
        {
            if (builder.Web == null || builder.Web.Layout == null) return;
            Vector3 hub = builder.Web.Layout.Hub;
            Vector3 nrm = builder.Normal;
            if (nrm.sqrMagnitude < 0.0001f) return;
            Vector3 pos = hub + nrm * 0.8f + Vector3.up * 0.12f;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(hub - pos); // ohne Up-Vektor, wie im Weltnetz-Test
        }

        /// <summary>Render die Kamera in die RenderTexture und speichert als PNG ab.</summary>
        private static void SaveFrame(Camera cam, RenderTexture rt, string file)
        {
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(file, png);
        }
    }
}
