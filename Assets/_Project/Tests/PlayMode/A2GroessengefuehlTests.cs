using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// A2 Groessengefuehl: man soll sich als Spinne klein fuehlen.
    /// Messen Kameraabstanze/Hoehe und Graeshoehe in Verhaeltnis zur Spinnenkoerper,
    /// Critera as 'Wert -> Kriterium -> OK/NOK'-Lines, plus a Proof-Shot from
    /// spider-perspective (Logs/ci/urwald_groesse.png).
    /// </summary>
    public class A2GroessengefuehlTests
    {
        private JungleWorldGenerator readyGen;

        [UnityTest]
        public IEnumerator A2_Groessengefuehl_KameraNiedrig_Nah_GraeshoeheVerhaeltnis()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null; // A1-Lehre: LoadScene defert zum naechsten Frame

            var timeout = 60f;
            while (timeout > 0f && readyGen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady)
                {
                    readyGen = g;
                }
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(readyGen, "A2: Welt (Seed 4242) not ready in time.");

            var spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "A2: SpiderMovement missing in Urwald.");

            // ERST walk: Movement brings the SpiderCamera into Nah-Mode (and lands the Spider).
            for (var f = 0; f < 120; f++)
            {
                yield return null;
            }

            var cam = Camera.main;
            Assert.IsNotNull(cam, "A2: no main camera.");

            // Proof-Shot from the in-game near-camera (what the player sees).
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f); // Vormittag-Light
            DisableDepthAndMotionBlur();
            yield return null;
            var veg = Object.FindFirstObjectByType<VegetationSystem>();
            // Prime: first RenderTexture-Capture per Run is broken in HDRP (cold RT-Path).
            var prime = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            prime.antiAliasing = 4;
            prime.Create();
            cam.targetTexture = prime;
            if (veg != null) veg.RenderForCamera(cam);
            cam.Render();
            cam.targetTexture = null;
            prime.Release();
            yield return null;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
            Debug.Log("A2 capture: veg=" + (veg == null ? "NULL" : "OK") + " camPos=" + cam.transform.position + " spiderPos=" + spider.transform.position);
            if (veg != null) veg.RenderForCamera(cam);
            cam.Render();
            SaveFrameTo(rt, "Logs/ci/urwald_groesse.png");
            cam.targetTexture = null;
            rt.Release();
            var col = spider.transform.GetComponent<Collider>();
            float bodyH = 0.13f;
            if (col is SphereCollider sc)
            {
                bodyH = sc.radius * 2f;
            }

            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "A2: no active terrain.");
            Vector3 s = spider.transform.position;
            Vector3 c = cam.transform.position;
            float camDist = (c - s).magnitude;
            float camGroundY = terrain.transform.position.y + terrain.SampleHeight(c);
            float camHoeheUeberBoden = c.y - camGroundY;

            float sumH = 0f;
            int nGrass = 0;
            foreach (var mr in GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var b = mr.bounds;
                float dx = b.center.x - s.x, dz = b.center.z - s.z;
                if (dx * dx + dz * dz > 36f)
                {
                    continue;
                }
                float gy = terrain.transform.position.y + terrain.SampleHeight(b.center);
                float gh = b.max.y - gy;
                if (gh < 0.05f || gh > 1.5f)
                {
                    continue; // neither Debris no Trees
                }
                sumH += gh;
                nGrass++;
            }

            float grassMean = nGrass > 0 ? sumH / nGrass : 0f;
            float ratio = grassMean / Mathf.Max(bodyH, 0.01f);

            Debug.Log("A2 Groessengefuehl messurement:");
            Debug.Log("Graeshoehe middle " + grassMean.ToString("F3") + " m vs body " + bodyH.ToString("F3")
                + " m -> ratio " + ratio.ToString("F2") + " -> Kriterium ratio >= 1.5 -> " + (ratio >= 1.5f ? "OK" : "NOK"));
            Debug.Log("Camera dist to spider " + camDist.ToString("F3") + " m -> Kriterium <= 1.2 m -> "
                + (camDist <= 1.2f ? "OK" : "NOK"));
            Debug.Log("Camera height above ground " + camHoeheUeberBoden.ToString("F3") + " m -> Kriterium <= 0.5 m -> "
                + (camHoeheUeberBoden <= 0.5f ? "OK" : "NOK"));
            Debug.Log("Field of View " + cam.fieldOfView.ToString("F1") + " deg");

            Assert.GreaterOrEqual(ratio, 1.5f,
                "A2: Graeshoehe not >= 1.5x spider body height.");
            Assert.LessOrEqual(camDist, 1.2f, "A2: camera farther than 1.2 m from spider.");
            Assert.LessOrEqual(camHoeheUeberBoden, 0.5f, "A2: camera higher than 0.5 m above ground.");

            Debug.Log("[A2] proof shot written: Logs/ci/urwald_groesse.png");
        }

        /// <summary>Reads the active RenderTexture back and writes PNG to file.</summary>
        private static void SaveFrameTo(RenderTexture rt, string file)
        {
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
        }

        /// <summary>G6-Lehre: for Proof-Shots MotionBlur and DepthOfField switch off.</summary>
        private static void DisableDepthAndMotionBlur()
        {
            var volume = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (volume == null || volume.sharedProfile == null)
            {
                return;
            }
            foreach (var comp in volume.sharedProfile.components)
            {
                var compName = comp.GetType().Name; // HDRP-Components: HDRPDepthOfField etc.
                if (compName == "DepthOfField" || compName == "MotionBlur"
                    || compName.EndsWith("DepthOfField") || compName.EndsWith("MotionBlur"))
                {
                    comp.active = false;
                }
            }
        }
    }
}
