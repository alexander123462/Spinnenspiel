using System.Collections;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Milestone 7 (Teil 1): Seidenfaden spannen – Faden-Objekt, Ausfahren, Straffziehen (Szene "Klettertest").</summary>
    public class SilkTests
    {
        private const string SceneName = "Klettertest";

        private GameObject spider;
        private SpiderMovement movement;
        private SpiderSilk silk;

        private IEnumerator Setup()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            silk = spider.GetComponent<SpiderSilk>();
            Assert.IsNotNull(silk, "SpiderSilk fehlt");
            Assert.IsNotNull(silk.spinnerets, "Spinnerets-Transform fehlt");
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest]
        public IEnumerator Thread_BoardExistingThread()
        {
            yield return Setup();

            // Vorhandener Faden zwischen zwei Bäumen: Anker A nah am Boden, Anker B darüber.
            Vector3 a = new Vector3(2f, 0.02f, -1f);
            Vector3 b = new Vector3(2f, 1.2f, 1.5f);
            GameObject threadGo = new GameObject("TestFaden");
            SilkThread faden = threadGo.AddComponent<SilkThread>();
            faden.Init(a, b, 0.02f);
            Assert.IsTrue(SilkThread.All.Contains(faden), "Faden ist nicht in SilkThread.All");

            yield return new WaitForSeconds(0.5f);

            // Spinne neben Anker A (0.12 m seitlich), Blickrichtung zu B - aber NICHT am Faden.
            movement.Teleport(new Vector3(2f, 0.02f, -1.12f),
                Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.z - a.z), Vector3.up));
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(movement.CurrentThread, "Spinne ist direkt nach dem Teleport bereits am Faden");

            // Vorwärts laufen: die Spinne sollte auf den Faden steigen.
            movement.SetExternalMoveLocal(new Vector2(0f, 1f));
            float t = 0f;
            while (t < 3f && movement.CurrentThread != faden)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreSame(faden, movement.CurrentThread, "Spinne ist nicht auf den vorhandenen Faden gestiegen");

            // Eingabe halten: die Spinne klettert den Faden hinauf.
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(movement.transform.position.y > 0.3f,
                string.Format(CultureInfo.InvariantCulture, "Spinne ist nach dem Einsteigen nicht höher als 0.3 m (y={0:F2})",
                    movement.transform.position.y));

            movement.SetExternalMoveLocal(null);
        }

        [UnityTest]
        public IEnumerator Silk_ThreadToTrunk_Attaches()
        {
            yield return Setup();

            // Spinne oben auf dem Block (Oberkante y=0.4), Blick auf den Stamm (-X).
            movement.Teleport(new Vector3(3f, 0.4f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            bool attachedFired = false;
            SilkThread attachedThread = null;
            silk.ThreadAttached += th =>
            {
                attachedFired = true;
                attachedThread = th;
            };

            Vector3 dir = (new Vector3(-3f, 1.0f, 0f) - silk.spinnerets.position).normalized;
            SilkThread thread = silk.ShootThread(dir);
            Assert.IsNotNull(thread, "Faden nach Stamm ist null – Treffer erwartet");
            Assert.AreSame(thread, silk.LastThread, "LastThread zeigt nicht auf den neuen Faden");

            yield return new WaitForSeconds(1.0f);

            string stats = string.Format(CultureInfo.InvariantCulture,
                "Extend01={0:F2} Length={1:F3} anchorA=({2:F2},{3:F2},{4:F2}) anchorB=({5:F2},{6:F2},{7:F2}) attached={8}",
                thread.Extend01, thread.Length,
                thread.anchorA.x, thread.anchorA.y, thread.anchorA.z,
                thread.anchorB.x, thread.anchorB.y, thread.anchorB.z,
                attachedFired);

            Assert.AreEqual(1f, thread.Extend01, 1e-4f, "Faden ist nach 1 s nicht vollst&auml;ndig ausgefahren: " + stats);
            Assert.IsTrue(thread.Length >= 5.3f && thread.Length <= 6.2f,
                "Fadenl&auml;nge au&szlig;erhalb 5.3–6.2 m: " + stats);
            Assert.IsTrue(Mathf.Abs(thread.anchorB.x - (-3f)) < 0.2f,
                "End-Anker liegt nicht am Stamm (x): " + stats);
            Assert.IsTrue(SilkThread.All.Contains(thread), "SilkThread.All enth&auml;lt den Faden nicht: " + stats);
            Assert.IsTrue(attachedFired, "ThreadAttached wurde nicht ausgel&ouml;st: " + stats);
            Assert.AreSame(thread, attachedThread, "ThreadAttached hat einen anderen Faden &uuml;bergeben: " + stats);
        }

        [UnityTest]
        public IEnumerator Silk_ShotIntoSky_Misses()
        {
            yield return Setup();

            // Spinne auf dem Block, Schuss senkrecht nach oben – dort ist nichts zu treffen.
            movement.Teleport(new Vector3(3f, 0.4f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            SilkThread thread = silk.ShootThread(Vector3.up);
            Assert.IsNull(thread, "Schuss ins Leere hat unerwartet einen Faden erzeugt");
        }

        [UnityTest]
        public IEnumerator Silk_ShootIntoGroundNearby_Rejected()
        {
            yield return Setup();

            // Spinne auf dem Boden, Blick +X: Schuss nach vorn-unten trifft den Boden direkt vor ihr.
            movement.Teleport(new Vector3(0f, 0f, -2f), Quaternion.LookRotation(Vector3.right, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            Vector3 dir = spider.transform.forward + Vector3.down * 0.6f;
            SilkThread thread = silk.ShootThread(dir);
            Assert.IsNull(thread, "Schuss in den Boden vor der Spinne hat unerwartet einen Faden erzeugt");

            // Nach 1 s: keine Fadenverbindung, Spinne steht noch aufrecht und haftet.
            yield return new WaitForSeconds(1.0f);

            float dotUp = Vector3.Dot(spider.transform.up, Vector3.up);
            string stats = string.Format(CultureInfo.InvariantCulture,
                "thread={0} upDot={1:F3} attached={2} pos=({3:F2},{4:F2},{5:F2})",
                movement.CurrentThread != null, dotUp, movement.IsAttached,
                spider.transform.position.x, spider.transform.position.y, spider.transform.position.z);

            Assert.IsNull(movement.CurrentThread, "Spinne ist unerwartet am Faden: " + stats);
            Assert.IsTrue(dotUp > 0.9f, "Spinne liegt nach dem Schuss in den Boden nicht aufrecht: " + stats);
            Assert.IsTrue(movement.IsAttached, "Spinne haftet nach dem Schuss in den Boden nicht: " + stats);
        }

        [UnityTest]
        public IEnumerator Silk_CrossThread_ToTrunk()
        {
            yield return Setup();

            // Spinne oben auf dem Block (Oberkante y=0.4), Blick auf den Stamm (-X).
            movement.Teleport(new Vector3(3f, 0.4f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            Vector3 dir = (new Vector3(-3f, 1.0f, 0f) - silk.spinnerets.position).normalized;
            SilkThread thread = silk.ShootThread(dir);
            Assert.IsNotNull(thread, "Faden nach Stamm ist null – Treffer erwartet");

            // Auto-Anhaken an den Faden (autoAttach) abwarten, max 5 s.
            float wait = 0f;
            while (movement.CurrentThread == null && wait < 5f)
            {
                yield return null;
                wait += Time.deltaTime;
            }
            Assert.IsNotNull(movement.CurrentThread, "Spinne hat sich in 5 s nicht am Faden verankert");
            Assert.AreSame(thread, movement.CurrentThread, "Spinne ist am falschen Faden verankert");

            // Vorwärts (Richtung Stamm) den Faden entlanglaufen.
            movement.SetExternalMoveLocal(new Vector2(0f, 1f));

            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            float maxDist = 0f;
            int groundedFrames = 0;
            int frames = 0;
            float runTime = 0f;
            while (movement.CurrentThread != null && runTime < 20f)
            {
                SilkThread th = movement.CurrentThread;
                Vector3 onThread = th.PointAt(th.ClosestT(spider.transform.position));
                maxDist = Mathf.Max(maxDist, (spider.transform.position - onThread).magnitude);

                int groundedLegs = 0;
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] != null && legs[i].IsGrounded)
                    {
                        groundedLegs++;
                    }
                }
                if (groundedLegs >= 4)
                {
                    groundedFrames++;
                }

                frames++;
                runTime += Time.deltaTime;
                yield return null;
            }

            float share = frames > 0 ? (float)groundedFrames / frames : 0f;
            string stats = string.Format(CultureInfo.InvariantCulture,
                "runTime={0:F1} maxDist={1:F4} groundedShare={2:F2} frames={3} pos=({4:F2},{5:F2},{6:F2}) attached={7} thread={8}",
                runTime, maxDist, share, frames,
                spider.transform.position.x, spider.transform.position.y, spider.transform.position.z,
                movement.IsAttached, movement.CurrentThread != null);

            Assert.IsTrue(maxDist <= 0.05f, "Spinne ist zu weit von der Fadenlinie abgewichen: " + stats);
            Assert.IsTrue(share >= 0.7f, "In < 70 % der Frames halten 4+ Beine den Faden: " + stats);
            Assert.IsNull(movement.CurrentThread, "Spinne ist am Ende nicht vom Faden los: " + stats);
            Assert.IsTrue(movement.IsAttached, "Spinne haftet nach dem Überqueren nicht an der Fläche: " + stats);
            Assert.IsTrue(Mathf.Abs(spider.transform.position.x - (-3f)) < 0.4f, "Spinne nicht am Stamm (x): " + stats);
        }

        [UnityTest]
        public IEnumerator Silk_LetGo_Falls()
        {
            yield return Setup();

            // Wie im Querungstest: Faden spannen, anhängen.
            movement.Teleport(new Vector3(3f, 0.4f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            Vector3 dir = (new Vector3(-3f, 1.0f, 0f) - silk.spinnerets.position).normalized;
            SilkThread thread = silk.ShootThread(dir);
            Assert.IsNotNull(thread, "Faden nach Stamm ist null – Treffer erwartet");

            float wait = 0f;
            while (movement.CurrentThread == null && wait < 5f)
            {
                yield return null;
                wait += Time.deltaTime;
            }
            Assert.IsNotNull(movement.CurrentThread, "Spinne hat sich in 5 s nicht am Faden verankert");

            // 1 s Richtung Stamm den Faden entlanglaufen, dann loslassen.
            movement.SetExternalMoveLocal(new Vector2(0f, 1f));
            yield return new WaitForSeconds(1.0f);
            float yAtLetGo = spider.transform.position.y;
            movement.LetGo();
            Assert.IsNull(movement.CurrentThread, "LetGo hat den Fadenbezug nicht entfernt");

            // 2 s warten: Spinne fällt und haftet wieder auf dem Boden.
            yield return new WaitForSeconds(2.0f);

            string stats = string.Format(CultureInfo.InvariantCulture,
                "yAtLetGo={0:F3} yEnd={1:F3} attached={2} thread={3}",
                yAtLetGo, spider.transform.position.y, movement.IsAttached, movement.CurrentThread != null);

            Assert.IsNull(movement.CurrentThread, "Spinne ist nach dem Loslassen noch am Faden: " + stats);
            Assert.IsTrue(spider.transform.position.y < yAtLetGo, "Spinne ist nicht tiefer als beim Loslassen: " + stats);
            Assert.IsTrue(movement.IsAttached, "Spinne haftet nach dem Fallen nicht wieder: " + stats);
            Assert.IsTrue(spider.transform.position.y < 0.2f, "Spinne haftet nicht am Boden (y < 0.2): " + stats);
        }

        [UnityTest]
        public IEnumerator Silk_SpinningPose()
        {
            yield return Setup();

            movement.Teleport(new Vector3(3f, 0.4f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            // Ausgangslage des Hinterleibs merken (vor dem Spinn-Anlauf).
            Transform abdomen = spider.transform.Find("Body/Abdomen");
            Assert.IsNotNull(abdomen, "Abdomen-Transform fehlt");
            Vector3 abdStartPos = abdomen.localPosition;
            Quaternion abdStartRot = abdomen.localRotation;

            // Beide Hinterbeine (PairIndex 3) bestimmen.
            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();
            SpiderLeg rearA = null;
            SpiderLeg rearB = null;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] != null && legs[i].PairIndex == 3)
                {
                    if (rearA == null)
                    {
                        rearA = legs[i];
                    }
                    else
                    {
                        rearB = legs[i];
                    }
                }
            }
            Assert.IsNotNull(rearA, "Hinterbein (PairIndex 3) fehlt");
            Assert.IsNotNull(rearB, "Zweites Hinterbein (PairIndex 3) fehlt");

            Vector3 dir = (new Vector3(-3f, 1.0f, 0f) - silk.spinnerets.position).normalized;
            SilkThread thread = silk.ShootThread(dir);
            Assert.IsNotNull(thread, "Faden nach Stamm ist null – Treffer erwartet");

            // Erste 0.3 s: Spinn-Animation aktiv, mindestens ein Hinterbein an den Spinnwarzen.
            bool spinningSeen = false;
            float bestDist = float.MaxValue;
            float t = 0f;
            while (t < 0.3f)
            {
                if (silk.IsSpinning)
                {
                    spinningSeen = true;
                }

                Vector3 sp = silk.spinnerets != null ? silk.spinnerets.position : spider.transform.position;
                bestDist = Mathf.Min(bestDist, (rearA.FootPosition - sp).magnitude);
                bestDist = Mathf.Min(bestDist, (rearB.FootPosition - sp).magnitude);
                t += Time.deltaTime;
                yield return null;
            }

            string stats = string.Format(CultureInfo.InvariantCulture,
                "spinningSeen={0} bestDist={1:F4}", spinningSeen, bestDist);
            Assert.IsTrue(spinningSeen, "IsSpinning war in den ersten 0.3 s nie true");
            Assert.IsTrue(bestDist <= 0.05f, "Kein Hinterbein innerhalb 0.05 m der Spinnwarzen: " + stats);

            // Nach 1.5 s: Animation beendet, Hinterleib zurück in der Ausgangslage.
            yield return new WaitForSeconds(1.5f);

            string endStats = string.Format(CultureInfo.InvariantCulture,
                "IsSpinning={0} posDelta={1:F4} angleDelta={2:F2}",
                silk.IsSpinning,
                Vector3.Distance(abdomen.localPosition, abdStartPos),
                Quaternion.Angle(abdomen.localRotation, abdStartRot));
            Assert.IsFalse(silk.IsSpinning, "IsSpinning ist nach 1.5 s noch true: " + endStats);
            Assert.IsTrue(Quaternion.Angle(abdomen.localRotation, abdStartRot) < 2f,
                "Abdomen weicht in der Rotation von der Ausgangslage ab: " + endStats);
            Assert.IsTrue(Vector3.Distance(abdomen.localPosition, abdStartPos) < 0.002f,
                "Abdomen weicht in der Position von der Ausgangslage ab: " + endStats);
        }

        [UnityTest]
        public IEnumerator Silk_Dragline_PlacesAnchors()
        {
            yield return Setup();

            var dragline = spider.GetComponent<SpiderDragline>();
            Assert.IsNotNull(dragline, "SpiderDragline fehlt");

            // Spinne bei (0, 0, -2) mit Blick +X, 4 s vorwärts.
            movement.Teleport(new Vector3(0f, 0f, -2f), Quaternion.LookRotation(Vector3.right, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            movement.SetExternalMoveLocal(new Vector2(0f, 1f));
            yield return new WaitForSeconds(4f);

            string stats = string.Format(CultureInfo.InvariantCulture,
                "anchors={0} lastAnchor=({1:F2},{2:F2},{3:F2}) spider=({4:F2},{5:F2},{6:F2}) lineEnabled={7}",
                dragline.AnchorCount,
                dragline.LastAnchor.x, dragline.LastAnchor.y, dragline.LastAnchor.z,
                spider.transform.position.x, spider.transform.position.y, spider.transform.position.z,
                dragline.Line != null && dragline.Line.enabled);

            Assert.IsTrue(dragline.AnchorCount >= 3, "Weniger als 3 Anker nach 4 s Vorwärts: " + stats);
            float dist = (spider.transform.position - dragline.LastAnchor).magnitude;
            Assert.IsTrue(dist <= dragline.AnchorSpacing + 0.1f, "Abstand LastAnchor↔Spinne > anchorSpacing + 0.1: " + stats);
            Assert.IsFalse(dragline.Line.enabled, "Dragline beim Laufen sichtbar: " + stats);
        }

        [UnityTest]
        public IEnumerator Silk_Dragline_CatchesFall()
        {
            yield return Setup();

            // Unterseite des Überhangs, kopfüber: Blick nach hinten (-Z), Up nach unten.
            movement.Teleport(new Vector3(0f, 2.0f, 2.2f), Quaternion.LookRotation(Vector3.back, Vector3.down));
            yield return new WaitForSeconds(0.5f);

            movement.LetGo();

            // Nach 1.5 s muss der Sicherheitsfaden die Spinne halten: am Seil, fast in Startshöhe.
            yield return new WaitForSeconds(1.5f);
            float yCatched = spider.transform.position.y;
            bool onRopeAtCatch = movement.IsOnRope;
            bool attachedAtCatch = movement.IsAttached;
            string catchStats = string.Format(CultureInfo.InvariantCulture,
                "y={0:F3} IsOnRope={1} IsAttached={2} (t=1.5s nach LetGo)", yCatched, onRopeAtCatch, attachedAtCatch);
            Assert.IsTrue(onRopeAtCatch, "Nach 1.5 s ist die Spinne nicht am Seil: " + catchStats);
            Assert.IsTrue(yCatched > 1.5f && yCatched < 1.99f, "Höhe beim Fang außerhalb 1.5–1.99: " + catchStats);

            // Ablassen, bis wieder angedockt (max. 10 s).
            movement.SetExternalRopeInput(1f);
            float wait = 0f;
            while (!movement.IsAttached && wait < 10f)
            {
                yield return null;
                wait += Time.deltaTime;
            }
            movement.SetExternalRopeInput(null);

            float yEnd = spider.transform.position.y;
            string endStats = string.Format(CultureInfo.InvariantCulture,
                "y={0:F3} IsOnRope={1} IsAttached={2} WinkelUp={3:F1}° AblassenZeit={4:F1}s",
                yEnd, movement.IsOnRope, movement.IsAttached, Vector3.Angle(spider.transform.up, Vector3.down), wait);
            Assert.IsTrue(movement.IsAttached, "Ablassen nicht innerhalb 10 s angedockt: " + endStats);
            Assert.IsFalse(movement.IsOnRope, "Nach Ablassen immer noch am Seil: " + endStats);
            Assert.IsTrue(yEnd < 0.15f, "Endhöhe nicht < 0.15: " + endStats);
        }

        [UnityTest]
        public IEnumerator Silk_Rope_ClimbBack()
        {
            yield return Setup();

            // Unterseite des Überhangs, kopfüber: Blick nach hinten (-Z), Up nach unten.
            movement.Teleport(new Vector3(0f, 2.0f, 2.2f), Quaternion.LookRotation(Vector3.back, Vector3.down));
            yield return new WaitForSeconds(0.5f);

            movement.LetGo();

            // 1.0 s am Seil hängen, dann 1.0 s ablassen.
            yield return new WaitForSeconds(1.0f);
            movement.SetExternalRopeInput(1f);
            yield return new WaitForSeconds(1.0f);

            // Hochklettern, bis wieder kopfüber an der Decke angedockt (max. 8 s).
            movement.SetExternalRopeInput(-1f);
            float wait = 0f;
            while (!movement.IsAttached && wait < 8f)
            {
                yield return null;
                wait += Time.deltaTime;
            }
            movement.SetExternalRopeInput(null);

            float yEnd = spider.transform.position.y;
            float angle = Vector3.Angle(spider.transform.up, Vector3.down);
            string stats = string.Format(CultureInfo.InvariantCulture,
                "y={0:F3} IsOnRope={1} IsAttached={2} Winkel(Up,down)={3:F1}° KletterZeit={4:F1}s",
                yEnd, movement.IsOnRope, movement.IsAttached, angle, wait);
            Assert.IsTrue(movement.IsAttached, "Hochklettern: nicht innerhalb 8 s angedockt: " + stats);
            Assert.IsTrue(angle < 45f, "Spinne nicht kopfüber an der Decke (Winkel > 45°): " + stats);
        }

        [UnityTest]
        public IEnumerator SpinningPose_ScriptedApi()
        {
            yield return Setup();

            movement.Teleport(new Vector3(3f, 0.4f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            Transform abdomen = spider.transform.Find("Body/Abdomen");
            Assert.IsNotNull(abdomen, "Abdomen-Transform fehlt");
            Quaternion startRot = abdomen.localRotation;

            silk.BeginSpinningPose();
            yield return new WaitForSeconds(0.4f);

            // 0.6 s: jeden Frame die Spinnpose weiterführen (Fadenpunkt 0.3 m vor der Spinne).
            float t = 0f;
            while (t < 0.6f)
            {
                silk.UpdateSpinningPose(spider.transform.position - spider.transform.forward * 0.3f);
                t += Time.deltaTime;
                yield return null;
            }

            string stats = string.Format(CultureInfo.InvariantCulture,
                "IsSpinning={0} angleDelta={1:F2}",
                silk.IsSpinning, Quaternion.Angle(abdomen.localRotation, startRot));
            Assert.IsTrue(silk.IsSpinning, "IsSpinning ist nicht true: " + stats);
            Assert.IsTrue(Quaternion.Angle(abdomen.localRotation, startRot) > 15f,
                "Abdomen hebt sich nicht um > 15°: " + stats);

            silk.EndSpinningPose();
            yield return null;

            string endStats = string.Format(CultureInfo.InvariantCulture,
                "IsSpinning={0} angleDelta={1:F2}",
                silk.IsSpinning, Quaternion.Angle(abdomen.localRotation, startRot));
            Assert.IsFalse(silk.IsSpinning, "IsSpinning ist nach EndSpinningPose noch true: " + endStats);
            Assert.IsTrue(Quaternion.Angle(abdomen.localRotation, startRot) < 0.5f,
                "Abdomen nicht exakt in der Ausgangspose: " + endStats);
        }

        [UnityTest]
        public IEnumerator Scripted_MovementIgnoresInput()
        {
            yield return Setup();

            movement.Teleport(new Vector3(3f, 0.4f, 0f), Quaternion.LookRotation(Vector3.left, Vector3.up));
            yield return new WaitForSeconds(0.3f);

            movement.SetScripted(true);
            Vector3 startPos = spider.transform.position;
            movement.SetExternalMoveInput(new Vector2(0f, 1f));
            yield return new WaitForSeconds(0.5f);

            float delta = (spider.transform.position - startPos).magnitude;
            Assert.IsTrue(delta < 0.001f,
                "Spinne bewegt sich im Skriptmodus trotz Eingabe: " + delta.ToString(CultureInfo.InvariantCulture));

            movement.SetScripted(false);
            movement.SetExternalMoveInput(null);
        }
    }
}
