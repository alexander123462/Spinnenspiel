using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>K4b: Tests that a web drips drops while it rains, max 12 per net.</summary>
    public class WebRainDripsTests
    {
        private static OrbWebLayout CreateLayout()
        {
            var anchors = new Vector3[4]
            {
                new Vector3(-0.5f, 1f, -0.5f),
                new Vector3(0.5f, 1f, -0.5f),
                new Vector3(0.5f, 1f, 0.5f),
                new Vector3(-0.5f, 1f, 0.5f),
            };

            return new OrbWebLayout(anchors, 24, 0.025f, 0.04f, 42);
        }

        [UnityTest]
        public IEnumerator WebRainDrips_TropfenAppearWhileRaining()
        {
            var webGo = new GameObject("DripWeb");
            OrbWeb web = webGo.AddComponent<OrbWeb>();
            web.Build(CreateLayout());

            var dripGo = new GameObject("DripWatch");
            WebRainDrips drips = dripGo.AddComponent<WebRainDrips>();
            drips.web = web;
            drips.forcedRain01 = 1f;

            var deadline = Time.time + 12f;
            while (Time.time < deadline)
            {
                yield return null;
            }

            Assert.GreaterOrEqual(drips.TropfenCount, 1,
                "After 12 s of forced rain no Tropfen spawned.");
            Assert.LessOrEqual(drips.TropfenCount, 12,
                "More than 12 Tropfen on one net: " + drips.TropfenCount);

            Object.DestroyImmediate(webGo);
            Object.DestroyImmediate(dripGo);
        }

        [UnityTest]
        public IEnumerator WebRainDrips_NoeTropfenWhenDry()
        {
            var webGo = new GameObject("DryWeb");
            OrbWeb web = webGo.AddComponent<OrbWeb>();
            web.Build(CreateLayout());

            var dripGo = new GameObject("DryWatch");
            WebRainDrips drips = dripGo.AddComponent<WebRainDrips>();
            drips.web = web;
            drips.forcedRain01 = 0f;

            var deadline = Time.time + 6f;
            while (Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(0, drips.TropfenCount,
                "Tropfen spawned while dry: " + drips.TropfenCount);

            Object.DestroyImmediate(webGo);
            Object.DestroyImmediate(dripGo);
        }

        [UnityTest]
        public IEnumerator WebRainDrips_ProofImageShowsDrops()
        {
            var webGo = new GameObject("ProofWeb");
            OrbWeb web = webGo.AddComponent<OrbWeb>();
            web.Build(CreateLayout());

            var dripGo = new GameObject("ProofWatch");
            WebRainDrips drips = dripGo.AddComponent<WebRainDrips>();
            drips.web = web;
            drips.forcedRain01 = 1f;
            drips.dropScale = 0.12f;

            var deadline = Time.time + 8f;
            while (Time.time < deadline)
            {
                yield return null;
            }

            Assert.GreaterOrEqual(drips.TropfenCount, 1, "Proof: no Tropfen to photograph.");

            var camGo = new GameObject("ProofCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(-2.2f, 1.8f, -2.2f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1f, 0f) - cam.transform.position);
            cam.fieldOfView = 50f;

            var rt = new RenderTexture(960, 540, 24);
            rt.Create();
            cam.targetTexture = rt;
            yield return new WaitForSeconds(0.1f);
            SaveFrame(cam, rt, System.IO.Path.Combine(Application.dataPath, "../Logs/ci/nettropfen.png"));

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "../Logs/ci/nettropfen.png")),
                "nettropfen.png fehlt.");

            Object.DestroyImmediate(webGo);
            Object.DestroyImmediate(dripGo);
        }

        private static void SaveFrame(Camera cam, RenderTexture rt, string file)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(file, png);
        }
    }
}
