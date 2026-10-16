using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// K5 Wachstum durch Fressen: nach je 10 gefressenen Insekten waechst die Spinne um
    /// 8 %, bis maximal 150 % der Startgroesse. Kamera und Beinlaengen folgen.
    /// Kriterien as 'Wert -> Kriterium -> OK/NOK':
    ///   - after 10 eaten: scale 1.08 +-0.01
    ///   - after 100 eaten: scale exactly 1.5 (cap)
    ///   - feet stay on ground (+-0.02 m)
    /// Proof-shots: Logs/ci/k5_growth_before.png / k5_growth_after.png.
    /// </summary>
    public class GrowthTests
    {
        [UnityTest]
        public IEnumerator K5_Growth_DurchFressen_StepsAndCap()
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
            Assert.True(ready, "K5: world (Seed 4242) not ready in time.");

            var growth = Object.FindFirstObjectByType<SpiderGrowth>();
            Assert.IsNotNull(growth, "K5: SpiderGrowth missing in Urwald.");
            var spiderRoot = growth.transform;
            var legs = spiderRoot.GetComponentsInChildren<SpiderLeg>();
            Assert.AreEqual(8, legs.Length, "K5: spider must have 8 legs.");

            // A few frames so the legs initialize and snap to ground.
            for (var f = 0; f < 30; f++) yield return null;

            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "K5: no active terrain.");

            var cam = Camera.main;
            Assert.IsNotNull(cam, "K5: no main camera.");

            // Baseline: growth 1.0 at start.
            Assert.True(Near(1f, growth.GrowthScale, 0.0001f), "K5: start scale must be 1.");
            var reachBefore = legs[0].MaxReach;
            Assert.Greater(reachBefore, 0f, "K5: leg MaxReach baseline positive.");

            yield return CaptureProof("Logs/ci/k5_growth_before.png");

            // 10 eaten -> one step -> 1.08.
            for (var i = 0; i < 10; i++) growth.OnInsectEaten(null);
            var after10 = growth.GrowthScale;
            Report("scale after 10", after10);
            Assert.True(Near(1.08f, after10, 0.01f), "K5: after 10 eaten scale must be 1.08 +-0.01.");
            var reachAfter10 = legs[0].MaxReach;
            Report("leg MaxReach ratio after 10", reachAfter10 / reachBefore);
            Assert.True(Near(after10, reachAfter10 / reachBefore, 0.01f), "K5: leg length follows growth.");

            // To the cap: 90 more eaten (total 100) -> capped at 1.5.
            for (var i = 0; i < 90; i++) growth.OnInsectEaten(null);
            var after100 = growth.GrowthScale;
            Report("scale after 100", after100);
            Assert.True(Near(1.5f, after100, 0.0001f), "K5: cap exactly 1.5.");
            var reachAfter100 = legs[0].MaxReach;
            Report("leg MaxReach ratio after 100", reachAfter100 / reachBefore);
            Assert.True(Near(1.5f, reachAfter100 / reachBefore, 0.01f), "K5: leg length at cap.");

            // Feet stay on ground (+-0.02 m).
            for (var f = 0; f < 30; f++) yield return null; // let legs re-snap after growth
            for (var i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                Assert.True(leg.IsGrounded, "K5: leg " + i + " must stay grounded.");
                var foot = leg.FootPosition;
                var groundY = terrain.transform.position.y + terrain.SampleHeight(foot);
                var dy = Mathf.Abs(foot.y - groundY);
                Report("foot " + i + " height diff", dy);
                Assert.LessOrEqual(dy, 0.02f, "K5: foot " + i + " must stay on ground (+-0.02m).");
            }

            // Proof-shots before/after (visible change -> image required).
            yield return CaptureProof("Logs/ci/k5_growth_after.png");

            Debug.Log("[K5] proof shots written: Logs/ci/k5_growth_before.png and k5_growth_after.png");
        }

        private static void Report(string label, float value)
        {
            Debug.Log("K5 -> " + label + ": " + value.ToString("0.####") + " -> checked by asserts above");
        }

        /// <summary>Float comparison with tolerance (Mathf has no IsNearlyEqual).</summary>
        private static bool Near(float a, float b, float tol)
        {
            return Mathf.Abs(a - b) <= tol;
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

        /// <summary>Reads the active RenderTexture back and writes PNG to file.</summary>
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
