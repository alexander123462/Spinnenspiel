using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;

namespace Spinnenspiel.Tests
{
    /// <summary>WALD6: Farne and Jungbaeumchen statt Tropenplants in the forest area.
    /// Criteria per line 'Value -> Criterion -> OK/NOK' in the log.</summary>
    public class Wald6FernTests
    {
        [UnityTest]
        public IEnumerator Wald6_TropicalOffAndFernsUp()
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

            // Riesenblaetter and Palmen-Baumfarne OUT of the forest.
            Debug.Log(string.Format("[WALD6] GiantLeafCount={0} -> Kriterium = 0 -> {1}",
                gen.GiantLeafCount, gen.GiantLeafCount == 0 ? "OK" : "NOK"));
            Assert.AreEqual(0, gen.GiantLeafCount, "Riesenblaetter still placed (config says off).");
            Debug.Log(string.Format("[WALD6] TreeFernCount={0} -> Kriterium = 0 -> {1}",
                gen.TreeFernCount, gen.TreeFernCount == 0 ? "OK" : "NOK"));
            Assert.AreEqual(0, gen.TreeFernCount, "Palmen-Baumfarne still placed (config says off).");

            // Schwertfarne at least 2.5x the old baseline (1500 at Seed 4242).
            Debug.Log(string.Format("[WALD6] FernCount={0} -> Kriterium >= 3750 -> {1}",
                gen.FernCount, gen.FernCount >= 3750 ? "OK" : "NOK"));
            Assert.GreaterOrEqual(gen.FernCount, 3750, "Ferns not clearly more frequent (< 2.5x baseline).");

            // Jungbaeumchen as Gebuesch present.
            Debug.Log(string.Format("[WALD6] YoungConiferCount={0} -> Kriterium >= 300 -> {1}",
                gen.YoungConiferCount, gen.YoungConiferCount >= 300 ? "OK" : "NOK"));
            Assert.GreaterOrEqual(gen.YoungConiferCount, 300, "No Jungbaeumchen Gebuesch placed.");

            // Gruppen an Staemmen: within 1.6 m of a trunk stand >= 2 plant colliders.
            var under = GameObject.Find("Unterholz");
            Assert.IsNotNull(under, "No Unterholz root node.");
            var cols = new List<Transform>();
            foreach (var t in under.GetComponentsInChildren<Transform>(false))
                if (t.name == "PflanzenCollider") cols.Add(t);

            var trees = new List<Vector3>();
            foreach (var t in GameObject.Find("Bäume").transform.GetComponentsInChildren<Transform>(false))
                if (t.name == "PH-Tree") trees.Add(t.position);
            Assert.GreaterOrEqual(trees.Count, 8, "Too few trees for the group check.");

            int grouped = 0;
            int checkedTrees = 0;
            for (int i = 0; i < trees.Count && checkedTrees < 12; i++)
            {
                Vector3 tp = trees[i];
                int near = 0;
                foreach (var c in cols)
                {
                    Vector3 d = c.position - tp; d.y = 0f;
                    if (d.sqrMagnitude <= 1.6f * 1.6f) near++;
                }
                if (near >= 2) grouped++;
                checkedTrees++;
            }
            Debug.Log(string.Format("[WALD6] trees with >=2 ferns within 1.6 m: {0}/{1} -> Kriterium >= 50%% -> {2}",
                grouped, checkedTrees, grouped * 2 >= checkedTrees ? "OK" : "NOK"));
            Assert.GreaterOrEqual(grouped * 2, checkedTrees,
                "Ferns do not form groups along trunks (< half of the trees have >= 2 ferns within 1.6 m).");

            // Beweisbild: Nahaufnahme of trunk fern groups + Jungbaumchen (proven HDR path).
            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain for the proof shot.");
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f);
            foreach (var volume in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null) continue;
                foreach (var comp in volume.sharedProfile.components)
                {
                    string n = comp.GetType().Name;
                    if (n.EndsWith("DepthOfField") || n.EndsWith("MotionBlur")) comp.active = false;
                }
            }
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "No main camera.");
            var spiderCam = cam.GetComponent<SpiderCamera>();
            if (spiderCam != null) spiderCam.enabled = false;
            // Aim at the first trunk that has >= 2 ferns nearby: groups in frame.
            Vector3 aimAt = Vector3.zero; int aimIdx = -1;
            for (int i = 0; i < trees.Count; i++)
            {
                Vector3 tp = trees[i];
                int near = 0;
                foreach (var c in cols)
                {
                    Vector3 d = c.position - tp; d.y = 0f;
                    if (d.sqrMagnitude <= 1.6f * 1.6f) near++;
                }
                if (near >= 2) { aimAt = tp; aimIdx = i; break; }
            }
            Assert.Greater(aimIdx, -1, "No trunk with fern group to frame.");
            Vector3 eye = new Vector3(aimAt.x + 3.5f, terrain.SampleHeight(aimAt) + terrain.transform.position.y + 1.4f, aimAt.z + 1.2f);
            cam.transform.position = eye;
            cam.transform.LookAt(new Vector3(aimAt.x, eye.y - 0.7f, aimAt.z));
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 120f;
            var veg = Object.FindFirstObjectByType<VegetationSystem>();
            for (int rest = 0; rest < 3; rest++) yield return null; // HDRP resting frames
            if (veg != null) veg.RenderForCamera(cam);
            string file = "Logs/ci/wald6_ferns.png";
            Vector4 st = UrwaldTests.SaveFrameHdr(cam, 1024, 768, file);
            Debug.Log(string.Format("[WALD6] proof {0}: Mittel={1} Max={2} SharpFrac={3}",
                file, st.x.ToString("F0"), st.y.ToString("F0"), st.w.ToString("P1")));
        }

        [Test]
        public void YoungConifer_IsDeterministicAndHasGeometry()
        {
            var a = Spinnenspiel.JunglePlantFactory.YoungConifer(7);
            Assert.That(a.vertices.Length, Is.GreaterThan(20), "YoungConifer has too few vertices.");
            float yMin = a.bounds.center.y - a.bounds.size.y * 0.5f;
            float yMax = a.bounds.center.y + a.bounds.size.y * 0.5f;
            Assert.That(yMin, Is.LessThanOrEqualTo(0.05f), "YoungConifer does not start at the ground.");
            Assert.That(a.bounds.size.y, Is.GreaterThan(0.8f), "YoungConifer too short (mesh normalised ~1 m).");
            var b = Spinnenspiel.JunglePlantFactory.YoungConifer(7);
            Assert.AreEqual(a.vertices.Length, b.vertices.Length, "YoungConifer not deterministic from seed.");
            var c = Spinnenspiel.JunglePlantFactory.YoungConifer(8);
            Assert.That(a.vertices.Length != c.vertices.Length
                || a.bounds.size.z != c.bounds.size.z, "Seeds 7 and 8 give identical YoungConifers.");
        }
    }
}
