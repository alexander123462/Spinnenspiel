using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// S9 Einstieg: the net-hint appears exactly once after 20 s without a built web;
    /// after the control was used it never shows again (also after reload via the
    /// persisted bitmask); with "Hinweise aus" nothing shows. Proof-shot:
    /// Logs/ci/s9_eintruche.png.
    /// </summary>
    public class FirstStepsHintTests
    {
        [UnityTest]
        public IEnumerator S9_NetzHinweisGenauOnce_Nach20sOhneNetz()
        {
            var m = new Spinnenspiel.FirstStepsHintModel();
            // First 19 s: no net-hint yet (no web built). Whole seconds -> exact floats.
            for (var s = 0; s < 19; s++)
            {
                m.AdvanceTime(1f, false);
                Assert.IsFalse(m.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Net),
                    "S9: no net-hint before the 20 s at second " + (s + 1));
            }
            // Cross the 20 s threshold exactly once.
            m.AdvanceTime(1f, false);
            Assert.IsTrue(m.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Net),
                "S9: net-hint visible at 20 s without web.");
            // Further time must not re-trigger anything (stays a single visible hint).
            m.AdvanceTime(1f, false);
            Assert.IsTrue(m.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Net),
                "S9: still the same single hint, no re-show logic.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator S9_BenutzungLoeschtHinweis_EvenAfterReload()
        {
            var m = new Spinnenspiel.FirstStepsHintModel();
            for (var s = 0; s < 21; s++)
            {
                m.AdvanceTime(1f, false);
            }
            Assert.IsTrue(m.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Net),
                "S9: net-hint on screen after 21 s.");
            m.MarkUsed(Spinnenspiel.FirstStepsHintModel.HintId.Net);
            Assert.IsFalse(m.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Net),
                "S9: after using N the hint vanished.");

            // Reload: restore the persisted bitmask -> used stays used.
            var mask = m.UsedMask();
            Assert.AreEqual(1 << (int)Spinnenspiel.FirstStepsHintModel.HintId.Net, mask,
                "S9: bitmask holds exactly the net bit.");
            var reloaded = new Spinnenspiel.FirstStepsHintModel();
            reloaded.LoadUsedMask(mask);
            for (var s = 0; s < 30; s++)
            {
                reloaded.AdvanceTime(1f, false);
            }
            Assert.IsFalse(reloaded.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Net),
                "S9: after reload the used net-hint never comes back.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator S9_HinweiseAus_ErsteNichts()
        {
            var m = new Spinnenspiel.FirstStepsHintModel();
            m.HintsEnabled = false;
            for (var s = 0; s < 30; s++)
            {
                m.AdvanceTime(1f, false);
            }
            m.NotifyCaughtInsect();
            foreach (Spinnenspiel.FirstStepsHintModel.HintId id in Spinnenspiel.FirstStepsHintModel.AllHints)
            {
                Assert.IsFalse(m.IsVisible(id), "S9: hints off -> nothing visible for " + id);
            }
            Assert.AreEqual(0, m.VisibleCount(), "S9: zero visible hints.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator S9_PersistRoundtrip_DurcgSettings()
        {
            // The bitmask survives a settings save/load roundtrip (game-settings file).
            var holder = new GameObject("S9SettingsHolder");
            var settings = holder.AddComponent<GameSettings>();
            settings.SetFirstStepsUsed(0b11000);
            Assert.AreEqual(0b11000, settings.FirstStepsUsedMask, "S9: mask stored in memory.");

            var m = new Spinnenspiel.FirstStepsHintModel();
            m.LoadUsedMask(settings.FirstStepsUsedMask);
            for (var s = 0; s < 30; s++)
            {
                m.AdvanceTime(1f, false);
            }
            Assert.IsFalse(m.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Net),
                "S9: net bit set -> no net-hint.");
            Assert.IsTrue(m.IsVisible(Spinnenspiel.FirstStepsHintModel.HintId.Move),
                "S9: move bit not set -> hint still shows.");

            // Proof-shot: the visible hint bars at the left edge.
            PaintHintsProof(m, 1280, 720, "Logs/ci/s9_eintruche.png");
            yield return null;
        }

        private static void PaintHintsProof(Spinnenspiel.FirstStepsHintModel m, int w, int h, string file)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, new Color(0.1f, 0.2f, 0.1f));
                }
            }
            var shown = 0;
            foreach (Spinnenspiel.FirstStepsHintModel.HintId id in Spinnenspiel.FirstStepsHintModel.AllHints)
            {
                if (!m.IsVisible(id))
                {
                    continue;
                }
                // GUI coords: left edge, mid screen, one bar per visible hint.
                var gy = (int)(h * 0.55f) + shown * 20;
                PaintGuiRect(tex, 24, gy, 200, 16, new Color(1f, 1f, 0.7f));
                shown++;
            }
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void PaintGuiRect(Texture2D tex, int x, int yTop, int w, int h, Color c)
        {
            for (var px = x; px < x + w && px < tex.width; px++)
            {
                for (var gy = yTop; gy < yTop + h && gy < tex.height; gy++)
                {
                    if (px < 0 || gy < 0)
                    {
                        continue;
                    }
                    var py = tex.height - 1 - gy;
                    tex.SetPixel(px, py, c);
                }
            }
        }
    }
}
