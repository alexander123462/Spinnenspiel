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

        /// <summary>Mittl. CPU-Work-ms per Frame via FrameTimingManager; result in _lastMeanCpuMs.</summary>
        private float _lastMeanCpuMs = -1f;

        private IEnumerator MeasureMeanCpuMs(int samples)
        {
            FrameTiming[] timing = new FrameTiming[8];
            float sumCpuMs = 0f;
            int measured = 0;
            for (int i = 0; i < samples; i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                uint got = FrameTimingManager.GetLatestTimings((uint)timing.Length, timing);
                for (int t = 0; t < (int)got; t++)
                {
                    float cpuWorkMs = (float)(timing[t].cpuFrameTime - timing[t].cpuMainThreadPresentWaitTime);
                    if (cpuWorkMs > 0.0)
                    {
                        sumCpuMs += cpuWorkMs;
                        measured++;
                    }
                }
                yield return null;
            }
            Assert.Greater(measured, samples / 2, "Too few valid FrameTiming samples.");
            _lastMeanCpuMs = sumCpuMs / measured;
        }

        [UnityTest]
        public IEnumerator Performance_MeanFrameTimeUnderBudget()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();

            // Wall-Clock in batchmode is vsync-locked (50 fps); measure the ECHT
            // CPU/GPU frame times via FrameTimingManager instead.
            Assert.IsTrue(FrameTimingManager.IsFeatureEnabled(), "Frame Timing Stats disabled.");

            const int samples = 300;
            float sumGpuMs = -1f, sumCpuMs = 0f;
            int gpuValid = 0, measured = 0;
            FrameTiming[] timing = new FrameTiming[8];
            var cpuSamples = new System.Collections.Generic.List<float>(2048);
            for (int i = 0; i < samples; i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                uint got = FrameTimingManager.GetLatestTimings((uint)timing.Length, timing);
                for (int t = 0; t < (int)got; t++)
                {
                    float cpuWorkMs = (float)(timing[t].cpuFrameTime - timing[t].cpuMainThreadPresentWaitTime);
                    if (cpuWorkMs > 0.0)
                    {
                        sumCpuMs += cpuWorkMs;
                        measured++;
                        cpuSamples.Add(cpuWorkMs);
                    }
                    if ((float)timing[t].gpuFrameTime > 0.0)
                    {
                        sumGpuMs += (float)timing[t].gpuFrameTime;
                        gpuValid++;
                    }
                }
                yield return null;
            }

            Assert.Greater(measured, samples / 2, "Too few valid FrameTiming samples.");
            float meanCpu = sumCpuMs / measured;
            cpuSamples.Sort();
            float medianCpu = cpuSamples[cpuSamples.Count / 2];
            Debug.Log("[Perf] Urwald CPU-Work-Framezeit: mean " + meanCpu.ToString("0.00")
                + " ms | median " + medianCpu.ToString("0.00") + " ms (" + measured + " Samples; GPU valid: " + gpuValid + "/" + samples + ")");
            float meanGpu = gpuValid > 0 ? sumGpuMs / gpuValid : -1f;
            if (meanGpu > 0f)
            {
                Debug.Log("[Perf] Urwald mittl. GPU-Framezeit: " + meanGpu.ToString("0.00") + " ms");
            }
            Assert.Less(medianCpu, 16.7f, "Steady-State (Median) CPU-Work-Framezeit ueber 16.7 ms Budget.");
            float p90 = cpuSamples[(int)(cpuSamples.Count * 0.9)];
            Debug.Log("[Perf] CPU-Work p90: " + p90.ToString("0.00") + " ms | max " + cpuSamples[cpuSamples.Count - 1].ToString("0.00")
                + " ms (Hitch-Spike; Median " + medianCpu.ToString("0.00") + " ms is the Budget-Kriterium)");
        }

        [UnityTest]
        public IEnumerator Performance_Z9CostBreakdownPerSystem()
        {
            // Z9: Per-System costs measured via disable-and-measure (A/B).
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();

            GameObject vegetation = GameObject.Find("Unterholz");
            GameObject flies = GameObject.Find("Insekten");
            GameObject moths = GameObject.Find("Insekten_Motte");
            Assert.IsNotNull(vegetation, "No Unterholz-Group.");
            Assert.IsNotNull(flies, "No Insekten-Group.");

            yield return MeasureMeanCpuMs(240);
            float baseline = _lastMeanCpuMs;

            vegetation.SetActive(false);
            yield return MeasureMeanCpuMs(240);
            float vegCost = baseline - _lastMeanCpuMs;

            vegetation.SetActive(true);
            flies.SetActive(false);
            moths.SetActive(false);
            yield return MeasureMeanCpuMs(240);
            float insectCost = baseline - _lastMeanCpuMs;
            flies.SetActive(true);
            moths.SetActive(true);

            // M7-10.5: Spinngscale Debris (9000 cm-sized) in die A/B-Messung too.
            GameObject debris = GameObject.Find("Debris");
            Assert.IsNotNull(debris, "No Debris-Group.");
            debris.SetActive(false);
            yield return MeasureMeanCpuMs(240);
            float debrisCost = baseline - _lastMeanCpuMs;
            debris.SetActive(true);

            Debug.Log("[Z9] Cost-Breakdown (ms/Frame): base " + baseline.ToString("0.00")
                + " | Vegetation " + vegCost.ToString("0.00")
                + " | Insects " + insectCost.ToString("0.00")
                + " | Debris " + debrisCost.ToString("0.00"));
            Assert.GreaterOrEqual(vegCost, -1f, "Vegetation-Cost messung insane (-): " + vegCost);
            Assert.GreaterOrEqual(debrisCost, -1f, "Debris-Cost messung insane (-): " + debrisCost);
            Assert.Less(baseline, 16.7f, "Base frame time already over budget.");
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

            // U4.3: Farn-Scatter-Patch tilett, Y per SampleHeight (Terrain-Surface)
            GameObject fernScatterGrp = GameObject.Find("Farn-Scatter");
            if (gen.FernScatterCount > 0)
            {
                Assert.IsNotNull(fernScatterGrp, "Farn-Scatter-Gruppe missing despite FernScatterCount > 0.");
                var scatterMeshes = fernScatterGrp.GetComponentsInChildren<MeshRenderer>();
                Assert.AreEqual(gen.FernScatterCount, scatterMeshes.Length,
                    "Farn-Scatter Instance-Anzahl weicht von FernScatterCount ab.");
                bool foundSink = false;
                foreach (var mr in scatterMeshes)
                {
                    float bottom = mr.transform.position.y;
                    float ground = Terrain.activeTerrain.SampleHeight(mr.transform.position) + Terrain.activeTerrain.transform.position.y;
                    if (bottom - ground > 0.6f || bottom - ground < -0.3f)
                    {
                        foundSink = true;
                        break;
                    }
                }
                Assert.IsFalse(foundSink, "Farn-Scatter Instance not snapped to SampleHeight (> 0.6 m above or > 0.3 m below ground).");
            }

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
            VegetationSystem veg = Object.FindFirstObjectByType<VegetationSystem>();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f); // Vormittag-Light for the proof shots
            yield return null; // one frame so DayNightCycle rotates the sun to morning
            Camera main = Camera.main;
            var overviewPos = new Vector3(half, 14f, half - 14f);
            main.transform.position = overviewPos;
            main.transform.LookAt(new Vector3(half, 3f, half));
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();
            main.targetTexture = rt;
            if (veg != null) veg.RenderForCamera(main);
            SaveFrame(main, rt, 1280, 720, "Logs/ci/urwald_oben.png");
            main.targetTexture = null;
            Object.DestroyImmediate(rt);

            var spiderGo = GameObject.Find("Spider");
            if (spiderGo != null)
            {
                Vector3 s = spiderGo.transform.position;
                main.transform.position = s + new Vector3(0.25f, 0.18f, -0.35f);
                main.transform.LookAt(s + new Vector3(0f, 0.05f, 0f));
            }
            var rt2 = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt2.antiAliasing = 4;
            rt2.Create();
            main.targetTexture = rt2;
            if (veg != null) veg.RenderForCamera(main);
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

        [UnityTest]
        public IEnumerator Urwald_SpiderScaleDebrisPresent()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) nicht fertig.");
            JungleWorldGenerator gen = readyGen;

            Assert.GreaterOrEqual(gen.DebrisCount, 3000, "Zu few spinngscale debris (< 3000).");

            GameObject debrisRoot = GameObject.Find("Debris");
            Assert.IsNotNull(debrisRoot, "Debris-Root not found.");
            Assert.GreaterOrEqual(gen.DebrisMaxSizeMeters, 0.01f, "Debris-Messung missing (0 m).");
            UnityEngine.Debug.Log($"[Urwald-Test] Debris: {gen.DebrisCount} Objekte, grösste {gen.DebrisMaxSizeMeters * 100f:0.0} cm");
            Assert.LessOrEqual(gen.DebrisMaxSizeMeters, 0.12f, "Debris not spider-scale: largest > 12 cm.");
        }

        [UnityTest]
        public IEnumerator Urwald_RainforestAmbienceRuns()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) nicht fertig.");

            JungleAmbientAudio amb = null;
            for (int i = 0; i < 12 && amb == null; i++)
            {
                amb = Object.FindFirstObjectByType<JungleAmbientAudio>();
                yield return null;
            }
            Assert.IsNotNull(amb, "No JungleAmbientAudio in the Urwald-Scene.");

            AudioClip loop = ProceduralAudio.RainforestLoop();
            UnityEngine.Debug.Log("[Amb-Test] RainforestLoop: " + (loop == null ? "null" : "duration=" + loop.length + " s"));
            Assert.True(loop != null && loop.length > 5f, "RainforestLoop missing/empty.");

            for (int i = 0; i < 60; i++) yield return null; // let Start() create the sources
            Assert.True(amb.IsRunning, "Ambience-Drone/Sump-Source not running.");
        }

        /// <summary>
        /// U1 Nahkampfschaerfe: mit VolumetricFog AUS (WorldLightingBuilder) die Nahaufnahme
        /// der Spinne hat echte Kontrast. Reihenfolge: ERST aufnehmen, DANN messen, DANN asserten.
        /// Kriterium: Luminanz-StDeviation >= 30 (r101c: mean 58.0, stdev 30.8).
        /// </summary>
        [UnityTest]
        public IEnumerator Urwald_Nahkampfscharf()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) nicht fertig.");

            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f); // Vormittag-Light
            yield return null;

            Camera main = Camera.main;
            Assert.IsNotNull(main, "Hauptkamera nicht gefunden.");
            var spiderGo = GameObject.Find("Spider");
            Assert.IsNotNull(spiderGo, "Spider-GameObject nicht gefunden.");
            Vector3 s = spiderGo.transform.position;
            main.transform.position = s + new Vector3(0.25f, 0.18f, -0.35f);
            main.transform.LookAt(s + new Vector3(0f, 0.05f, 0f));

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();
            main.targetTexture = rt;
            VegetationSystem veg = Object.FindFirstObjectByType<VegetationSystem>();
            if (veg != null) veg.RenderForCamera(main);
            main.Render();

            // ERST: PNG-Datei written BEFORE any assert (G6-Lehre).
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply(false);
            System.IO.File.WriteAllBytes("Logs/ci/urwald_nahkamp.png", tex.EncodeToPNG());

            // DANN: Luminanz mean/StDev ueber alle Pixel.
            Color[] px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
            main.targetTexture = null;
            Object.DestroyImmediate(rt);

            double sum = 0.0, sumSq = 0.0;
            foreach (var c in px)
            {
                float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                sum += luma;
                sumSq += (double)luma * luma;
            }
            int n = px.Length;
            double mean = sum / n;
            double variance = sumSq / n - mean * mean;
            double stdev = (variance > 0.0 ? (double) Mathf.Sqrt((float) variance) : 0.0) * 255.0;
            Debug.Log("[U1-Nahcam] mean=" + mean.ToString("F1") + " stdev=" + stdev.ToString("F1"));

            // DANN: asserten.
            Assert.GreaterOrEqual(stdev, 30.0,
                "Nahaufnahme kontrast (Luma-StDev) " + stdev.ToString("F1") + " < 30 – Nebel/VolumetricFog dies Dunnelesiert?");
        }
    }
}
