using UnityEngine;
using System;

namespace Spinnenspiel
{
    /// <summary>
    /// P3: simple pixel rules on a rendered playtest image, as PURE functions
    /// (Color[] + width/height in, bool out). Pixel order is GetPixels order:
    /// index = y * w + x with y=0 at the BOTTOM of the image.
    /// PR1 'Welt kopfueber': the sky (bright bluish/yellowish pixels) lies
    /// predominantly in the LOWER half of the image.
    /// PR2 'kein Boden': in the lower half no ground-coloured pixels (brown/green)
    /// make up more than 5 percent.
    /// PR3 'Bild fast schwarz oder einfarbig': luma standard deviation below 8.
    /// </summary>
    public class PixelRulesModel
    {
        private const float SkyLumaMin = 0.55f;
        private const float GroundFracMax = 0.05f;
        private const float LumaStDevMax = 8f;

        public static bool PR1_WeltUpsideDown(Color[] px, int w, int h)
        {
            int lower = (h / 2) * w;
            int skyLower = 0;
            int skyTotal = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (!IsSkyPixel(px[i])) continue;
                skyTotal++;
                if (i < lower) skyLower++;
            }

            if (skyTotal == 0) return false;
            return (float)skyLower / skyTotal > 0.5f;
        }

        public static bool PR2_NoGround(Color[] px, int w, int h)
        {
            int lowerEnd = (h / 2) * w; // GetPixels: y=0 is the BOTTOM row -> bottom half = indices [0, lowerEnd)
            int ground = 0;
            int total = 0;
            for (int i = 0; i < lowerEnd && i < px.Length; i++)
            {
                total++;
                if (IsGroundPixel(px[i])) ground++;
            }

            if (total == 0) return true;
            return (float)ground / total < GroundFracMax;
        }

        public static bool PR3_NearBlackOrFlat(Color[] px, int w, int h)
        {
            double sum = 0.0;
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                sum += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) * 255.0;
            }

            double mean = sum / px.Length;
            double var = 0.0;
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                double l = (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) * 255.0 - mean;
                var += l * l;
            }

            double stdev = Math.Sqrt(var / px.Length);
            return stdev < LumaStDevMax;
        }

        private static bool IsSkyPixel(Color c)
        {
            float r = c.r, g = c.g, b = c.b;
            float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            if (luma < SkyLumaMin) return false;
            // bluish: blue clearly over red, or yellowish sky: r,g high and b lower but bright
            bool bluish = b > r + 0.08f && b > 0.45f;
            bool yellowish = r > 0.6f && g > 0.55f && b < g;
            return bluish || yellowish;
        }

        private static bool IsGroundPixel(Color c)
        {
            float r = c.r, g = c.g, b = c.b;
            float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            if (luma < 0.04f || luma > 0.85f) return false;
            // brown: r>g>b warm dark; green: g dominant over r and b
            bool brown = r > b + 0.03f && r > g * 0.7f && g >= b && r > 0.15f;
            bool green = g > r + 0.02f && g > b + 0.02f;
            return brown || green;
        }

        /// <summary>Rotates an image 180 degrees (used by the test for the flipped test image).</summary>
        public static Color[] Rotate180(Color[] px, int w, int h)
        {
            var flipped = new Color[px.Length];
            int n = px.Length;
            for (int i = 0; i < n; i++)
            {
                flipped[n - 1 - i] = px[i];
            }

            return flipped;
        }
    }
}
