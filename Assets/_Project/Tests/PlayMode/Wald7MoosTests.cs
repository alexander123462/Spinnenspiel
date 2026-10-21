using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;

namespace Spinnenspiel.Tests
{
    /// <summary>WALD7: Moos at tree bases (bottom 1.5 m, fading up) and on top of Totholz.
    /// Criteria per line 'Value -> Kriterium -> OK/NOK' in the log.</summary>
    public class Wald7MoosTests
    {
        [UnityTest]
        public IEnumerator Moos_AtTrunkBasesAndOnTotholz()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            JungleWorldGenerator gen = null;
            float timeout = 15f;
            while (timeout > 0f && gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(gen != null && gen.IsReady, "World not ready in time.");

            Debug.Log(string.Format("[WALD7] MoosShellCount={0} -> Kriterium > 0 -> {1}",
                gen.MoosShellCount, gen.MoosShellCount > 0 ? "OK" : "NOK"));
            // Stamm-Fuss-Huellen sind derzeit aus (standen als gruene Boxen frei in der Welt, 2026-10-21): nur pruefen, wenn eingeschaltet.
            if (gen.moosShellsOn) Assert.Greater(gen.MoosShellCount, 0, "No moos shells placed.");
            Debug.Log(string.Format("[WALD7] MoosCardCount={0} -> Kriterium > 0 -> {1}",
                gen.MoosCardCount, gen.MoosCardCount > 0 ? "OK" : "NOK"));
            Assert.Greater(gen.MoosCardCount, 0, "No moos cards placed on Totholz.");

            // ONE shared material for all moos objects (no new material per object).
            var mats = new Dictionary<Material, int>();
            int renderers = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.name.Contains("Moos")) continue;
                renderers++;
                int n; mats.TryGetValue(r.sharedMaterial, out n);
                mats[r.sharedMaterial] = n + 1;
            }
            Debug.Log(string.Format("[WALD7] distinct moos materials={0} -> Kriterium = 1 -> {1}",
                mats.Count, mats.Count == 1 ? "OK" : "NOK"));
            Assert.AreEqual(1, mats.Count, "Moos uses more than one material.");

            // Shell height at the trunk base: >= ~1.3 m (config 1.5 m).
            var moosRoot = GameObject.Find("Moos");
            Assert.IsNotNull(moosRoot, "No Moos root node.");
            float maxShellH = 0f;
            foreach (var t in moosRoot.transform.GetComponentsInChildren<Transform>())
            {
                if (t == moosRoot.transform) continue;
                if (t.name != "MoosBase") continue;
                var go = t.gameObject;
                float h = go.transform.localScale.y * go.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
                if (h > maxShellH) maxShellH = h;
            }
            Debug.Log(string.Format("[WALD7] maxShellHeightM={0} -> Kriterium >= 1.3 -> {1}",
                maxShellH.ToString("F2"), maxShellH >= 1.3f ? "OK" : "NOK"));
            if (gen.moosShellsOn) Assert.GreaterOrEqual(maxShellH, 1.3f, "Moos shell does not reach the configured base height.");

            // Cards sit above the Totholz top face (moos on the UPPER side).
            var totholz = GameObject.Find("Totholz");
            Assert.IsNotNull(totholz, "No Totholz root node.");
            int cardsAboveLogs = 0;
            foreach (var logT in totholz.transform.GetComponentsInChildren<Transform>())
            {
                if (logT.gameObject.GetComponent<MeshCollider>() == null) continue;
                Bounds lb = logT.gameObject.GetComponent<MeshFilter>().sharedMesh.bounds;
                float topY = logT.TransformPoint(lb.max).y;
                foreach (var t in moosRoot.transform.GetComponentsInChildren<Transform>())
                {
                    if (t.name != "MoosCap") continue;
                    Vector3 d = t.position - logT.position; d.y = 0f;
                    float reach = Mathf.Max(lb.extents.x, lb.extents.z) * logT.localScale.x;
                    if (d.sqrMagnitude <= reach * reach && t.position.y > topY) cardsAboveLogs++;
                }
            }
            Debug.Log(string.Format("[WALD7] cards above log tops={0} -> Kriterium > 0 -> {1}",
                cardsAboveLogs, cardsAboveLogs > 0 ? "OK" : "NOK"));
            Assert.Greater(cardsAboveLogs, 0, "Moos cards are not on the upper side of the Totholz.");

            // Beweisbild: Nahaufnahme of a trunk base + adjacent Totholz with moos.
            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain for the proof shot.");
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f);
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "No main camera.");
            var spiderCam = cam.GetComponent<SpiderCamera>();
            if (spiderCam != null) spiderCam.enabled = false;

            // Pick the tree closest to a log -> both moos types in one frame.
            Vector3 aimAt = Vector3.zero; float best = 1e9f;
            foreach (var tree in GameObject.Find("Bäume").transform.GetComponentsInChildren<Transform>())
            {
                if (tree.name != "PH-Tree") continue;
                foreach (var logT in totholz.transform.GetComponentsInChildren<Transform>())
                {
                    Vector3 d = tree.position - logT.position; d.y = 0f;
                    float dist = d.sqrMagnitude;
                    if (dist < best) { best = dist; aimAt = tree.position; }
                }
            }
            Assert.Less(best, 100f, "No tree near a log to frame.");
            Vector3 eye = new Vector3(aimAt.x + 2.6f, terrain.SampleHeight(aimAt) + terrain.transform.position.y + 1.1f, aimAt.z + 1.4f);
            cam.transform.position = eye;
            cam.transform.LookAt(new Vector3(aimAt.x, eye.y - 0.5f, aimAt.z));
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 120f;
            for (int rest = 0; rest < 3; rest++) yield return null; // HDRP resting frames
            string file = "Logs/ci/wald7_moos.png";
            Vector4 st = UrwaldTests.SaveFrameHdr(cam, 1024, 768, file);
            Debug.Log(string.Format("[WALD7] proof {0}: Mittel={1} Max={2}",
                file, st.x.ToString("F0"), st.y.ToString("F0")));
        }

        [Test]
        public void MoosMeshes_AreDeterministicUnitShapes()
        {
            var a = Spinnenspiel.JunglePlantFactory.MoosBaseShell(7);
            Assert.That(a.vertices.Length, Is.GreaterThan(20), "Moos shell has too few vertices.");
            Assert.That(Mathf.Abs(a.bounds.size.y - 1f), Is.LessThan(0.05f), "Moos shell not unit height.");
            var b = Spinnenspiel.JunglePlantFactory.MoosBaseShell(7);
            Assert.AreEqual(a.vertices.Length, b.vertices.Length, "Moos shell not deterministic from seed.");
            var cards = Spinnenspiel.JunglePlantFactory.MoosCapCards(7);
            Assert.That(cards.vertices.Length, Is.GreaterThan(20), "Moos cards have too few vertices.");
            Assert.That(cards.bounds.center.y, Is.GreaterThanOrEqualTo(0f), "Moos cards below the log surface.");
        }
    }
}
