using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Spinnenspiel.Rendering
{
    /// <summary>
    /// Erzeugt HDRP-Materialien (HDRP/Lit, HDRP/Unlit) und stellt Pipeline-Eigenschaften
    /// in den passenden HDRP-Property-Namen ein. Jede Methode validiert das Material
    /// am Ende per HDMaterial, damit HDRP keine unzulässigen Werte stehen lässt.
    /// </summary>
    public static class PipelineMaterials
    {
        /// <summary>Erzeugt ein HDRP/Lit-Material mit Basisfarbe und optionaler Albedo-Textur.</summary>
        public static Material Lit(Color c, Texture albedo = null)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new System.InvalidOperationException("[Spinnenspiel] Shader nicht gefunden: HDRP/Lit");
            }

            Material m = new Material(shader);
            m.SetColor("_BaseColor", c);
            if (albedo != null)
            {
                m.SetTexture("_BaseColorMap", albedo);
            }

            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Erzeugt ein HDRP/Unlit-Material mit Farbe und optionaler Textur.</summary>
        public static Material Unlit(Color c, Texture tex = null)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                throw new System.InvalidOperationException("[Spinnenspiel] Shader nicht gefunden: HDRP/Unlit");
            }

            Material m = new Material(shader);
            m.SetColor("_UnlitColor", c);
            if (tex != null)
            {
                m.SetTexture("_UnlitColorMap", tex);
            }

            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Setzt die Normal-Map (HDRP: _NormalMap + _NormalScale).</summary>
        public static void SetNormalMap(Material m, Texture n, float scale = 1f)
        {
            if (n != null)
            {
                m.SetTexture("_NormalMap", n);
                m.SetFloat("_NormalScale", scale);
            }

            HDMaterial.ValidateMaterial(m);
        }

        /// <summary>Setzt die Härte (HDRP: _Smoothness).</summary>
        public static void SetSmoothness(Material m, float s)
        {
            m.SetFloat("_Smoothness", s);
            HDMaterial.ValidateMaterial(m);
        }

        /// <summary>Transparent ein/aus (HDRP: _SurfaceType 1 = transparent, 0 = opak).</summary>
        public static void SetTransparent(Material m, bool on)
        {
            m.SetFloat("_SurfaceType", on ? 1f : 0f);
            m.SetOverrideTag("RenderType", on ? "Transparent" : "Opaque");
            if (on)
            {
                m.renderQueue = 3000;
            }

            HDMaterial.ValidateMaterial(m);
        }

        /// <summary>Alpha-Clipping mit Schwellwert (HDRP: _AlphaCutoffEnable + _AlphaCutoff).</summary>
        public static void SetAlphaClip(Material m, float cutoff)
        {
            m.SetFloat("_AlphaCutoffEnable", 1f);
            m.SetFloat("_AlphaCutoff", cutoff);
            HDMaterial.ValidateMaterial(m);
        }

        /// <summary>Doppelseitiges Rendern ein/aus (HDRP: _DoubleSidedEnable).</summary>
        public static void SetDoubleSided(Material m, bool on)
        {
            m.SetFloat("_DoubleSidedEnable", on ? 1f : 0f);
            HDMaterial.ValidateMaterial(m);
        }
    }
}
