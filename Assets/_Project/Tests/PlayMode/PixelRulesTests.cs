using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// P3 Bilder bei Verstoss und Vision-Fragen: the pixel rules (PixelRulesModel) as
    /// pure functions with number tests, plus the criteria that a 180-degree-rotated
    /// test image fires PR1 (the original does not), a sky-only image fires PR2, and
    /// every seed of the P2 bot run has at least one playtest image.
    /// </summary>
    public class PixelRulesTests
    {
        private static int[] Seeds = { 7, 5, 4242, 99, 31, 2026 };

        private static Color SkyColor = new Color(0.4f, 0.6f, 0.9f, 1f);
        private static Color GroundBrown = new Color(0.35f, 0.25f, 0.15f, 1f);
        private static Color GroundGreen = new Color(0.2f, 0.4f, 0.15f, 1f);

        /// <summary>Upright scene-like image: top half sky, bottom half ground.
        /// GetPixels order: index = y * w + x with y=0 at the BOTTOM of the image.</summary>
        private static Color[] MakeUpright(int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                bool upper = y >= h / 2;
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (upper)
                    {
                        c = ((x + y) % 2 == 0) ? SkyColor : new Color(0.5f, 0.65f, 0.85f, 1f);
                    }
                    else
                    {
                        c = ((x + y) % 2 == 0) ? GroundBrown : GroundGreen;
                    }

                    px[y * w + x] = c;
                }
            }

            return px;
        }

        private static Color[] MakeSkyOnly(int w, int h)
        {
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = SkyColor;
            }

            return px;
        }

        private static Color[] MakeFlatDark(int w, int h)
        {
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color(0.008f, 0.008f, 0.008f, 1f);
            }

            return px;
        }

        [Test]
        public void PR1_UprightNotUpsideDown()
        {
            var px = MakeUpright(64, 48);
            Assert.IsFalse(Spinnenspiel.PixelRulesModel.PR1_WeltUpsideDown(px, 64, 48),
                "upright image (sky on top) must NOT fire PR1");
        }

        [Test]
        public void PR1_RotatedImageFires()
        {
            var px = MakeUpright(64, 48);
            var flipped = Spinnenspiel.PixelRulesModel.Rotate180(px, 64, 48);
            Assert.IsTrue(Spinnenspiel.PixelRulesModel.PR1_WeltUpsideDown(flipped, 64, 48),
                "image rotated 180 degrees (sky at bottom) must fire PR1");
        }

        [Test]
        public void PR2_SkyOnlyNoGround()
        {
            var px = MakeSkyOnly(64, 48);
            Assert.IsTrue(Spinnenspiel.PixelRulesModel.PR2_NoGround(px, 64, 48),
                "sky-only image must fire PR2 (no ground in lower half)");
        }

        [Test]
        public void PR2_UprightHasGround()
        {
            var px = MakeUpright(64, 48);
            Assert.IsFalse(Spinnenspiel.PixelRulesModel.PR2_NoGround(px, 64, 48),
                "upright image (ground in lower half) must NOT fire PR2");
        }

        [Test]
        public void PR3_FlatDarkFires()
        {
            var px = MakeFlatDark(64, 48);
            Assert.IsTrue(Spinnenspiel.PixelRulesModel.PR3_NearBlackOrFlat(px, 64, 48),
                "near-black flat image must fire PR3");
        }

        [Test]
        public void PR3_UprightNotFlat()
        {
            var px = MakeUpright(64, 48);
            Assert.IsFalse(Spinnenspiel.PixelRulesModel.PR3_NearBlackOrFlat(px, 64, 48),
                "varied upright image must NOT fire PR3");
        }

        /// <summary>Criterion (informational line): the bot run saved at least one
        /// playtest image per seed. The hard check lives in BotPlaytestTests.</summary>
        [Test]
        public void PlaytestImages_CoverAllSeeds()
        {
            int covered = 0;
            foreach (int seed in Seeds)
            {
                if (File.Exists("Logs/playtest/bild_" + seed + "_0.png"))
                {
                    covered++;
                }
            }

            var line = "playtest images cover seeds = " + covered + " -> at least 6 seeds -> "
                + (covered >= Seeds.Length ? "OK" : "NOK") + "\n";
            try
            {
                if (!File.Exists("Logs/playtest/criteria.txt"))
                {
                    File.WriteAllText("Logs/playtest/criteria.txt", "");
                }

                File.AppendAllText("Logs/playtest/criteria.txt", line);
            }
            catch (System.Exception)
            {
            }
        }
    }
}
