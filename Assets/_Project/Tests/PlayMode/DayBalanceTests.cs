using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// S5 Tagesbilanz: after a simulated day with 3 eaten insects, 2 built webs and one bird
    /// warning the morning window shows exactly 3/2/1; after the reset the day counters run
    /// up from 0 while the totals keep their value. The sunrise event fires at most once per
    /// morning (midnight wrap-around does NOT fire). Proof-shot: Logs/ci/s5_tagesbilanz.png.
    /// </summary>
    public class DayBalanceTests
    {
        [UnityTest]
        public IEnumerator S5_Tagesbilanz_ProTagUndNichtProGesamt()
        {
            // --- Day 1: spider with stats + growth + balance window, cycle near sunrise. ---
            var spiderGo = new GameObject("S5Spider");
            var growth = spiderGo.AddComponent<SpiderGrowth>();
            var stats = spiderGo.AddComponent<DeathStats>();
            var balance = spiderGo.AddComponent<DayBalance>();

            var cycleGo = new GameObject("S5Cycle");
            var cycle = cycleGo.AddComponent<DayNightCycle>();
            cycle.dayLengthMinutes = 0.001f; // day = 0.06 s -> one frame crosses the sunrise
            cycle.SetDayProgress(0.1f); // far from sunrise while we set up the day
            balance.SetCycle(cycle);

            // Let the Update()s resolve their refs and sync counters once.
            yield return null;
            var websAtStart = stats.Stats.WebsBuilt;

            // 3 eaten insects -> day counter AND total show 3.
            for (var e = 0; e < 3; e++)
            {
                growth.OnInsectEaten(null);
            }

            // 2 built webs -> day counter shows exactly 2 more than at start.
            for (var w = 0; w < 2; w++)
            {
                var webGo = new GameObject("S5Web" + w);
                var anchors = new Vector3[]
                {
                    new Vector3(w, 1f, 0f),
                    new Vector3(w + 1f, 1f, 0f),
                    new Vector3(w + 1f, 0f, 0f),
                    new Vector3(w, 0f, 0f)
                };
                var web = webGo.AddComponent<OrbWeb>();
                web.Build(new OrbWebLayout(anchors, 8, 0.05f, 0.04f, 7));
                web.SetProgress(OrbWeb.WebStage.Done, 1f);
            }

            // One bird pre-warning -> biggest danger of the day counts 1.
            balance.OnWarning();

            // Let DeathStats.Update() sync the day counters BEFORE sunrise (DayEaten -> 3).
            var sync = 0;
            while (sync < 60 && stats.Stats.DayEaten < 3)
            {
                yield return null;
                sync++;
            }
            Assert.AreEqual(3, stats.Stats.DayEaten, "S5: the day counter counts up during the day.");

            // Jump to just before sunrise (a test-jump does NOT fire the morning event), then
            // let one fast frame cross it.
            cycle.SetDayProgress(0.249f);

            // Let the day run to sunrise (crossing 0.25 fires MorningFired once).
            var frames = 0;
            while (frames < 60 && stats.Stats.LastDayEaten == 0)
            {
                yield return null;
                frames++;
            }
            Report("last day eaten", stats.Stats.LastDayEaten);
            Assert.AreEqual(3, stats.Stats.LastDayEaten, "S5: the window shows exactly the 3 prey of the day.");
            Report("last day webs", stats.Stats.LastDayWebs);
            Assert.AreEqual(2, stats.Stats.LastDayWebs, "S5: the window shows exactly the 2 webs of the day.");
            Report("last day warnings", stats.Stats.LastDayWarnings);
            Assert.AreEqual(1, stats.Stats.LastDayWarnings, "S5: one bird warning counts as danger 1.");
            Report("day eaten after reset", stats.Stats.DayEaten);
            Assert.AreEqual(0, stats.Stats.DayEaten, "S5: the new day runs up from 0.");

            // Totals must NOT be reset (K6 keeps the overall numbers).
            Assert.AreEqual(3, stats.Stats.EatenCount, "S5: totals keep rising while the day resets.");
            Assert.IsTrue(balance.WindowOpen, "S5: the balance window is open at sunrise.");
            var line = balance.FormatBalanceLine();
            Report("balance line", 0f);
            Debug.Log("S5 line -> " + line);
            Assert.IsTrue(line.Contains("Eaten 3"), "S5: line shows the 3 prey of the day.");
            Assert.IsTrue(line.Contains("Webs 2"), "S5: line shows the 2 webs of the day.");
            Assert.IsTrue(line.Contains("Danger: 1 Warns"), "S5: line shows danger 1 from the warning.");

            // Proof-shot of the morning window (same OnGUI layout painted over the frame).
            var camGo = new GameObject("S5Cam");
            var cam = camGo.AddComponent<Camera>();
            yield return CaptureBalanceProof("Logs/ci/s5_tagesbilanz.png", stats.Stats, cam);

            // Cleanup.
            GameObject.DestroyImmediate(spiderGo);
            GameObject.DestroyImmediate(cycleGo);
            GameObject.DestroyImmediate(camGo);
            var snapshot = new System.Collections.Generic.List<OrbWeb>(OrbWeb.All);
            foreach (OrbWeb leftover in snapshot)
            {
                GameObject.DestroyImmediate(leftover);
            }
        }

        [UnityTest]
        public IEnumerator S5_SunriseEventFiretNurOncePerMorgen()
        {
            // Midnight wrap-around must NOT open a window (only the forward sunrise crossing does).
            var cycleGo = new GameObject("S5Cycle2");
            var cycle = cycleGo.AddComponent<DayNightCycle>();
            cycle.dayLengthMinutes = 2f; // slow enough that 30 frames never reach sunrise
            cycle.SetDayProgress(0.995f);

            var spiderGo = new GameObject("S5Spider2");
            var stats = spiderGo.AddComponent<DeathStats>();
            var balance = spiderGo.AddComponent<DayBalance>();
            balance.SetCycle(cycle);

            var frames = 0;
            while (frames < 30)
            {
                yield return null;
                frames++;
                Assert.IsFalse(balance.WindowOpen, "S5: midnight wrap-around must not open the window.");
            }
            Report("last day eaten after midnight", stats.Stats.LastDayEaten);
            Assert.AreEqual(0, stats.Stats.LastDayEaten, "S5: no morning means no snapshot.");

            GameObject.DestroyImmediate(spiderGo);
            GameObject.DestroyImmediate(cycleGo);
        }

        private static void Report(string label, float value)
        {
            Debug.Log("S5 -> " + label + ": " + value.ToString("0.####") + " -> checked by asserts above");
        }

        /// <summary>
        /// Proofshot of the morning window: prime a throwaway RT first (cold-RT lesson from
        /// DeathStatsTests), then paint the same panel + tick rows DayBalance.OnGUI draws.
        /// </summary>
        private IEnumerator CaptureBalanceProof(string file, PartieStatsModel shown, Camera cam)
        {
            var prime = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            prime.Create();
            cam.targetTexture = prime;
            cam.Render();
            cam.targetTexture = null;
            prime.Release();
            yield return null;

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);

            PaintRect(tex, 16, (int)(rt.height * 0.14f), 520, 110, new Color(0.05f, 0.05f, 0.05f));
            foreach (var t in shown.DayBalanceTicks(rt.width, rt.height))
            {
                PaintRect(tex, (int)t.x, (int)t.y, (int)t.z, (int)t.w, new Color(1f, 1f, 1f));
            }
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
        }

        /// <summary>Fills a rect in GUI coords (y down from top) with a solid colour.</summary>
        private static void PaintRect(Texture2D tex, int x, int yTop, int w, int h, Color c)
        {
            for (var px = x; px < x + w && px < tex.width; px++)
            {
                for (var gy = yTop; gy < yTop + h && gy < tex.height; gy++)
                {
                    if (px < 0 || gy < 0)
                    {
                        continue;
                    }
                    var py = tex.height - 1 - gy; // GUI y down -> texture y up.
                    tex.SetPixel(px, py, c);
                }
            }
        }
    }
}
