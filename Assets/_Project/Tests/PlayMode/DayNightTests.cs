using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Z4 Tag/Nacht: the Sun rotates slowly (one day = 20 min), DayProgress rises
    /// linearly, and the brightness of the sun follows the altitude of the sun.
    /// </summary>
    public class DayNightTests
    {
        /// <summary>J5: Fixed exposure for the proof shots (G6-Lehre: NOT auto-exposure, so
        /// the Mittel of both images is comparable and reproducible across runs).</summary>
        // J5: Raw noon luma ~5.6E-2, night ~2.7E-3 (measured). E=20 puts noon Mittel
        // ~140..190 (window 60..200) and night ~10..20 (window 10..45).
        private const float fixedExposure = 20f;

        /// <summary>FARBE1a: HDRP-Exposure (EV100, hoeher = dunkler) Startwert der Kalibrierung.
        /// Auto-Exposure (AutomaticHistogram) normalisierte Nacht hoch auf Mittelgrau (Raw
        /// noon 0.063 / night 0.086 in o-farbe1c). Die Kalibrierschleife verschiebt das EV dann
        /// auf Ziel-Noon-Raw calibTargetRaw.</summary>
        private const float fixedHdrvEv = 14f;

        /// <summary>FARBE1a: Ziel-Noon-Raw der Grobkalibrierung (nur gegen Schwarzrahmen
        /// abgesichert; die Feinkalibrierung erfolgt auf den gemessenen Noon-Mittel).</summary>
        private const float calibTargetRaw = 0.06f;

        /// <summary>FARBE1a: Ziel-Mittel des Noon-Rahmens nach der Feinkalibrierung.
        /// Das echte Tag/Nacht-Roh-Verhaeltnis betraegt ~23 (r101-f5: noon Raw 0.364,
        /// night Raw 0.0155) — bei Noon ~130 landet die Nacht dann bei ~15 (Fenster 10..45).</summary>
        private const float noonCalibTarget = 130f;

        /// <summary>J5: Live flies (day species) via their spawner 'Insekten' (activeAtNight=false),
        /// so the disabled template is not miscounted.</summary>
        private static int DayInsectCount()
        {
            GameObject root = GameObject.Find("Insekten");
            Assert.IsNotNull(root, "Spawner 'Insekten' missing in Urwald.");
            InsectSpawner spawner = root.GetComponent<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner missing on 'Insekten'.");
            return spawner.AliveCount;
        }

        /// <summary>J5: Live moths (night species) via their spawner 'Insekten_Motte'.</summary>
        private static int NightInsectCount()
        {
            GameObject root = GameObject.Find("Insekten_Motte");
            Assert.IsNotNull(root, "Spawner 'Insekten_Motte' missing in Urwald.");
            InsectSpawner spawner = root.GetComponent<InsectSpawner>();
            Assert.IsNotNull(spawner, "InsectSpawner missing on 'Insekten_Motte'.");
            return spawner.AliveCount;
        }

        [UnityTest]
        public IEnumerator Tag_Nacht_SunRotatesWithDayLength()
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
            Assert.IsNotNull(cycle, "No DayNightCycle found in the Urwald scene.");

            Light sun = Object.FindFirstObjectByType<Light>();
            Assert.IsNotNull(sun, "No Sun found in the Urwald scene.");

            // Setting value: one day = 20 min (default from the Inspector).
            Assert.AreEqual(20f, cycle.dayLengthMinutes);

            float progressBefore = cycle.DayProgress;
            Vector3 fwdBefore = sun.transform.forward;

            // Simulate approx. 12 s of GAME time (CI runs at high fps; wall-clock differs).
            const float elapsed = 12f;
            float waited = 0f;
            while (waited < elapsed)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // Expectation: DayProgress rises by elapsed / (dayLength*60).
            float expectedDelta = elapsed / (cycle.dayLengthMinutes * 60f);
            float actualDelta = cycle.DayProgress - progressBefore;
            Assert.Greater(actualDelta, 0f, "DayProgress does not increase.");
            Assert.Less(Mathf.Abs(actualDelta - expectedDelta), 0.15f * expectedDelta,
                "DayProgress rises too strongly/weakeley from the day length ("
                + actualDelta.ToString("F4") + " vs. expected " + expectedDelta.ToString("F4") + ").");

            // The Sun has rotated (forward vector differs).
            float dot = Mathf.Clamp(Vector3.Dot(fwdBefore, sun.transform.forward), -1f, 1f);
            float rotDeg = Mathf.Acos(dot) * (180f / Mathf.PI);
            Assert.GreaterOrEqual(rotDeg, 2, "Sun did not rotate within 12 s.");

            // Brightness follows the altitude: never brighter than dayLux, at night darker.
            Assert.LessOrEqual(sun.intensity, cycle.dayLux * 1.01f, "Sun is brighter than dayLux.");
        }
        /// <summary>
        /// J5: Fixed-exposure capture (NOT auto-exposure!). The G6-Learning says auto-exposure
        /// is not reproducible and would wash out the day/night difference; a fixed factor keeps
        /// Mittel comparable across runs. Returns (Mittel 0..255, Max, Magenta count).
        /// </summary>
        /// <summary>J5: Raw (pre-exposure) luma mean of the last SaveFrameFixed call.</summary>
        private static float lastRawLumaMean;

        /// <summary>FARBE1: KLEE1b-Lehre — der erste Offscreen-Capture nach dem Editor-Start kann
        /// einen einfarbigen Rahmen liefern (HDRP-Shader kompilieren asynchron; Raw luma dann 0 oder ~1).
        /// T7 loest das mit Warmup-Frames; J5 faengt hier mit Kleinst-Proben dasselbe ab, BEVOR
        /// gemessen wird. Abbruch, sobald eine Probe Kontrast zeigt (Max - Mittel > 8) und kein
        /// Weiss-/Schwarzframe ist (Raw zwischen 1E-4 und 0.3; echter Wald-Noon-Roh liegt ~0.05).</summary>
        private static IEnumerator WarmUpFixed(Camera cam)
        {
            for (int probe = 0; probe < 25; probe++)
            {
                Vector3 p = SaveFrameFixed(cam, 64, 48, "Logs/ci/j5_warmup_probe.png", 1f);
                if (p.y - p.x > 8f && lastRawLumaMean > 1e-4f && lastRawLumaMean < 0.3f)
                {
                    Debug.Log("[J5] WarmUp nach " + probe + " Proben (Raw " + lastRawLumaMean.ToString("E2") + ")");
                    yield break;
                }
                yield return new UnityEngine.WaitForSecondsRealtime(1.5f);
            }
            Debug.Log("[J5] WarmUp ohne Erfolg (Raw " + lastRawLumaMean.ToString("E2") + ")");
        }

        private static Vector3 SaveFrameFixed(Camera cam, int w, int h, string file, float exposure)
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
            Color[] outPx = new Color[n];
            double sum = 0.0;
            double rawSum = 0.0;
            int maxv = 0;
            long magenta = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                rawSum += 0.299 * Mathf.Max(c.r, 0f) + 0.587 * Mathf.Max(c.g, 0f) + 0.114 * Mathf.Max(c.b, 0f);
                float r = AcesTone(Mathf.Max(c.r * exposure, 0f));
                float g = AcesTone(Mathf.Max(c.g * exposure, 0f));
                float b = AcesTone(Mathf.Max(c.b * exposure, 0f));
                outPx[i] = new Color(r, g, b, 1f);
                float luma = 0.299f * r + 0.587f * g + 0.114f * b;
                sum += luma * 255.0;
                int ri = (int)(Mathf.Max(r, Mathf.Max(g, b)) * 255f);
                if (ri > maxv) maxv = ri;
                // J5: only near-pure error-magenta (Unity's hole colour is exactly (1,0,1));
                // the Urwald has real pink flowers that a loose threshold would miscount.
                if (r > 0.96f && g < 0.06f && b > 0.96f) magenta++;
            }

            var ldr = new Texture2D(w, h, TextureFormat.RGB24, false);
            ldr.SetPixels(outPx);
            ldr.Apply(false);
            System.IO.File.WriteAllBytes(file, ldr.EncodeToPNG());
            Object.DestroyImmediate(hdr);
            Object.DestroyImmediate(ldr);
            Object.DestroyImmediate(rt);
            lastRawLumaMean = (float)(rawSum / n);
            return new Vector3((float)(sum / n), maxv, magenta);
        }

        private static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
        }

        /// <summary>FARBE1a: DayFactorAt is the shared curve of Update and SetDayProgress.
        /// EditMode-pinned: deep night 0, noon 1, monotonic ramp between (no abrupt jumps),
        /// symmetric around midnight.</summary>
        [Test]
        public void J5_DayFactorAt_Kurve()
        {
            // Test cycle like J5: 0.2 min day, 0.04 min ramps, 6 min night (clamped to day).
            float dayFactorNight = DayNightCycle.DayFactorAt(0f, 0.2f, 0.04f, 6f);
            Assert.AreEqual(0f, dayFactorNight, 1e-4f, "Midnight must be dark (0).");

            float dayFactorNoon = DayNightCycle.DayFactorAt(0.5f, 0.2f, 0.04f, 6f);
            Assert.AreEqual(1f, dayFactorNoon, 1e-4f, "Noon must be full day (1).");

            // Monotonic rising noon -> midnight core (with tolerance for the piecewise ramp).
            float prev = dayFactorNoon;
            for (float t = 0.5f; t <= 1f; t += 0.01f)
            {
                float cur = DayNightCycle.DayFactorAt(t, 0.2f, 0.04f, 6f);
                Assert.LessOrEqual(cur, prev + 1e-4f, "dayFactor not monotonic toward night at " + t.ToString("F2"));
                prev = cur;
            }

            // Symmetric around midnight: 0.1 and 0.9 give the same factor.
            Assert.AreEqual(DayNightCycle.DayFactorAt(0.1f, 0.2f, 0.04f, 6f),
                DayNightCycle.DayFactorAt(0.9f, 0.2f, 0.04f, 6f), 1e-4f,
                "dayFactor must be symmetric around midnight.");
        }

        /// <summary>FARBE1a: SetDayProgress applies the light state IMMEDIATELY (same frame),
        /// so a capture right after the jump already shows night/day — the root cause of the
        /// night frame mirroring the day frame.</summary>
        [Test]
        public void J5_SetDayProgress_SetztLichtSofort()
        {
            GameObject go = new GameObject("SkySnapTest");
            Light testSun = go.AddComponent<Light>();
            testSun.type = LightType.Directional;
            DayNightCycle cycle = go.AddComponent<DayNightCycle>();
            cycle.sun = testSun;
            cycle.dayLengthMinutes = 0.2f;
            cycle.transitionMinutes = 0.04f;
            cycle.dayLux = 100000f;
            cycle.moonLux = 16000f;

            cycle.SetDayProgress(0.0f); // midnight
            Assert.AreEqual(0f, cycle.CurrentDayFactor, 1e-3f, "SetDayProgress(0) must set dayFactor 0 in the SAME call.");
            Assert.AreEqual(16000f, testSun.intensity, 1f, "SetDayProgress(0) must set moonLux in the SAME call.");

            cycle.SetDayProgress(0.5f); // noon
            Assert.AreEqual(1f, cycle.CurrentDayFactor, 1e-3f, "SetDayProgress(0.5) must set dayFactor 1 in the SAME call.");
            Assert.AreEqual(100000f, testSun.intensity, 1f, "SetDayProgress(0.5) must set dayLux in the SAME call.");

            // No sky volume wired in this bare setup -> sentinel -1 (no crash).
            Assert.AreEqual(-1f, cycle.CurrentSkyMultiplier(), "Unwired sky must report -1.");

            cycle.sun = null;
            GameObject.DestroyImmediate(go);
        }

        /// <summary>
        /// Sun-Test: The DayNightCycle rotates the sun and adjusts its brightness
        /// according to the altitude (night darker than day).
        /// </summary>
        [UnityTest]
        public IEnumerator Tag_Nacht_SunRotatesAndDimms()
        {
            GameObject sunGo = new GameObject("SunTest");
            Light testSun = sunGo.AddComponent<Light>();
            testSun.type = LightType.Directional;

            DayNightCycle cycle = sunGo.AddComponent<DayNightCycle>();
            cycle.sun = testSun;
            // Speed up: one day = 12 s so the rotation is measurable in a Test.
            cycle.dayLengthMinutes = 0.2f;

            Vector3 fwdBefore = testSun.transform.forward;
            float waited = 0f;
            while (waited < 3f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(cycle.DayProgress, 0f, "DayProgress does not increase.");

            float dot = Mathf.Clamp(Vector3.Dot(fwdBefore, testSun.transform.forward), -1f, 1f);
            float rotDeg = Mathf.Acos(dot) * (180f / Mathf.PI);
            Assert.Greater(rotDeg, 30f, "Sun did not rotate within 3 s.");

            // The brightness follows the altitude and never exceeds dayLux.
            Assert.LessOrEqual(testSun.intensity, cycle.dayLux * 1.01f, "Sun is brighter than dayLux.");
            Assert.GreaterOrEqual(testSun.intensity, cycle.moonLux * 0.99f, "Sun is darker than moonLux.");

            GameObject destroyed = sunGo;
            destroyed.GetComponent<DayNightCycle>().sun = null;
            GameObject.DestroyImmediate(destroyed);
        }

        /// <summary>
        /// J5: Cycle changes light AND insects. Day flies out by day, moths out at night.
        /// Fixed exposure (G6-Lehre: auto-exposure not reproducible) keeps the two proof
        /// images comparable: noon must be clearly brighter than midnight.
        /// </summary>
        [UnityTest]
        public IEnumerator J5_TagAndNacht_LightAndInsectsFollowCycle()
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

            cycle.dayLengthMinutes = 0.2f; // 12 s per day so both phases are reachable.
            cycle.transitionMinutes = 0.04f; // 2.4 s dawn/dusk ramps for the short test cycle.

            // G6-Lehre: ERST the world ready, THEN capture (otherwise black frames).
            timeout = 25f;
            JungleWorldGenerator worldGen = null;
            while (timeout > 0f && worldGen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady)
                {
                    worldGen = g;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(worldGen, "Urwald world (Seed 4242) never ready.");
            // FARBE1: volumetrischer Nebel (WALD8c) macht die Offscreen-Rahmen weiss (KLEE1b-Lehre).
            cycle.SetVolumetricFogForTests(false);
            // FARBE1a: Auto-Exposure (AutomaticHistogram) normalisiert Tag UND Nacht auf Mittelgrau —
            // der Nacht-Rahmen wurde hochgeregelt (nacht Raw > noon Raw). Fuer die Messung auf
            // Fixed-Exposure (EV) umschalten, dann ist der Rahmen reproduzierbar.
            Assert.IsTrue(cycle.SetFixedExposureForTests(fixedHdrvEv),
                "Exposure volume not wired to the DayNightCycle.");
            Camera cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "No Camera in Urwald.");

            // Wait until high noon (dayFactor near 1).
            timeout = 40f;
            while (timeout > 0f && cycle.CurrentDayFactor < 0.95f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(timeout, 0f, "Noon never reached.");

            // Count per species via the spawners (FindObjectsByType would count the
            // disabled templates as well). activeAtNight=false -> day species (flies).
            int fliesAtNoon = DayInsectCount();
            int mothsAtNoon = NightInsectCount();
            // FARBE1a: WarmUp + Kalibrierung in EINER Schleife aus Vollgroessen-Aufnahmen:
            // die 64x48-Proben bleiben teils komplett schwarz (spaet kompilierende Shader,
            // r101-f4) und taugen nicht zur Helligkeitskalibrierung.
            // Schritt 1: helles Start-EV (8) — echte Noon-Szene liegt dort bei Raw ~0.3,
            // Schwarzrahmen bei ~0..3E-4. Stabil = Raw > 1E-3 (kein Schwarzrahmen) und zwei
            // Aufnahmen in Folge innerhalb Faktor 2.
            const float calibStartEv = 8f;
            cycle.SetFixedExposureForTests(calibStartEv);
            float calRaw = 0f;
            float prevCalRaw = -1f;
            for (int cal = 0; cal < 20; cal++)
            {
                SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-noon-calib.png", 1f);
                calRaw = lastRawLumaMean;
                if (calRaw > 1e-3f && prevCalRaw > 0f && calRaw < prevCalRaw * 2f && calRaw * 2f > prevCalRaw)
                {
                    break;
                }
                prevCalRaw = calRaw;
                yield return new UnityEngine.WaitForSecondsRealtime(1f);
            }
            // Schritt 2: EV-Roheinstellung aus dem Raw (Raw skaliert linear mit 2^-EV), dann
            // zwei Fein-Kalibrierungen auf den ENGUELTIG gemessenen Noon-Mittel (ACES ist
            // nichtlinear, deshalb nicht auf den Raw kalibrieren).
            float ev = calibStartEv + Mathf.Clamp(Mathf.Log(Mathf.Clamp(calRaw, 1e-4f, 3f) / calibTargetRaw) * 1.4427f, -10f, 10f);
            float noonCalMean = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                cycle.SetFixedExposureForTests(ev);
                for (int settle = 0; settle < 3; settle++) yield return null;
                noonCalMean = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-noon-calib.png", fixedExposure).x;
                if (noonCalMean > 1f)
                {
                    ev += Mathf.Log(noonCalMean / noonCalibTarget) * 1.4427f; // log2; EV hoch = dunkler
                }
                Debug.Log("[J5-noon-cal] pass " + pass + " EV " + ev.ToString("F2") + " Mittel " + noonCalMean.ToString("F1"));
            }
            cycle.SetFixedExposureForTests(ev);
            for (int settle = 0; settle < 3; settle++) yield return null;
            Debug.Log("[J5-noon-setup] dayFactor " + cycle.CurrentDayFactor.ToString("F3")
                + " sunLux " + (cycle.sun != null ? cycle.sun.intensity.ToString("F0") : "n/a")
                + " skyMultiplier " + cycle.CurrentSkyMultiplier().ToString("E2")
                + " EV " + ev.ToString("F2") + " (Calib-Raw " + calRaw.ToString("E2") + ")");
            // FARBE1: WarmUp gegen Blank-Rahmen (KLEE1b-Lehre), bevor gemessen wird.
            yield return WarmUpFixed(cam);
            Vector3 noonStats = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-noon.png", fixedExposure);
            Debug.Log("[J5-noon] Mittel " + noonStats.x.ToString("F1") + " Max " + noonStats.y.ToString("F0") + " Magenta " + noonStats.z.ToString("F0") + " Raw " + lastRawLumaMean.ToString("E2"));

            // Wait until deep night (dayFactor near 0).
            timeout = 40f;
            while (timeout > 0f && cycle.CurrentDayFactor > 0.05f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(timeout, 0f, "Night never reached.");

            int fliesAtNight = DayInsectCount();
            int mothsAtNight = NightInsectCount();
            // FARBE1a: 2-3 Frames warten, damit Volume/Exposure/Himmel (DynamicGI) im neuen
            // Nacht-Zustand ankommen, BEVOR gemessen wird. Die Kontrast-WarmUp-Probe faellt
            // nachts weg: ein korrekter Nachtrahmen ist dunkel und wuerde sie bis zum Timeout
            // laufen lassen.
            for (int settle = 0; settle < 3; settle++) yield return null;
            Debug.Log("[J5-night-setup] dayFactor " + cycle.CurrentDayFactor.ToString("F3")
                + " sunLux " + (cycle.sun != null ? cycle.sun.intensity.ToString("F0") : "n/a")
                + " skyMultiplier " + cycle.CurrentSkyMultiplier().ToString("E2")
                + " ambientMode " + UnityEngine.RenderSettings.ambientMode);
            // FARBE1: WarmUp gegen Blank-Rahmen (KLEE1b-Lehre), bevor gemessen wird.
            yield return WarmUpFixed(cam);
            Vector3 nightStats = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-night.png", fixedExposure);
            Debug.Log("[J5-night] Mittel " + nightStats.x.ToString("F1") + " Max " + nightStats.y.ToString("F0") + " Magenta " + nightStats.z.ToString("F0") + " Raw " + lastRawLumaMean.ToString("E2"));

            // Backlog criteria: day Mittel 60..200, night 10..45, both Magenta 0.
            Assert.GreaterOrEqual(noonStats.x, 60f, "Day Mittel < 60 (fixed exposure).");
            Assert.LessOrEqual(noonStats.x, 200f, "Day Mittel > 200 (blendout).");
            Assert.GreaterOrEqual(nightStats.x, 10f, "Night Mittel < 10 (too dark).");
            Assert.LessOrEqual(nightStats.x, 45f, "Night Mittel > 45 (not dark enough).");
            Assert.LessOrEqual(noonStats.z + nightStats.z, 0f, "Magenta pixels (holes) in proof images.");

            // Species swap: flies out by day (8), retreated at night; moths reversed.
            Assert.GreaterOrEqual(fliesAtNoon, 6, "Day insects not out at noon.");
            Assert.LessOrEqual(fliesAtNight, 2, "Day insects still out at night.");
            // Backlog: Mottenanzahl nachts mindestens dreimal so hoch wie tagsueber.
            Assert.GreaterOrEqual(mothsAtNight, 3 * Mathf.Max(mothsAtNoon, 1),
                "Moths at night not >= 3x moths at noon (" + mothsAtNight + " vs. " + mothsAtNoon + ").");

            // Fixed exposure: noon clearly brighter than midnight (visible light change).
            Assert.Greater(noonStats.x, nightStats.x * 1.4f, "Noon not clearly brighter than night (fixed exposure).");
        }
    }
}
