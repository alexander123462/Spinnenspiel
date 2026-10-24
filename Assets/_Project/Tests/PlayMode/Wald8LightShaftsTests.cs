using System.Collections;
using System;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Collections.Generic;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// WALD8c: Volumetrischer Nebel / Lichtbahnen. The volumetric Fog volume is switched ON over
    /// the WaldLook parameter (WorldLightingBuilder reads it), with anisotropy toward the sun so
    /// light shafts appear in the haze. Criterion: CPU median frame time with volumetric fog ON
    /// must stay within 1.15 x the SAME scene with it OFF (measured in ONE run, self-relative).
    /// Captures three forest standpoints with fog ON into Logs/ci/wald8c/wald8c_*.png so Claude
    /// judges visible light shafts there (Claude-Urteil). T7 and J5 must stay green (checked in CI).
    /// </summary>
    public class Wald8LightShaftsTests
    {
        private const int W = 1280;
        private const int H = 800;
        private const int Seed = 4242;

        private static float medOn = 0f;  // volumetric fog ON median frame time (ms)
        private static float medOff = 0f; // volumetric fog OFF reference median frame time (ms)
        private static float[] ftBuf = new float[32]; // frametime ring buffer
        private static int ftN; // frametime sample count

        private JungleWorldGenerator readyGen;

        [UnityTest]
        public IEnumerator WALD8c_VolumetricFogBudgetAndLightShafts()
        {
            JungleWorldGenerator.NextSeed = Seed;
            SceneManager.LoadScene("Urwald");
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "World not ready.");

            var cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.dayLengthMinutes = 1e6f; // freeze the sun: identical light in both frametime passes
                cycle.SetDayProgress(0.45f);
            }

            float settle = 3f;
            while (settle > 0f) { settle -= Time.unscaledDeltaTime; yield return null; }

            // --- Median frametime WITH volumetric fog (scene config from WaldLook) ---
            medOn = 0f; ftN = 0;
            for (int fi = 0; fi < 32; fi++)
            {
                yield return null;
                if (fi >= 6) { ftBuf[ftN % ftBuf.Length] = Time.deltaTime * 1000f; ftN++; }
            }
            medOn = MedianOf(ftBuf, Math.Min(ftN, ftBuf.Length));

            if (cycle != null) cycle.SetVolumetricFogForTests(false); // reference config: volumetric off
            medOff = 0f; ftN = 0;
            for (int fi = 0; fi < 32; fi++)
            {
                yield return null;
                if (fi >= 6) { ftBuf[ftN % ftBuf.Length] = Time.deltaTime * 1000f; ftN++; }
            }
            medOff = MedianOf(ftBuf, Math.Min(ftN, ftBuf.Length));
            if (cycle != null) cycle.SetVolumetricFogForTests(true); // captures show the WALD8c config

            Assert.Greater(medOn, 0f, "No frametimes captured (fog ON).");
            Assert.Greater(medOff, 0f, "No frametimes captured (fog OFF).");
            double ftRatio = medOn / medOff;
            Debug.Log("[WALD8c] Frametime Median: Ist " + Math.Round(medOn, 2) + " ms | Baseline " + Math.Round(medOff, 2) + " ms -> Ratio " + Math.Round(ftRatio, 2) + " -> Ziel <= 1.15 -> " + (ftRatio <= 1.15 ? "OK" : "NOK"));
            Assert.LessOrEqual(ftRatio, 1.15, "WALD8c volumetric fog costs more than 15% over the fog-off config: ");

            // --- Proof images WITH volumetric fog ON: three forest standpoints (same framing as WALD8) ---
            System.IO.Directory.CreateDirectory("Logs/ci/wald8c");
            Camera cam = UnityEngine.Camera.main;
            Assert.IsNotNull(cam, "No camera.");
            var spiderCam = cam.GetComponent<SpiderCamera>();
            if (spiderCam != null) spiderCam.enabled = false;
            NoTemporalAA(cam);
            var veg = UnityEngine.Object.FindFirstObjectByType<VegetationSystem>();
            var world = readyGen.World;
            Vector3 spawn = world.FindSpawnPoint();
            var angles = new float[] { 0f, 120f, 240f };
            for (int i = 0; i < 3; i++)
            {
                float deg = angles[i];
                Vector3 dir = new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad));
                Vector3 pos = FindStandpoint(world, spawn, dir);
                Assert.IsTrue(pos.x > -1e9f, "No valid standpoint for direction " + i + ".");
                float groundY = world.Height(pos.x, pos.z);
                cam.transform.position = new Vector3(pos.x, groundY + 1.7f, pos.z);
                cam.transform.LookAt(new Vector3(pos.x + dir.x * 50f, groundY + 1.7f, pos.z + dir.z * 50f));
                yield return null; yield return null; yield return null; // HDRP rest so no motion smear
                if (veg != null) veg.RenderForCamera(cam);
                CaptureFrameLdr(cam, "Logs/ci/wald8c/wald8c_" + (i + 1) + ".png");
            }

            var lines = new List<string>();
            lines.Add("Frametime Median Nebel ON/OFF -> Ist " + Math.Round(ftRatio, 2) + " -> Ziel <= 1.15 -> " + (ftRatio <= 1.15 ? "OK" : "NOK"));
            lines.Add("ON " + Math.Round(medOn, 2) + " ms | OFF " + Math.Round(medOff, 2) + " ms");
            lines.Add("Proof images -> Logs/ci/wald8c/wald8c_1..3.png (volumetric fog ON) -> Claude-Urteil Lichtbahnen");
            System.IO.File.WriteAllText("Logs/ci/wald8c/messung.txt", string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine);
        }

        /// <summary>Up to 15 s on IsReady, remembers the generator (like WaldVergleichTests).</summary>
        private IEnumerator WaitReady()
        {
            readyGen = null;
            float timeout = 15f;
            while (timeout > 0f)
            {
                var g = UnityEngine.Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady)
                {
                    readyGen = g;
                    break;
                }

                timeout -= UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>First point along `dir` from spawn: dry ground, at least 8 m from water
        /// (same rule as WaldVergleichTests so both tests measure the same standpoints).</summary>
        private static Vector3 FindStandpoint(JungleWorld world, Vector3 spawn, Vector3 dir)
        {
            foreach (float clear in new[] { 8f, 4f, 0f })
            {
                for (float d = 6f; d < world.Size; d += 2f)
                {
                    float x = spawn.x + dir.x * d;
                    float z = spawn.z + dir.z * d;
                    if (x < 2f || z < 2f || x > world.Size - 2f || z > world.Size - 2f) break;
                    if (world.Height(x, z) < JungleWorld.WaterLevel + 0.3f) continue;
                    bool ok = true;
                    for (int a = 0; a < 16 && ok && clear > 0f; a++)
                    {
                        float ang = a * (360f / 16f) * Mathf.Deg2Rad;
                        if (world.Height(x + Mathf.Cos(ang) * clear, z + Mathf.Sin(ang) * clear) < JungleWorld.WaterLevel) ok = false;
                    }

                    if (ok) return new Vector3(x, 0f, z);
                }
            }

            return new Vector3(-1e9f, 0f, 0f);
        }

        /// <summary>HDRP-TAA smears offscreen captures after camera jumps: switch off.</summary>
        private static void NoTemporalAA(Camera cam)
        {
            var hd = cam.GetComponent("HDAdditionalCameraData");
            if (hd == null) return;
            var prop = hd.GetType().GetProperty("antialiasing");
            if (prop != null) prop.SetValue(hd, System.Enum.ToObject(prop.PropertyType, 0));
        }

        /// <summary>HDR offscreen capture incl. exposure + ACES (proven SaveFrameHdr path),
        /// returns the LDR pixels and writes wald8c_N.png.</summary>
        private Color[] CaptureFrameLdr(Camera cam, string file)
        {
            var rt = new RenderTexture(W, H, 0, RenderTextureFormat.ARGBHalf);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var hdr = new Texture2D(W, H, TextureFormat.RGBAHalf, false);
            hdr.ReadPixels(new Rect(0, 0, W, H), 0, 0);
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

            var ldr = new Texture2D(W, H, TextureFormat.RGB24, false);
            ldr.SetPixels(outPx);
            ldr.Apply(false);
            byte[] png = ldr.EncodeToPNG();
            System.IO.File.WriteAllBytes(file, png);
            UnityEngine.Object.DestroyImmediate(ldr);
            UnityEngine.Object.DestroyImmediate(hdr);
            UnityEngine.Object.DestroyImmediate(rt);
            return outPx;
        }

        internal static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
        }

        /// <summary>Median of the first `count` entries in buf (count <= buf.Length).</summary>
        private static float MedianOf(float[] buf, int count)
        {
            if (count <= 0) return 0f;
            if (count > buf.Length) count = buf.Length;
            var sorted = new float[count];
            for (int i = 0; i < count; i++) sorted[i] = buf[i];
            for (int i = 1; i < count; i++) // insertion sort, small n
            {
                float v = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j] > v) { sorted[j + 1] = sorted[j]; j--; }
                sorted[j + 1] = v;
            }
            return count % 2 == 1 ? sorted[count / 2] : 0.5f * (sorted[count / 2 - 1] + sorted[count / 2]);
        }
    }
}
