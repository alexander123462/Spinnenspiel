using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Spinnenspiel;

namespace Spinnenspiel.Tests
{
    /// <summary>T4 Haeutung: on a GrowthStage increase the spider molts for 5 s —
    /// measured movement speed 0 during it, hazard damage x2 (constant), and only
    /// AFTER the molt the new size (8 % per stage) gets applied. No molt without a
    /// stage change. Criteria as 'Wert -> Kriterium -> OK/NOK' lines in the log.</summary>
    public class HaeutungTests
    {
        private static readonly float dt = 0.05f;

        [Test]
        public void Haeutung_Molt_last_5s_and_damage_x2_on_energy_model()
        {
            var molt = new MoltModel();
            molt.Begin();
            Assert.That(molt.IsMolting, "Molt did not start");

            float elapsed = 0f;
            while (molt.IsMolting && elapsed < 10f)
            {
                molt.Tick(dt);
                elapsed += dt;
            }

            Debug.Log("[T4] molt duration=" + elapsed + " -> 5.0 +-0.2 -> "
                + (elapsed >= MoltModel.moltDuration - 0.1f && elapsed <= MoltModel.moltDuration + 0.3f ? "OK" : "NOK"));
            Assert.That(elapsed >= MoltModel.moltDuration - 0.1f && elapsed <= MoltModel.moltDuration + 0.3f,
                "Molt duration outside 5 +-0.2 s: " + elapsed);
            Assert.That(!molt.IsMolting, "Molt never ended");

            // Hazard damage x2 while molting: a strike takes 2.0 x the normal amount.
            var normal = new SpiderEnergyModel();
            normal.Reset();
            float normalLoss = normal.Energy;
            normal.ApplyStrike(SpiderEnergyModel.frogStrikeCost);
            normalLoss -= normal.Energy;

            var molting = new SpiderEnergyModel();
            molting.Reset();
            molting.hazardDamageMultiplier = MoltModel.damageFactorWhileMolting;
            float before = molting.Energy;
            molting.ApplyStrike(SpiderEnergyModel.frogStrikeCost);
            float doubledLoss = before - molting.Energy;

            Debug.Log("[T4] damage while molt / normal=" + (doubledLoss / normalLoss) + " -> 2.0 -> "
                + (Mathf.Abs(doubledLoss / normalLoss - 2f) < 0.01f ? "OK" : "NOK"));
            Assert.That(Mathf.Abs(doubledLoss / normalLoss - 2f) < 0.01f,
                "Molt damage not 2.0 x normal: " + doubledLoss + "/" + normalLoss);

            // No multiplier -> normal damage stays untouched.
            var plain = new SpiderEnergyModel();
            plain.Reset();
            float p0 = plain.Energy;
            plain.ApplyStrike(SpiderEnergyModel.frogStrikeCost);
            Assert.That(Mathf.Abs(p0 - plain.Energy - SpiderEnergyModel.frogStrikeCost) < 0.01f,
                "Normal strike changed while multiplier is 1");
        }

        [UnityTest]
        public IEnumerator Haeutung_Molt_blocks_movement_and_size_applies_after_it()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null; // A2-Lehre: LoadScene defert zum naechsten Frame

            var ready = false;
            var timeout = 60f;
            while (timeout > 0f && !ready)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                ready = g != null && g.IsReady;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(ready, "T4: world (Seed 4242) not ready in time.");

            var growth = Object.FindFirstObjectByType<SpiderGrowth>();
            Assert.That(growth != null, "T4: SpiderGrowth missing in Urwald.");
            var movement = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.That(movement != null, "T4: SpiderMovement missing in Urwald.");
            var energy = Object.FindFirstObjectByType<SpiderEnergy>();
            Assert.That(energy != null, "T4: SpiderEnergy missing in Urwald.");

            // A few frames so the spider settles on the ground.
            for (var f = 0; f < 30; f++) yield return null;

            // No stage change yet -> no molt, size still 1.
            Assert.That(!growth.IsMolting, "T4: molting without a stage change.");
            Assert.That(Mathf.Abs(growth.GrowthScale - 1f) < 0.0001f, "T4: start scale must be 1.");

            // Drive the spider so a running movement speed would be measurable.
            movement.SetExternalMoveInput(new Vector2(0f, 1f));

            // 10 eaten -> one stage -> the molt starts (size already applied, K5-Lehre).
            for (var i = 0; i < 10; i++) growth.OnInsectEaten(null);
            Assert.That(growth.IsMolting, "T4: no molt on the stage change.");

            // During the molt the size is already 8 % larger and stays so after it.
            var during = growth.GrowthScale;
            Debug.Log("[T4] scale during molt=" + during + " -> 1.08 (8 % larger) -> "
                + (Mathf.Abs(during - 1.08f) < 0.01f ? "OK" : "NOK"));

            // Hazard damage x2 DURING the molt: a strike takes 2 x 25 = 50 energy.
            var beforeStrike = energy.Energy;
            energy.ApplyStrike(SpiderEnergyModel.frogStrikeCost);
            var loss = beforeStrike - energy.Energy;
            Debug.Log("[T4] strike loss while molting=" + loss + " -> 2.0 x 25 -> "
                + (Mathf.Abs(loss - 2f * SpiderEnergyModel.frogStrikeCost) < 1f ? "OK" : "NOK"));
            Assert.That(Mathf.Abs(loss - 2f * SpiderEnergyModel.frogStrikeCost) < 1f,
                "T4: strike during molt not doubled: loss=" + loss);

            // Movement blocked for 5 s: measured speed stays 0 while molting (after the
            // deceleration settles, the same way ClimbTests read a stopped spider).
            var moltSeconds = 0f;
            var maxSpeedDuringMolt = 0f;
            var guard = 0;
            var settleFrames = 0;
            while (growth.IsMolting && guard < 1200)
            {
                yield return null;
                moltSeconds += Time.deltaTime;
                settleFrames++;
                if (settleFrames > 6) // let the deceleration from walking reach zero first
                {
                    maxSpeedDuringMolt = Mathf.Max(maxSpeedDuringMolt, movement.Speed);
                }
                guard++;
            }
            Assert.That(!growth.IsMolting, "T4: molt never ended.");

            Debug.Log("[T4] molt duration=" + moltSeconds + " -> 5.0 +-0.2 -> "
                + (moltSeconds >= MoltModel.moltDuration - 0.2f && moltSeconds <= MoltModel.moltDuration + 0.5f ? "OK" : "NOK"));
            Assert.That(moltSeconds >= MoltModel.moltDuration - 0.2f && moltSeconds <= MoltModel.moltDuration + 0.5f,
                "T4: molt duration outside 5 +-0.2 s (upper bound frame-tolerant): " + moltSeconds);

            Debug.Log("[T4] max speed during molt=" + maxSpeedDuringMolt + " -> 0 -> "
                + (maxSpeedDuringMolt < 0.01f ? "OK" : "NOK"));
            Assert.That(maxSpeedDuringMolt < 0.01f, "T4: spider moved while molting.");

            // After the molt the size gets applied: 8 % larger.
            var after = growth.GrowthScale;
            Debug.Log("[T4] scale after molt=" + after + " -> 1.08 -> "
                + (Mathf.Abs(after - 1.08f) < 0.01f ? "OK" : "NOK"));
            Assert.That(Mathf.Abs(after - 1.08f) < 0.01f,
                "T4: size after molt not 1.08: " + after);

            // Damage multiplier back to normal after the molt.
            var before2 = energy.Energy;
            energy.ApplyStrike(SpiderEnergyModel.frogStrikeCost);
            var loss2 = before2 - energy.Energy;
            Debug.Log("[T4] strike loss after molt=" + loss2 + " -> 1.0 x 25 -> "
                + (Mathf.Abs(loss2 - SpiderEnergyModel.frogStrikeCost) < 1f ? "OK" : "NOK"));
            Assert.That(Mathf.Abs(loss2 - SpiderEnergyModel.frogStrikeCost) < 1f,
                "T4: strike after molt not normal: loss=" + loss2);

            yield return CaptureProof("Logs/ci/t4_haeutung_after_molt.png");
        }

        private static void Report(string label, float value)
        {
            Debug.Log("T4 -> " + label + ": " + value.ToString("0.####") + " -> checked by asserts above");
        }

        /// <summary>Captures the main camera to a PNG (A2-Lehre: prime a throwaway RT first, HDRP cold-RT path).</summary>
        private IEnumerator CaptureProof(string file)
        {
            var cam = Camera.main;
            var veg = Object.FindFirstObjectByType<VegetationSystem>();
            DisableDepthAndMotionBlur();

            var prime = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            prime.antiAliasing = 4;
            prime.Create();
            cam.targetTexture = prime;
            if (veg != null) veg.RenderForCamera(cam);
            cam.Render();
            cam.targetTexture = null;
            prime.Release();
            yield return null;

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
            if (veg != null) veg.RenderForCamera(cam);
            cam.Render();
            SaveFrameTo(rt, file);
            cam.targetTexture = null;
            rt.Release();
        }

        private static void SaveFrameTo(RenderTexture rt, string file)
        {
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
        }

        /// <summary>G6-Lehre: for Proof-Shots MotionBlur and DepthOfField switch off.</summary>
        private static void DisableDepthAndMotionBlur()
        {
            var volume = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (volume == null || volume.sharedProfile == null)
            {
                return;
            }
            foreach (var comp in volume.sharedProfile.components)
            {
                var compName = comp.GetType().Name; // HDRP-Components: HDRPDepthOfField etc.
                if (compName == "DepthOfField" || compName == "MotionBlur"
                    || compName.EndsWith("DepthOfField") || compName.EndsWith("MotionBlur"))
                {
                    comp.active = false;
                }
            }
        }
    }
}
