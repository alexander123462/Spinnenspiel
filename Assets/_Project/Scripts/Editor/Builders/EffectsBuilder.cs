using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>Baut die Partikelsysteme für Fußeffekte (Wasserringe, Staub) und hängt sie an die Spinne.</summary>
    public static class EffectsBuilder
    {
        private const string MaterialFolder = AssetUtil.GeneratedRoot + "/Materials";

        public static void Build(GameObject spider)
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
            {
                Debug.LogError("[Spinnenspiel] URP-Partikelshader nicht gefunden – Fußeffekte werden nicht gebaut.");
                return;
            }

            Texture2D circle = TextureFactory.SoftCircle();
            Material rippleMaterial = ParticleMaterial("MAT_Ripple", particleShader, circle, new Color(0.85f, 0.92f, 0.95f, 0.5f));
            Material dustMaterial = ParticleMaterial("MAT_Dust", particleShader, circle, new Color(0.45f, 0.38f, 0.28f, 0.35f));

            Transform root = new GameObject("FootEffects").transform;
            ParticleSystem ripples = CreateSystem(root, "Ripples", rippleMaterial, true);
            ParticleSystem dust = CreateSystem(root, "Dust", dustMaterial, false);

            SpiderFootEffects effects = spider.AddComponent<SpiderFootEffects>();
            effects.Configure(ripples, dust);
        }

        private static ParticleSystem CreateSystem(Transform parent, string name, Material material, bool flat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            // System läuft dauerhaft (ohne eigene Emission), damit per Emit() erzeugte Partikel simuliert werden.
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.gravityModifier = flat ? 0f : -0.05f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, flat ? AnimationCurve.Linear(0f, 0.3f, 1f, 3f) : AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            return system;
        }

        private static Material ParticleMaterial(string name, Shader shader, Texture2D texture, Color color)
        {
            Material material = new Material(shader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            // Transparent, Alpha-Blending
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            return AssetUtil.ReplaceAsset(material, MaterialFolder + "/" + name + ".mat");
        }
    }
}
