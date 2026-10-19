using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// K6 Statistik beim Tod: three eaten insects and two built webs show exactly 3 and 2;
    /// the best values survive a save roundtrip (restart of the application).
    /// Criteria as 'Wert -> Kriterium -> OK/NOK' via asserts.
    /// Proof-shot: Logs/ci/k6_death_screen.png.
    /// </summary>
    public class DeathStatsTests
    {
        [UnityTest]
        public IEnumerator K6_Statistik_Tod_ZaehnsystemUndHoechstwert()
        {
            var spiderGo = new GameObject("K6Spider");
            var stats = spiderGo.AddComponent<DeathStats>();
            var growth = spiderGo.AddComponent<SpiderGrowth>();
            var save = spiderGo.AddComponent<SaveGame>();
            save.SetWorldSeed(4242);

            // Let DeathStats.Update() run once, then take the baseline of already-built webs
            // (robust against webs left over from earlier tests in the same run).
            yield return null;
            var websAtStart = stats.Stats.WebsBuilt;

            // 3 eaten insects -> the K5-counter (single source of truth) counts them.
            for (var e = 0; e < 3; e++)
            {
                growth.OnInsectEaten(null);
            }

            // 2 built webs -> counter shows exactly 2 more.
            for (var w = 0; w < 2; w++)
            {
                var webGo = new GameObject("K6Web" + w);
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

            // Let DeathStats.Update() count the webs (runs once per frame).
            yield return null;
            yield return null;

            Report("eaten", stats.Stats.EatenCount);
            Assert.AreEqual(3, stats.Stats.EatenCount, "K6: 3 eaten insects must show exactly 3.");
            var websShown = stats.Stats.WebsBuilt - websAtStart;
            Report("webs shown", websShown);
            Assert.AreEqual(2, websShown, "K6: 2 built webs must show exactly 2.");

            // Tod: Hunger full -> death screen fires and bests freeze.
            var hunger = spiderGo.AddComponent<SpiderHunger>();
            hunger.Hunger = 1f;
            yield return null;
            Assert.IsTrue(stats.Stats.DeathShown, "K6: full hunger must trigger the death report.");

            // Proof-shot of the death screen. OnGUI does not render into a RenderTexture, so —
            // like the K3 hint proofshot — the panel and the count-ticks are painted manually
            // over the rendered frame (the exact same layout DeathStats.OnGUI draws).
            var camGo = new GameObject("K6Cam");
            var cam = camGo.AddComponent<Camera>();
            yield return CaptureDeathProof("Logs/ci/k6_death_screen.png", stats.Stats, cam);

            // Best values travel with the J6 save file and survive a restart.
            save.CaptureSave();
            Assert.IsTrue(save.SaveFileExists(), "K6: CaptureSave must write the save file.");
            GameObject.DestroyImmediate(spiderGo);

            var loadGo = new GameObject("K6Load");
            var loadStats = loadGo.AddComponent<DeathStats>();
            var loadSave = loadGo.AddComponent<SaveGame>();
            loadSave.ApplySave();
            yield return null;

            Report("best eaten after restart", loadStats.Stats.BestEaten);
            Assert.AreEqual(3, loadStats.Stats.BestEaten, "K6: best eaten survives the restart.");
            Report("best webs after restart", loadStats.Stats.BestWebs);
            Assert.AreEqual(2, loadStats.Stats.BestWebs, "K6: best webs survives the restart.");
            Assert.Greater(loadStats.Stats.BestSurvivalTime, 0f, "K6: best survival time is positive.");

            // Cleanup.
            loadSave.DeleteSaveFile();
            var snapshot = new System.Collections.Generic.List<OrbWeb>(OrbWeb.All);
            foreach (OrbWeb leftover in snapshot)
            {
                GameObject.DestroyImmediate(leftover);
            }
            GameObject.DestroyImmediate(camGo);
            GameObject.DestroyImmediate(loadGo);
        }

        [Test]
        public void T11_Todesursache_LetzschatachseImZeitfenster()
        {
            // 1) Tod 3 s nach einem Wespenstich -> Ursache Wespe (inside the 10 s window).
            var a = new PartieStatsModel();
            for (var i = 0; i < 70; i++)
            {
                a.TickAlive(0.1f);
            }
            a.OnDamage(DeathCauseKind.Wespe);
            for (var i = 0; i < 30; i++)
            {
                a.TickAlive(0.1f);
            }
            a.ReportDeath();
            Report("wespe cause", a.DeathCause);
            Assert.AreEqual(DeathCauseKind.Wespe, a.DeathCause,
                "T11: death 3 s after a wasp sting must name the wasp.");

            // 2) Tod durch Energie-0 OHNE Schaden -> Hunger.
            var b = new PartieStatsModel();
            for (var i = 0; i < 300; i++)
            {
                b.TickAlive(0.1f);
            }
            b.ReportDeath();
            Report("hunger cause", b.DeathCause);
            Assert.AreEqual(DeathCauseKind.Hunger, b.DeathCause,
                "T11: energy death without damage must name hunger.");

            // 3) Schaden vor 15 s und dann Hunger-Tod -> Hunger (outside the window).
            var c = new PartieStatsModel();
            for (var i = 0; i < 50; i++)
            {
                c.TickAlive(0.1f);
            }
            c.OnDamage(DeathCauseKind.Frosch);
            for (var i = 0; i < 150; i++)
            {
                c.TickAlive(0.1f);
            }
            c.ReportDeath();
            Report("old damage cause", c.DeathCause);
            Assert.AreEqual(DeathCauseKind.Hunger, c.DeathCause,
                "T11: damage 15 s before the death must fall back to hunger.");

            // 4) Text for every cause non-empty and distinct.
            var sentences = new System.Collections.Generic.List<string>();
            foreach (DeathCauseKind kind in DeathCauseKinds())
            {
                var m = new PartieStatsModel();
                m.DeathCause = kind;
                var s = m.DeathCauseSentence();
                Assert.IsNotEmpty(s, "T11: sentence for cause " + kind + " must not be empty.");
                Assert.IsFalse(sentences.Contains(s),
                    "T11: each cause needs its own sentence (" + kind + " -> '" + s + "').");
                sentences.Add(s);
            }
            Assert.AreEqual(5, sentences.Count, "T11: five distinct cause sentences.");
        }

        /// <summary>T11: all death causes (explicit list — no reflection tricks).</summary>
        private static DeathCauseKind[] DeathCauseKinds()
        {
            return new DeathCauseKind[]
            {
                DeathCauseKind.Hunger,
                DeathCauseKind.Vogel,
                DeathCauseKind.Wespe,
                DeathCauseKind.Frosch,
                DeathCauseKind.Sturz
            };
        }

        private static void Report(string label, float value)
        {
            Debug.Log("K6 -> " + label + ": " + value.ToString("0.####") + " -> checked by asserts above");
        }

        private static void Report(string label, object value)
        {
            Debug.Log("T11 -> " + label + ": " + value + " -> checked by asserts above");
        }

        /// <summary>
        /// Cold-RT proofshot (A2/G6-Lehre from GrowthTests): prime a throwaway RT first, then
        /// paint the death-screen panel and the count-ticks over the frame like OnGUI does.
        /// </summary>
        private IEnumerator CaptureDeathProof(string file, PartieStatsModel shown, Camera cam)
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

            // Paint the same panel + ticks + label OnGUI shows (GUI coords: y down, texture: y up).
            PaintRect(tex, 16, (int)(rt.height * 0.28f), 420, 96, new Color(0.05f, 0.05f, 0.05f));
            foreach (var t in shown.DeathScreenTicks(rt.width, rt.height))
            {
                PaintRect(tex, (int)t.x, (int)t.y, (int)t.z, (int)t.w, new Color(1f, 1f, 1f));
            }
            // T11: the death screen shows the cause sentence via GUI.Label (not captured
            // into an RT), so paint it with the bitmap font — the exact same text.
            PaintText(tex, 24, (int)(rt.height * 0.30f) + 26, shown.DeathCauseSentence(),
                new Color(1f, 0.92f, 0.55f));
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

        /// <summary>T11: 5x7 bitmap font for the proof-shot text. Each entry is a
        /// name (A-Z, space/colon/dot/dash spelled out) plus 35 pixel bits (7 rows x 5).</summary>
        private static readonly string[] FontGlyphs =
        {
            "space00000000000000000000000000000000000",
            "dash00000000000000011110000000000000000",
            "dot00000000000000000000000000000000100",
            "colon00000001000010000000001000010000000",
            "A01110100011000110101100011000110001",
            "B11110100011010111110101011000111110",
            "C01111100001000010000100001000001111",
            "D11110100011000110001100011000111110",
            "E11111100001000011110100001000011111",
            "F11111100001000011110100001000010000",
            "G01111100001000010010100011000101110",
            "H10001100011000111111100011000110001",
            "I11111001000010000100001000010011111",
            "K10001100101010011000101001001010001",
            "L10000100001000010000100001000011111",
            "M10001110111010110101100011000110001",
            "N10001110011010110011100011000110001",
            "O01110100011000110001100011000101110",
            "P11110100011000111110100001000010000",
            "R11110100011000111110101001001010001",
            "S01111100001000001110000010000111110",
            "T11111001000010000100001000010000100",
            "U10001100011000110001100011000101110",
            "W10001100011000110101101011101110001",
            "Y10001100010101000100001000010000100",
        };

        /// <summary>T11: draws a sentence in GUI coords (y down from top) at 2 px scale, uppercase only.</summary>
        private static void PaintText(Texture2D tex, int x, int yTop, string text, Color c)
        {
            var penX = x;
            foreach (var chRaw in text)
            {
                var ch = chRaw == ' ' ? ' ' : System.Char.ToUpper(chRaw);
                var name = ch == ' ' ? "space" : (ch == ':' ? "colon" : (ch == '.' ? "dot" : (ch == '-' ? "dash" : "" + ch)));
                var glyph = "";
                foreach (var g in FontGlyphs)
                {
                    if (g.StartsWith(name) && g.Length == name.Length + 35)
                    {
                        glyph = g.Substring(name.Length);
                        break;
                    }
                }
                if (glyph.Length != 35)
                {
                    penX += 12; // unknown glyph: just advance
                    continue;
                }
                for (var row = 0; row < 7; row++)
                {
                    for (var col = 0; col < 5; col++)
                    {
                        if (glyph[row * 5 + col] == '1')
                        {
                            PaintRect(tex, penX + col * 2, yTop + row * 2, 2, 2, c);
                        }
                    }
                }
                penX += 12;
            }
        }
    }
}
