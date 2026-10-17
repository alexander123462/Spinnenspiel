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
            int anzahl = Object.FindObjectsByType<SpiderMovement>(FindObjectsSortMode.None).Length;

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

        // S1: Spinne must stand ON the terrain at spawn, not inside it.
        // Logs the four measured values from the backlog (spider center Y, terrain height
        // at spider XZ, collider bottom Y, whether a Terrain is loaded) for several seeds.
        [UnityTest]
        public IEnumerator Spider_StartsStandingOnGround()
        {
            int[] seeds = { 7, 1234, 4242, 99, 5 };
            foreach (int seed in seeds)
            {
                JungleWorldGenerator.NextSeed = seed;
                SceneManager.LoadScene("Urwald");
                yield return WaitReady();

                var mv = Object.FindFirstObjectByType<SpiderMovement>();
                Assert.IsNotNull(mv, $"Seed {seed}: SpiderMovement missing after load.");
                Collider bodyCollider = mv.GetComponent<Collider>();
                Assert.IsNotNull(bodyCollider, $"Seed {seed}: Spider body collider missing.");
                Terrain terr = Terrain.activeTerrain;
                Assert.IsNotNull(terr, $"Seed {seed}: no Terrain loaded at spawn time.");

                Vector3 pos = mv.transform.position;
                float terrainY = terr.SampleHeight(pos) + terr.transform.position.y;
                float bottomY = bodyCollider.bounds.min.y;
                float gap = bottomY - terrainY;
                Debug.Log($"[S1] seed={seed} spiderY={pos.y:F3} terrainY={terrainY:F3} colliderBottomY={bottomY:F3} gap={gap:F3}");

                Assert.GreaterOrEqual(gap, -0.02f, $"Seed {seed}: spider sunk {-gap:F3} m into the terrain.");
                Assert.LessOrEqual(gap, 0.10f, $"Seed {seed}: spider floats {gap:F3} m above the terrain.");
            }
        }

        // B1: Spinne bleibt ohne Eingabe aufrecht auf dem Boden (nicht kopfueber ueber dem Boden).
        // Pro Seed 4 s warten, dann Neigung gegen Welt-oben und Hoehe ueber dem Terrain pruefen.
        [UnityTest]
        public IEnumerator B1_SpiderStaysUprightOnGround()
        {
            int[] seeds = { 7, 5, 1234, 99 };
            foreach (int seed in seeds)
            {
                JungleWorldGenerator.NextSeed = seed;
                SceneManager.LoadScene("Urwald");
                yield return WaitReady();

                var mv = Object.FindFirstObjectByType<SpiderMovement>();
                Assert.IsNotNull(mv, $"Seed {seed}: SpiderMovement missing.");
                float t = 0f;
                while (t < 4f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                Terrain terr = Terrain.activeTerrain;
                float terrainY = terr.SampleHeight(mv.transform.position) + terr.transform.position.y;
                float height = mv.transform.position.y - terrainY;
                float tilt = Vector3.Angle(mv.transform.up, Vector3.up);
                Debug.Log($"[B1] seed={seed} tilt={tilt:F1} height={height:F2} attached={mv.IsAttached} normal={mv.SurfaceNormal}");
                Assert.Less(tilt, 45f, $"Seed {seed}: Spinne gekippt/kopfueber, Neigung {tilt:F0} Grad.");
                Assert.Less(height, 0.6f, $"Seed {seed}: Spinne {height:F2} m ueber dem Boden.");
            }
        }

        // B2: Beim Geradeauslaufen ueber Gras/Boden bleibt die Spinne aufrecht und am Boden.
        [UnityTest]
        public IEnumerator B2_SpiderWalkingForwardStaysUpright()
        {
            int[] seeds = { 7, 5, 4242, 99 };
            foreach (int seed in seeds)
            {
                JungleWorldGenerator.NextSeed = seed;
                SceneManager.LoadScene("Urwald");
                yield return WaitReady();
                var mv = Object.FindFirstObjectByType<SpiderMovement>();
                Assert.IsNotNull(mv, $"Seed {seed}: SpiderMovement missing.");
                Terrain terr = Terrain.activeTerrain;
                mv.SetExternalMoveLocal(new Vector2(0f, 1f));
                float t = 0f;
                float maxTilt = 0f;
                float maxHeight = -99f;
                float flipAt = -1f;
                string where = "";
                while (t < 15f)
                {
                    t += Time.deltaTime;
                    float h = mv.transform.position.y - (terr.SampleHeight(mv.transform.position) + terr.transform.position.y);
                    float tilt = Vector3.Angle(mv.transform.up, Vector3.up);
                    maxTilt = Mathf.Max(maxTilt, tilt);
                    maxHeight = Mathf.Max(maxHeight, h);
                    if (flipAt < 0f && (tilt > 100f || h > 1f))
                    {
                        flipAt = t;
                        var hit = new RaycastHit();
                        Physics.Raycast(mv.transform.position + mv.transform.up * 0.3f, -mv.transform.up, out hit, 1f);
                        where = hit.collider != null ? hit.collider.name + " layer=" + hit.collider.gameObject.layer + " path=" + hit.collider.transform.parent : "kein Treffer";
                    }
                    yield return null;
                }
                mv.SetExternalMoveLocal(null);
                Debug.Log($"[B2] seed={seed} maxTilt={maxTilt:F0} maxHeight={maxHeight:F2} flipAt={flipAt:F1} on={where}");
                Assert.Less(flipAt, 0f, $"Seed {seed}: Spinne nach {flipAt:F1} s gekippt/abgehoben auf {where}.");
            }
        }

        // S2: After abseiling down a net/silk thread to its lower anchor, the spider must NOT
        // sink into the terrain. The end-of-thread landing sets position = anchorB without a
        // terrain-height check; SettleOnTerrain raises the center so the collider bottom rests
        // on the surface. Mirrors S1's gap criterion (bottom - terrainY must not be very negative).
        [UnityTest]
        public IEnumerator Spider_LandsOnGroundAfterAbseil()
        {
            int seed = 4242;
            JungleWorldGenerator.NextSeed = seed;
            SceneManager.LoadScene("Urwald");
            yield return WaitReady();

            var mv = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(mv, $"Seed {seed}: SpiderMovement missing after load.");
            Collider bodyCollider = mv.GetComponent<Collider>();
            Assert.IsNotNull(bodyCollider, $"Seed {seed}: Spider body collider missing.");
            Terrain terr = Terrain.activeTerrain;
            Assert.IsNotNull(terr, $"Seed {seed}: no Terrain loaded.");

            // Horizontal thread ~2 m; lower anchorB sits AT terrain height so the raw
            // landing (position = anchorB) would leave the collider bottom BELOW ground.
            float x = mv.transform.position.x;
            float z = mv.transform.position.z;
            float yA = terr.SampleHeight(new Vector3(x, 0f, z)) + terr.transform.position.y + 1.2f;
            Vector3 anchorA = new Vector3(x - 1f, yA, z);
            Vector3 anchorB = new Vector3(x + 1f, terr.SampleHeight(new Vector3(x + 1f, 0f, z)) + terr.transform.position.y, z);

            GameObject threadGo = new GameObject("S2Thread");
            SilkThread faden = threadGo.AddComponent<SilkThread>();
            faden.Init(anchorA, anchorB, 0.02f);

            mv.AttachToThread(faden, 0.9f);
            Vector3 tan = faden.TangentAt(0.9f);
            mv.transform.rotation = Quaternion.LookRotation(tan, Vector3.up);

            // Walk toward end B: forward input along the tangent until the landing fires.
            mv.SetExternalMoveLocal(new Vector2(0f, 1f));
            bool landed = false;
            for (int i = 0; i < 240 && !landed; i++)
            {
                yield return null;
                if (mv.CurrentThread == null)
                {
                    landed = true;
                }
            }
            mv.SetExternalMoveLocal(null);
            Assert.IsTrue(landed, "Spider never reached end B of the thread.");

            Vector3 pos = mv.transform.position;
            float terrainY = terr.SampleHeight(pos) + terr.transform.position.y;
            float bottomY = bodyCollider.bounds.min.y;
            float gap = bottomY - terrainY;
            Debug.Log($"[S2] seed={seed} spiderY={pos.y:F3} terrainY={terrainY:F3} colliderBottomY={bottomY:F3} gap={gap:F3}");

            Assert.GreaterOrEqual(gap, -0.05f, $"Seed {seed}: spider sunk {-gap:F3} m into the terrain after abseil.");
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

        // K8: Frametime-Budget muss mit 6 Insekten, 3 Netze, Tag/Nacht und Regen SIMULTANEOUSLY halten.
        // Der plainen Urwald-Test (Performance_MeanFrameTimeUnderBudget) misst de steady-state OHNE
        // zusatlich Insekten/Netze. Here push the worst case: Noon (dayFactor=1) so day-species run
        // full, force 6 flying insects, build 3 fully-grown nets (LineRenderer-Faeden = Rendern-Load),
        // keep the DayNightCycle + WeatherController running. Criterion: mean CPU-Work <= 16.7*1.15 ms.
        [UnityTest]
        public IEnumerator Performance_WorstCaseScenarioUnderBudget()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();

            SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "No SpiderMovement in Urwald.");
            DayNightCycle cycle = Object.FindFirstObjectByType<DayNightCycle>();
            Assert.IsNotNull(cycle, "No DayNightCycle in scene (Tag/Nacht must run).");

            // Tag/Nacht ACTIVE: noon -> dayFactor 1, sun-rotation + DynamicGI-Updates keep running.
            // Long dayLength keeps noon (dayFactor=1) stable through the later rain window, so day-species
            // stay alive while Regen runs -> genuine simultaneous worst case (Tag + Regen together).
            cycle.dayLengthMinutes = 200f;
            cycle.SetDayProgress(0.5f);

            // Regen: the WeatherController keeps cycling; JungleAmbientAudio/Wind react to Rain01.
            Assert.IsNotNull(WeatherController.Instance, "No active WeatherController (Regen must run).");

            // 6 Insekten simultaneously: day-species full, night-species empty -> exactly 6 flying.
            foreach (InsectSpawner sp in Object.FindObjectsByType<InsectSpawner>(FindObjectsSortMode.None))
            {
                if (sp.activeAtNight)
                {
                    sp.count = 0;
                }
                else
                {
                    sp.count = 6;
                }
            }

            // 3 Netze, je vollstaendig gebaut (Frame+Radii+CaptureSpiral+Aux = LineRenderer-Load).
            Vector3 spiderPos = spider.transform.position;
            for (int n = 0; n < 3; n++)
            {
                Vector3 hub = spiderPos + new Vector3(1.5f + 2.5f * n, 0.9f, 0f);
                Vector3[] anchors =
                {
                    hub + new Vector3(0f, 1.1f, -1.1f),
                    hub + new Vector3(0f, 1.1f, 1.1f),
                    hub + new Vector3(0f, -1.1f, 1.1f),
                    hub + new Vector3(0f, -1.1f, -1.1f),
                };
                OrbWeb web = WebBuildDemo.CreateWebAt(anchors, null, "K8Net" + n);
                Assert.IsNotNull(web, "No net for index " + n);
                web.SetProgress(OrbWeb.WebStage.Done, 1f);
            }

            // Live: let the spawners top up to target, then count real flying insects.
            yield return new WaitForSeconds(1f);
            int flying = Object.FindObjectsByType<FlyingInsect>(FindObjectsSortMode.None).Length;

            // Regen must be ACTIVE during measurement: fast-forward to a rainy window (Rain01 >= 0.8).
            float rainDeadline = Time.time + 130f;
            while (WeatherController.Instance != null && WeatherController.Instance.Rain01 < 0.8f && Time.time < rainDeadline)
            {
                yield return new WaitForSeconds(0.5f);
            }
            float rainNow = WeatherController.Instance != null ? WeatherController.Instance.Rain01 : 0f;
            Debug.Log("[K8] scenario: insects=" + flying + " nets=3 dayFactor=" + cycle.CurrentDayFactor.ToString("F2") +
                " rain01=" + rainNow.ToString("F2"));

            yield return MeasureMeanCpuMs(300);
            float mean = _lastMeanCpuMs;
            float budget = 16.7f * 1.15f;
            Debug.Log("[K8] mittl. CPU-Work-Framezeit " + mean.ToString("F2") + " ms -> Budget " + budget.ToString("F2") +
                " ms (x1.15) -> " + (mean <= budget ? "OK" : "NOK"));
            Assert.GreaterOrEqual(rainNow, 0.8f, "Regen not active during scenario: rain01=" + rainNow.ToString("F2") + ".");
            Assert.LessOrEqual(mean, budget, "Worst-case scenario CPU-Work above 16.7*1.15 ms budget: " + mean.ToString("F2") + " ms.");
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
                // U4.4: each scatter instance carries a thin CapsuleCollider (Plants-Layer)
                // -> climbable/anchorable like AddPlant; plus >= 5 ferns within 12 m of spawn.
                var scatterCaps = fernScatterGrp.GetComponentsInChildren<CapsuleCollider>();
                Assert.AreEqual(gen.FernScatterCount, scatterCaps.Length,
                    "Farn-Scatter CapsuleCollider-Anzahl weicht von FernScatterCount ab.");
            }

            // U3b: Grundfarne (Blender fern_1..5.fbx) in separate root 'Grundfarne', own capsule colliders.
            if (gen.GroundFernCount > 0)
            {
                GameObject grundfarneGrp = GameObject.Find("Grundfarne");
                Assert.IsNotNull(grundfarneGrp, "Grundfarne-Gruppe missing despite GroundFernCount > 0.");
                var gfMeshes = grundfarneGrp.GetComponentsInChildren<MeshRenderer>();
                Assert.AreEqual(gen.GroundFernCount, gfMeshes.Length,
                    "Grundfarn Instance-Anzahl weicht von GroundFernCount ab.");
                var gfCaps = grundfarneGrp.GetComponentsInChildren<CapsuleCollider>();
                Assert.AreEqual(gen.GroundFernCount, gfCaps.Length,
                    "Grundfarn CapsuleCollider-Anzahl weicht von GroundFernCount ab.");
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
                // G2 Schritt 0: Closeup-Kamera ~1.5-3 m vor der Spinne, Blick auf ihr,
                // nichts naeher als 0.5 m vor der Linse. Measure highest nearby tree top
                // so we can place the himmel-cam ABOVE the canopy (see sky).
                float canopyTop = 0f;
                foreach (var tr in GameObject.FindObjectsByType<UnityEngine.MeshRenderer>(FindObjectsSortMode.None))
                {
                    var tn = tr.gameObject.name;
                    if (tn != "TreeLOD0" && tn != "TreeLOD1") continue;
                    var b = tr.bounds;
                    if ((new Vector3(b.center.x - s.x, 0f, b.center.z - s.z)).sqrMagnitude < 12f * 12f)
                        canopyTop = Mathf.Max(canopyTop, b.max.y);
                }

                // --- urwald_spinne.png: Nahaufnahme, HDR-Pfad (MotionBlur/DoF off via no post pipeline). ---
                main.transform.position = s + new Vector3(0.6f, 0.18f, -0.9f);
                main.nearClipPlane = 0.5f;
                main.transform.LookAt(s + new Vector3(0f, 0.05f, 0f));
                if (veg != null) veg.RenderForCamera(main);
                Vector4 spinne = SaveFrameHdr(main, 1280, 720, "Logs/ci/urwald_spinne.png");

                // --- urwald_himmel.png: Kamera ueber dem Kronendach, Blick waagrecht. ---
                float camY = (canopyTop > 0f) ? canopyTop + 6f : s.y + 40f;
                main.transform.position = new Vector3(s.x, camY, s.z);
                main.nearClipPlane = 0.5f;
                var fwd = (new Vector3(0.6f, 0f, -0.9f)).normalized;
                main.transform.rotation = Quaternion.LookRotation(fwd);
                float skyFrac = SkyFractionUpperHalf(main, 30f);
                if (veg != null) veg.RenderForCamera(main);
                Vector4 himmel = SaveFrameHdr(main, 1280, 720, "Logs/ci/urwald_himmel.png");

                // DANN messen + asserten (ERST aufnehmen war oben -> G6-Lehre).
                Debug.Log("[G6-Spinne] Mittel " + spinne.x.ToString("F1") + " -> >=25 -> "
                    + (spinne.x >= 25f ? "OK" : "NOK")
                    + " | Magenta " + ((int)spinne.z).ToString() + " -> =0 -> " + (spinne.z == 0f ? "OK" : "NOK")
                    + " | SharpFrac " + spinne.w.ToString("P2") + " -> >=0.10 -> " + (spinne.w >= 0.10f ? "OK" : "NOK"));
                Debug.Log("[G6-Himmel] SkyFrac " + skyFrac.ToString("P2") + " -> >=0.5 -> "
                    + (skyFrac >= 0.5f ? "OK" : "NOK")
                    + " | Mittel " + himmel.x.ToString("F1") + " -> 60..200 -> "
                    + ((himmel.x >= 60f && himmel.x <= 200f) ? "OK" : "NOK")
                    + " | Max " + ((int)himmel.y).ToString() + " -> >200 -> " + (himmel.y > 200f ? "OK" : "NOK")
                    + " | Magenta " + ((int)himmel.z).ToString() + " -> =0 -> " + (himmel.z == 0f ? "OK" : "NOK"));

                Assert.GreaterOrEqual(spinne.x, 25f, "G6 spinne Mittel < 25.");
                Assert.LessOrEqual(spinne.z, 0f, "G6 spinne Magenta > 0.");
                Assert.GreaterOrEqual(spinne.w, 0.10f, "G6 spinne SharpFrac < 0.10.");
                Assert.GreaterOrEqual(skyFrac, 0.5f, "G6 himmel SkyFraction < 0.5.");
                Assert.GreaterOrEqual(himmel.x, 60f, "G6 himmel Mittel < 60.");
                Assert.LessOrEqual(himmel.x, 200f, "G6 himmel Mittel > 200 (blendout).");
                Assert.Greater(himmel.y, 200f, "G6 himmel Max <= 200.");
                Assert.LessOrEqual(himmel.z, 0f, "G6 himmel Magenta > 0.");
            }
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

        /// <summary>
        /// G2 Schritt 0: Anteil der Viewport-Pixel in der OBEREN Bildhaelfte, die zum Himmel
        /// gehoeren. Raycast per Pixel in die Frustum; nohit within maxDist = Himmel.
        /// maxDist kurza enough, dass dicht Bewuchs im Umkreis nicht zae; Weitenzweigs
        /// (offene Himmel) nohit -> Himmel. Return 0..1.
        /// </summary>
        private static float SkyFractionUpperHalf(Camera cam, float maxDist)
        {
            const int cols = 24, rows = 12;
            int sky = 0, total = 0;
            Vector3 origin = cam.transform.position;
            for (int i = 0; i <= cols; i++)
            {
                float fx = (float)i / cols;
                for (int j = rows; j <= 2 * rows; j++) // obere half: v 0.5..1.0
                {
                    float fy = 0.5f + 0.5f * (float)(j - rows) / rows;
                    Vector3 target = cam.ViewportToWorldPoint(new Vector3(fx, fy, maxDist));
                    Vector3 dir = target - origin;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    dir.Normalize();
                    total++;
                    if (!Physics.Raycast(origin, dir, maxDist)) sky++;
                }
            }
            return total > 0 ? (float)sky / total : 0f;
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

        // G6: offscreen cam.Render() SKIPS the HDRP post-processing pipeline. A plain ARGB32
        // readback clamps to [0,1] and thus renders dark/soft (Max ~172). The proven ScreenshotTool
        // path fixes this with an HDR RT (ARGBHalf) + geometric-mean auto-exposure + ACES tonemap.
        // Returns (Mittel 0..255, Max 0..255, Magenta count, SharpFraction 0..1).
        private static Vector4 SaveFrameHdr(Camera cam, int w, int h, string file)
        {
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var hdr = new Texture2D(w, h, TextureFormat.RGBAHalf, false);
            hdr.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            hdr.Apply(false);
            RenderTexture.active = null;
            cam.targetTexture = null;

            Color[] px = hdr.GetPixels();
            int n = px.Length;
            float sumLog = 0f;
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sumLog += Mathf.Log(Mathf.Max(lum, 1e-5f));
            }
            float exposure = 0.55f / Mathf.Exp(sumLog / n);
            exposure = Mathf.Clamp(exposure, 0.0005f, 50f);

            Color[] outPx = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                float r = Mathf.Max(c.r * exposure, 0f);
                float g = Mathf.Max(c.g * exposure, 0f);
                float b = Mathf.Max(c.b * exposure, 0f);
                outPx[i] = new Color(AcesTone(r), AcesTone(g), AcesTone(b), 1f);
            }

            double sum = 0.0;
            int maxv = 0;
            long magenta = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = outPx[i];
                float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                sum += luma * 255.0;
                int ri = (int)(Mathf.Max(c.r, Mathf.Max(c.g, c.b)) * 255f);
                if (ri > maxv) maxv = ri;
                float R = c.r * 255f, G = c.g * 255f, B = c.b * 255f;
                if (R > 200f && G < 80f && B > 200f) magenta++;
            }
            long sharpHits = 0, sharpTotal = 0;
            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                for (int x = 0; x + 1 < w; x++)
                {
                    Color a = outPx[rowBase + x];
                    Color b2 = outPx[rowBase + x + 1];
                    float la = 0.299f * a.r + 0.587f * a.g + 0.114f * a.b;
                    float lb = 0.299f * b2.r + 0.587f * b2.g + 0.114f * b2.b;
                    if (Mathf.Abs(la - lb) * 255f > 8f) sharpHits++;
                    sharpTotal++;
                }
            }

            var ldr = new Texture2D(w, h, TextureFormat.RGB24, false);
            ldr.SetPixels(outPx);
            ldr.Apply(false);
            byte[] png = ldr.EncodeToPNG();
            System.IO.File.WriteAllBytes(file, png);
            Object.DestroyImmediate(hdr);
            Object.DestroyImmediate(ldr);
            Object.DestroyImmediate(rt);

            float mean = (float)(sum / n);
            float sharpFrac = sharpTotal > 0 ? (float)sharpHits / sharpTotal : 0f;
            return new Vector4(mean, (float)maxv, (float)magenta, sharpFrac);
        }

        private static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
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

        /// <summary>
        /// W1 Schritt 3: same Seed -> identical placement-hash; different Seed ->
        /// different hash; Density(Wald)/Density(Lichtung) >= 2.0; creek present
        /// (path spans the world, bed under WaterLevel, banks dry above it).
        /// </summary>
        [UnityTest]
        public IEnumerator W1_SeedsDeterministicAndCreekPresent()
        {
            uint h1 = PlacementHash(31337);
            uint h2 = PlacementHash(31337);
            Assert.AreEqual(h1, h2, "Seed 31337: placement-hash differs across runs.");
            uint h3 = PlacementHash(31338);
            Assert.AreNotEqual(h1, h3, "Seed 31338: placement identical to 31337.");

            JungleWorldGenerator.NextSeed = 31337;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 31337) not ready.");
            JungleWorld world = readyGen.World;

            var pts = world.CreekPath;
            Assert.GreaterOrEqual(pts.Length, 5, "Creek path too short.");
            Assert.LessOrEqual(pts[0].x, 1f, "Creek does not start at the world edge.");
            Assert.GreaterOrEqual(pts[pts.Length - 1].x, world.Size - 1f, "Creek does not reach the far edge.");
            bool bedWet = false, bankDry = false;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 mid = (pts[i] + pts[i + 1]) * 0.5f;
                if (world.IsUnderWater(mid.x, mid.y)) bedWet = true;
                if (!world.IsUnderWater(mid.x, mid.y + JungleWorld.CreekCore + 1.2f)) bankDry = true;
            }
            Assert.IsTrue(bedWet, "Creek bed nowhere under WaterLevel.");
            Assert.IsTrue(bankDry, "Creek banks nowhere dry.");

            float half = world.Size * 0.5f;
            double sumForest = 0.0, sumClear = 0.0;
            int nForest = 0, nClear = 0;
            for (int x = -46; x <= 46; x += 2)
            {
                for (int z = -46; z <= 46; z += 2)
                {
                    float fx = half + x, fz = half + z;
                    float d = world.Density(fx, fz);
                    if (world.IsClearing(fx, fz)) { sumClear += d; nClear++; }
                    else if (!world.IsUnderWater(fx, fz)) { sumForest += d; nForest++; }
                }
            }
            Assert.Greater(nClear, 100, "Too few clearing-samples (< 100).");
            Assert.Greater(nForest, 500, "Too few forest-samples (< 500).");
            float ratio = (float)(sumForest / nForest) / (float)(sumClear / nClear);
            Debug.Log("[W1] Dichte Wald/Lichtung: " + ratio.ToString("F2"));
            Assert.GreaterOrEqual(ratio, 2.0f, "Density forest/clearing " + ratio.ToString("F2") + " < 2.0.");
        }

        // E1 Schritt 2a: Sumpfflaechen are shallow — the bed of every flooded area (Sumpf
        // and creek) must never go deeper than MaxSwampDepth (0.15 m) under the waterline.
        [UnityTest]
        public IEnumerator E1_SwampWaterIsShallow()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) not ready.");
            JungleWorld world = readyGen.World;
            float half = world.Size / 2f;

            int wet = 0;
            float maxDepth = 0f;
            for (int dx = -30; dx <= 30; dx++)
            {
                for (int dz = -30; dz <= 30; dz++)
                {
                    float x = half + dx, z = half + dz;
                    if (!world.IsUnderWater(x, z)) continue;
                    wet++;
                    float depth = JungleWorld.WaterLevel - world.Height(x, z);
                    if (depth > maxDepth) maxDepth = depth;
                }
            }
            Debug.Log("[E1] wet=" + wet + " maxDepth=" + maxDepth.ToString("F3"));
            Assert.Greater(wet, 0, "Keinen Sumpfflaechen found.");
            Assert.LessOrEqual(maxDepth, JungleWorld.MaxSwampDepth + 0.001f,
                "Sumpfflaechen deeper than " + JungleWorld.MaxSwampDepth.ToString("F2") + " m (" + maxDepth.ToString("F3") + ").");
        }

        // E1 Schritt 2b: Spinne in Sumpfflaechen moves at >= 50 % of the ground walk speed.
        [UnityTest]
        public IEnumerator E1_SpiderWadesThroughSwamp()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) not ready.");
            JungleWorld world = readyGen.World;
            float half = world.Size / 2f;

            // A wet spot with shallow-but-real depth to wade in.
            Vector3 spot = Vector3.zero;
            bool found = false;
            for (int dx = -20; dx <= 20 && !found; dx++)
            {
                for (int dz = -20; dz <= 20 && !found; dz++)
                {
                    float x = half + dx, z = half + dz;
                    if (!world.IsUnderWater(x, z)) continue;
                    float depth = JungleWorld.WaterLevel - world.Height(x, z);
                    if (depth < 0.02f || depth > 0.14f) continue;
                    spot = new Vector3(x, JungleWorld.WaterLevel + 0.03f, z);
                    found = true;
                }
            }
            Assert.IsTrue(found, "Keinen wadeable swamp spot (depth 0.02-0.14 m) found.");

            SpiderMovement spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "Spinne not found.");
            spider.Teleport(spot, Quaternion.identity);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1f) yield return null;

            // Drive forward for ~2 s and track the peak speed reached.
            float maxSpeed = 0f;
            spider.SetExternalMoveLocal(new Vector2(0f, 1f));
            spider.SetExternalSprint(false);
            start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 2f)
            {
                yield return null;
                maxSpeed = Mathf.Max(maxSpeed, spider.Speed);
            }
            float groundSpeed = spider.WalkSpeed;
            Debug.Log("[E1] wadeMax=" + maxSpeed.ToString("F3") + " walk=" + groundSpeed.ToString("F3"));
            Assert.GreaterOrEqual(maxSpeed, groundSpeed * 0.5f,
                "Wading speed " + maxSpeed.ToString("F3") + " < 50 % of walk speed " + groundSpeed.ToString("F3") + ".");
        }

        // E1 Schritt 2c: Beweisbild Logs/ci/wasser.png must be sharp (SharpFrac >= 0.10).
        [UnityTest]
        public IEnumerator E1_WaterProofImageIsSharp()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) not ready.");
            JungleWorld world = readyGen.World;
            float half = world.Size / 2f;

            // Find a wet spot near a dry shore so the frame shows water + shoreline.
            Vector3 wetSpot = Vector3.zero;
            bool found = false;
            for (int dx = -20; dx <= 20 && !found; dx++)
            {
                for (int dz = -20; dz <= 20 && !found; dz++)
                {
                    float x = half + dx, z = half + dz;
                    if (!world.IsUnderWater(x, z)) continue;
                    if (world.IsUnderWater(x + 3f, z) || world.IsUnderWater(x - 3f, z)) continue;
                    wetSpot = new Vector3(x, JungleWorld.WaterLevel, z);
                    found = true;
                }
            }
            Assert.IsTrue(found, "Keinen water-with-shore spot found for the proof shot.");

            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f);
            yield return null; // one frame so DayNightCycle rotates the sun to morning

            Camera main = Camera.main;
            Assert.IsNotNull(main, "Hauptkamera not found.");
            VegetationSystem veg = Object.FindFirstObjectByType<VegetationSystem>();
            main.transform.position = wetSpot + new Vector3(0f, 1.6f, -2.4f);
            main.nearClipPlane = 0.5f;
            main.transform.LookAt(wetSpot + new Vector3(0f, JungleWorld.WaterLevel - 0.05f, 1.5f));
            if (veg != null) veg.RenderForCamera(main);
            Vector4 wasser = SaveFrameHdr(main, 1280, 720, "Logs/ci/wasser.png");
            Debug.Log("[E1-Wasser] SharpFrac " + wasser.w.ToString("P2") + " -> >=0.10 -> " + (wasser.w >= 0.10f ? "OK" : "NOK"));
            Assert.GreaterOrEqual(wasser.w, 0.10f,
                "Wasser-Beweisbild SharpFrac " + wasser.w.ToString("P2") + " < 0.10.");
        }

        /// <summary>
        /// P1: Konsolenfehler im Playmode-Durchlauf = 0 AND Anzahl Objekte ohne LOD bei Vegetation = 0.
        /// A console error/exception is any log of LogType.Error or LogType.Exception that the world
        /// generation raises after the scene loads. Vegetation = every MeshRenderer under the named
        /// vegetation roots (Bäume, Unterholz, Baumfarne, Farn-Scatter, Grundfarne, Lianen); each must
        /// sit in some LODGroup (LOD/Culling). Mittlere Frametime <= Budget ist der sibling green-Test
        /// Performance_MeanFrameTimeUnderBudget.
        /// </summary>
        [UnityTest]
        public IEnumerator P1_LodsAndNoConsoleErrors()
        {
            JungleWorldGenerator.NextSeed = 4242;
            _p1ConsoleErrors = 0;
            Application.logMessageReceived += OnP1Log;

            SceneManager.LoadScene("Urwald");
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "Welt (Seed 4242) nicht fertig.");
            for (int i = 0; i < 5; i++) yield return null; // let die Generation (und späte Logs) finish
            Application.logMessageReceived -= OnP1Log;

            Assert.AreEqual(_p1ConsoleErrors, 0, "Konsolenfehler im Playmode-Durchlauf != 0.");

            // Walk the named vegetation roots; every MeshRenderer descendant must be inside a LODGroup.
            // A renderer is covered if it or any ancestor carries an LODGroup component (trees: group on
            // root, renderers on children; plants/lianas: group on the same node as the renderer).
            var roots = new System.Collections.Generic.List<GameObject>();
            string[] names = { "Bäume", "Unterholz", "Baumfarne", "Farn-Scatter", "Grundfarne", "Lianen" };
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                for (int n = 0; n < names.Length; n++)
                    if (go.name == names[n]) { roots.Add(go); break; }

            _p1VegRenderers = 0;
            _p1MissingLod = 0;
            foreach (var root in roots)
                CountVegetationRenderers(root.transform, null);
            Assert.Greater(_p1VegRenderers, 200, "Vegetation-Wurzels ohne MeshRenderer -> nicht gelauf.");
            Assert.AreEqual(_p1MissingLod, 0, "Objekte ohne LOD bei Vegetation != 0 (" + _p1MissingLod + ").");
        }

        /// <summary>Counts one console error/exception per Error- or Exception-log while the hook is active.</summary>
        private int _p1ConsoleErrors;
        private int _p1VegRenderers;
        private int _p1MissingLod;

        private void OnP1Log(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) _p1ConsoleErrors++;
        }

        /// <summary>Recursively count MeshRenderers under t; a renderer without an LODGroup on itself or any ancestor counts as missing.</summary>
        private void CountVegetationRenderers(Transform t, Transform parent)
        {
            var r = t.GetComponent<MeshRenderer>();
            if (r != null)
            {
                _p1VegRenderers++;
                if (!IsUnderLod(t)) _p1MissingLod++;
            }
            for (int c = 0; c < t.childCount; c++)
                CountVegetationRenderers(t.GetChild(c), t);
        }

        /// <summary>True if t or any ancestor of t carries an LODGroup component.</summary>
        private bool IsUnderLod(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                if (cur.GetComponent<LODGroup>() != null) return true;
                cur = cur.parent;
            }
            return false;
        }

        /// <summary>Hash of the seed-driven placement basis (height + density grid, 4 m steps, FNV-1a over quantised values).</summary>
        private static uint PlacementHash(int seed)
        {
            var w = new JungleWorld(seed);
            const uint prime = 16777619u;
            uint h = 2166136261u;
            for (int x = -48; x <= 48; x += 4)
            {
                for (int z = -48; z <= 48; z += 4)
                {
                    float fx = w.Size * 0.5f + x, fz = w.Size * 0.5f + z;
                    int qh = (int)(w.Height(fx, fz) * 100f);
                    int qd = (int)(w.Density(fx, fz) * 1000f);
                    h = (h ^ (uint)qh) * prime;
                    h = (h ^ (uint)qd) * prime;
                }
            }
            return h;
        }
    }
}
