using System.Collections;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Netz 1 (Teil 1): Geometrie eines Radnetzes. Rahmen: Rechteck in der XY-Ebene.</summary>
    public class OrbWebTests
    {
        private static OrbWebLayout CreateLayout()
        {
            var anchors = new[]
            {
                new Vector3(-0.4f, 0f, 0f),
                new Vector3(0.4f, 0f, 0f),
                new Vector3(0.45f, 0.8f, 0f),
                new Vector3(-0.45f, 0.8f, 0f)
            };
            return new OrbWebLayout(anchors, 24, 0.025f, 0.04f, 42);
        }

        [Test]
        public void NormalIstUngefaehrParallelZuZ()
        {
            var layout = CreateLayout();
            float angle = Vector3.Angle(layout.Normal, Vector3.forward);
            Assert.That(angle, Is.LessThan(5f),
                "Normale weicht mehr als 5° von +/-Z ab: " + angle.ToString("0.00", CultureInfo.InvariantCulture));
        }

        [Test]
        public void Layout_WideFrame_RoundOrb()
        {
            var anker = new[]
            {
                new Vector3(-1f, 0.5f, 0f),
                new Vector3(1f, 0.5f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f)
            };
            var layout = new OrbWebLayout(anker);
            float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
            float minV = float.PositiveInfinity, maxV = float.NegativeInfinity;
            for (int i = 0; i < layout.CaptureSpiral.Count; i++)
            {
                Vector2 u = layout.ToPlane(layout.CaptureSpiral[i]);
                minU = Mathf.Min(minU, u.x); maxU = Mathf.Max(maxU, u.x);
                minV = Mathf.Min(minV, u.y); maxV = Mathf.Max(maxV, u.y);
            }
            float breite = maxU - minU;
            float hoehe = maxV - minV;
            Assert.That(layout.CaptureSpiral.Count, Is.GreaterThan(0), "Fangspirale leer");
            Assert.That(breite / hoehe, Is.LessThan(1.7f), "Spirale zu elliptisch: " + (breite / hoehe).ToString("0.00", CultureInfo.InvariantCulture));
        }

        [Test]
        public void RadiusEndenLiegenAmRahmen()
        {
            var layout = CreateLayout();
            Assert.AreEqual(24, layout.RadiusEnds.Count, "Erwartet 24 Radius-Enden");
            for (int i = 0; i < layout.RadiusEnds.Count; i++)
            {
                float d = layout.DistanceToFrame(layout.RadiusEnds[i]);
                Assert.That(d, Is.LessThan(0.02f),
                    "Radius " + i + " liegt " + d.ToString("0.000", CultureInfo.InvariantCulture) + " m vom Rahmen-Polygon.");
            }
        }

        [Test]
        public void HubLiegtOberhalbDesSchwerpunkts()
        {
            var layout = CreateLayout();
            Vector3 centroid = Vector3.zero;
            foreach (var a in layout.FrameAnchors) centroid += a;
            centroid /= layout.FrameAnchors.Length;
            Assert.Greater(layout.Hub.y, centroid.y,
                "Hub (" + layout.Hub + ") ist nicht oberhalb des Schwerpunkts (" + centroid + ").");
        }

        [Test]
        public void FangspiraleIstAusreichendLang()
        {
            var layout = CreateLayout();
            Assert.Greater(layout.CaptureSpiral.Count, 150,
                "Fangspirale hat nur " + layout.CaptureSpiral.Count + " Punkte.");
        }

        [Test]
        public void FangspiralpunkteLiegenInnerhalbDesRahmens()
        {
            var layout = CreateLayout();
            for (int i = 0; i < layout.CaptureSpiral.Count; i++)
            {
                Vector3 p = layout.CaptureSpiral[i];
                Assert.IsTrue(layout.IsInsideFrame(p),
                    "Fangspiralpunkt " + i + " (" + p + ") liegt außerhalb des Rahmens.");
            }
        }

        [Test]
        public void FangspiraleHatKeineSpruenge()
        {
            var layout = CreateLayout();
            for (int i = 1; i < layout.CaptureSpiral.Count; i++)
            {
                float d = Vector3.Distance(layout.CaptureSpiral[i - 1], layout.CaptureSpiral[i]);
                Assert.That(d, Is.LessThan(0.15f),
                    "Sprung an Index " + i + " ist " + d.ToString("0.000", CultureInfo.InvariantCulture) + " m.");
            }
        }

        [Test]
        public void OrbWebBautAlleFäden()
        {
            var layout = CreateLayout();
            var go = new GameObject("OrbWebTest");
            var web = go.AddComponent<OrbWeb>();
            web.Build(layout);

            int lines = 0;
            foreach (Transform child in go.transform)
                if (child.GetComponent<LineRenderer>() != null) lines++;

            Object.DestroyImmediate(go);
            Assert.GreaterOrEqual(lines, 26,
                "Erwartet >= 26 LineRenderer als Kinder, gefunden: " + lines);
        }

        [Test]
        public void Progress_RevealsStages()
        {
            var layout = CreateLayout();
            var go = new GameObject("OrbWebProgressTest");
            var web = go.AddComponent<OrbWeb>();
            web.Build(layout);

            // Bei Stage Radii (t=0.5): ca. Hälfte der Radien sichtbar, Fangspirale unsichtbar.
            web.SetProgress(OrbWeb.WebStage.Radii, 0.5f);
            int visible = 0;
            foreach (Transform child in go.transform)
            {
                if (!child.name.StartsWith("Radius"))
                    continue;
                LineRenderer lr = child.GetComponent<LineRenderer>();
                if (lr != null && lr.positionCount > 0)
                    visible++;
            }

            int total = layout.RadiusCount;
            Assert.That(visible, Is.InRange(total / 2 - 2, total / 2 + 2),
                "Sichtbare Radien bei Radii/0.5: " + visible + " von " + total + ", erwartet ca. " + (total / 2) + " (±2).");

            LineRenderer capture = go.transform.Find("CaptureSpiral").GetComponent<LineRenderer>();
            Assert.That(capture.positionCount, Is.Zero,
                "Fangspirale soll bei Stage Radii unsichtbar sein, positionCount=" + capture.positionCount + ".");

            // Bei Done: Hilfsspirale weg, Fangspirale voll.
            web.SetProgress(OrbWeb.WebStage.Done, 1f);
            LineRenderer aux = go.transform.Find("AuxSpiral").GetComponent<LineRenderer>();
            Assert.That(aux.positionCount, Is.Zero,
                "Hilfsspirale soll bei Done unsichtbar sein, positionCount=" + aux.positionCount + ".");
            Assert.AreEqual(layout.CaptureSpiral.Count, capture.positionCount,
                "Fangspirale soll bei Done vollständig sein (" + layout.CaptureSpiral.Count + " Punkte), gefunden: " + capture.positionCount + ".");

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator Builder_BuildsWebInOrder()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderSilk silk = spider.GetComponent<SpiderSilk>();
            Assert.IsNotNull(silk, "SpiderSilk fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");

            // Netz wie ScreenshotTool shot_netz.
            GameObject webGo = new GameObject("BuilderWeb");
            OrbWeb web = webGo.AddComponent<OrbWeb>();
            Vector3[] anchors =
            {
                new Vector3(5.02f, 1.15f, 2.0f),
                new Vector3(5.78f, 1.15f, 2.0f),
                new Vector3(5.72f, 0.02f, 2.0f),
                new Vector3(5.08f, 0.02f, 2.0f)
            };
            OrbWebLayout layout = new OrbWebLayout(anchors, 16, 0.02f, 0.03f, 7);
            web.Build(layout);

            // Spinne vor das Netz (Ebenen-Seite -Z).
            movement.SetScripted(true);
            movement.Teleport(new Vector3(5.4f, 0.6f, 1.9f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            builder.durationScale = 0.1f;
            builder.StartBuild(web);
            Assert.IsTrue(builder.IsBuilding, "StartBuild hat nicht gestartet");

            int lastStage = -1;
            bool spinningDuringCapture = false;
            float elapsed = 0f;
            while (builder.IsBuilding && elapsed < 15f)
        {
                Assert.GreaterOrEqual((int)builder.CurrentStage, lastStage,
                    "Baustufe geht zurück: " + builder.CurrentStage + " < " + lastStage);
                lastStage = (int)builder.CurrentStage;
                if (builder.CurrentStage == OrbWeb.WebStage.CaptureSpiral)
                {
                    spinningDuringCapture = spinningDuringCapture || silk.IsSpinning;
                }

                Assert.IsTrue(movement.IsScripted, "movement.IsScripted ist false während IsBuilding");
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsFalse(builder.IsBuilding, "Netzbau nach 15 s noch nicht fertig");
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "CurrentStage != Done");
            float d = Vector3.Distance(spider.transform.position, layout.Hub);
            Assert.That(d, Is.LessThan(0.1f),
                "Spinne liegt " + d.ToString("F3", CultureInfo.InvariantCulture) + " m vom Hub");
            Assert.IsFalse(silk.IsSpinning, "silk.IsSpinning ist nach dem Bau noch true");
            Assert.IsTrue(spinningDuringCapture, "silk.IsSpinning war nie true während CaptureSpiral");

            Object.DestroyImmediate(webGo);
        }

        [UnityTest]
        public IEnumerator Builder_RecordsFrames()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");
            WebBuildDemo demo = Object.FindFirstObjectByType<WebBuildDemo>();
            Assert.IsNotNull(demo, "WebBuildDemo fehlt");

            // Spinne vor das Netz (Ebenen-Seite -Z).
            movement.SetScripted(true);
            movement.Teleport(new Vector3(5.4f, 0.6f, 1.9f), Quaternion.LookRotation(Vector3.up, Vector3.back));

            OrbWeb web = demo.CreateWeb();
            Assert.IsNotNull(web, "CreateWeb lieferte null");
            builder.durationScale = 0.25f;
            builder.StartBuild(web);
            Assert.IsTrue(builder.IsBuilding, "StartBuild hat nicht gestartet");

            // Eigene Aufnahme-Kamera: aus -Z auf die Netzebene.
            GameObject camGo = new GameObject("RecCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(5.4f, 0.6f, 0.55f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(5.4f, 0.6f, 2.0f) - cam.transform.position);
            cam.fieldOfView = 55f;
            RenderTexture rt = new RenderTexture(960, 540, 24);
            rt.Create();
            cam.targetTexture = rt;

            string dir = System.IO.Path.Combine(Application.dataPath, "../Logs/ci");
            System.IO.Directory.CreateDirectory(dir);

            // 8 Zeitpunkte: je 2 pro Stufe (30 % und 80 % der Stufendauer * 0.25),
            // dazu 0.4 * 0.25 s Überweg pro Stufenanfang.
            float[] stageDurations = { 5f, 12f, 5f, 20f };
            float ds = builder.durationScale;
            var shots = new System.Collections.Generic.List<float>();
            float t = 0.4f * ds;
            for (int s = 0; s < stageDurations.Length; s++)
            {
                float dur = stageDurations[s] * ds;
                shots.Add(t + 0.3f * dur);
                shots.Add(t + 0.8f * dur);
                t += dur + 0.4f * ds;
            }

            int shot = 0;
            float elapsed = 0f;
            while (builder.IsBuilding && elapsed < 60f)
            {
                if (shot < shots.Count && elapsed >= shots[shot])
                {
                    SaveFrame(cam, rt, System.IO.Path.Combine(dir, "netzbau_" + shot + ".png"));
                    shot++;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Auf Done warten, dann das letzte Bild.
            float wait = 0f;
            while (builder.IsBuilding && wait < 20f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(builder.IsBuilding, "Netzbau nach 20 s nicht fertig");
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "CurrentStage != Done");
            // Batchmode: WaitForEndOfFrame nicht erlaubt, einfache Frame-Wartung.
            yield return new WaitForSeconds(0.1f);
            SaveFrame(cam, rt, System.IO.Path.Combine(dir, "netzbau_9.png"));

            // Alle 9 Bilder müssen existieren (0-7 während des Baus, 9 nach Done).
            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "netzbau_" + i + ".png")),
                    "netzbau_" + i + ".png fehlt");
            }
            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "netzbau_9.png")),
                "netzbau_9.png fehlt");

            // Aufräumen: Kamera + RenderTexture (und das Testnetz).
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(web.gameObject);
        }

        [UnityTest]
        public IEnumerator Builder_LegsHoldWebLines()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");

            // Gleiches Netz wie Builder_BuildsWebInOrder (durationScale 0.1).
            GameObject webGo = new GameObject("LegsHoldWeb");
            OrbWeb web = webGo.AddComponent<OrbWeb>();
            Vector3[] anchors =
            {
                new Vector3(5.02f, 1.15f, 2.0f),
                new Vector3(5.78f, 1.15f, 2.0f),
                new Vector3(5.72f, 0.02f, 2.0f),
                new Vector3(5.08f, 0.02f, 2.0f)
            };
            OrbWebLayout layout = new OrbWebLayout(anchors, 16, 0.02f, 0.03f, 7);
            web.Build(layout);

            movement.SetScripted(true);
            movement.Teleport(new Vector3(5.4f, 0.6f, 1.9f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            builder.durationScale = 0.1f;
            builder.StartBuild(web);
            Assert.IsTrue(builder.IsBuilding, "StartBuild hat nicht gestartet");

            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length, "Erwartet 8 Beine, gefunden: " + legs.Length);

            bool fourOnThreadDuringCapture = false;
            float elapsed = 0f;
            while (builder.IsBuilding && elapsed < 15f)
            {
                if (builder.CurrentStage == OrbWeb.WebStage.CaptureSpiral)
                {
                    fourOnThreadDuringCapture = fourOnThreadDuringCapture || CountLegsOnWeb(web, legs) >= 4;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsFalse(builder.IsBuilding, "Netzbau nach 15 s noch nicht fertig");
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "CurrentStage != Done");
            // Nach Done 0.5 s warten, damit die Beine absetzen.
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(fourOnThreadDuringCapture,
                "Während CaptureSpiral waren nie mindestens 4 Beine auf einem Faden.");

            int grounded = 0;
            int onThread = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                SpiderLeg leg = legs[i];
                if (!leg.IsGrounded)
                    continue;
                grounded++;
                if (web.TryClosestPoint(leg.FootPosition, 0.01f, out _))
                    onThread++;
            }
            Assert.GreaterOrEqual(grounded, 6,
                "Nur " + grounded + " von 8 Beinen geerdet.");
            Assert.AreEqual(grounded, onThread,
                "Nicht alle geerdeten Beine greifen einen Faden: " + onThread + "/" + grounded);

            Object.DestroyImmediate(webGo);
        }

        [UnityTest]
        public IEnumerator Builder_SitAtHub_LegsOnWeb()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");
            WebBuildDemo demo = Object.FindFirstObjectByType<WebBuildDemo>();
            Assert.IsNotNull(demo, "WebBuildDemo fehlt");

            OrbWeb web = demo.CreateWeb();
            Assert.IsNotNull(web, "CreateWeb lieferte null");

            // Spinne vor das Netz (Ebenen-Seite -Z), dann direkt in die Nabe setzen.
            movement.SetScripted(true);
            movement.Teleport(new Vector3(5.4f, 0.6f, 1.9f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            builder.SitAtHub(web);

            // Beine absetzen lassen.
            yield return new WaitForSeconds(0.8f);

            Assert.IsTrue(builder.IsSittingInWeb, "IsSittingInWeb ist false");
            Assert.IsTrue(movement.IsScripted, "movement.IsScripted ist false");
            float d = Vector3.Distance(spider.transform.position, web.Layout.Hub);
            Assert.That(d, Is.LessThan(0.06f),
                "Spinne liegt " + d.ToString("F3", CultureInfo.InvariantCulture) + " m vom Hub");

            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length, "Erwartet 8 Beine, gefunden: " + legs.Length);

            int grounded = 0;
            int onThread = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                SpiderLeg leg = legs[i];
                if (!leg.IsGrounded)
                    continue;
                grounded++;
                if (web.TryClosestPoint(leg.FootPosition, 0.01f, out _))
                    onThread++;
            }
            Assert.GreaterOrEqual(grounded, 6,
                "Nur " + grounded + " von 8 Beinen geerdet.");
            Assert.AreEqual(grounded, onThread,
                "Nicht alle geerdeten Beine greifen einen Faden: " + onThread + "/" + grounded);

            Object.DestroyImmediate(web.gameObject);
        }

        /// <summary>Zählt geerdete Beine, deren Fuß auf einem Faden des Netzes liegt.</summary>
        private static int CountLegsOnWeb(OrbWeb web, SpiderLeg[] legs)
        {
            int n = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i].IsGrounded && web.TryClosestPoint(legs[i].FootPosition, 0.01f, out _))
                    n++;
            }
            return n;
        }

        /// <summary>
        /// Netz 3a: WebSiteFinder findet am Klettertest-Netzplatz (zwei Stäbe bei
        /// x=5.0/5.8, y=0..1.2) vier Anker und daraus ein brauchbares Netz.
        /// </summary>
        [UnityTest]
        public IEnumerator SiteFinder_FindsNetzplatz()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Vector3 center = new Vector3(5.4f, 0.6f, 2.0f);
            Assert.IsTrue(WebSiteFinder.TryFind(center, Vector3.forward, null, out Vector3[] a),
                "TryFind findet am Klettertest-Netzplatz (5.4, 0.6, 2.0) keinen Platz");
            Assert.IsTrue(a[0].x < a[1].x, "TL.x muss kleiner als TR.x sein: " + a[0] + " / " + a[1]);
            Assert.IsTrue(a[0].y > a[3].y, "TL.y muss größer als BL.y sein: " + a[0] + " / " + a[3]);
            for (int i = 0; i < a.Length; i++)
            {
                float d = Vector3.Distance(center, a[i]);
                Assert.That(d, Is.LessThan(1.25f), "Anker " + i + " liegt " + d + " m vom Zentrum");
            }

            OrbWeb web = WebBuildDemo.CreateWebAt(a, null, "TestNetz");
            Assert.IsNotNull(web, "CreateWebAt lieferte null");
            float dh = Vector3.Distance(web.Layout.Hub, center);
            Assert.That(dh, Is.LessThan(0.5f), "Hub liegt " + dh.ToString("F3", CultureInfo.InvariantCulture) + " m vom Zentrum");
            Object.DestroyImmediate(web.gameObject);
        }

        /// <summary>
        /// Netz 3a: In freier Luft findet der Finder nichts. (0, 0.6, -6) ist in
        /// der Klettertest-Szene frei: der einzige Naheobjekt ist der Boden (Oberkante
        /// y=0); die flachsten Abwärtsstrahlen (11.25°) treffen ihn erst in
        /// 0.6/tan(11.25°) ≈ 3.0 m > Reichweite 1.2 m, und TL/TR haben gar keinen Treffer.
        /// </summary>
        [UnityTest]
        public IEnumerator SiteFinder_NothingInOpenSpace()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Vector3 p = new Vector3(0f, 0.6f, -6f);
            Assert.IsFalse(WebSiteFinder.TryFind(p, Vector3.forward, null, out _),
                "TryFind findet am freien Punkt (0, 0.6, -6) unerwartet einen Netzplatz");
        }

        /// <summary>
        /// Netz 3b: SpiderWebPlacer baut per TryPlaceWebNow ein Netz am
        /// Klettertest-Netzplatz, die Spinne sitzt danach im fertigen Netz.
        /// </summary>
        [UnityTest]
        public IEnumerator Placer_BuildsWebAtNetzplatz()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderWebPlacer placer = spider.GetComponent<SpiderWebPlacer>();
            Assert.IsNotNull(placer, "SpiderWebPlacer fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");

            // z=1.8: damit der erste TryFindNear-Punkt (d=0.2) exakt in der
            // Stab-Ebene z=2.0 liegt (Stäbe sind nur 0,04 m dünn; z=1.7 würde
            // alle Gitterpunkte daneben setzen).
            spider.transform.position = new Vector3(5.4f, 0.05f, 1.8f);
            spider.transform.rotation = Quaternion.identity;
            yield return new WaitForSeconds(0.5f);

            placer.viewOverride = Vector3.forward;
            builder.durationScale = 0.1f;

            Assert.IsTrue(placer.TryPlaceWebNow(), "TryPlaceWebNow scheiterte am Klettertest-Netzplatz");
            Assert.IsTrue(builder.IsBuilding, "Builder baut nicht");

            float t = 0f;
            while (builder.CurrentStage != OrbWeb.WebStage.Done && t < 15f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "Bau nicht fertig nach 15 s");
            Assert.AreEqual(1, placer.WebCount, "WebCount != 1: " + placer.WebCount);
            Assert.IsTrue(builder.IsSittingInWeb, "Spinne sitzt nicht im Netz");
        }

        /// <summary>
        /// Netz 3b: SpiderWebPlacer hält maxWebs = 3, das älteste Netz wird
        /// zerstört.
        /// </summary>
        [UnityTest]
        public IEnumerator Placer_KeepsMaxWebs()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderWebPlacer placer = spider.GetComponent<SpiderWebPlacer>();
            Assert.IsNotNull(placer, "SpiderWebPlacer fehlt");

            GameObject first = new GameObject("TestWeb0");
            first.AddComponent<OrbWeb>();
            placer.RegisterWeb(first.GetComponent<OrbWeb>());
            for (int i = 1; i < 4; i++)
            {
                GameObject go = new GameObject("TestWeb" + i);
                go.AddComponent<OrbWeb>();
                placer.RegisterWeb(go.GetComponent<OrbWeb>());
            }
            yield return null;

            Assert.AreEqual(3, placer.WebCount, "WebCount != 3: " + placer.WebCount);
            Assert.IsTrue(first == null, "Ältestes Netz wurde nicht entfernt");
        }

        /// <summary>
        /// Netz 3c: Unter einem gespannten Brückenfaden findet die Spinne
        /// einen Netzplatz (wie echte Radnetzspinnen) und baut ein Netz
        /// darunter.
        /// </summary>
        [UnityTest]
        public IEnumerator Web_FromBridgeThread()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");
            SpiderWebPlacer placer = spider.GetComponent<SpiderWebPlacer>();
            Assert.IsNotNull(placer, "SpiderWebPlacer fehlt");

            // Brückenfaden zwischen zwei gleich hohen Punkten.
            GameObject threadGo = new GameObject("BridgeThread");
            SilkThread faden = threadGo.AddComponent<SilkThread>();
            faden.Init(new Vector3(-1.5f, 1.2f, 1.5f), new Vector3(1.5f, 1.2f, 1.5f), 0.01f);
            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(WebSiteFinder.TryFindFromThread(faden, new Vector3(0f, 1.2f, 1.5f), spider.transform, out Vector3[] a),
                "TryFindFromThread findet unter dem Brückenfaden keinen Platz");
            Assert.IsTrue(a[0].y > 0.9f, "a[0].y muss am Faden hängen: " + a[0]);
            Assert.IsTrue(a[3].y < 0.3f, "a[3].y muss unten am Boden sein: " + a[3]);
            Assert.IsTrue(a[0].x < a[1].x, "a[0].x muss kleiner als a[1].x sein: " + a[0] + " / " + a[1]);

            // Spinne auf den Faden: von dort baut TryPlaceWebNow das Netz darunter.
            movement.AttachToThread(faden, 0.5f);
            yield return new WaitForSeconds(0.5f);
            builder.durationScale = 0.1f;
            Assert.IsTrue(placer.TryPlaceWebNow(), "TryPlaceWebNow scheitert unter dem Brückenfaden");
            Assert.IsTrue(builder.IsBuilding, "Builder baut nicht");

            float elapsed = 0f;
            while (builder.IsBuilding && elapsed < 15f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsFalse(builder.IsBuilding, "Netzbau nach 15 s noch nicht fertig");
            Assert.AreEqual(OrbWeb.WebStage.Done, builder.CurrentStage, "CurrentStage != Done");

            Object.DestroyImmediate(threadGo);
        }

        /// <summary>Render die Kamera in die RenderTexture und speichert als PNG ab.</summary>
        private static void SaveFrame(Camera cam, RenderTexture rt, string file)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(file, png);
        }
    }
}
