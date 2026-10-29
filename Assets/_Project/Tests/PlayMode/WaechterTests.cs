using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>P1 Waechter: rules model (pure) + activation + protocol dedup window.</summary>
    public class WaechterTests
    {
        // ---- R1: violation fires, normal case no false alarm ----
        [Test]
        public void Waechter_R1_FiresWhenUnderTerrainAndNotInNormalCase()
        {
            bool hit = Spinnenspiel.VerstossRulesModel.R1_BelowTerrain(0.8f, 1.0f);
            Assert.IsTrue(hit, "0.2 m under terrain -> R1");

            bool ok = Spinnenspiel.VerstossRulesModel.R1_BelowTerrain(1.04f, 1.0f);
            Assert.IsFalse(ok, "on terrain (within tolerance) -> no R1");
        }

        // ---- R2: bad slope fires, climb state suppresses, normal slope no alarm ----
        [Test]
        public void Waechter_R2_FiresOver100DegUnlessClimbState()
        {
            float deg130 = 130f * Mathf.Rad2Deg;
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R2_BadSlope(Mathf.Cos(deg130), false), "130 deg -> R2");
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R2_BadSlope(Mathf.Cos(deg130), true), "climb state suppresses");
            float deg40 = 40f * Mathf.Rad2Deg;
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R2_BadSlope(Mathf.Cos(deg40), false), "40 deg -> no R2");
        }

        // ---- R3: input active 5 s, moved < 0.05 m fires; walking normally no alarm ----
        [Test]
        public void Waechter_R3_FiresOnStuckInputAndNotWhenWalking()
        {
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R3_NoProgress(1f, 0.02f, 5f), "5 s input, 2 cm -> R3");
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R3_NoProgress(1f, 0.02f, 4.9f), "below 5 s -> no R3");
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R3_NoProgress(1f, 0.5f, 6f), "walking -> no R3");
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R3_NoProgress(0f, 0f, 9f), "no input -> no R3");
        }

        // ---- R4: NaN/inf position or fall speed > 30 m/s fires ----
        [Test]
        public void Waechter_R4_FiresOnNaNAndHardFall()
        {
            float nan = float.NaN;
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R4_PositionOrSpeedInvalid(nan, 1f, 1f, 0f), "NaN x -> R4");
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R4_PositionOrSpeedInvalid(1f, 1f, float.PositiveInfinity, 0f), "inf z -> R4");
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R4_PositionOrSpeedInvalid(1f, 1f, 1f, 35f), "fall 35 m/s -> R4");
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R4_PositionOrSpeedInvalid(1f, 2f, 3f, 5f), "normal -> no R4");
        }

        // ---- R5: frame time spike over 100 ms fires ----
        [Test]
        public void Waechter_R5_FiresOnFrameSpike()
        {
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R5_FrameSpike(120f), "120 ms -> R5");
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R5_FrameSpike(16f), "16 ms -> no R5");
        }

        // ---- R6: inside-tree flag passes through ----
        [Test]
        public void Waechter_R6_PassesColliderQueryThrough()
        {
            Assert.IsTrue(Spinnenspiel.VerstossRulesModel.R6_InsideTreeCollider(true), "inside -> R6");
            Assert.IsFalse(Spinnenspiel.VerstossRulesModel.R6_InsideTreeCollider(false), "outside -> no R6");
        }

        // ---- Protocol: at most one entry per rule and second (dedup window) ----
        [Test]
        public void WaechterProtocol_DedupesToOnePerRuleAndSecond()
        {
            var protocol = new Spinnenspiel.VerstossProtocol();
            protocol.BeginRun("Logs/playtest/verstoesse_test_dedup.jsonl");

            // Spider deliberately 0.3 m under ground: R1 must log exactly once per second.
            int written = 0;
            for (int step = 0; step <= 15; step++)
            {
                float t = step * 0.2f;
                if (Spinnenspiel.VerstossRulesModel.R1_BelowTerrain(0.7f, 1.0f))
                {
                    if (protocol.Report(0, t, 4242, 0f, 0.7f, 0f))
                    {
                        written++;
                    }
                }
            }

            // t = 0, 1, 2, 3 -> four lines over 3 s; the rest suppressed by the window.
            Assert.AreEqual(4, written, "R1 at 0.3 m under ground -> exactly once per second");
        }

        // ---- Activation gate: without forceActive and without -playtest the component
        // stays inactive and costs 0 frames (CheckedFrames stays 0). ----
        [UnityTest]
        public IEnumerator Waechter_InactiveWithoutPlaytestCostsZeroFrames()
        {
            var go = new GameObject("waechter_off");
            var waechter = go.AddComponent<Spinnenspiel.SpielWaechter>();

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.IsFalse(waechter.IsActive, "no -playtest and no forceActive -> stays off");
            Assert.AreEqual(0, waechter.CheckedFrames, "inactive component -> 0 checked frames");

            GameObject.Destroy(go);
        }

        // ---- forceActive: the test can switch the waechter on; it then checks every
        // frame (CheckedFrames grows). ----
        [UnityTest]
        public IEnumerator Waechter_ForceActiveChecksEveryFrame()
        {
            var go = new GameObject("waechter_on");
            var waechter = go.AddComponent<Spinnenspiel.SpielWaechter>();
            waechter.forceActive = true;

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.IsTrue(waechter.IsActive, "forceActive -> active");
            Assert.IsTrue(waechter.CheckedFrames > 6, "active -> checks every frame after warmup");

            GameObject.Destroy(go);
        }
    }
}

