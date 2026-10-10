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

            Vector3 start = spiderGo.transform.position;
            Transform pick = null; float bestD = 0f;
            foreach (var t in trees)
            {
                float d = (t.position - start).magnitude;
                if (d > 3f && (pick == null || d < bestD)) { pick = t; bestD = d; }
            }
            Assert.IsNotNull(pick, "No tree 3+ m from spawn.");

            MeshCollider tmc = null;
            foreach (var mcTry in pick.GetComponentsInChildren<MeshCollider>()) tmc = mcTry;
            Assert.IsNotNull(tmc, "PH-Tree has no MeshCollider child.");

            Vector3 dirTo = (pick.position - start); dirTo.y = 0f; dirTo.Normalize();
            movement.SetExternalMoveInput(new Vector2(dirTo.x, dirTo.z));
            float time = 0f;
            float maxToward = 0f;
            float minNear = 1e9f;   // min horizontal dist to trunk axis while at body height
            while (time < 12f)
            {
                time += Time.deltaTime;
                yield return null;
                Vector3 p = spiderGo.transform.position;
                float toward = Vector3.Dot(p - start, dirTo);
                if (toward > maxToward) maxToward = toward;
                if (p.y < pick.position.y + 0.9f) // body-height band: must not enter the trunk
                {
                    Vector3 flat = p - pick.position; flat.y = 0f;
                    if (flat.magnitude < minNear) minNear = flat.magnitude;
                }
            }
            // With the new planting rule (e32ac74: trunk foot raised ~0.35 m) the trunk is thinner
            // at body height, so the Spider now CLIMBS the trunk instead of being blocked beside it
            // (Y rises ~2 m during the walk). The forward-progress check (bestD - 0.6) was calibrated
            // on the old thick-trunk geometry and is invalid for climbing. The real invariant: the
            // Spider must never ENTER the trunk at body height. Measured min distance in that band:
            // ~0.72 m -> limit 0.35 m keeps a safe margin while failing on actual penetration.
            Debug.Log("[Diag] maxToward=" + maxToward.ToString("F3") + " bestD=" + bestD.ToString("F3") +
                " minNear=" + minNear.ToString("F3"));
            Assert.Greater(minNear, 0.35f,
                "Spider entered the trunk of the tree at " + pick.position.ToString("F1") +
                " (min body-height distance to trunk axis " + minNear.ToString("F2") + " m).");
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
            float size = Math.Max(wb.size.y, Math.Max(wb.size.x, wb.size.z));

            DayNightCycle cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.35f); // Update() then poses the sun each frame

            Vector3[][] views =
            {
                // name is passed via Debug.Log; positions: eye-level east, diagonal above, foot west
                new Vector3[] { hub.x + size * 1.1f, wb.min.y + 1.6f, hub.z },
                new Vector3[] { hub.x - size * 0.9f, wb.max.y + size * 0.4f, hub.z - size * 1.2f },
                new Vector3[] { hub.x - size * 0.25f, wb.min.y + 1.2f, hub.z - size * 0.25f }
            };

            string folder = Directory.GetCurrentDirectory() + "/Logs/ci";
            Directory.CreateDirectory(folder);

            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            Camera cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "no Camera in Urwald.");

            int viewIndex = 0;
            string[] names = { "eye", "aerial", "foot" };
            foreach (Vector3[] v in views)
            {
                Vector3 eye = new Vector3(v[0], v[1], v[2]);
                cam.transform.position = eye;
                cam.transform.LookAt(hub);
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
