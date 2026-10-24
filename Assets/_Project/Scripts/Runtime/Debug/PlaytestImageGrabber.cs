using UnityEngine;
using System;
using System.IO;

namespace Spinnenspiel
{
    /// <summary>
    /// P3: clean playtest image from the player/spider perspective (Q1_Galerie technique:
    /// HDR offscreen render, own exposure + ACES tone map, PNG saved). Also evaluates the
    /// PixelRulesModel on the LDR pixels and appends 'file;PR1;PR2;PR3' to
    /// Logs/playtest/pixel.txt. Returns Vector4(mean, max, magentaPct, sharpFrac).
    /// </summary>
    public class PlaytestImageGrabber
    {
        private const string PixelLog = "Logs/playtest/pixel.txt";

        public static Vector4 CaptureFrame(Camera cam, int w, int h, string file)
        {
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;

            // Q1_Galerie: HDRP-TAA/Verlauf verschmiert Offscreen-Aufnahmen nach Kamerasprung — off.
            var hd = cam.GetComponent("HDAdditionalCameraData");
            if (hd != null)
            {
                var prop = hd.GetType().GetProperty("antialiasing");
                if (prop != null) prop.SetValue(hd, System.Enum.ToObject(prop.PropertyType, 0));
            }

            cam.Render();
            RenderTexture.active = rt;
            var hdr = new Texture2D(w, h, TextureFormat.RGBAHalf, false);
            hdr.ReadPixels(new Rect(0, 0, w, h), 0, 0);
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

            float exposure = Mathf.Clamp(0.55f / Mathf.Exp(sumLog / n), 0.0005f, 50f);

            Color[] outPx = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                float r = Mathf.Max(c.r * exposure, 0f);
                float g = Mathf.Max(c.g * exposure, 0f);
                float b = Mathf.Max(c.b * exposure, 0f);
                outPx[i] = new Color(AcesTone(r), AcesTone(g), AcesTone(b), 1f);
            }

            double sum = 0.0;
            int maxv = 0;
            long magenta = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = outPx[i];
                float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                sum += luma * 255.0;
                int ri = (int)(Mathf.Max(c.r, Mathf.Max(c.g, c.b)) * 255f);
                if (ri > maxv) maxv = ri;
                float R = c.r * 255f, G = c.g * 255f, B = c.b * 255f;
                if (R > 200f && G < 80f && B > 200f) magenta++;
            }

            long sharpHits = 0, sharpTotal = 0;
            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                for (int x = 0; x + 1 < w; x++)
                {
                    Color a = outPx[rowBase + x];
                    Color b2 = outPx[rowBase + x + 1];
                    float la = 0.299f * a.r + 0.587f * a.g + 0.114f * a.b;
                    float lb = 0.299f * b2.r + 0.587f * b2.g + 0.114f * b2.b;
                    if (Mathf.Abs(la - lb) * 255f > 8f) sharpHits++;
                    sharpTotal++;
                }
            }

            var ldr = new Texture2D(w, h, TextureFormat.RGB24, false);
            ldr.SetPixels(outPx);
            ldr.Apply(false);
            byte[] png = ldr.EncodeToPNG();
            DirectoryEnsure(file);
            File.WriteAllBytes(file, png);

            bool pr1 = PixelRulesModel.PR1_WeltUpsideDown(outPx, w, h);
            bool pr2 = PixelRulesModel.PR2_NoGround(outPx, w, h);
            bool pr3 = PixelRulesModel.PR3_NearBlackOrFlat(outPx, w, h);
            try
            {
                if (!File.Exists(PixelLog))
                {
                    File.WriteAllText(PixelLog, "");
                }

                File.AppendAllText(PixelLog, file + ";PR1=" + (pr1 ? "1" : "0")
                    + ";PR2=" + (pr2 ? "1" : "0") + ";PR3=" + (pr3 ? "1" : "0") + "\n");
            }
            catch (Exception)
            {
            }

            UnityEngine.Object.DestroyImmediate(hdr);
            UnityEngine.Object.DestroyImmediate(ldr);
            UnityEngine.Object.DestroyImmediate(rt);

            float mean = (float)(sum / n);
            float sharpFrac = sharpTotal > 0 ? (float)sharpHits / sharpTotal : 0f;
            return new Vector4(mean, (float)maxv, 100f * magenta / n, sharpFrac);
        }

        private static void DirectoryEnsure(string path)
        {
            int cut = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            if (cut > 0)
            {
                System.IO.Directory.CreateDirectory(path.Substring(0, cut));
            }
        }

        // Reflection helpers copied from the proven Q1_Galerie code (HDRP volume params).
        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }

            return null;
        }

        private static void SetParam(UnityEngine.Rendering.VolumeComponent comp, string field, object value)
        {
            var f = comp.GetType().GetField(field);
            if (f == null)
            {
                Debug.Log("[P3] Parameter fehlt: " + comp.GetType().Name + "." + field);
                return;
            }

            var p = f.GetValue(comp) as UnityEngine.Rendering.VolumeParameter;
            p.overrideState = true;
            p.GetType().GetProperty("value").SetValue(p, value);
        }

        private static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
        }

        /// <summary>Q1_Galerie-Technique: global Override-Volume with MotionBlur intensity 0
        /// and DoF/MotionBlur components inactive on every scene volume. Idempotent: reuses
        /// the existing 'PlaytestOverride' GameObject.</summary>
        public static void EnsureCleanVolume()
        {
            foreach (var volume in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null || volume.name == "PlaytestOverride") continue;
                foreach (var comp in volume.sharedProfile.components)
                {
                    string n = comp.GetType().Name;
                    if (n.EndsWith("DepthOfField") || n.EndsWith("MotionBlur")) comp.active = false;
                }
            }

            GameObject go = null;
            foreach (var vol2 in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (vol2.name == "PlaytestOverride")
                {
                    go = vol2.gameObject;
                    break;
                }
            }

            if (go == null)
            {
                go = new GameObject("PlaytestOverride");
            }

            var vol = go.GetComponent<UnityEngine.Rendering.Volume>();
            if (vol == null) vol = go.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true;
            vol.priority = 1000f;
            vol.weight = 1f;
            if (vol.sharedProfile == null)
            {
                var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                vol.sharedProfile = prof;
                var mb = FindType("UnityEngine.Rendering.HighDefinition.MotionBlur");
                if (mb != null) SetParam(prof.Add(mb, false), "intensity", 0f);
            }
        }
    }
}
