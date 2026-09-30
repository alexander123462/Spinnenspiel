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

        [UnityTest]
        public IEnumerator Climb_TreeInWorld()
        {
            // Szene "Testwelt": Stamm (CapsuleCollider, direction=1, Radius >= 0.2) am
            // naechsten am Spawnpunkt suchen, Spinne 0.45 m vor den Stamm setzen,
            // 8 s vorwaerts laufen: Hoehe und Anzahl stehender Beine messen.
            SceneManager.LoadScene("Testwelt");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht in Testwelt gefunden");
            movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length, "Die Spinne braucht 8 Beine");

            // Baumstamm am naechsten am Spawnpunkt (GO "Trunk" mit MeshCollider und SurfaceTag).
            Vector3 spawn = WorldLayout.SpawnPoint;
            Transform trunk = null;
            float bestDist = float.MaxValue;
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name != "Trunk" || go.GetComponent<MeshCollider>() == null || go.GetComponent<SurfaceTag>() == null)
                {
                    continue;
                }
                float dist = Vector3.Distance(go.transform.position, spawn);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    trunk = go.transform;
                }
            }
            Assert.IsNotNull(trunk, "Kein Baumstamm (GameObject \"Trunk\" mit MeshCollider + SurfaceTag) gefunden");

            // 0.45 m vor den Stamm (auf der Spawnpunkt-Seite), auf der Terrainhoehe + 0.02, Blick zum Stamm.
            Vector3 toTrunk = trunk.position - spawn;
            toTrunk.y = 0f;
            Assert.IsTrue(toTrunk.sqrMagnitude > 1e-6f, "Stamm sitzt exakt im Spawnpunkt");
            toTrunk.Normalize();
            Vector3 pos = new Vector3(trunk.position.x, 0f, trunk.position.z) - toTrunk * 0.45f;
            RaycastHit ground;
            Assert.IsTrue(Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out ground, 100f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                "Kein Terrain unter der Startposition gefunden");
            pos.y = ground.point.y + 0.02f;
            Quaternion rot = Quaternion.LookRotation(toTrunk, Vector3.up);

            movement.Teleport(pos, rot);
            foreach (SpiderLeg leg in legs)
            {
                leg.SnapToGround();
            }
            yield return new WaitForSeconds(0.3f);

            float startY = pos.y;
            float maxY = startY;
            int standingFrames = 0;
            int totalFrames = 0;
            movement.SetExternalMoveLocal(new Vector2(0f, 1f));
            float t = 0f;
            while (t < 8f)
            {
                totalFrames++;
                maxY = Mathf.Max(maxY, spider.transform.position.y);
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
                    standingFrames++;
                }
                t += Time.deltaTime;
                yield return null;
            }
            movement.SetExternalMoveLocal(null);
            yield return new WaitForSeconds(0.2f);

            float rise = maxY - startY;
            float fraction = totalFrames > 0 ? (float)standingFrames / totalFrames : 0f;
            string stats = string.Format(CultureInfo.InvariantCulture,
                "startY={0:F3} maxHoehe={1:F3} Aufstieg={2:F3} AnteilStandbeine={3:F2} ({4}/{5})",
                startY, maxY, rise, fraction, standingFrames, totalFrames);

            List<string> fehler = new List<string>();
            if (rise < 1.5f)
            {
                fehler.Add("Spinne ist in der Testwelt nicht >= 1.5 m ueber ihrer Startposition: " + stats);
            }
            if (fraction < 0.7f)
            {
                fehler.Add("In zu wenigen Frames der Testwelt stehen >= 4 Beine (Anteil < 0.7): " + stats);
            }
            Check(fehler.ToArray());
        }

        /// <summary>
        /// Auftrag 9.4: Kompletter Stamm in der Testwelt: Die Spinne klettert von unten bis
        /// nahe zur Krone und bleibt dabei in jeder Hoehe AUF dem Stamm - die Distanz zur
        /// Stammachse darueber, dass sie niemals in den verjuengten Stamm hineinfallen koennte
        /// (r >= 0.85 * Radius der sichtbaren Bande). Am Ende >= 70 % der Stammhoehe erreicht.
        /// </summary>
        [UnityTest]
        public IEnumerator Climb_TreeFullHeight_StaysOutsideTrunk()
        {
            SceneManager.LoadScene("Testwelt");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht in Testwelt gefunden");
            movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            legs = spider.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length, "Die Spinne braucht 8 Beine");

            // Naechster Stamm (GO "Trunk", identisch mit der Suche in Climb_TreeInWorld).
            Vector3 spawn = WorldLayout.SpawnPoint;
            GameObject trunkGo = null;
            float bestDist = float.MaxValue;
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name != "Trunk" || go.GetComponent<MeshCollider>() == null || go.GetComponent<SurfaceTag>() == null)
                {
                    continue;
                }
                float dist = Vector3.Distance(go.transform.position, spawn);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    trunkGo = go;
                }
            }
            Assert.IsNotNull(trunkGo, "Kein Baumstamm gefunden (\"Trunk\" mit MeshCollider + SurfaceTag)");

            // Radius wird pro Hoehe direkt am MeshCollider per Raycast gemessen (die Flaeche, auf der die Spinne laeuft).
            Transform trunk = trunkGo.transform;
            MeshCollider mc = trunkGo.GetComponent<MeshCollider>();
            float y0 = trunkGo.transform.position.y;
            float yTop = mc.bounds.max.y;
            Assert.IsTrue(yTop - y0 > 3f, "Unerwartete Stamm-Hoehe: y0=" + y0 + " yTop=" + yTop);
            Vector2 guess = new Vector2(trunkGo.transform.position.x, trunkGo.transform.position.z);

            bool MeasureTrunk(float h, ref Vector2 c, out float radius)
            {
                radius = 0f;
                var hits = new List<Vector2>();
                for (int k = 0; k < 8; k++)
                {
                    Vector3 dir = Quaternion.Euler(0f, 45f * k, 0f) * Vector3.forward;
                    Vector3 origin = new Vector3(c.x, h, c.y) + dir * 3f;
                    if (mc.Raycast(new Ray(origin, -dir), out RaycastHit hit, 3f))
                        hits.Add(new Vector2(hit.point.x, hit.point.z));
                }
                if (hits.Count < 4) return false;
                Vector2 m = Vector2.zero; foreach (var p in hits) m += p; m /= hits.Count;
                var d = new List<float>(); foreach (var p in hits) d.Add(Vector2.Distance(p, m));
                d.Sort(); radius = d[d.Count / 2]; c = m; return true;
            }

            // 0.45 m vor den Stamm, Blickrichtung zum Stamm, auf Terrainhoehe.
            Vector3 toTrunk = new Vector3(trunk.position.x - spawn.x, 0f, trunk.position.z - spawn.z);
            Assert.IsTrue(toTrunk.sqrMagnitude > 1e-6f, "Stamm sitzt exakt im Spawnpunkt");
            toTrunk.Normalize();
            Vector3 pos = new Vector3(trunk.position.x, 0f, trunk.position.z) - toTrunk * 0.45f;
            RaycastHit ground;
            Assert.IsTrue(Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out ground, 100f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                "Kein Terrain unter der Startposition gefunden");
            pos.y = ground.point.y + 0.02f;
            movement.Teleport(pos, Quaternion.LookRotation(toTrunk, Vector3.up));
            foreach (SpiderLeg leg in legs)
            {
                leg.SnapToGround();
            }
            yield return new WaitForSeconds(0.3f);

            float startY = spider.transform.position.y;
            float maxY = startY;
            string viola = null;
            movement.SetExternalSprint(true);
            movement.SetExternalMoveLocal(new Vector2(0f, 1f));
            float t = 0f;
            bool reachedTop = false;
            while (t < 25f)
            {
                Vector3 s = spider.transform.position;
                maxY = Mathf.Max(maxY, s.y);
                float h = spider.transform.position.y;
                if (h >= y0 + 0.3f && h <= yTop && viola == null)
                {
                    // Radius des Stammcolliders bei Hoehe h per Raycast (8 Richtungen, Median).
                    if (MeasureTrunk(h, ref guess, out float rad))
                    {
                        float r = Vector2.Distance(new Vector2(spider.transform.position.x, spider.transform.position.z), guess);
                        // Stammradius GENAU in Richtung der Spinne messen (Median kann durch Aeste verfalscht sein).
                        Vector2 sp = new Vector2(spider.transform.position.x, spider.transform.position.z);
                        Vector2 radial2 = (sp - guess).sqrMagnitude > 1e-6f ? (sp - guess).normalized : Vector2.right;
                        Vector3 radial = new Vector3(radial2.x, 0f, radial2.y);
                        Vector3 outside = new Vector3(guess.x, h, guess.y) + radial * 3f;
                        float rLocal = -1f;
                        if (mc.Raycast(new Ray(outside, -radial), out RaycastHit lh, 3f))
                            rLocal = Vector2.Distance(new Vector2(lh.point.x, lh.point.z), guess);
                        if (rLocal > 0f && r < rLocal - 0.01f)
                        {
                            viola = string.Format(CultureInfo.InvariantCulture,
                                "Spinne im Stamm: h={0:F2} r={1:F3} rLocal={2:F3} rMedian={3:F3} normal·radial={4:F2} attached={5}",
                                h, r, rLocal, rad, Vector3.Dot(movement.SurfaceNormal, radial), movement.IsAttached);
                        }
                    }
                }
                if (maxY >= y0 + 0.7f * (yTop - y0))
                {
                    reachedTop = true;
                    break;
                }
                t += Time.deltaTime;
                yield return null;
            }
            movement.SetExternalMoveLocal(null);
            movement.SetExternalSprint(null);

            float rise = maxY - startY;
            List<string> fehler = new List<string>();
            if (viola != null)
            {
                fehler.Add(viola);
            }
            if (!reachedTop)
            {
                fehler.Add(string.Format(CultureInfo.InvariantCulture,
                    "Spinne hat nicht >= 70 % der Stammhoehe erreicht: erreichte Hoehe={0:F2} m, Aufstieg={1:F2} m, Ziel={2:F2} m",
                    maxY, rise, y0 + 0.7f * (yTop - y0)));
            }
            Check(fehler.ToArray());
        }

        /// <summary>Bildserie: Gehen und Wanduebergang (Kontrolle der Bewegung).</summary>
        [UnityTest]
        public IEnumerator Gait_RecordsFrames()
        {
            yield return Setup(new Vector3(0f, 0.05f, -1.5f), Quaternion.identity);

            // Aufnahme-Kamera seitlich, folgt der Spinne mit festem Versatz.
            GameObject camGo = new GameObject("GaitRecCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            RenderTexture rt = new RenderTexture(960, 540, 24);
            rt.Create();
            cam.targetTexture = rt;

            string dir = System.IO.Path.Combine(Application.dataPath, "../Logs/ci");
            System.IO.Directory.CreateDirectory(dir);

            // 1) Gehen: 0.6 s Anlauf, dann 8 Bilder im Abstand 0.07 s.
            movement.SetExternalMoveInput(new Vector2(0f, 1f));
            float t = 0f;
            while (t < 0.6f)
            {
                FollowCam(cam);
                t += Time.deltaTime;
                yield return null;
            }
            for (int i = 0; i < 8; i++)
            {
                FollowCam(cam);
                SaveFrame(cam, rt, System.IO.Path.Combine(dir, "gang_" + i + ".png"));
                if (i < 7)
                {
                    t = 0f;
                    while (t < 0.07f)
                    {
                        FollowCam(cam);
                        t += Time.deltaTime;
                        yield return null;
                    }
                }
            }

            // 2) Wanduebergang: laufen bis z >= 2.85 oder max. 4 s.
            t = 0f;
            while (spider.transform.position.z < 2.85f && t < 4f)
            {
                FollowCam(cam);
                t += Time.deltaTime;
                yield return null;
            }

            // Kamera fest bei (0.9, 0.5, 2.2), LookAt(Spinne); 6 Bilder alle 0.25 s.
            cam.transform.position = new Vector3(0.9f, 0.5f, 2.2f);
            for (int i = 0; i < 6; i++)
            {
                cam.transform.LookAt(spider.transform.position);
                SaveFrame(cam, rt, System.IO.Path.Combine(dir, "wand_" + i + ".png"));
                if (i < 5)
                {
                    t = 0f;
                    while (t < 0.25f)
                    {
                        cam.transform.LookAt(spider.transform.position);
                        t += Time.deltaTime;
                        yield return null;
                    }
                }
            }

            // 3) Eingabe zuruecksetzen.
            movement.SetExternalMoveInput(null);

            // Alle 14 Dateien muessen existieren.
            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "gang_" + i + ".png")),
                    "gang_" + i + ".png fehlt");
            }
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "wand_" + i + ".png")),
                    "wand_" + i + ".png fehlt");
            }

            // Aufraeumen: Kamera + RenderTexture.
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
        }

        /// <summary>Kamera seitlich neben der Spinne mit festem Versatz (gleicher Abstand, LookAt leicht ueber der Spinne).</summary>
        private void FollowCam(Camera cam)
        {
            cam.transform.position = spider.transform.position + Vector3.right * 0.55f + Vector3.up * 0.12f;
            cam.transform.LookAt(spider.transform.position + Vector3.up * 0.03f);
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
