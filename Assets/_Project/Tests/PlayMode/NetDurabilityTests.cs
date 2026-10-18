using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// S6 Netz-Verschleiss: 13 catches tear the net (100/8), repair costs exactly 5 energy
    /// and sets 100, rain loses 1 % per 10 s (+-0.1), a torn net catches nothing anymore.
    /// Proof-shot: Logs/ci/s6_net_color.png (fresh white vs worn yellowish vs torn invisible).
    /// </summary>
    public class NetDurabilityTests
    {
        private static OrbWebLayout CreateLayout()
        {
            var anchors = new[]
            {
                new Vector3(-0.4f, 0f, 0f),
                new Vector3(0.4f, 0f, 0f),
                new Vector3(0.45f, 0.8f, 0f),
                new Vector3(-0.45f, 0.8f, 0f)
            };
            return new OrbWebLayout(anchors, 24, 0.025f, 0.04f, 42);
        }

        private static OrbWeb BuildFinishedWeb(Vector3 at)
        {
            var go = new GameObject("DurWeb");
            go.transform.position = at;
            var web = go.AddComponent<OrbWeb>();
            web.Build(CreateLayout());
            web.SetProgress(OrbWeb.WebStage.Done, 1f);
            return web;
        }

        [Test]
        public void S6_13FaengenReisstDasNetz()
        {
            var web = BuildFinishedWeb(Vector3.zero);
            Assert.AreEqual(100f, web.Durability.Durability, "S6: fresh net at 100 %.");
            for (int i = 0; i < 12; i++)
            {
                web.ApplyCatch();
            }
            Report("after 12 catches", web.Durability.Durability);
            Assert.AreEqual(4f, web.Durability.Durability, "S6: 12 catches leave 4 %.");
            Assert.IsTrue(web.CanCatch, "S6: after 12 catches the net still catches.");
            web.ApplyCatch();
            Report("after 13 catches", web.Durability.Durability);
            Assert.IsTrue(web.Durability.Torn, "S6: 13th catch brings durability below 0.");
            Assert.IsFalse(web.CanCatch, "S6: torn net catches nothing anymore.");
            GameObject.DestroyImmediate(web.transform.gameObject);
        }

        [Test]
        public void S6_ReparaturKostenau5EnerguieUndSett100()
        {
            var spiderGo = new GameObject("RepairSpider");
            var energy = spiderGo.AddComponent<SpiderEnergy>();
            var web = BuildFinishedWeb(new Vector3(5f, 0f, 0f));
            for (int i = 0; i < 12; i++)
            {
                web.ApplyCatch();
            }
            Assert.AreEqual(4f, web.Durability.Durability, "S6: worn to 4 %.");
            bool ok = energy.SpendEnergy(NetDurabilityModel.repairEnergyCost);
            Report("energy after repair", energy.Energy);
            Assert.IsTrue(ok, "S6: repair spends exactly 5 energy (100 -> 95).");
            Assert.AreEqual(95f, energy.Energy, "S6: exactly 5 energy spent.");
            web.Repair();
            Report("durability after repair", web.Durability.Durability);
            Assert.AreEqual(100f, web.Durability.Durability, "S6: repair sets durability to 100 %.");
            Assert.IsFalse(web.Durability.Torn, "S6: repaired net is not torn.");
            GameObject.DestroyImmediate(spiderGo);
            GameObject.DestroyImmediate(web.transform.gameObject);
        }

        [Test]
        public void S6_RegenVerlustPro10s()
        {
            var web = BuildFinishedWeb(Vector3.zero);
            Assert.AreEqual(100f, web.Durability.Durability, "S6: fresh net at 100 %.");
            // Simulate 10 s of full rain through the OrbWeb coupling (dt=1 s steps, full intensity).
            for (int i = 0; i < 10; i++)
            {
                web.TickRain(1f, 1f);
            }
            Report("durability after 10 s full rain", web.Durability.Durability);
            var lost = 100f - web.Durability.Durability;
            Report("rain loss in that window", lost);
            Assert.Greater(lost, 0.9f, "S6: rain wears the net down.");
            Assert.Less(lost, 1.1f, "S6: rain loses 1 % per 10 s (+-0.1).");
            Assert.IsFalse(web.Durability.Torn, "S6: after 10 s rain the net is still intact.");
            GameObject.DestroyImmediate(web.transform.gameObject);
        }

        private static void Report(string label, float value)
        {
            Debug.Log("S6NET " + label + "=" + value.ToString("0.###"));
        }

        // G6-Lehre: offscreen cam.Render() under HDRP skips post-processing, so a plain ARGB32
        // readback renders the HDRP-transparent threads black. An HDR RT (ARGBHalf) + geometric-mean
        // auto-exposure + ACES tonemap reproduces what the game shows on screen.
        private static void SaveFrameHdr(Camera cam, int w, int h, string file)
        {
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
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
            float exposure = 0.55f / Mathf.Exp(sumLog / n);
            exposure = Mathf.Clamp(exposure, 0.0005f, 50f);

            Color[] outPx = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                float r = Mathf.Max(c.r * exposure, 0f);
                float g = Mathf.Max(c.g * exposure, 0f);
                float b = Mathf.Max(c.b * exposure, 0f);
                outPx[i] = new Color(AcesTone(r), AcesTone(g), AcesTone(b), 1f);
            }

            var ldr = new Texture2D(w, h, TextureFormat.RGB24, false);
            ldr.SetPixels(outPx);
            ldr.Apply(false);
            System.IO.File.WriteAllBytes(file, ldr.EncodeToPNG());
            Object.DestroyImmediate(hdr);
            Object.DestroyImmediate(ldr);
            Object.DestroyImmediate(rt);
        }

        private static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
        }

        /// <summary>
        /// Proof-shot: three nets side by side — fresh (white), worn 40 % (yellowish),
        /// torn (threads gone). PNG at Logs/ci/s6_net_color.png.
        /// </summary>
        [UnityTest]
        public IEnumerator S6_FaedenZeigHaltbarkeits()
        {
            var fresh = BuildFinishedWeb(new Vector3(-0.62f, 0f, 0f));
            var worn = BuildFinishedWeb(new Vector3(0f, 0f, 0f));
            for (int i = 0; i < 6; i++)
            {
                worn.ApplyCatch();
            }
            var torn = BuildFinishedWeb(new Vector3(0.62f, 0f, 0f));
            for (int i = 0; i < 13; i++)
            {
                torn.ApplyCatch();
            }

            var camGo = new GameObject("ProofCam");
            var cam = camGo.AddComponent<Camera>();
            // Front view of the vertical webs (anchors at z=0).
            cam.transform.position = new Vector3(0f, 0.42f, -1.9f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.08f, 1f));
            cam.fieldOfView = 50f;

            // Let LateUpdate apply the durability tints.
            for (int i = 0; i < 6; i++)
            {
                yield return null;
            }

            // G6-Lehre: offscreen cam.Render() under HDRP needs an HDR RT (ARGBHalf) + auto-exposure
            // + ACES tonemap, otherwise a plain ARGB32 readback renders the threads black.
            SaveFrameHdr(cam, 640, 360, "Logs/ci/s6_net_color.png");

            Assert.IsTrue(torn.Durability.Torn, "S6: proof net torn.");
            Assert.IsFalse(fresh.Durability.Torn, "S6: fresh net intact.");

            // Ground-truth of the visible colour: the thread material's tint after LateUpdate.
            // Fresh (100 %) is near-white; worn (52 %) is yellowish (blue channel clearly lower).
            var freshLine = fresh.GetComponentsInChildren<LineRenderer>()[0];
            var wornLine = worn.GetComponentsInChildren<LineRenderer>()[0];
            Color fc = freshLine.material.color;
            Color wc = wornLine.material.color;
            Report("fresh_b", fc.b);
            Report("worn_b", wc.b);
            Assert.Greater(fc.b, 0.9f, "S6: fresh threads near-white (blue high).");
            Assert.Less(wc.b, fc.b - 0.1f, "S6: worn threads more yellow than fresh (blue lower).");

            GameObject.DestroyImmediate(camGo);
            GameObject.DestroyImmediate(fresh.transform.gameObject);
            GameObject.DestroyImmediate(worn.transform.gameObject);
            GameObject.DestroyImmediate(torn.transform.gameObject);
        }
    }
}
