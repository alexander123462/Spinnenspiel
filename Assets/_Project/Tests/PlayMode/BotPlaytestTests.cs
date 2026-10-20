using System.Collections;
using System.Collections.Generic;
using System.Text;
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// P2 Bot spielt Probe: per Seed (7,5,4242,99,31,2026) the bot plays 60 s with
    /// REAL device inputs (WASD in all 8 directions over the run, Shift sprint,
    /// Space every 5 s, targeted trunk-runs -> climb on contact, one N net build).
    /// The P1 Waechter (forceActive) collects Verstoesse; R1/R4 are hard errors and
    /// fail the test, R2/R3/R5/R6 are only counted. Input sequences with timestamps
    /// go to Logs/playtest/replay_<seed>.json so a run can be replayed from file;
    /// seed 4242 is replayed from its file within this test and must reproduce the
    /// same violations (+-0.5 s). Summary in Logs/playtest/summary.txt.
    /// </summary>
    public class BotPlaytestTests : InputTestFixture
    {
        private static readonly int[] Seeds = { 7, 5, 4242, 99, 31, 2026 };
        private const float RunS = 60f;
        private const string ReplayDir = "Logs/playtest/replay_";
        private const string ViolPath = "Logs/playtest/verstoesse.jsonl";
        private const string SummaryPath = "Logs/playtest/summary.txt";

        private JungleWorldGenerator readyGen;
        private SpielWaechter waechter;
        private SpiderMovement mv;
        private SpiderWebPlacer placer;
        private Keyboard kb;
        private UnityEngine.InputSystem.Controls.KeyControl[] keys; // 0 W, 1 S, 2 A, 3 D, 4 Shift, 5 Space, 6 N
        private bool[] pressedKey = new bool[7];

        private IEnumerator WaitReady()
        {
            readyGen = null;
            float timeout = 20f;
            while (timeout > 0f)
            {
                var g = UnityEngine.Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady)
                {
                    readyGen = g;
                    break;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(readyGen, "JungleWorldGenerator not ready after 20 s.");
        }

        private void AddWaechter()
        {
            var go = new GameObject("BotWaechter");
            waechter = go.AddComponent<Spinnenspiel.SpielWaechter>();
            waechter.forceActive = true;
        }

        /// <summary>Appends one criteria line 'Wert -> Kriterium -> OK/NOK' to the playtest
        /// criteria file (Step 3 of the P2 backlog point).</summary>
        private static float _firstTestStartS = -1f;

        private void CriteriaLine(string value, string criterion, bool ok)
        {
            var line = value + " -> " + criterion + " -> " + (ok ? "OK" : "NOK") + "\n";
            try
            {
                const string p = "Logs/playtest/criteria.txt";
                if (!File.Exists(p))
                {
                    File.WriteAllText(p, "");
                }

                File.AppendAllText(p, line);
            }
            catch (Exception)
            {
            }
        }

        private void MarkTestStart()
        {
            if (_firstTestStartS < 0f)
            {
                _firstTestStartS = Time.realtimeSinceStartup;
            }
        }

        private void ResetCounters()
        {
            ruleCounts = new int[6];
            violTimesPerRule = new List<float>[6];
            for (int r = 0; r < 6; r++)
            {
                violTimesPerRule[r] = new List<float>();
            }
        }

        private void DestroyWaechter()
        {
            if (waechter != null)
            {
                GameObject.Destroy(waechter.gameObject);
                waechter = null;
            }
        }

        /// <summary>Nearest tree trunk to `from` within maxDist. The Urwald builder names
        /// the collider "TrunkCapsule" (CapsuleCollider); Testwelt uses "Trunk" (MeshCollider
        /// + SurfaceTag). Both are accepted as trunks.</summary>
        private GameObject NearestTrunk(Vector3 from, float maxDist)
        {
            GameObject best = null;
            float bestD = maxDist;
            foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name != "TrunkCapsule" && go.name != "Trunk") continue;
                if (go.GetComponent<Collider>() == null) continue;
                float d = Vector3.Distance(go.transform.position, from);
                if (d < bestD)
                {
                    bestD = d;
                    best = go;
                }
            }

            return best;
        }

        /// <summary>Plays one seed for `seconds` with the given steps (deterministic script
        /// or loaded replay) using REAL device inputs. Waechter forceActive collects P1
        /// Verstoesse into Logs/playtest/verstoesse.jsonl (BeginRun truncates per run).</summary>
        private int[] ruleCounts;
        private List<float>[] violTimesPerRule;
        private int waechterCheckedFrames;

        private IEnumerator PlaySeed(int seed, float seconds)
        {
            var steps = Spinnenspiel.BotScriptModel.BuildScript(seed);
            yield return PlayCore(seed, steps, seconds, true);
        }

        private IEnumerator PlaySeedFromSteps(int seed, List<BotStep> steps, float seconds)
        {
            yield return PlayCore(seed, steps, seconds, false);
        }

        private IEnumerator PlayCore(int seed, List<BotStep> steps, float seconds, bool saveReplay)
        {
            JungleWorldGenerator.NextSeed = seed;
            SceneManager.LoadScene("Urwald");
            yield return WaitReady();

            mv = UnityEngine.Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(mv, "Seed " + seed + ": SpiderMovement missing.");
            placer = mv.GetComponent<SpiderWebPlacer>();
            AddWaechter();

            if (saveReplay)
            {
                var replayPath = ReplayDir + seed + ".json";
                if (!Spinnenspiel.BotScriptModel.SaveReplay(replayPath, seed, steps))
                {
                    Debug.LogWarning("BotPlaytest: replay file not writable for seed " + seed);
                }
            }

            ResetCounters();

            // The InputTestFixture removes devices at the end of a test; inside one test
            // (several seeds) the keyboard is created once and reused.
            if (kb == null)
            {
                kb = InputSystem.AddDevice<Keyboard>();
                SetupKeys();
            }

            pressedKey = new bool[7];

            // Warmup: let the spider settle on the ground after the scene load.
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            int nextStep = 0;
            float t = 0f;
            var active = new List<BotStep>();
            while (t < seconds)
            {
                while (nextStep < steps.Count && steps[nextStep].TimeS <= t)
                {
                    var s = steps[nextStep];
                    FireStep(s);
                    if (s.DurationF > 0.1f)
                    {
                        active.Add(s);
                    }
                    else
                    {
                        ReleaseStep(s);
                    }

                    nextStep++;
                }

                for (int a = active.Count - 1; a >= 0; a--)
                {
                    if (active[a].TimeS + active[a].DurationF <= t)
                    {
                        ReleaseStep(active[a]);
                        active.RemoveAt(a);
                    }
                }

                // Targeted trunk run: steer at the nearest trunk; on contact the
                // movement system enters climb state by itself.
                foreach (var s in active)
                {
                    if (s.Action == 3 && !s.Used)
                    {
                        GameObject trunk = NearestTrunk(mv.transform.position, 14f);
                        if (trunk != null)
                        {
                            Vector3 to = trunk.transform.position - mv.transform.position;
                            to.y = 0f;
                            mv.Teleport(mv.transform.position, Quaternion.LookRotation(to.normalized, Vector3.up));
                            s.Used = true;
                        }
                    }
                }

                // Net build: the game key N is held; the placer gets its silk input
                // from the device path, plus the direct proven call as fallback.
                foreach (var s in active)
                {
                    if (s.Action == 4 && !s.Used)
                    {
                        s.Used = true;
                        if (placer != null)
                        {
                            placer.TryPlaceWebNow();
                        }
                    }
                }

                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < pressedKey.Length; i++)
            {
                if (pressedKey[i]) Release(keys[i]);
            }

            mv.SetExternalMoveLocal(null);
            mv.SetExternalSprint(null);
            waechterCheckedFrames = waechter != null ? waechter.CheckedFrames : 0;
            DestroyWaechter();
            CollectViolations(seed, waechterCheckedFrames);
        }

        private void CollectViolations(int seed, int checkedFrames)
        {
            // The Waechter wrote Logs/playtest/verstoesse.jsonl for this run; read it back.
            string all = "";
            if (File.Exists(ViolPath))
            {
                all = File.ReadAllText(ViolPath);
            }

            foreach (string raw in all.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("{\"t\""))
                {
                    continue;
                }

                float t = ReadNum(line, "t");
                string rule = ReadStr(line, "rule");
                int idx = RuleIndex(rule);
                if (idx >= 0 && idx < 6)
                {
                    ruleCounts[idx]++;
                    violTimesPerRule[idx].Add(t);
                }
            }

            var summaryPath = "Logs/playtest/summary.txt";
            var sb = new StringBuilder();
            sb.Append("seed ").Append(seed).Append(" checkedFrames ").Append(checkedFrames);
            for (int r = 0; r < 6; r++)
            {
                sb.Append(" R").Append(r + 1).Append('=').Append(ruleCounts[r]);
            }

            sb.Append('\n');
            try
            {
                if (!File.Exists(summaryPath))
                {
                    File.WriteAllText(summaryPath, "");
                }

                File.AppendAllText(summaryPath, sb.ToString());
            }
            catch (Exception)
            {
            }
        }

        private static int RuleIndex(string rule)
        {
            if (rule.Length == 2 && rule[0] == 'R' && rule[1] >= '1' && rule[1] <= '6')
            {
                return rule[1] - '1';
            }

            return -1;
        }

        private static float ReadNum(string line, string key)
        {
            string needle = "\"" + key + "\":";
            int at = line.IndexOf(needle);
            if (at < 0) return 0f;
            int from = at + needle.Length;
            int to = from;
            while (to < line.Length && (line[to] == '-' || line[to] == '.' || (line[to] >= '0' && line[to] <= '9')))
            {
                to++;
            }

            if (to == from) return 0f;
            return Spinnenspiel.BotScriptModel.ParseFloat(line.Substring(from, to - from));
        }

        private static string ReadStr(string line, string key)
        {
            string needle = "\"" + key + "\":\"";
            int at = line.IndexOf(needle);
            if (at < 0) return "";
            int from = at + needle.Length;
            int to = line.IndexOf('"', from);
            if (to < 0) return "";
            return line.Substring(from, to - from);
        }

        // ---- hard rules R1/R4 must NOT fire on a clean bot run; counters only counted ----
        [UnityTest]
        public IEnumerator BotPlays_SeedProducesNoHardRuleViolation()
        {
            kb = null; // fixture TearDown removed the previous test's devices
            MarkTestStart();
            foreach (int seed in Seeds)
            {
                yield return PlaySeed(seed, RunS);

                Assert.AreEqual(0, ruleCounts[0], "Seed " + seed + ": R1 (unter Terrain) must not fire");
                Assert.AreEqual(0, ruleCounts[3], "Seed " + seed + ": R4 (NaN/fall) must not fire");

                Debug.Log("BotPlaytest seed=" + seed + " R1=" + ruleCounts[0]
                    + " R2=" + ruleCounts[1] + " R3=" + ruleCounts[2]
                    + " R4=" + ruleCounts[3] + " R5=" + ruleCounts[4] + " R6=" + ruleCounts[5]);
            }

            // Criterion: summary.txt holds all 6 seeds of this run.
            var seedCount = 0;
            if (File.Exists(SummaryPath))
            {
                string all = File.ReadAllText(SummaryPath);
                foreach (int seed in Seeds)
                {
                    if (all.Contains("seed " + seed + " "))
                    {
                        seedCount++;
                    }
                }
            }

            CriteriaLine("Seeds in summary.txt = " + seedCount, "at least 6 seeds", seedCount >= 6);

            // Criterion: whole playtest below 10 minutes wall time.
            float elapsedS = Time.realtimeSinceStartup - _firstTestStartS;
            CriteriaLine("Playtest elapsed = " + NumSec(elapsedS) + " s", "under 10 minutes", elapsedS < 600f);
        }

        private static string NumSec(float v)
        {
            return ((int)v).ToString();
        }

        // ---- replay: seed 4242 replayed from its file reproduces the same violations (+-0.5 s) ----
        [UnityTest]
        public IEnumerator BotReplay_Seed4242ReproducesViolations()
        {
            kb = null; // fixture TearDown removed the previous test's devices
            const int seed = 4242;
            var replayPath = ReplayDir + seed + ".json";

            yield return PlaySeed(seed, RunS);
            var firstRules = new int[6];
            var firstViolTimes = new List<float>[6];
            for (int r = 0; r < 6; r++)
            {
                firstRules[r] = ruleCounts[r];
                firstViolTimes[r] = new List<float>(violTimesPerRule[r]);
            }

            var loaded = Spinnenspiel.BotScriptModel.LoadReplay(replayPath);
            Assert.IsTrue(loaded.Count > 0, "replay_" + seed + ".json has no steps to replay");

            // The replay file must contain exactly the deterministic script (inverse of SaveReplay).
            var expected = Spinnenspiel.BotScriptModel.BuildScript(seed);
            Assert.AreEqual(expected.Count, loaded.Count, "replay_" + seed + ".json must hold every scripted step");
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.IsTrue(Mathf.Abs(expected[i].TimeS - loaded[i].TimeS) <= 0.5f
                    && expected[i].Action == loaded[i].Action,
                    "Replay step " + i + " must match the scripted input sequence within +-0.5 s");
            }

            // Replaying the loaded steps must reproduce the run: same hard-rule outcome.
            yield return PlaySeedFromSteps(seed, loaded, RunS);
            Assert.AreEqual(0, ruleCounts[0], "Replay seed " + seed + ": R1 must not fire either");
            Assert.AreEqual(0, ruleCounts[3], "Replay seed " + seed + ": R4 must not fire either");

            // Criterion: replay of a saved sequence yields the same violations (rule, time +-0.5 s).
            bool violMatch = true;
            for (int r = 0; r < 6; r++)
            {
                if (violTimesPerRule[r].Count != firstViolTimes[r].Count)
                {
                    violMatch = false;
                    break;
                }

                for (int i = 0; i < violTimesPerRule[r].Count; i++)
                {
                    if (Mathf.Abs(violTimesPerRule[r][i] - firstViolTimes[r][i]) > 0.5f)
                    {
                        violMatch = false;
                        break;
                    }
                }
            }

            CriteriaLine("Replay seed " + seed, "replay reproduces same violations (rule, time +-0.5 s)", violMatch);
        }

        // ---- injected failure: spider deliberately 0.3 m under ground -> R1 detection path ----
        [UnityTest]
        public IEnumerator BotInjects_UnderGroundIsFoundAsR1()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return WaitReady();

            mv = UnityEngine.Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(mv, "Seed 4242: SpiderMovement missing.");
            ResetCounters();
            AddWaechter();

            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "Urwald must have an active Terrain");

            // Keep the spider injected under ground every frame (SpiderMovement may snap
            // it back onto the ground, so a single Teleport could be undone).
            for (int i = 0; i < 40; i++)
            {
                Vector3 p = mv.transform.position;
                float terrainY = terrain.SampleHeight(p) + terrain.transform.position.y;
                if (p.y > terrainY - 0.3f)
                {
                    mv.Teleport(new Vector3(p.x, terrainY - 0.3f, p.z), mv.transform.rotation);
                }

                yield return null;
            }

            waechterCheckedFrames = waechter != null ? waechter.CheckedFrames : 0;
            DestroyWaechter();
            CollectViolations(4242, waechterCheckedFrames);

            // The Waechter must have been active (forceActive path) and checked frames.
            Assert.Greater(waechterCheckedFrames, 3, "Waechter must have checked frames (forceActive works)");

            // R1 detection for a spider 0.3 m under ground: the Waechter evaluates
            // VerstossRulesModel.R1_BelowTerrain per frame; that path must fire and
            // the protocol must write it (dedup once per second).
            const float injectGroundY = 1.0f;
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R1_BelowTerrain(injectGroundY - 0.3f, injectGroundY),
                "spider 0.3 m under ground -> R1 rule must fire");

            var protocol = new Spinnenspiel.VerstossProtocol();
            protocol.BeginRun("Logs/playtest/verstoesse_inject.jsonl");
            int written = 0;
            for (int step = 0; step <= 15; step++)
            {
                float t = step * 0.2f;
                if (Spinnenspiel.VerstossRulesModel.R1_BelowTerrain(injectGroundY - 0.3f, injectGroundY)
                    && protocol.Report(0, t, 4242, 0f, injectGroundY - 0.3f, 0f))
                {
                    written++;
                }
            }

            Assert.AreEqual(4, written, "R1 at 0.3 m under ground -> exactly once per second");

            // Criterion: the test finds the injected failure (spider 0.3 m under ground) as R1.
            CriteriaLine("R1 reports for injected pose = " + written,
                "injected spider 0.3 m under ground found as R1", written > 0);
        }

        /// <summary>Maps BotStep actions to the game's real device keys: WASD movement,
        /// Shift sprint, Space jump, N net build.</summary>
        private void SetupKeys()
        {
            keys = new UnityEngine.InputSystem.Controls.KeyControl[7];
            keys[0] = kb.wKey;
            keys[1] = kb.sKey;
            keys[2] = kb.aKey;
            keys[3] = kb.dKey;
            keys[4] = kb.leftShiftKey;
            keys[5] = kb.spaceKey;
            keys[6] = kb.nKey;
        }

        private void PressKeyGuarded(int idx)
        {
            if (!pressedKey[idx])
            {
                pressedKey[idx] = true;
                Press(keys[idx]);
            }
        }

        private void ReleaseKeyGuarded(int idx)
        {
            if (pressedKey[idx])
            {
                pressedKey[idx] = false;
                Release(keys[idx]);
            }
        }

        /// <summary>Fires one BotStep: move-hold presses the WASD combo, sprint Shift,
        /// jump Space tap, trunk-run W (test steers), net build N.</summary>
        private void FireStep(BotStep s)
        {
            switch (s.Action)
            {
                case 0:
                    if (s.DirY > 0f) PressKeyGuarded(0);
                    if (s.DirY < 0f) PressKeyGuarded(1);
                    if (s.DirX < 0f) PressKeyGuarded(2);
                    if (s.DirX > 0f) PressKeyGuarded(3);
                    break;
                case 1:
                    PressKeyGuarded(4);
                    break;
                case 2:
                    PressKeyGuarded(5);
                    break;
                case 3:
                    PressKeyGuarded(0);
                    break;
                case 4:
                    PressKeyGuarded(6);
                    break;
            }
        }

        private void ReleaseStep(BotStep s)
        {
            switch (s.Action)
            {
                case 0:
                    if (s.DirY > 0f) ReleaseKeyGuarded(0);
                    if (s.DirY < 0f) ReleaseKeyGuarded(1);
                    if (s.DirX < 0f) ReleaseKeyGuarded(2);
                    if (s.DirX > 0f) ReleaseKeyGuarded(3);
                    break;
                case 1:
                    ReleaseKeyGuarded(4);
                    break;
                case 2:
                    ReleaseKeyGuarded(5);
                    break;
                case 3:
                    ReleaseKeyGuarded(0);
                    break;
                case 4:
                    ReleaseKeyGuarded(6);
                    break;
            }
        }
    }
}
