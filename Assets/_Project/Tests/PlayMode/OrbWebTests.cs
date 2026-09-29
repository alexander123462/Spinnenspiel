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
