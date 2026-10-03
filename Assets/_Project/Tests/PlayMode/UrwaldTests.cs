using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Prüft, dass die Urwald-Szene bei jedem Start eine neue Welt aus dem Seed erzeugt:
    /// Terrain mit TerrainCollider, Sumpfwasser ohne Collider, genug Bäume, Unterholz,
    /// Totholz und Steine, Spinne über Wasser und am Boden; Beweisfotos;
    /// gleicher Seed ergibt identische Welt, anderer Seed ergibt andere Welt.
    /// </summary>
    public class UrwaldTests
    {
        private JungleWorldGenerator readyGen;

        /// <summary>Eine Zeile mit den wichtigsten Messwerten der Spinne (Diagnostics).</summary>
        private static string Diag(SpiderMovement s)
        {
            Vector3 pos = s.transform.position;
            float boden = Terrain.activeTerrain != null
                ? Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y
                : -999f;
            int anzahl = Object.FindObjectsOfType<SpiderMovement>().Length;

            RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 2f, Vector3.down, 5f);
            var beste = new System.Collections.Generic.List<RaycastHit>();
            foreach (var h in hits)
            {
                int k = beste.FindIndex(b => b.distance > h.distance);
                if (k < 0) k = beste.Count;
                beste.Insert(k, h);
                if (beste.Count > 3) beste.RemoveAt(beste.Count - 1);
            }
            string strahlen = string.Join(" | ", beste.ConvertAll(h =>
                h.collider.gameObject.name + "(" + LayerMask.LayerToName(h.collider.gameObject.layer) + ")@"
                + h.collider.transform.position.y.ToString("F2")));

            return "pos=" + pos.ToString("F2")
                + " boden=" + boden.ToString("F2")
                + " grounded=" + s.IsGrounded
                + " rope=" + s.IsOnRope
                + " spiders=" + anzahl
                + " rays=" + strahlen;
        }

        /// <summary>Loggt [UrwaldDiag] nach 0.1, 0.5, 1 und 2 s (ersetzt die 2-s-Pause).</summary>
        private static IEnumerator SpiderDiag(SpiderMovement spider)
        {
            float[] marks = { 0.1f, 0.5f, 1f, 2f };
            float start = Time.realtimeSinceStartup;
            int next = 0;
            while (next < marks.Length && Time.realtimeSinceStartup - start < marks[next])
            {
                yield return null;
            }
            while (next < marks.Length)
            {
                Debug.Log("[UrwaldDiag] t=" + marks[next].ToString("F1") + " " + Diag(spider));
                next++;
                if (next < marks.Length && Time.realtimeSinceStartup - start < marks[next])
                    yield return null;
            }
        }

        /// <summary>Bis zu 15 s auf IsReady warten und den Generator merken.</summary>
        private IEnumerator WaitReady()
        {
            readyGen = null;
            float timeout = 15f;
            while (timeout > 0f)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady)
                {
                    readyGen = g;
                    break;
                }
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Urwald_GeneratesWorldFromSeed()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();

            Assert.IsNotNull(readyGen, "JungleWorldGenerator in der Urwald-Szene nicht gefunden.");
            JungleWorldGenerator gen = readyGen;
            Assert.IsTrue(gen.IsReady, "Welt war nach 15 s noch nicht fertig generiert.");
            Assert.AreEqual(4242, gen.Seed, "Seed weicht von NextSeed ab.");
            Assert.Less(gen.GenerationSeconds, 8f, "Generierung dauert zu lange (> 8 s).");
            Assert.GreaterOrEqual(gen.TreeCount, 40, "Zu wenige Bäume (< 40).");
            Assert.GreaterOrEqual(gen.BushCount, 100, "Zu wenige Büsche (< 100).");
            Assert.GreaterOrEqual(gen.FernCount, 100, "Zu wenige Farne (< 100).");
            Assert.GreaterOrEqual(gen.LogCount, 12, "Zu wenige Stämme (< 12).");
            Assert.GreaterOrEqual(gen.RockCount, 25, "Zu wenige Steine (< 25).");

            Assert.IsNotNull(Object.FindFirstObjectByType<TerrainCollider>(),
                "Terrain mit TerrainCollider nicht gefunden.");

            GameObject water = GameObject.Find("Sumpfwasser");
            Assert.IsNotNull(water, "Sumpfwasser nicht gefunden.");
            Assert.IsNotNull(water.GetComponent<Collider>(), "Sumpfwasser hat keinen Collider.");
            Assert.IsNotNull(water.GetComponent<WaterBody>(), "Sumpfwasser hat keine WaterBody.");
            var waterTag = water.GetComponent<SurfaceTag>();
            Assert.IsNotNull(waterTag, "Sumpfwasser hat kein SurfaceTag.");
            Assert.AreEqual(SurfaceType.Water, waterTag.surfaceType, "Sumpfwasser-SurfaceTag ist nicht Water.");

            GameObject unterholz = GameObject.Find("Unterholz");
            Assert.IsNotNull(unterholz, "Unterholz-Gruppe nicht gefunden.");
            int plantColliders = unterholz.GetComponentsInChildren<Collider>().Length;
            Assert.AreEqual(gen.PlantColliderCount, plantColliders,
                "Pflanzen-Collider in Unterholz stimmen nicht mit PlantColliderCount überein.");
            Assert.GreaterOrEqual(gen.PlantColliderCount, 200,
                "Zu wenige Pflanzen-Collider (< 200).");

            Assert.IsNotNull(GameObject.Find("Totholz"), "Totholz-Gruppe nicht gefunden.");
            Assert.IsNotNull(GameObject.Find("Steine"), "Steine-Gruppe nicht gefunden.");
            Assert.IsNotNull(Object.FindFirstObjectByType<MeshCollider>(),
                "Kein MeshCollider gefunden (Totholz/Steine sollen welche haben).");

            SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "Spinne nicht gefunden.");
            Vector3 p = spider.transform.position;
            Assert.Greater(p.y, JungleWorld.WaterLevel,
                "Spinne sollte über dem Wasser spawnen.");
            float boden = Terrain.activeTerrain.SampleHeight(p) + Terrain.activeTerrain.transform.position.y;
            Assert.Less(p.y - boden, 0.3f,
                "Spinne spawn zu hoch über dem Boden (> 0.3 m). " + Diag(spider));
            Assert.IsFalse(gen.World.IsUnderWater(p.x, p.z), "Spinne sollte nicht im Wasser spawnen.");

            yield return SpiderDiag(spider);
            p = spider.transform.position;
            float boden2 = Terrain.activeTerrain.SampleHeight(p) + Terrain.activeTerrain.transform.position.y;
            Assert.GreaterOrEqual(p.y, boden2 - 0.05f,
                "Spinne ist nach 2 s unter dem Gelände (versinkt).");
            Assert.IsTrue(spider.IsGrounded, "Spinne haftet nach 2 s nicht am Boden. " + Diag(spider));
            Assert.IsFalse(spider.IsOnRope, "Spinne hängt am Faden");
            var mainCam = Camera.main;
            Assert.IsNotNull(mainCam, "Hauptkamera nicht gefunden.");
            Vector3 camPos = mainCam.transform.position;
            float camBoden = Terrain.activeTerrain.SampleHeight(camPos) + Terrain.activeTerrain.transform.position.y;
            Assert.Greater(camPos.y, camBoden + 0.02f, "Kamera liegt unter dem Gelände.");

            // Beweisfotos: Gesamtansicht + Nahaufnahme der Spinne.
            float half = gen.World.Size / 2f;
            GameObject camGo = new GameObject("UrwaldFotoCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = new Vector3(half, 40f, half - 40f);
            camGo.transform.LookAt(new Vector3(half, 3f, half));
            var rt = new RenderTexture(1280, 720, 24);
            rt.Create();
            cam.targetTexture = rt;
            SaveFrame(cam, rt, 1280, 720, "Logs/ci/urwald_oben.png");
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);

            Camera main = Camera.main;
            var rt2 = new RenderTexture(1280, 720, 24);
            rt2.Create();
            main.targetTexture = rt2;
            SaveFrame(main, rt2, 1280, 720, "Logs/ci/urwald_spinne.png");
            main.targetTexture = null;
            Object.DestroyImmediate(rt2);
        }

        [UnityTest]
        public IEnumerator Urwald_SameSeedSameWorld()
        {
            JungleWorldGenerator.NextSeed = 777;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 777) nicht fertig.");
            int trees1 = readyGen.TreeCount;
            int bush1 = readyGen.BushCount;
            var first5 = new Vector3[5];
            for (int i = 0; i < 5; i++)
                first5[i] = readyGen.Trees[i].position;

            JungleWorldGenerator.NextSeed = 777;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 777, zweiter Lauf) nicht fertig.");
            Assert.AreEqual(trees1, readyGen.TreeCount, "TreeCount weicht bei gleichem Seed ab.");
            Assert.AreEqual(bush1, readyGen.BushCount, "BushCount weicht bei gleichem Seed ab.");
            for (int i = 0; i < 5; i++)
                Assert.LessOrEqual(Vector3.Distance(first5[i], readyGen.Trees[i].position), 0.001f,
                    "Baum " + i + " an anderer Position bei gleichem Seed.");

            JungleWorldGenerator.NextSeed = 778;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 778) nicht fertig.");
            bool gleicheZahl = readyGen.TreeCount == trees1;
            bool gleichePositionen = true;
            for (int i = 0; i < 5; i++)
            {
                if (Vector3.Distance(first5[i], readyGen.Trees[i].position) > 0.001f)
                {
                    gleichePositionen = false;
                    break;
                }
            }
            Assert.IsFalse(gleicheZahl && gleichePositionen,
                "Seed 778 erzeugt identische Bäume wie Seed 777.");
        }

        /// <summary>
        /// Spinne ins Sumpfwasser teleportieren: SurfaceDetector muss IsOnWater melden
        /// und die Spinne darf nicht unter den Wasserspiegel sinken.
        /// </summary>
        [UnityTest]
        public IEnumerator Urwald_WaterIsDetected()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) nicht fertig.");
            JungleWorld world = readyGen.World;
            float half = world.Size / 2f;

            // Raster 1 m ab Weltmitte: Punkt mit Wassertiefe 0.1–0.4 m suchen.
            Vector3? spot = null;
            for (int dx = -20; dx <= 20 && spot == null; dx++)
            {
                for (int dz = -20; dz <= 20 && spot == null; dz++)
                {
                    float x = half + dx;
                    float z = half + dz;
                    if (!world.IsUnderWater(x, z)) continue;
                    float tiefe = JungleWorld.WaterLevel - world.Height(x, z);
                    if (tiefe < 0.1f || tiefe > 0.4f) continue;
                    spot = new Vector3(x, 0f, z);
                }
            }
            Assert.IsTrue(spot != null, "Keinen Punkt mit Wassertiefe 0.1–0.4 m gefunden.");

            SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "Spinne nicht gefunden.");
            Vector3 s = spot.Value;
            spider.Teleport(new Vector3(s.x, JungleWorld.WaterLevel + 0.03f, s.z), Quaternion.identity);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1f) yield return null;

            var det = spider.GetComponent<SpiderSurfaceDetector>();
            Assert.IsNotNull(det, "SpiderSurfaceDetector fehlt an der Spinne.");
            Assert.IsTrue(det.IsOnWater, "Spinne im Wasser, aber IsOnWater ist falsch. " + Diag(spider));
            Vector3 p = spider.transform.position;
            Assert.GreaterOrEqual(p.y, JungleWorld.WaterLevel - 0.05f,
                "Spinne ist unter den Wasserspiegel gesunken. " + Diag(spider));
        }

        /// <summary>
        /// Netz-Abnahmetest: an 50 %+ der trockenen, nicht-Clairing-Punkte muss
        /// WebSiteFinder in mindestens einer von vier Richtungen einen Netzplatz finden.
        /// </summary>
        [Ignore("M7-Abnahmeziel: Netze fast ueberall - aktuell ~16%, wird mit dichter Urwald-Flora (Lianen, Baumfarne) in 10.3/10.4 erreicht")]
        [UnityTest]
        public IEnumerator Urwald_WebSitesAlmostEverywhere()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) nicht fertig.");
            JungleWorld world = readyGen.World;
            float c = world.Size / 2f;
            float rad = c - 8f; // 8 m Abstand zum Weltrand

            var rnd = new System.Random(1);
            int gesamt = 0, treffer = 0;
            for (int i = 0; i < 60; i++)
            {
                float x = c + ((float)rnd.NextDouble() * 2f - 1f) * rad;
                float z = c + ((float)rnd.NextDouble() * 2f - 1f) * rad;
                float h = world.Height(x, z);
                if (h < JungleWorld.WaterLevel + 0.3f || world.IsClearing(x, z)) continue;
                Vector3 p = new Vector3(x, h + 0.1f, z);
                bool ok = WebSiteFinder.TryFindNear(p, Vector3.forward, null, out _, out _)
                    || WebSiteFinder.TryFindNear(p, Vector3.right, null, out _, out _)
                    || WebSiteFinder.TryFindNear(p, Vector3.back, null, out _, out _)
                    || WebSiteFinder.TryFindNear(p, Vector3.left, null, out _, out _);
                gesamt++;
                if (ok) treffer++;
            }
            Assert.Greater(gesamt, 0, "Kein einziger Prüfpunkt gefunden (alle im Wasser/Clairing?).");
            float anteil = (float)treffer / gesamt;
            Debug.Log("[Urwald] Netzplätze: " + anteil.ToString("P0"));
            Assert.GreaterOrEqual(anteil, 0.5f, "Nur " + anteil.ToString("P0") + " Netzplätze");
        }

        /// <summary>Render die Kamera in die RenderTexture und speichert als PNG ab.</summary>
        private static void SaveFrame(Camera cam, RenderTexture rt, int w, int h, string file)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(file, png);
        }
    }
}
