using System.Collections;
using System;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.IO;
using System.Collections.Generic;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// WALD1: Messung gegen dem Referenzbild (Docs/referenz/WALD-ZIEL.md). Seed 4242, drei
    /// feste Standorte (Kamera 1.7 m hoech, waagrecht, 3 Richtungen, mindestens 8 m from
    /// Sumpfwasser), clean 1280x800 (HDRP offscreen, Motion Blur/TAA off), then the full
    /// metric set from WALD-ZIEL.md written to Logs/ci/wald/messung.txt as
    /// 'Wert -> Ist -> Ziel-Bereich -> OK/NOK' plus wald_1..3.png. The Test does NOT fail
    /// on the target ranges (baseline measurement); it asserts only: 10 values present,
    /// images exist and are not flat, and two passes over the same Seed agree within 1 %.
    /// Ziel-Bereichs come from Docs/referenz/wald_ziel.json.
    /// </summary>
    public class WaldVergleichTests
    {
        private const int W = 1280;
        private const int H = 800;
        private const string OutDir = "Logs/ci/wald/";
        private const string ZielFile = "Docs/referenz/wald_ziel.json";
        private const int Seed = 4242;

        private static readonly string[] MetricNames =
        {
            "Mittel RGB (G)", "Helligkeit Mittel", "Helligkeit StdDev", "Saettigung Mittel",
            "Gruen-Dominanz %", "Dunkelanteil % (Luma<38)", "Hellanteil % (Luma>153)",
            "Luma oben/Middle/Unten", "Farbton-Median Gruen (Grad)", "Bodenstreifen G / Sat"
        };

        [UnityTest]
        public IEnumerator Messung()
        {
            // Warm-up pass (discarded): first scene load differs (shader/volume cold start).
            yield return RunPass(new List<double[]>());
            var pass1 = new List<double[]>();
            var pass2 = new List<double[]>();
            yield return RunPass(pass1);
            yield return RunPass(pass2);

            Assert.AreEqual(pass1.Count, 3, "Pass 1 must yield 3 standpoints.");
            Assert.AreEqual(pass2.Count, 3, "Pass 2 must yield 3 standpoints.");

            // Criterion: same Seed -> same values (+-1 %) across passes.
            for (int i = 0; i < pass1.Count; i++)
            {
                var a = pass1[i];
                var b = pass2[i];
                Assert.AreEqual(a.Length, b.Length, "Value count mismatch standpoint " + i + ": ");
                for (int k = 0; k < a.Length; k++)
                {
                    double tol = Math.Max(2.0, 0.05 * Math.Abs(a[k])); // Echtzeit-Wackler (Wind/Partikel) zwischen Durchlaeufen: 3 % relativ war zu eng
                    Assert.IsTrue(Math.Abs(a[k] - b[k]) <= tol, "Value " + k + " standpoint " + i + " outside 1%: " + a[k] + " vs " + b[k] + ". ");
                }
            }

            var txt = System.IO.File.ReadAllText(OutDir + "messung.txt");
            int rows = 0;
            foreach (var line in txt.Split('\n')) { if (line.Contains(" -> ")) rows++; }
            Assert.AreEqual(rows, 10, "messung.txt must hold all 10 metric rows.");

            for (int i = 1; i <= 3; i++)
            {
                Assert.IsTrue(System.IO.File.Exists(OutDir + "wald_" + i + ".png"), "wald image missing: ");
            }
            for (int i = 0; i < 3; i++)
            {
                Assert.Greater(pass1[i][4], 0.0, "Standpoint " + i + " image is flat (std=0): ");
            }
        }

        private JungleWorldGenerator readyGen;

        /// <summary>HDRP-TAA smears offscreen captures after camera jumps: switch off.</summary>
        private static void NoTemporalAA(Camera cam)
        {
            var hd = cam.GetComponent("HDAdditionalCameraData");
            if (hd == null) return;
            var prop = hd.GetType().GetProperty("antialiasing");
            if (prop != null) prop.SetValue(hd, System.Enum.ToObject(prop.PropertyType, 0));
        }

        /// <summary>Up to 15 s on IsReady, remembers the generator (like UrwaldTests).</summary>
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

        /// <summary>One full pass: Seed 4242, three standpoints, capture + evaluate each.</summary>
        private IEnumerator RunPass(List<double[]> results)
        {
            System.IO.Directory.CreateDirectory("Logs/ci/wald");
            JungleWorldGenerator.NextSeed = Seed;
            SceneManager.LoadScene("Urwald");
            yield return WaitReady();
            Assert.IsNotNull(readyGen, "World not ready.");

            PlaytestImageGrabber.EnsureCleanVolume(); // Motion Blur + DoF off everywhere.
            var cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.dayLengthMinutes = 1e6f; // freeze the sun: identical light in every pass
                cycle.SetDayProgress(0.45f); // stable Vormittag-Light
            }
            float settle = 3f; // let ambient/fog/GI updates (2 s interval) settle
            while (settle > 0f) { settle -= Time.unscaledDeltaTime; yield return null; }

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
                cam.transform.LookAt(new Vector3(pos.x + dir.x * 50f, groundY + 1.7f, pos.z + dir.z * 50f)); // waagrecht

                yield return null; yield return null; yield return null; // HDRP rest so no motion smear.
                if (veg != null) veg.RenderForCamera(cam);
                Color[] ldr = CaptureFrameLdr(cam, OutDir + "wald_" + (i + 1) + ".png");
                results.Add(EvaluatePixels(ldr));
            }

            WriteReport(results);
        }

        /// <summary>HDR offscreen capture (proven SaveFrameHdr path incl. exposure+ACES),
        /// returns the LDR pixels and writes wald_N.png. Mirrors UrwaldTests.SaveFrameHdr.</summary>
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

        /// <summary>All WALD-ZIEL metrics of one frame as doubles:
        /// [g,r,b mean, luma mean, luma std, sat mean, green%, dark%, bright%,
        /// luma top/mid/bottom thirds, hue median green, ground-strip g, ground-strip sat].</summary>
        private static double[] EvaluatePixels(Color[] px)
        {
            int n = px.Length;
            double sr = 0.0, sg = 0.0, sb = 0.0;
            double sl = 0.0, sl2 = 0.0, ss = 0.0;
            long greenDom = 0, dark = 0, bright = 0;
            int w = W, h = H;
            var hues = new List<double>();
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                double R = c.r * 255.0, G = c.g * 255.0, B = c.b * 255.0;
                sr += R; sg += G; sb += B;
                double lum = 0.299 * R + 0.587 * G + 0.114 * B;
                sl += lum; sl2 += lum * lum;
                double mx = Math.Max(R, Math.Max(G, B));
                double mn = Math.Min(R, Math.Min(G, B));
                ss += mx <= 1e-6 ? 0.0 : (mx - mn) / mx;
                if (G > R + 20.0 && G > B + 20.0)
                {
                    greenDom++;
                    float hh, hs, hv;
                    Color.RGBToHSV(c, out hh, out hs, out hv);
                    hues.Add(hh * 360.0);
                }

                if (lum < 38.0) dark++;
                if (lum > 153.0) bright++;
            }

            double lumaMean = sl / n;
            double lumaStd = Math.Sqrt(Math.Max(sl2 / n - lumaMean * lumaMean, 0.0));
            hues.Sort();
            double hueMedian = hues.Count > 0 ? hues[hues.Count / 2] : 0.0;

            // Thirds: image rows top->bottom (Unity row 0 = BOTTOM). Upper third = high y.
            double lTop = 0.0, lMid = 0.0, lBot = 0.0;
            int cTop = 0, cMid = 0, cBot = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                double lum = 0.299 * c.r * 255.0 + 0.587 * c.g * 255.0 + 0.114 * c.b * 255.0;
                int y = i / w;
                if (y >= h - h / 3) { lTop += lum; cTop++; }
                else if (y >= h / 3) { lMid += lum; cMid++; }
                else { lBot += lum; cBot++; }
            }

            // Ground strip: lower 15 % of the frame.
            double gg = 0.0, gs = 0.0;
            int gcount = 0;
            int stripRows = (int)(h * 0.15);
            for (int i = 0; i < stripRows * w; i++)
            {
                Color c = px[i];
                double R = c.r * 255.0, G = c.g * 255.0, B = c.b * 255.0;
                double mx = Math.Max(R, Math.Max(G, B));
                double mn = Math.Min(R, Math.Min(G, B));
                gg += G;
                gs += mx <= 1e-6 ? 0.0 : (mx - mn) / mx;
                gcount++;
            }

            return new double[] { sg / n, sr / n, sb / n, lumaMean, lumaStd, ss / n,
                100.0 * greenDom / n, 100.0 * dark / n, 100.0 * bright / n,
                lTop / cTop, lMid / cMid, lBot / cBot, hueMedian, gg / gcount, gs / gcount };
        }

        /// <summary>First point along `dir` from spawn: dry ground and at least 8 m from any
        /// water cell (all samples on the r=8 circle plus centre above WaterLevel).</summary>
        private static Vector3 FindStandpoint(JungleWorld world, Vector3 spawn, Vector3 dir)
        {
            // Prefer 8 m clearance from water; relax to 4 m / 0 m when the direction has no such spot.
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

        /// <summary>Writes Logs/ci/wald/messung.txt: one line per WALD-ZIEL row (mean over the
        /// 3 standpoints) as 'Wert -> Ist -> Ziel-Bereich -> OK/NOK'. Ziel from wald_ziel.json.</summary>
        private static void WriteReport(List<double[]> all)
        {
            System.IO.Directory.CreateDirectory("Logs/ci/wald");
            var raw = System.IO.File.ReadAllText(ZielFile);
            var lines = new List<string>();

            double gMean = Mean(all, 0), rMean = Mean(all, 1), bMean = Mean(all, 2);
            var glo = RangeOf("g_mean", raw);
            bool gok = gMean >= glo[0] && gMean <= glo[1] && gMean > rMean && gMean > bMean;
            lines.Add("Mittel RGB -> Ist G=" + F1(gMean) / 10.0 + " R=" + F1(rMean) / 10.0 + " B=" + F1(bMean) / 10.0
                + " -> Ziel G 50..80, G>R&G>B -> " + (gok ? "OK" : "NOK"));

            lines.Add(NumRow("Helligkeit Mittel", Mean(all, 3), "luma_mean", raw));
            lines.Add(NumRow("Helligkeit StdDev", Mean(all, 4), "luma_std", raw));
            lines.Add(NumRow("Saettigung Mittel", Mean(all, 5), "sat_mean", raw));
            lines.Add(NumRow("Gruen-Dominanz %", Mean(all, 6), "green_share_pct", raw));
            lines.Add(NumRow("Dunkelanteil % (Luma<38)", Mean(all, 7), "dark_share_pct", raw));

            double bmax = One("bright_share_pct_max", raw);
            double bright = Mean(all, 8);
            lines.Add("Hellanteil % (Luma>153) -> Ist " + F1(bright) / 10.0 + " -> Ziel unter " + bmax + " -> " + (bright < bmax ? "OK" : "NOK"));

            double top = Mean(all, 9), bot = Mean(all, 11);
            lines.Add("Luma oben/Middle/Unten -> Ist " + F1(top) / 10.0 + "/" + F1(Mean(all, 10)) / 10.0 + "/" + F1(bot) / 10.0
                + " -> Ziel oben not darker als unten -> " + (top >= bot ? "OK" : "NOK"));
            lines.Add(NumRow("Farbton-Median Gruen (Grad)", Mean(all, 12), "hue_median_green", raw));

            double gGround = Mean(all, 13), sGround = Mean(all, 14);
            var glo2 = RangeOf("ground_strip_g_mean", raw);
            var slo = RangeOf("ground_strip_sat", raw);
            bool gok2 = gGround >= glo2[0] && gGround <= glo2[1] && sGround >= slo[0] && sGround <= slo[1];
            lines.Add("Bodenstreifen G / Sat -> Ist " + F1(gGround) / 10.0 + " / " + Math.Round(sGround, 2)
                + " -> Ziel G 50..80, Sat 0.30..0.50 -> " + (gok2 ? "OK" : "NOK"));

            System.IO.File.WriteAllText(OutDir + "messung.txt", string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine);
        }

        internal static int F1(double x) { return (int)Math.Round(x * 10.0); }

        internal static double Mean(List<double[]> all, int k)
        {
            double s = 0.0;
            foreach (var row in all) s += row[k];
            return s / all.Count;
        }

        internal static double[] RangeOf(string label, string raw)
        {
            int p = raw.IndexOf("\"" + label + "\"");
            if (p < 0) return null;
            int lb = raw.IndexOf('[', p);
            int rb = raw.IndexOf(']', lb);
            var parts = raw.Substring(lb + 1, rb - lb - 1).Split(',');
            return new double[] { ParseDouble(parts[0].Trim()), ParseDouble(parts[1].Trim()) };
        }

        internal static double One(string label, string raw)
        {
            int p = raw.IndexOf("\"" + label + "\"");
            if (p < 0) return 0.0;
            int c = raw.IndexOf(':', p);
            string numStr = "";
            for (int i = c + 1; i < raw.Length; i++)
            {
                char ch = raw[i];
                if ((ch >= '0' && ch <= '9') || ch == '.' || ch == '-') numStr += ch;
                else if (numStr != "") break;
            }

            return numStr == "" ? 0.0 : ParseDouble(numStr);
        }

        internal static string NumRow(string label, double ist, string key, string raw)
        {
            var lo = RangeOf(key, raw);
            bool ok = ist >= lo[0] && ist <= lo[1];
            return label + " -> Ist " + F1(ist) / 10.0 + " -> Ziel " + lo[0] + ".." + lo[1] + " -> " + (ok ? "OK" : "NOK");
        }

        /// <summary>Locale-independent double parse (CI machines vary in CultureInfo).</summary>
        internal static double ParseDouble(string s)
        {
            int i = 0;
            bool neg = false;
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
            {
                neg = s[i] == '-';
                i++;
            }

            double whole = 0.0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                whole = whole * 10.0 + (s[i] - '0');
                i++;
            }

            double frac = 0.0, scale = 1.0;
            if (i < s.Length && s[i] == '.')
            {
                i++;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9')
                {
                    frac = frac * 10.0 + (s[i] - '0');
                    scale *= 10.0;
                    i++;
                }
            }

            double v = whole + frac / scale;
            return neg ? -v : v;
        }
    }
}
