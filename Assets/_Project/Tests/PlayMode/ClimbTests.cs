using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Milestone 3: Klettern an Wand, ueber Kante und am Stamm (Szene "Klettertest").</summary>
    public class ClimbTests
    {
        private const string SceneName = "Klettertest";

        private GameObject spider;
        private SpiderMovement movement;
        private SpiderLeg[] legs;

        /// <summary>Messwerte eines Kletterlaufs. Pro Frame werden hier Werte aufgeloest.</summary>
        private class ClimbStats
        {
            public float MaxY;
            public float MinAngle = float.MaxValue;
            public int StandingFrames;
            public int TotalFrames;
            public float LongestDetached;
            public int CamFrames;
            public int CamFreeFrames;

            private float detached;

            public float StandingFraction => TotalFrames > 0 ? (float)StandingFrames / TotalFrames : 0f;

            public float CamFreeFraction => CamFrames > 0 ? (float)CamFreeFrames / CamFrames : 0f;

            public void Tick(GameObject spider, SpiderMovement movement, SpiderLeg[] legs)
            {
                MaxY = Mathf.Max(MaxY, spider.transform.position.y);
                TotalFrames++;

                // Kamera steckt in Geometrie? (Layer 2 = Spinne, bewusst ausgenommen)
                Camera cam = Camera.main;
                if (cam != null)
                {
                    CamFrames++;
                    bool frei = !Physics.CheckSphere(cam.transform.position, 0.02f,
                        Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore);
                    if (frei)
                    {
                        CamFreeFrames++;
                    }
                }

                int standing = 0;
                foreach (SpiderLeg leg in legs)
                {
                    if (leg.IsGrounded && !leg.IsStepping)
                    {
                        standing++;
                    }
                }

                if (standing >= 4)
                {
                    StandingFrames++;
                }

                if (movement.IsAttached)
                {
                    LongestDetached = Mathf.Max(LongestDetached, detached);
                    detached = 0f;
                }
                else
                {
                    detached += Time.deltaTime;
                }
            }

            public void Finish()
            {
                LongestDetached = Mathf.Max(LongestDetached, detached);
            }

            public string Summary()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "maxY={0} minWinkel={1} AnteilStandbeine={2} laengsteAbloesung={3}s kameraFrei={4}/{5}",
                    MaxY.ToString("F3", CultureInfo.InvariantCulture),
                    MinAngle.ToString("F1", CultureInfo.InvariantCulture),
                    StandingFraction.ToString("F2", CultureInfo.InvariantCulture),
                    LongestDetached.ToString("F2", CultureInfo.InvariantCulture),
                    CamFreeFrames,
                    CamFrames);
            }
        }

        /// <summary>Szene laden, Spinne setzen, Beine einrasten, kurze Einrastzeit.</summary>
        private IEnumerator Setup(Vector3 pos, Quaternion rot)
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length, "Die Spinne braucht 8 Beine");

            movement.Teleport(pos, rot);
            foreach (SpiderLeg leg in legs)
            {
                leg.SnapToGround();
            }
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>
        /// Vorwaerts laufen (relativ zur Spinne) und waehrenddessen messen.
        /// angleRef wird gegen transform.up geprueft (kleinster Winkel wird gemerkt).
        /// </summary>
        private IEnumerator RunFor(float seconds, ClimbStats stats, Vector3 angleRef)
        {
            movement.SetExternalMoveLocal(new Vector2(0f, 1f));
            float t = 0f;
            while (t < seconds)
            {
                stats.Tick(spider, movement, legs);
                stats.MinAngle = Mathf.Min(stats.MinAngle, Vector3.Angle(spider.transform.up, angleRef));
                t += Time.deltaTime;
                yield return null;
            }

            stats.Finish();
            movement.SetExternalMoveLocal(null);
            yield return new WaitForSeconds(0.2f);
        }

        private static void Check(params string[] fehler)
        {
            Assert.IsEmpty(fehler, string.Join("\n", fehler));
        }

        [UnityTest]
        public IEnumerator Climb_Wall()
        {
            // Start vor der Wand (Vorderseite z=2.85), Blick auf die Wand (+Z).
            yield return Setup(new Vector3(0f, 0f, 2.2f), Quaternion.identity);
            ClimbStats stats = new ClimbStats();
            yield return RunFor(6f, stats, Vector3.back);

            List<string> fehler = new List<string>();
            if (stats.MaxY <= 1.0f)
            {
                fehler.Add("Spinne ist an der Wand nicht ueber y=1.0: " + stats.Summary());
            }

            if (stats.MinAngle >= 35f)
            {
                fehler.Add("Spinne hat sich nicht an die Wand gedreht (minWinkel zu Vector3.back >= 35 Grad): " + stats.Summary());
            }

            if (stats.StandingFraction < 0.7f)
            {
                fehler.Add("In zu wenigen Frames stehen >= 4 Beine (Anteil < 0.7): " + stats.Summary());
            }

            if (stats.CamFrames == 0)
            {
                fehler.Add("Keine Kamera (Camera.main) gefunden.");
            }
            else if (stats.CamFreeFraction < 0.95f)
            {
                fehler.Add(string.Format(CultureInfo.InvariantCulture,
                    "Kamera steckt in zu vielen Frames in Geometrie (freie Anteil {0:F2} < 0.95): {1}",
                    stats.CamFreeFraction, stats.Summary()));
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                float camAngle = Vector3.Angle(cam.transform.up, spider.transform.up);
                if (camAngle >= 70f)
                {
                    fehler.Add(string.Format(CultureInfo.InvariantCulture,
                        "Kamerahochachse folgt der Spinne nicht (Winkel {0:F1} >= 70 Grad): {1}",
                        camAngle, stats.Summary()));
                }
            }

            Check(fehler.ToArray());
        }

        [UnityTest]
        public IEnumerator Climb_OverEdge()
        {
            // Start oben auf dem Block (Oberkante y=0.4), Blick ueber die Kante (+X).
            yield return Setup(new Vector3(3f, 0.4f, 0f), Quaternion.Euler(0f, 90f, 0f));
            ClimbStats stats = new ClimbStats();
            yield return RunFor(3f, stats, Vector3.right);

            List<string> fehler = new List<string>();
            if (stats.MinAngle >= 45f)
            {
                fehler.Add("Spinne kippt nicht ueber die Kante (minWinkel zu Vector3.right >= 45 Grad): " + stats.Summary());
            }

            if (stats.LongestDetached > 0.5f)
            {
                fehler.Add("Spinne ist einmal laenger als 0.5 s am Stueck nicht angebunden: " + stats.Summary());
            }

            Check(fehler.ToArray());
        }

        [UnityTest]
        public IEnumerator Climb_Pole()
        {
            // Start neben dem Stamm (Radius 0.15 bei (-3,*,0)), Blick auf den Stamm (+Z).
            yield return Setup(new Vector3(-3f, 0f, -0.6f), Quaternion.identity);
            ClimbStats stats = new ClimbStats();
            yield return RunFor(6f, stats, Vector3.back);

            List<string> fehler = new List<string>();
            if (stats.MaxY <= 1.2f)
            {
                fehler.Add("Spinne ist am Stamm nicht ueber y=1.2: " + stats.Summary());
            }

            if (stats.StandingFraction < 0.7f)
            {
                fehler.Add("In zu wenigen Frames stehen >= 4 Beine (Anteil < 0.7): " + stats.Summary());
            }

            Check(fehler.ToArray());
        }

        [UnityTest]
        public IEnumerator Climb_Ceiling()
        {
            // Start an der Wand (Vorderseite z=2.85), Blick nach oben (Bauch zur Wand).
            // Die Spinne soll ueber die Kante klettern und an der Ueberhang-Unterseite (y=2.0) haengen.
            yield return Setup(new Vector3(0f, 1.6f, 2.85f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            ClimbStats stats = new ClimbStats();
            yield return RunFor(8f, stats, Vector3.down);

            List<string> fehler = new List<string>();
            if (stats.MinAngle >= 35f)
            {
                fehler.Add(string.Format(CultureInfo.InvariantCulture,
                    "Spinne haengt zu keinem Zeitpunkt kopfueber an der Unterseite (minWinkel zu down {0:F1} >= 35 Grad; laengsteAbloesung {1:F2} s; maxY {2:F3})",
                    stats.MinAngle, stats.LongestDetached, stats.MaxY));
            }

            if (stats.LongestDetached > 0.5f)
            {
                fehler.Add(string.Format(CultureInfo.InvariantCulture,
                    "Spinne ist einmal laenger als 0.5 s am Stueck nicht haftend ({0:F2} s; minWinkel zu down {1:F1}; maxY {2:F3})",
                    stats.LongestDetached, stats.MinAngle, stats.MaxY));
            }

            Check(fehler.ToArray());
        }
    }
}
