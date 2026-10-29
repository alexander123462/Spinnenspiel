using System.Collections;
using System.Reflection;
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
            // LICHT1c3: Rahmen vor dem Rendern auf Fehler-Magenta loeschen (RT-Pool kann
            // alte Rahmen enthalten; bleibt HDRP-Camues still aus, zaehlt der Test Magenta).
            var clr = new UnityEngine.Rendering.CommandBuffer();
            clr.SetRenderTarget(rt);
            clr.ClearRenderTarget(true, true, new Color(1f, 0f, 1f, 1f));
            UnityEngine.Graphics.ExecuteCommandBuffer(clr);
            clr.Release();
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
            double sr = 0.0, sg = 0.0, sb = 0.0; // LICHT1c3 Diagnose: Kanalmittel (Roh-HDR)
            int maxv = 0;
            long magenta = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                sr += Mathf.Max(c.r, 0f); sg += Mathf.Max(c.g, 0f); sb += Mathf.Max(c.b, 0f);
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
            // LICHT1c3 Diagnose: Roh-Kanalmittel + grobes Perzentil des HDR-Rahmens.
            System.Array.Sort(px, (a, b) => (0.299f * a.r + 0.587f * a.g + 0.114f * a.b)
                .CompareTo(0.299f * b.r + 0.587f * b.g + 0.114f * b.b));
            Debug.Log("[J5-raw] " + file + " RGB " + (sr / n).ToString("E2") + ","
                + (sg / n).ToString("E2") + "," + (sb / n).ToString("E2")
                + " p1 " + LumaOf(px[n / 100]).ToString("E2") + " p50 "
                + LumaOf(px[n / 2]).ToString("E2") + " p99 " + LumaOf(px[n * 99 / 100])
                .ToString("E2") + " max " + LumaOf(px[n - 1]).ToString("E2"));
            return new Vector3((float)(sum / n), maxv, magenta);
        }

        // LICHT1c3 Diagnose: Roh-Luma eines HDR-Pixels (vor Belichtung/Tonemap).
        private static float LumaOf(Color c) => 0.299f * Mathf.Max(c.r, 0f)
            + 0.587f * Mathf.Max(c.g, 0f) + 0.114f * Mathf.Max(c.b, 0f);

        /// <summary>LICHT1c4 Diagnose: Modus + EV der BELICHTUNG, die im wirksamen
        /// Volume-Stack ankommt (nicht die, die der Zyklus setzt). Der HDRP-Exposure-Typ
        /// kommt per Reflection aus dem Zyklus-Feld — die Test-Assembly referenziert HDRP
        /// nicht. Verglichen wird die Stack-Instanz mit der des Zyklus: unterschiedliche
        /// Instance-IDs = der Fixed-Pin des Zyklus gewinnt den Stack nicht.</summary>
        private static string ExposureStackDiag(DayNightCycle cycle)
        {
            var vm = UnityEngine.Rendering.VolumeManager.instance;
            if (vm == null || vm.stack == null) return "exp stack null";
            System.Type expType = typeof(DayNightCycle).GetField("exposureVolume").FieldType;
            var comp = vm.stack.GetComponent(expType);
            object cycExp = typeof(DayNightCycle).GetField("exposureVolume").GetValue(cycle);
            string cycleId = cycExp != null
                ? ((UnityEngine.Object)cycExp).GetInstanceID().ToString() : "null";
            if (comp == null) return "stackExp none cycleExp " + cycleId;
            return "stackExp inst " + ((UnityEngine.Object)comp).GetInstanceID()
                + " mode " + OverrideValueOf(comp, "mode")
                + " fixedEV " + OverrideValueOf(comp, "fixedExposure")
                + " limMin " + OverrideValueOf(comp, "limitMin")
                + " limMax " + OverrideValueOf(comp, "limitMax")
                + " postExp " + OverrideValueOf(comp, "postExposure")
                + " cycleExp inst " + cycleId;
        }

        /// <summary>LICHT1c4 Diagnose: Wert eines VolumeParameter-Felds als Text.</summary>
        private static string OverrideValueOf(object component, string parameterName)
        {
            var field = component.GetType().GetField(parameterName);
            if (field == null) return "?";
            object param = field.GetValue(component);
            if (param == null) return "null";
            object val = param.GetType().GetProperty("value").GetValue(param);
            return val != null ? val.ToString() : "null";
        }

        /// <summary>LICHT1c4 Diagnose: alle Volumes der Szene mit Prioritaet — taucht spaeter
        /// ein zweites Volume auf, das die Belichtung uebernimmt (Wald8c/J5-Override)?</summary>
        private static string VolumeListDiag()
        {
            var vols = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(
                FindObjectsSortMode.None);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < vols.Length; i++)
            {
                sb.Append(vols[i].name).Append(" p").Append(vols[i].priority.ToString("F0"))
                  .Append(vols[i].isGlobal ? "g" : "l")
                  .Append(vols[i].sharedProfile != null ? vols[i].sharedProfile.name : "null")
                  .Append(' ');
            }
            return sb.Length > 0 ? sb.ToString() : "keine Volumes";
        }

        /// <summary>LICHT1c4 Diagnose: laeuft noch asynchrone Shader-Kompilierung? Schwarze
        /// Nachtrahmen waeren dann "noch nicht renderbar" statt "Nacht". UnityEditor-API per
        /// Reflection (Test-Assembly referenziert UnityEditor nicht).</summary>
        private static string ShaderCompileDiag()
        {
            var t = System.Type.GetType("UnityEditor.ShaderUtil, UnityEditor.CoreModule");
            if (t == null) return "shaderutil n/a";
            var p = t.GetProperty("isCompiling", BindingFlags.Static | BindingFlags.Public);
            return p == null ? "isCompiling n/a" : "compiling " + p.GetValue(null);
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

        /// <summary>LICHT1c4 (Ursache der Nacht-Drift, r107-f2): nightMinutes und
        /// transitionMinutes sind ABSOLUTE Minuten. Wer den Zyklus ueber dayLengthMinutes
        /// "einfriert" (10000 min wie in J5 vorher), schrumpft den Nachtkern auf
        /// +/-3e-4 Fortschritt und die Daemmerungsrampe auf 4e-6 — der Zyklus springt mitten
        /// in der Nacht auf vollen Tag. Der Test pinnt genau diese Falle, damit niemand sie
        /// erneut einbaut (Messen geht ueber SetHoldDayProgressForTests).</summary>
        [Test]
        public void LICHT1c4_DayFactorAt_EingefrorenerTagZerstoertNachtfenster()
        {
            // Normaler 20-min-Tag: Mitternacht + 0.5 min ist noch tiefste Nacht.
            Assert.AreEqual(0f, DayNightCycle.DayFactorAt(0.0004f, 20f, 2f, 6f),
                "Normaler Tag: kurz nach Mitternacht muss Nacht sein.");
            // "Eingefrorener" 10000-min-Tag: derselbe Fortschritt liegt ausserhalb des
            // Nachtkerns (6 min / 10000 min = 6e-4 breit, halbseitig 3e-4) -> taghell.
            Assert.GreaterOrEqual(DayNightCycle.DayFactorAt(0.0004f, 10000f, 0.04f, 6f), 1f,
                "Nachtkern bei 10000 min Tageslaenge ist nur +/-0.0003 breit — genau die "
                + "Drift-Falle aus r107-f2; Messungen dürfen dayLengthMinutes nicht einfrieren.");
        }

        /// <summary>LICHT1c4: der Halte-Modus haelt den Tagesfortschritt zuverlaessig (Licht
        /// bleibt Mondlicht), und nach dem Ausschalten laeuft der Zyklus wieder weiter.</summary>
        [UnityTest]
        public IEnumerator LICHT1c4_HoldDayProgress_NachtBleibtNacht()
        {
            GameObject go = new GameObject("HoldCycleTest");
            Light testSun = go.AddComponent<Light>();
            testSun.type = LightType.Directional;
            DayNightCycle cycle = go.AddComponent<DayNightCycle>();
            cycle.sun = testSun;
            cycle.dayLengthMinutes = 0.2f; // wie J5: 12 s pro Tag
            cycle.transitionMinutes = 0.04f;
            cycle.dayLux = 100000f;
            cycle.moonLux = 800f;

            yield return null; // Start() durchlaufen lassen
            cycle.SetHoldDayProgressForTests(true);
            cycle.SetDayProgress(0f); // Mitternacht pinnen
            float pinned = cycle.DayProgress;

            // 2 s real = 0.17 Fortschritt bei 0.2 min/Tag — ohne Halte-Modus laengt das
            // deutlich ueber den Nachtkern hinaus (und war frueher der Drift-Effekt).
            float waited = 0f;
            while (waited < 2f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            Assert.AreEqual(pinned, cycle.DayProgress, 1e-5f,
                "Tagesfortschritt laeuft trotz Halte-Modus weiter.");
            Assert.LessOrEqual(cycle.CurrentDayFactor, 0.05f,
                "Nacht ist im Halte-Modus auf Tag gekippt (dayFactor " + cycle.CurrentDayFactor + ").");
            Assert.AreEqual(800f, testSun.intensity, 1f,
                "Lichtstaerke waehrend des Haltens nicht Mond-Lux.");

            // Halte-Modus aus -> Zyklus laeuft wieder (kein haengenbleibender Zustand).
            cycle.SetHoldDayProgressForTests(false);
            float before = cycle.DayProgress;
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.Greater(cycle.DayProgress, before,
                "Nach dem Ausschalten muss der Zyklus wieder weiterlaufen.");

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
            // LICHT1c3 Diagnose (r106-f9): Nacht-Helligkeit invers zum Mond-Lux (250 lx ->
            // Raw 3,5E-2 > 800 lx -> 1,9E-2) = Auto-Exposure gewinnt den Stack, obwohl der
            // Zyklus Fixed pinnt. Aufloesung des Volume-Stacks und Zielkomponente loggen.
            {
                // VolumeManager.instance kann frue im Test null sein; alle Volume-Komponenten
                // der Szene plus Zyklus-Zielkomponente direkt loggen.
                var vols = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(
                    FindObjectsSortMode.None);
                foreach (var v in vols)
                {
                    Debug.Log("[J5-pin-exposure] scene vol '" + v.name + "' global " + v.isGlobal
                        + " prio " + v.priority + " profile "
                        + (v.sharedProfile != null ? v.sharedProfile.name : "null"));
                }
                object cycExp = typeof(DayNightCycle).GetField("exposureVolume")
                    .GetValue(cycle);
                string cycleInfo = "null";
                if (cycExp != null)
                {
                    object pmc = cycExp.GetType().GetField("mode").GetValue(cycExp);
                    object cmVal = pmc.GetType().GetProperty("value").GetValue(pmc);
                    cycleInfo = "inst " + ((UnityEngine.Object)cycExp).GetInstanceID()
                        + " mode " + cmVal;
                }
                Debug.Log("[J5-pin-exposure] cycle.comp " + cycleInfo);
            }
            Camera cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "No Camera in Urwald.");

            // LICHT1c2 (J5-Kalibrierung): die Follow-Kamera rahmte zum Aufnahmezeitpunkt genau das ein,
            // was die Spinne gerade tat — im Voll-Lauf mass die Nacht darum 3x heller als in der
            // gezielten Auswahl (Mittel 90.7 vs. 40.9 bei identischen EV/Himmels-Werten). Die
            // Messung braucht einen deterministischen Standpunkt (G6-Lehre): feste Pose ueber dem
            // Terrain am Weltmittelpunkt, fester Blick (Seed 4242 -> identische Vegetation).
            var followCam = cam.GetComponent<SpiderCamera>();
            if (followCam != null) followCam.enabled = false;
            float halfJ5 = worldGen.World.Size / 2f;
            UnityEngine.Terrain terJ5 = UnityEngine.Terrain.activeTerrain;
            Assert.IsNotNull(terJ5, "No active terrain in Urwald (J5 pose pin).");
            Vector3 midJ5 = new Vector3(halfJ5, 0f, halfJ5);
            float groundYJ5 = terJ5.SampleHeight(midJ5) + terJ5.transform.position.y;
            cam.transform.position = midJ5 + new Vector3(0f, groundYJ5 + 1.6f, 0f);
            cam.transform.rotation = Quaternion.Euler(-6f, 45f, 0f);
            Debug.Log("[J5-pin] pos " + cam.transform.position.ToString("F1") + " ground " + groundYJ5.ToString("F1"));

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
            // LICHT1c3: Kalibrierung + Aufnahme kosten Sekunden; der Zyklus darf dabei nicht
            // bis zur Daemmerung laufen (r105-f3 dayFactor 0.708). LICHT1c4: NICHT mehr ueber
            // dayLengthMinutes=10000 einfrieren — das schrumpft den Nachtkern
            // (nightMinutes/dayLengthMinutes) auf +/-0.0003 Fortschritt und der Zyklus schnappt
            // mitten in der Nacht auf Tag (r107-f2). Fortschritt halten, Tageslaenge laesst man.
            cycle.SetHoldDayProgressForTests(true);
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
                // LICHT1c3: der Zyklus laeuft mit dayLengthMinutes=0.2 (12 s/Tag) weiter —
                // die Kalibrierung kostet Sekunden, dabei driftete der Tag auf halbe
                // Daemmerung (r105-f3: [J5-noon-setup] dayFactor 0.708 statt ~0.95).
                // Vor jeder Messung exakt Mittag pinnen (Mitternacht pinnt der Nachtblock).
                cycle.SetDayProgress(0.5f);
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
            // LICHT1c4: Haelte-Modus aus — der Zyklus laeuft mit 0.2 min/Tag (12 s) weiter und
            // erreicht die Nacht innerhalb des 40-s-Fensters. Tageslaenge bleibt 0.2: damit
            // bleibt der Nachtkern in der Fortschritts-Skala breit (min(6, 0.2-2*0.04)=0.12 min
            // = 60 % des Tags) und kann nicht mehr auf Tag zurueckkippen.
            cycle.SetHoldDayProgressForTests(false);

            // Wait until deep night (dayFactor near 0).
            timeout = 40f;
            while (timeout > 0f && cycle.CurrentDayFactor > 0.05f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(timeout, 0f, "Night never reached.");

            // LICHT1c2: die NachtMessung war nichtdeterministisch — Update() schiebt den Zyklus
            // pro Frame weiter, die Aufnahme landete je nach Lauf an einer anderen Nachtposition
            // (Mondhoehe + sunLux = Lerp(moonLux, dayLux, dayF) streute: Raw 7.2E-3 vs 1.5E-2,
            // Mittel 44.9 vs 80.4 bei identischen Einstellungen). Exakt Mitternacht pinnen:
            // dayFactor 0, Mond im Zenit (Hoehe = -elev spiegelt 90 Grad), alle Werte fix.
            // LICHT1c4 (r107-f2): das fruehere "Einfrieren" ueber dayLengthMinutes=10000 war die
            // Ursache der Nacht-Drift — bei 10000 min ist der Nachtkern nur noch +/-0.0003
            // Fortschritt breit, und nach ~3 min Spielzeit kippte der Zyklus auf vollen Tag
            // (dayFactor 0 -> 1, sunLux 250 -> 100000, Himmel 0.003 -> 1). Jetzt: Fortschritt
            // halten (hold), Tageslaenge auf 0.2 min lassen -> breites Nachtfenster.
            cycle.SetHoldDayProgressForTests(true);
            cycle.SetDayProgress(0f);

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
            // LICHT1c3 Diagnose (r106-f6): Nacht-Rahmen reagierte auf GAR keine Licht-/
            // Himmels-Aenderung (Mittel bitgleich 132.98 bei 800 vs 8000 lx, mit/ohne
            // interactsWithSky). Vollstaendigen Lichtzustand direkt vor der Messung loggen.
            Debug.Log("[J5-night-state] fog " + RenderSettings.fog + " fogColor "
                + RenderSettings.fogColor.ToString("F4") + " fogDensity "
                + RenderSettings.fogDensity.ToString("E2") + " sunInt "
                + (cycle.sun != null ? cycle.sun.intensity.ToString("F0") : "n/a") + " sunOn "
                + (cycle.sun != null && cycle.sun.enabled) + " sunCol "
                + (cycle.sun != null ? cycle.sun.color.ToString("F2") : "n/a")
                + " ambInt " + RenderSettings.ambientIntensity + " ambSky "
                + RenderSettings.ambientSkyColor.ToString("F2") + " ambEq "
                + RenderSettings.ambientEquatorColor.ToString("F2") + " ambGnd "
                + RenderSettings.ambientGroundColor.ToString("F2"));
            object hdSunDbg = cycle.sun != null
                ? cycle.sun.GetComponent("HDAdditionalLightData") : null;
            if (hdSunDbg != null)
            {
                System.Type hdt = hdSunDbg.GetType();
                Debug.Log("[J5-night-state] skyPart " + hdt.GetProperty("interactsWithSky")
                    .GetValue(hdSunDbg) + " shadSrc " + hdt.GetField("celestialBodyShadingSource")
                    .GetValue(hdSunDbg) + " luxUnit " + cycle.sun.lightUnit);
            }
            // LICHT1c3 Diagnose (r106-f15): Nacht-Rahmen weiterhin BITGLEICH 132.98 trotz
            // Magenta-Clear, Priority 100 und Fixed-Pin. Beweisprobe: Ziel-RT auf Magenta
            // clearn OHNE cam.Render(), dann exakt so ablesen wie SaveFrameFixed. Liefert
            // der Read-Pfad einen Wald statt Magenta, liest ReadPixels am RT vorbei.
            {
                var probeRt = new RenderTexture(64, 36, 0, RenderTextureFormat.ARGBHalf);
                probeRt.Create();
                var pc = new UnityEngine.Rendering.CommandBuffer();
                pc.SetRenderTarget(probeRt);
                pc.ClearRenderTarget(true, true, new Color(1f, 0f, 1f, 1f));
                UnityEngine.Graphics.ExecuteCommandBuffer(pc);
                pc.Release();
                RenderTexture.active = probeRt;
                var ptex = new Texture2D(64, 36, TextureFormat.RGBAHalf, false);
                ptex.ReadPixels(new Rect(0, 0, 64, 36), 0, 0);
                ptex.Apply(false);
                RenderTexture.active = null;
                Color[] ppx = ptex.GetPixels();
                double pr = 0, pg = 0, pb = 0;
                foreach (Color c in ppx) { pr += c.r; pg += c.g; pb += c.b; }
                int n2 = ppx.Length;
                Debug.Log("[J5-readback-probe] RGB " + (pr / n2).ToString("F2") + ","
                    + (pg / n2).ToString("F2") + "," + (pb / n2).ToString("F2")
                    + " (1,0,1 = ReadPixels liest RT korrekt; Waldwerte = Read-Pfad falsch)");
                Object.DestroyImmediate(ptex);
                Object.DestroyImmediate(probeRt);
            }
            // FARBE1: WarmUp gegen Blank-Rahmen (KLEE1b-Lehre), bevor gemessen wird.
            // LICHT1c3 (r106-f16): die ersten 11 Nacht-Proben waren EXAKT schwarz
            // (Mitternacht, Sonne unter Horizont — korrekt!), ab Probe 12 wieder taghell.
            // LICHT1c4: Zustand pro Probe VOLLSTAENDIG loggen (wirksame Belichtung aus dem
            // Volume-Stack, Lichtintensitaeten, dayFactor, Volumes, Shader-Kompilierung) und
            // nach der ersten hellen Probe WEITER messen — erst der Vergleich der Zeilen
            // zeigt, ob der Zyklus driftet, ein Volume die Belichtung uebernimmt oder HDRP
            // vorher schlicht noch nicht rendert.
            // Ergebnis r107-f3: Drift ist weg — alle 24 Proben melden progress 0.0000,
            // dayFactor 0.000, sunInt konstant 16000 (vorher: Probe 12 plotzlich taghell).
            // Zweite Ursache (r107-f5 gemessen, NICHT behoben): die Nacht ist bei jeder
            // Lichtmenge schwarz — Mond 16000 lx, 100000 lx und Himmels-Multiplikator bis 0.1
            // ergeben Raw 0.00E+000; erst mit Himmelsbeteiligung der Sonne (interactsWithSky
            // true) misst man 5.7E-008. Das Dach (JungleWorldGenerator BuildCanopy, Schatten
            // An — LICHT1c5) blockiert das Direktlicht voll, der Tagesrahmen lebt vom
            // Nebelschleier (Nebelfarbe 0.4/0.58), der nachts auf FogNightDimFactor 0.015
            // gedimmt ist (Licht1cTests fordert genau diesen Wert), und J5 verlangt ausserdem
            // Nachthimmel < 0.02. Mit diesen drei festgelegten Werten ist das J5-Fenster
            // 10..45 bei fester EV nicht erreichbar — Entscheidung nach LICHT1c5 noetig.
            int brightStreak = 0;
            for (int probe = 0; probe < 24; probe++)
            {
                Vector3 p = SaveFrameFixed(cam, 64, 48, "Logs/ci/j5_warmup_probe.png", 1f);
                Debug.Log("[J5-night-probe " + probe + "] Raw " + lastRawLumaMean.ToString("E2")
                    + " dayFactor " + cycle.CurrentDayFactor.ToString("F3")
                    + " progress " + cycle.DayProgress.ToString("F4")
                    + " sunInt " + (cycle.sun != null ? cycle.sun.intensity.ToString("F0") : "n/a")
                    + " sunElev " + (cycle.sun != null ? cycle.sun.transform.eulerAngles.x.ToString("F1") : "n/a")
                    + " enabled " + cycle.enabled + " len " + cycle.dayLengthMinutes.ToString("F1")
                    + " | " + ExposureStackDiag(cycle)
                    + " | skyMul " + cycle.CurrentSkyMultiplier().ToString("E2")
                    + " ambInt " + RenderSettings.ambientIntensity.ToString("F3")
                    + " ambSky " + RenderSettings.ambientSkyColor.ToString("F3")
                    + " fogDens " + RenderSettings.fogDensity.ToString("E2")
                    + " fogCol " + RenderSettings.fogColor.ToString("F4")
                    + " | " + ShaderCompileDiag()
                    + " | vols " + VolumeListDiag());
                brightStreak = lastRawLumaMean > 1e-3f ? brightStreak + 1 : 0;
                // LICHT1c6: Schwelle 3 -> 8. Die Nacht ist jetzt berechtigt hell
                // (Mond-Atmosphaere speist den Schleier, r108-f3 Raw ~1.2E-3 > 1e-3) —
                // das alte Break endete nach Probe 2 und liess Probe 12..30 (Backlog-
                // Kriterium "gleich dunkel wie 1..11") ungeprüft. Bei 8 Proben in Folge
                // ist ein echter Shader-Kompilier-Kipp erkennbar, legitime Nacht-Helligkeit
                // durchlaeuft die volle Runde (Zyklus haelt progress 0.0000, r107-f6).
                if (brightStreak >= 8)
                {
                    Debug.Log("[J5] Night-WarmUp endhaeltig hell nach " + probe + " Proben");
                    break;
                }
                yield return new UnityEngine.WaitForSecondsRealtime(1.5f);
            }
            Vector3 nightStats = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-night.png", fixedExposure);
            cycle.SetHoldDayProgressForTests(false); // LICHT1c6: Zyklus nicht haengen lassen.
            Debug.Log("[J5-night] Mittel " + nightStats.x.ToString("F1") + " Max " + nightStats.y.ToString("F0") + " Magenta " + nightStats.z.ToString("F0") + " Raw " + lastRawLumaMean.ToString("E2"));
            // LICHT1c6 Attributions-Probe (einmalig, kein Assert): der klassische Nachtrahmen
            // reagierte NICHT auf Nebelfarbe (r108 f3 dim 0.08 vs. f4 dim 0.10: Raw bitgleich
            // 1.2E-3) und auch nicht auf den Himmel-Multiplikator (0.003 vs 0.010). Der helle
            // Horizont-Streifen ist mondbeleuchtete Geometrie durch die Dach-Oeffnungen.
            // Diese Probe trennt die Lichtquellen: Rahmen 1 = Volumennebel AN (Spiel-Modus,
            // HDRP-VolumetricFog-Streuung), Rahmen 2 = klassischer Nebel ganz AUS. Danach wird
            // der Ausgangszustand (Volumen aus, RenderSettings.fog aus) wiederhergestellt.
            {
                cycle.SetHoldDayProgressForTests(true);
                bool fogWasOn = RenderSettings.fog;
                RenderSettings.fog = true; // Zyklus schreibt Farbe/Dichte ab jetzt wieder absolut.
                cycle.SetVolumetricFogForTests(true);
                for (int settle = 0; settle < 6; settle++) yield return null; // Shader-Variante + Zyklus-Write.
                Vector3 volStats = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-night-vol.png", fixedExposure);
                Debug.Log("[J5-night-vol] (Volumennebel AN) Mittel " + volStats.x.ToString("F1")
                    + " Max " + volStats.y.ToString("F0") + " Raw " + lastRawLumaMean.ToString("E2"));
                cycle.SetVolumetricFogForTests(false);
                RenderSettings.fog = false; // klassischer Nebel aus: pur geometry.
                for (int settle = 0; settle < 3; settle++) yield return null;
                Vector3 noFogStats = SaveFrameFixed(cam, 480, 270, "Logs/ci/j5-night-nofog.png", fixedExposure);
                Debug.Log("[J5-night-nofog] (Nebel AUS) Mittel " + noFogStats.x.ToString("F1")
                    + " Max " + noFogStats.y.ToString("F0") + " Raw " + lastRawLumaMean.ToString("E2"));
                RenderSettings.fog = fogWasOn;
                cycle.SetHoldDayProgressForTests(false);
            }

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
