using NUnit.Framework;
using Spinnenspiel;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>T7 Morgennebel: the fog density follows the time of day. FogFactor(dayProgress)
    /// is a pure curve (morning plateau 3.0x noon to mid-morning, evening 1.4x, night 1.0x),
    /// applied by the DayNightCycle onto the HDRP Fog volume and RenderSettings.fogDensity.
    /// Proof image compares dense morning vs thin noon at EQUAL sunlight.</summary>
    public class MorgennebelTests
    {
        /// <summary>T7: The fog curve as a pure function — morning 2x noon, evening 1.4x,
        /// continuous (no abrupt jumps).</summary>
        [Test]
        public void T7_FogFactor_Kurve()
        {
            float noon = DayNightCycle.FogFactor(0.5f);
            Assert.Greater(noon, 0f, "Fog factor must be positive.");

            // Morning (0.1) sits on the ramp at ~2.33x noon; the plateau 3.0x holds to 0.3
            // (LICHT1c2: ramped rise 0..0.15 keeps the curve smooth for the jump guard).
            float morning = DayNightCycle.FogFactor(0.1f);
            float ratioMorning = morning / noon;
            Assert.GreaterOrEqual(ratioMorning, 2.25f, "Morning ramp not ~2.33x noon (" + ratioMorning + ").");
            Assert.LessOrEqual(ratioMorning, 2.42f, "Morning ramp too dense vs noon (" + ratioMorning + ").");

            // Evening (0.75) is 1.4x noon.
            float evening = DayNightCycle.FogFactor(0.75f);
            float ratioEvening = evening / noon;
            Assert.GreaterOrEqual(ratioEvening, 1.3f, "Evening not ~1.4x noon (" + ratioEvening + ").");
            Assert.LessOrEqual(ratioEvening, 1.5f, "Evening too dense vs noon (" + ratioEvening + ").");

            // LICHT1c2: the morning plateau reaches to 0.3 (five o'clock, just after sunrise
            // at 0.25) — the old curve was already down to 1.33 there, so the equal-sunlight
            // proof (0.3 vs 0.7) had no measurable fog difference (StDev delta 0.90 < 1.5).
            Assert.GreaterOrEqual(DayNightCycle.FogFactor(0.3f) / noon, 2.9f,
                "Morning fog must still be ~3x noon at dayProgress 0.3 (plateau to mid-morning).");

            // Night (0.9) equals noon (1.0x).
            float night = DayNightCycle.FogFactor(0.9f);
            Assert.GreaterOrEqual(night / noon, 0.9f, "Night brighter than noon (night must be 1.0x).");
            Assert.LessOrEqual(night / noon, 1.1f, "Night denser than noon.");

            // Continuity: adjacent samples on a fine grid differ by < 0.05 (no jumps).
            const float step = 0.002f;
            float prev = DayNightCycle.FogFactor(0f);
            for (float t = step; t <= 1f; t += step)
            {
                float cur = DayNightCycle.FogFactor(t);
                Assert.Less(Mathf.Abs(cur - prev), 0.05f,
                    "Fog curve jumps at dayProgress " + t.ToString("F3") + " (jump " + Mathf.Abs(cur - prev) + ").");
                prev = cur;
            }
        }

        /// <summary>T7: fixed-exposure raw-capture -> (mean luma 0..255 after ACES, stddev).
        /// Mirrors the G6-Lehre: auto-exposure is not reproducible, so a fixed factor keeps
        /// the morning vs noon Mittel comparable across runs.</summary>
        private static Vector3 CaptureMeanAndStdDev(Camera cam, int w, int h, string file)
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
            const float exposure = 1f; // RT already carries auto-exposure (AutomaticHistogram); at EQUAL sunlight the hazed frame reads brighter + flatter
            Color[] outPx = new Color[n];
            double sum = 0.0;
            double sumSq = 0.0;
            long foggy = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                // Fog blend-towards-albedo metric on RAW values: HDRP scatters distant
                // pixels towards the fog albedo (0.75, 0.82, 0.75, set by WorldLightingBuilder).
                // Denser fog (shorter meanFreePath) matches more of the frame.
                float d = Mathf.Abs(c.r - 0.75f) + Mathf.Abs(c.g - 0.82f) + Mathf.Abs(c.b - 0.75f);
                if (d < 0.15f) foggy++;
                float r = AcesTone(Mathf.Max(c.r * exposure, 0f));
                float g = AcesTone(Mathf.Max(c.g * exposure, 0f));
                float b = AcesTone(Mathf.Max(c.b * exposure, 0f));
                outPx[i] = new Color(r, g, b, 1f);
                float luma = (0.299f * r + 0.587f * g + 0.114f * b) * 255f;
                sum += luma;
                sumSq += (double)luma * luma;
            }
            float mean = (float)(sum / n);
            float var = (float)(sumSq / n - (double)mean * mean);
            float stddev = Mathf.Sqrt(Mathf.Max(var, 0f));
            if (file != null && file.Length > 0)
            {
                var ldr = new Texture2D(w, h, TextureFormat.RGB24, false);
                ldr.SetPixels(outPx);
                ldr.Apply(false);
                System.IO.File.WriteAllBytes(file, ldr.EncodeToPNG());
                Object.DestroyImmediate(ldr);
            }
            Object.DestroyImmediate(hdr);
            Object.DestroyImmediate(rt);
            return new Vector3(mean, stddev, (float)foggy / (float)n); // z = fraction of fog-albedo pixels
        }

        private static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
        }

        /// <summary>T7: The fog curve applied in the real Urwald scene. Two checks:
        /// (a) the Fog volume's meanFreePath actually follows FogFactor (denser = shorter path),
        /// and (b) a proof image pair at EQUAL sunlight (noon 0.5 vs evening 0.75, both dayFactor
        /// 1.0) so only the fog differs: denser evening fog must make the frame hazier
        /// (brighter Mittel, lower StDev). The morning-vs-noon pair is captured for the report;
        /// note that dayProgress 0.1 sits in the night core of the light cycle (sunrise = 0.25),
        /// so its brightness must not be compared across differing sunlight.</summary>
        [UnityTest]
        public IEnumerator T7_Morgennebel_FollowsDayProgress()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;

            DayNightCycle cycle = null;
            float timeout = 15f;
            while (timeout > 0f && cycle == null)
            {
                cycle = Object.FindFirstObjectByType<DayNightCycle>();
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(cycle, "No DayNightCycle in Urwald.");

            // World must be ready before capture (G6-Lehre: otherwise black frames).
            timeout = 25f;
            JungleWorldGenerator worldGen = null;
            while (timeout > 0f && worldGen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) worldGen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(worldGen, "Urwald world never ready.");

            Camera cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "No Camera in Urwald.");

            // LICHT1c2: T7 misst bewusst MIT Auto-Exposure (beide Frames bei gleichem Sonnenstand
            // werden auf Mittelgrau normalisiert — der Nebelunterschied bleibt als StDev-Signal
            // erhalten). Der Modus ist ein geteilter Profil-Wert: J5 (alphabetisch vorher) hinterlaesst
            // Fixed EV — mit dayLux 3000 kaemen dann schwarze Frames (Mittel 0.0, r104-f3). Hier
            // explicit zurueck auf Automatik.
            Assert.IsTrue(cycle.SetAutoExposureForTests(), "Exposure volume not wired to the DayNightCycle.");

            // Warmup so the HDRP volume/atmosphere pipeline is active before capture
            // (G6-Lehre: J5 runs many frames; instant jumps after LoadScene render blank frames).
            for (int warm = 0; warm < 30; warm++) yield return null;

            // T7: lower sun lux so the capture has exposure headroom (the Urwald noon at the
            // default 100000 lux blows the frame to white — same root as J5's exposure wobble).
            // dayFactor stays 1.0 at BOTH 0.5 and 0.75, so noon vs evening differ ONLY in fog.
            cycle.dayLux = 3000f;

            // LICHT1c2 (Bildbeweis-Redesign): die Follow-Kamera rahmt Nahfeld — bei gleichem
            // Sonnenstand (0.7 vs 0.3) maessen dort selbst 3x Nebel nur StDev-Delta 0.13
            // (r104-f5: noon 21.1 vs dense 21.0). Klassischer exp2-Nebel wirkt ab ~60 m: der
            // Beweis braucht wie J5 eine feste Pose am Weltmittelpunkt, etwas erhoeht und leicht
            // nach unten (Nebelband in der Mittel-Distanz, nicht der einheitliche Fern-Schleier).
            var followCamT7 = cam.GetComponent<SpiderCamera>();
            if (followCamT7 != null) followCamT7.enabled = false;
            float halfT7 = worldGen.World.Size / 2f;
            UnityEngine.Terrain terT7 = UnityEngine.Terrain.activeTerrain;
            Assert.IsNotNull(terT7, "No active terrain in Urwald (T7 pose pin).");
            Vector3 midT7 = new Vector3(halfT7, 0f, halfT7);
            float groundY7 = terT7.SampleHeight(midT7) + terT7.transform.position.y;
            cam.transform.position = midT7 + new Vector3(0f, groundY7 + 8f, 0f);
            cam.transform.rotation = Quaternion.Euler(-8f, 45f, 0f);

            // Noon (baseline fog, dayFactor 1.0): let HDRP settle the volume, then capture the live frame.
            cycle.SetDayProgress(0.7f);
            for (int warm = 0; warm < 3; warm++) yield return null;
            float pathAtNoon = cycle.CurrentFogMeanFreePath();
            Vector3 noon = CaptureMeanAndStdDev(cam, 480, 270, "Logs/ci/t7-noon.png");

            // Morning (denser): meanFreePath must be baseline / FogFactor(0.1) = /3.0.
            cycle.SetDayProgress(0.1f);
            for (int warm = 0; warm < 3; warm++) yield return null;
            float pathAtMorning = cycle.CurrentFogMeanFreePath();
            Vector3 morgen = CaptureMeanAndStdDev(cam, 480, 270, "Logs/ci/t7-morgen.png");

            // Equal-sunlight fog proof: dayFactor(0.3) == dayFactor(0.7) (symmetric light curve),
            // but FogFactor(0.3)=2.0 vs FogFactor(0.7)=1.0 -> only the FOG differs (LICHT1c2).
            cycle.SetDayProgress(0.3f);
            for (int warm = 0; warm < 3; warm++) yield return null;
            float pathAtDense = cycle.CurrentFogMeanFreePath();
            Vector3 nebeldense = CaptureMeanAndStdDev(cam, 480, 270, "Logs/ci/t7-nebel-compare.png");

            Debug.Log("[T7-pfad] noon " + pathAtNoon.ToString("F2") + " | morgen " + pathAtMorning.ToString("F2")
                + " (ratio " + (pathAtNoon / pathAtMorning).ToString("F2") + ") | dense(0.3) " + pathAtDense.ToString("F2")
                + " (ratio " + (pathAtNoon / pathAtDense).ToString("F2") + ")");
            Debug.Log("[T7-noon] Mittel " + noon.x.ToString("F1") + " StDev " + noon.y.ToString("F1"));
            Debug.Log("[T7-morgen] Mittel " + morgen.x.ToString("F1") + " StDev " + morgen.y.ToString("F1"));
            Debug.Log("[T7-dense] Mittel " + nebeldense.x.ToString("F1") + " StDev " + nebeldense.y.ToString("F1"));

            // (a) The volume really follows the curve: morning path = noon path / 2.33 (ramp).
            Assert.Greater(pathAtNoon, 0f, "Fog meanFreePath not set in the scene.");
            float ratioMorning = pathAtNoon / pathAtMorning;
            Assert.GreaterOrEqual(ratioMorning, 2.25f, "Morning fog not ~2.3x denser (" + ratioMorning + ").");
            Assert.LessOrEqual(ratioMorning, 2.42f, "Morning fog too dense (" + ratioMorning + ").");
            // Equal sunlight (0.7 vs 0.3): path ratio must be FogFactor(0.3)/FogFactor(0.7) = 3.0.
            float ratioDense = pathAtNoon / pathAtDense;
            Assert.GreaterOrEqual(ratioDense, 2.9f, "Dense-fog path ratio off (" + ratioDense + ").");
            Assert.LessOrEqual(ratioDense, 3.1f, "Dense-fog path ratio off (" + ratioDense + ").");

            // (b) Visible proof at EQUAL sunlight (0.7 vs 0.3): the denser veil FLATTENS the
            // mid-distance image — vegetation contrast collapses into the fog colour. The stable
            // signal is therefore noon StDev MINUS dense StDev (Auto-Exposure normalisiert den
            // Mittelwert, die Kontrast-Struktur bleibt messbar). PNGs: t7-noon.png vs
            // t7-nebel-compare.png.
            float stDevCollapse = noon.y - nebeldense.y;
            Assert.Greater(stDevCollapse, 1.5f, "Denser fog did not flatten the render (StDev noon " + noon.y + " - dense " + nebeldense.y + " = " + stDevCollapse + ").");

            // The noon frame must show real contrast content (not blank/uniform).
            Assert.Greater(noon.y, 5f, "Noon frame nearly uniform (blank capture?).");
        }
    }
}
