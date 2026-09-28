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
    }
}
