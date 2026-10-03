using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Stellt alle .mat-Dateien unter Assets/_Project von URP- auf HDRP-Shader um
    /// (HDRP-Umstieg, Schritt 2a). URP: Universal Render Pipeline/Lit, Simple Lit, Unlit,
    /// Particles/Unlit. Eigene Shader (Spinnenspiel/Foliage, Spinnenspiel/Water) bleiben unangetastet.
    /// Werte werden aus den URP-Properties gelesen und auf die HDRP-Counterparts übertragen.
    /// </summary>
    public static class UrpToHdrpMaterials
    {
        [MenuItem("Spinnenspiel/URP-Materialien nach HDRP")]
        public static void Run()
        {
            Shader lit = Shader.Find("HDRP/Lit");
            Shader unlit = Shader.Find("HDRP/Unlit");
            if (lit == null || unlit == null)
            {
                Debug.LogError("[HDRP-Mat] HDRP/Lit oder HDRP/Unlit nicht gefunden - HDRP-Paket fehlt?");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });
            int converted = 0;
            int skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                    continue;

                string shaderName = m.shader != null ? m.shader.name : "";
                if (!shaderName.StartsWith("Universal Render Pipeline/"))
                {
                    skipped++;
                    Debug.Log($"[HDRP-Mat] uebersprungen: {path} ({shaderName})");
                    continue;
                }

                bool isUnlit = shaderName.EndsWith("/Unlit"); // erfasst .../Unlit und .../Particles/Unlit

                // URP-Werte VOR dem Shader-Wechsel auslesen (HasProperty-Guards).
                Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                Vector2 baseMapTiling = m.HasProperty("_BaseMap") ? m.GetTextureScale("_BaseMap") : Vector2.one;
                Vector2 baseMapOffset = m.HasProperty("_BaseMap") ? m.GetTextureOffset("_BaseMap") : Vector2.zero;
                Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                Texture bumpMap = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
                float smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.5f;
                float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                bool transparent = m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f;
                bool alphaClip = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f;
                float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
                bool doubleSided = m.HasProperty("_Cull") && Mathf.Abs(m.GetFloat("_Cull")) < 0.5f;

                if (isUnlit)
                {
                    m.shader = unlit;
                    if (baseMap != null)
                    {
                        m.SetTexture("_UnlitColorMap", baseMap);
                        m.SetTextureScale("_UnlitColorMap", baseMapTiling);
                        m.SetTextureOffset("_UnlitColorMap", baseMapOffset);
                    }
                    m.SetColor("_UnlitColor", baseColor);
                }
                else
                {
                    m.shader = lit;
                    if (baseMap != null)
                    {
                        m.SetTexture("_BaseColorMap", baseMap);
                        m.SetTextureScale("_BaseColorMap", baseMapTiling);
                        m.SetTextureOffset("_BaseColorMap", baseMapOffset);
                    }
                    m.SetColor("_BaseColor", baseColor);
                    if (bumpMap != null)
                    {
                        m.SetTexture("_NormalMap", bumpMap);
                        m.SetFloat("_NormalScale", bumpScale);
                    }
                    m.SetFloat("_Smoothness", smoothness);
                    m.SetFloat("_Metallic", metallic);
                    if (emission.maxColorComponent > 0.001f)
                        m.SetColor("_EmissiveColor", emission);
                }

                // Oberflächeneigenschaften (gelten für beide Ziel-Shader).
                if (alphaClip)
                {
                    m.SetFloat("_AlphaCutoffEnable", 1f);
                    m.SetFloat("_AlphaCutoff", cutoff);
                }
                if (transparent)
                    m.SetFloat("_SurfaceType", 1f);
                if (doubleSided)
                    m.SetFloat("_DoubleSidedEnable", 1f);

                // HDRP-Materialkeywords aus den Properties neu ableiten.
                HDShaderUtils.ResetMaterialKeywords(m);
                EditorUtility.SetDirty(m);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[HDRP-Mat] umgestellt {converted}, uebersprungen {skipped}");
        }
    }
}
