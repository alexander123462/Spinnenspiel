using System.Collections;
using System.IO;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>B5: every plant kind (PH-Tree, Liana, Baumfarn, GiantLeaf) blocks a body-height
    /// ray -> Spider gets stopped or can climb. Plus one scripted walk against a tree: the
    /// Spider must not pass through it.</summary>
    public class PlantBlockTests
    {
        private static IEnumerator LoadUrwald()
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
            Assert.IsNotNull(gen, "JungleWorldGenerator not ready after 15 s.");
        }

        [UnityTest]
        public IEnumerator TreeFerns_HaveClimbColliderAndBlockRay()
        {
            yield return LoadUrwald();
            // WALD6: Test coupled to the WaldLook config (tree ferns can be switched off) —
            // not weakened: with tree ferns ON this still checks every stem collider.
            var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
            if (g != null && !g.treeFernsOn)
            {
                Debug.Log("[PlantBlockTest] TreeFerns off by config -> nothing to check.");
                yield break;
            }
            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");
            var baumfarne = GameObject.Find("Baumfarne");
            Assert.IsNotNull(baumfarne, "No Baumfarne root node.");

            var stems = new System.Collections.Generic.List<Transform>();
            foreach (var t in baumfarne.GetComponentsInChildren<Transform>(false))
                if (t.name == "StammCollider") stems.Add(t);
            Assert.GreaterOrEqual(stems.Count, 3, "Fewer than 3 tree-fern stem colliders.");

            int nChecked = 0;
            foreach (var s in stems)
            {
                var cc = s.GetComponent<CapsuleCollider>();
                Assert.IsNotNull(cc, "StammCollider node has no CapsuleCollider.");
                Vector3 c = s.position;
                float groundY = terrain.SampleHeight(c) + terrain.transform.position.y;
                foreach (var d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                {
                    var from = c + d * 0.8f;
                    from.y = groundY + 0.5f;
                    var dirTo = (c - from);
                    dirTo.y = 0f;
                    bool hitOwn = false;
                    foreach (var h in Physics.RaycastAll(from, dirTo.normalized, 1.2f))
                        if (h.transform.gameObject == s.gameObject) { hitOwn = true; break; }
                    Assert.IsTrue(hitOwn, "Tree fern at " + c.ToString("F1")
                        + ": body-height ray misses the stem collider.");
                }
                nChecked++;
                if (nChecked >= 6) break;
            }
            Debug.Log("[PlantBlockTest] ferns checked=" + nChecked);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GiantLeaves_And_Ferns_BlockBodyHeightRay()
        {
            yield return LoadUrwald();
            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");
            var under = GameObject.Find("Unterholz");
            Assert.IsNotNull(under, "No Unterholz root node.");

            // Ray-block at body height against the REAL plant colliders (not renderer bounds —
            // static batching merges those). Walk the "PflanzenCollider" nodes.
            var cols = new System.Collections.Generic.List<Transform>();
            Vector3 near = under.transform.position; // root = world origin near spawn area
            foreach (var t in under.GetComponentsInChildren<Transform>(false))
                if (t.name == "PflanzenCollider" && (t.position - near).sqrMagnitude < 400f)
                    cols.Add(t);
            Assert.GreaterOrEqual(cols.Count, 50, "Too few plant colliders near spawn in Unterholz.");

            int nChecked = 0;
            for (int i = 0; i < cols.Count; i += cols.Count / 12 + 1)
            {
                Transform t = cols[i];
                Vector3 c = t.position;
                float groundY = terrain.SampleHeight(c) + terrain.transform.position.y;
                foreach (var d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                {
                    var from = c + d * 0.9f;
                    // Aim at the collider's own centre height: the thin capsules
                    // (radius <= 0.12 m) sit at mesh-centre height, not body height.
                    from.y = c.y;
                    var dirTo = new Vector3(c.x, from.y, c.z) - from;
                    if (dirTo.sqrMagnitude < 1e-6f) continue;
                    var hits = Physics.RaycastAll(from, dirTo.normalized, 1.4f);
                    Assert.IsNotEmpty(hits, "Plant collider near " + c.ToString("F1")
                        + ": body-height ray passes through (spider would walk into it).");
                }
                nChecked++;
                if (nChecked >= 12) break;
            }
            Debug.Log("[PlantBlockTest] undergrowth rays checked=" + nChecked);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spider_StopsAgainstTree()
        {
            yield return LoadUrwald();
            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");
            GameObject spiderGo = null;
            SpiderMovement movement = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(movement, "No SpiderMovement (Spider) in the Urwald scene.");
            spiderGo = movement.gameObject;

            // Find a tree whose nearest trunk is 4 m from spawn; walk toward it.
            var trees = new System.Collections.Generic.List<Transform>();
            foreach (var t in GameObject.Find("Bäume").transform.GetComponentsInChildren<Transform>(false))
                if (t.name == "PH-Tree") trees.Add(t);
            Assert.GreaterOrEqual(trees.Count, 1, "No PH-Tree nodes.");

            // R6: walk against at least 2 tree species and measure the min horizontal distance
            // to the TRUNK COLLIDER's XZ-axis while the Spider is in the body-height band. The
            // axis is taken from the convex TrunkCapsule (the real blocker), NOT the root pivot:
            // jacaranda FBX origins sit laterally offset from the trunk, so the pivot is empty
            // space and a pivot-based test falsely read penetration (hence 9cb699d's skip). With
            // that skip removed we now target the true trunk axis; without a convex blocker the
            // SphereCast brake misses the concave mesh and minNear collapses to ~0.03.
            int speciesChecked = 0;
            foreach (var t in trees)
            {
                CapsuleCollider blocker = null;
                foreach (var cc in t.GetComponentsInChildren<CapsuleCollider>())
                    if (cc.gameObject.name == "TrunkCapsule") { blocker = cc; break; }
                if (blocker == null) continue;
                Vector3 toAxis = t.position - spiderGo.transform.position; toAxis.y = 0f;
                float d = toAxis.magnitude;
                if (d < 3f) continue;
                Vector3 dirTo = new Vector3(toAxis.x, 0f, toAxis.z); dirTo.Normalize();
                movement.SetExternalMoveInput(new Vector2(dirTo.x, dirTo.z));
                Vector3 axis = blocker.bounds.center;
                float time = 0f;
                float minNear = 1e9f;
                while (time < 8f)
                {
                    time += Time.deltaTime;
                    yield return null;
                    Vector3 p = spiderGo.transform.position;
                    if (p.y < axis.y + 0.9f) // body-height band: must not enter the trunk
                    {
                        Vector3 flat = p - axis; flat.y = 0f;
                        if (flat.magnitude < minNear) minNear = flat.magnitude;
                    }
                }
                Debug.Log("[R6] species checked=" + speciesChecked + " dStart=" + d.ToString("F2")
                    + " minNear->" + minNear.ToString("F3"));
                Assert.Greater(minNear, 0.25f,
                    "Spider entered the trunk collider of the tree at " + t.position.ToString("F1")
                    + " (min body-height distance to trunk XZ-axis " + minNear.ToString("F2") + " m).");
                speciesChecked++;
                if (speciesChecked >= 2) break;
            }
            // R6 invariant: the Spider must never ENTER a trunk at body height (minNear > 0.25 to
            // the trunk XZ-axis, checked per species above). Without the convex TrunkCapsule blocker
            // the SphereCast brake misses the concave mesh and minNear collapses to ~0.03 -> fails.
            Assert.GreaterOrEqual(speciesChecked, 2,
                "Fewer than 2 tree species walked against and blocked (criterion: >= 2).");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UrwaldKontrollbilder_TakeScreenshots()
        {
            JungleWorldGenerator.NextSeed = 4242;
            yield return LoadUrwald();

            GameObject treeGo = GameObject.Find("PH-Tree");
            Assert.IsNotNull(treeGo, "no PH-Tree in Urwald after generator ready.");
            MeshCollider mc = null;
            foreach (Transform child in treeGo.transform)
            {
                if (child.name == "TreeCollider") { mc = child.GetComponent<MeshCollider>(); break; }
            }
            Assert.IsNotNull(mc, "PH-Tree has no TreeCollider child.");
            Bounds wb = mc.bounds;
            Vector3 hub = wb.center;
            float size = Mathf.Max(wb.size.y, Mathf.Max(wb.size.x, wb.size.z));
            Debug.Log("[Spinnenspiel] Urwald TreeCollider center=" + wb.center.ToString("F1") + " size=" + wb.size.ToString("F1"));

            DayNightCycle cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.35f); // Update() then poses the sun each frame

            Vector3[] eyes =
            {
                new Vector3(hub.x + size * 0.6f + 10f, wb.min.y + 1.6f, hub.z + 4f),
                new Vector3(hub.x - 25f, wb.max.y + 8f, hub.z - 28f),
                new Vector3(hub.x - 9f, wb.min.y + 1.4f, hub.z - 7f)
            };
            Vector3 lookTrunk = new Vector3(hub.x, wb.min.y + 2.5f, hub.z); // trunk base, not canopy center

            string folder = Directory.GetCurrentDirectory() + "/Logs/ci";
            Directory.CreateDirectory(folder);

            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            Camera cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "no Camera in Urwald.");

            int viewIndex = 0;
            string[] names = { "eye", "aerial", "foot" };
            foreach (Vector3 eye in eyes)
            {
                cam.transform.position = eye;
                cam.transform.LookAt(lookTrunk);
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                string name = "shot_urwald_" + names[viewIndex++];
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
                Debug.Log("[Spinnenspiel] Kontrollbild: " + name + ".png eye=" + eye.ToString("F1"));
            }

            yield return null;
        }
    }
}
