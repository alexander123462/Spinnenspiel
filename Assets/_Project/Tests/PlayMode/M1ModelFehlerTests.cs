using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// M1 Modellfehler sammeln: fuer jeden Modelltyp (Farn, Riesenblatt, Ast/Stamm, Spinne)
    /// ein Beweisbild aus der Spielperspektive aufnehmen (Test legt PNG unter Logs/ci ab).
    /// Erst Befunde, nicht blind reparieren. Kriterium: fuer jeden Modelltyp existiert ein
    /// Beweisbild -> OK/NOK.
    /// </summary>
    public class M1ModelFehlerTests
    {
        private JungleWorldGenerator readyGen;

        [UnityTest]
        public IEnumerator M1_Modelfehler_BeweisbilderPerModelltyp()
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
            Assert.IsNotNull(readyGen, "M1: Welt (Seed 4242) not ready in time.");

            var spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "M1: SpiderMovement missing in Urwald.");

            // ERST walk: Movement bringt die Spinne auf den Grund und die Kamera im Nah-Mode.
            for (var f = 0; f < 120; f++)
            {
                yield return null;
            }

            var cam = Camera.main;
            Assert.IsNotNull(cam, "M1: no main camera.");

            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f); // Vormittag-Light
            DisableDepthAndMotionBlur();

            // Prime: first RT-Capture per Run is broken in HDRP (cold RT-Path).
            var prime = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            prime.antiAliasing = 4;
            prime.Create();
            cam.targetTexture = prime;
            cam.Render();
            cam.targetTexture = null;
            prime.Release();
            yield return null;

            var spiderPos = spider.transform.position;

            // VIER Modelltyps: je ein Beweisbild aus Spielperspektive.
            CaptureType(cam, spiderPos,
                new[] { "Baumfarn_Leaf", "Grundfarn_Leaf", "FernScatter_Leaf", "MAT_Fern" },
                "Logs/ci/m1_farn.png");
            CaptureType(cam, spiderPos,
                new[] { "Riesenblatt" },
                "Logs/ci/m1_riesenblatt.png");
            CaptureType(cam, spiderPos,
                new[] { "Wood", "Bark" },
                "Logs/ci/m1_aststamm.png");
            CaptureType(cam, spiderPos,
                new[] { "MAT_Spider" },
                "Logs/ci/m1_spinne.png");

            // Kriterium: fuer jeden Modelltyp existiert ein Beweisbild.
            var farn = System.IO.File.Exists("Logs/ci/m1_farn.png");
            var ries = System.IO.File.Exists("Logs/ci/m1_riesenblatt.png");
            var ast = System.IO.File.Exists("Logs/ci/m1_aststamm.png");
            var spin = System.IO.File.Exists("Logs/ci/m1_spinne.png");

            Debug.Log("M1 Modellfehler BeweBilds:");
            Debug.Log("Farn Bild vorhanden -> Kriterium exists -> " + (farn ? "OK" : "NOK"));
            Debug.Log("Riesenblatt Bild vorhanden -> Kriterium exists -> " + (ries ? "OK" : "NOK"));
            Debug.Log("Ast/Stamm Bild vorhanden -> Kriterium exists -> " + (ast ? "OK" : "NOK"));
            Debug.Log("Spinne Bild vorhanden -> Kriterium exists -> " + (spin ? "OK" : "NOK"));

            Assert.True(farn, "M1: Farn proofshot missing.");
            Assert.True(ries, "M1: Riesenblatt proofshot missing.");
            Assert.True(ast, "M1: Ast/Stamm proofshot missing.");
            Assert.True(spin, "M1: Spinne proofshot missing.");

            Debug.Log("[M1] proof shots written: m1_farn / m1_riesenblatt / m1_aststamm / m1_spinne");
        }

        // --- helpers ---

        /// <summary>
        /// Naechsten MeshRenderer whose Material-Name contains one of tags, places the
        /// camera between Spider and that object (Nah-View) and saves a PNG.
        /// Returns false when no such object exists in the scene.
        /// </summary>
        private static void CaptureType(Camera cam, Vector3 spiderPos, string[] tags, string file)
        {
            MeshRenderer best = null;
            var bestDist = float.MaxValue;
            foreach (var mr in GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mats = mr.sharedMaterials;
                if (mats == null)
                {
                    continue;
                }
                var match = false;
                foreach (var m in mats)
                {
                    if (m == null)
                    {
                        continue;
                    }
                    foreach (var tag in tags)
                    {
                        if (m.name.Contains(tag))
                        {
                            match = true;
                            break;
                        }
                    }
                    if (match)
                    {
                        break;
                    }
                }
                if (!match)
                {
                    continue;
                }
                var c = mr.bounds.center;
                var dx = c.x - spiderPos.x;
                var dz = c.z - spiderPos.z;
                var d2 = dx * dx + dz * dz;
                if (d2 < bestDist)
                {
                    bestDist = d2;
                    best = mr;
                }
            }

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();
            if (best != null)
            {
                // Kamera between Spider and the Model, Nah-View onto the Model.
                var target = best.bounds.center;
                var toSpider = spiderPos - target;
                toSpider.y = 0.12f;
                if (toSpider.sqrMagnitude < 0.001f)
                {
                    toSpider = new Vector3(0f, 0.12f, 1f);
                }
                var camPos = target + toSpider.normalized * 1.0f;
                cam.transform.position = camPos;
                cam.transform.LookAt(target + new Vector3(0f, 0.05f, 0f));
            }
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            SaveFrameTo(rt, file);
            rt.Release();
        }

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

        private static void DisableDepthAndMotionBlur()
        {
            var volume = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (volume == null || volume.sharedProfile == null)
            {
                return;
            }
            foreach (var comp in volume.sharedProfile.components)
            {
                var compName = comp.GetType().Name;
                if (compName.EndsWith("DepthOfField") || compName.EndsWith("MotionBlur"))
                {
                    comp.active = false;
                }
            }
        }
    }
}
