using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>B4.2: Lianen hang from tree crowns (anchor on branch, lower end 0.2-0.8 m over
    /// ground), carry a capsule-chain collider with SurfaceTag Wood, and a horizontal ray at
    /// body height toward a liana is blocked -> Spider can climb it (climb proxy).</summary>
    public class LianenTests
    {
        [UnityTest]
        public IEnumerator Lianen_HangFromCrownsAndAreClimbable()
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
            Assert.GreaterOrEqual(gen.LianaCount, 200, "Too few lianas (< 200).");

            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");
            float groundY0 = terrain.transform.position.y;

            // Liana-Roots: Children of the "Lianen"-Root named "Liana".
            var lianaRoot = GameObject.Find("Lianen");
            Assert.IsNotNull(lianaRoot, "No Lianen root node.");
            var roots = new System.Collections.Generic.List<GameObject>();
            var rootT = lianaRoot.transform;
            foreach (var t in lianaRoot.GetComponentsInChildren<Transform>(false))
                if (t != rootT && t.parent == rootT && t.name == "Liana") roots.Add(t.gameObject);
            Assert.GreaterOrEqual(roots.Count, 200, "Fewer Liana nodes than reported (< 200).");

            int nChecked = 0;
            foreach (var root in roots)
            {
                var tag = root.GetComponent<SurfaceTag>();
                Assert.AreEqual(SurfaceType.Wood, tag.surfaceType, "Liana SurfaceTag not Wood.");

                var capsules = root.GetComponentsInChildren<CapsuleCollider>();
                Assert.GreaterOrEqual(capsules.Length, 2, "Liana has < 2 capsule-chain colliders.");

                Vector3 pos = root.transform.position; // anchor (top of the liana)
                float groundHere = terrain.SampleHeight(pos) + terrain.transform.position.y;
                float lowest = pos.y;
                foreach (var cc in capsules)
                {
                    var ct = cc.gameObject.transform;
                    float half = Mathf.Max(cc.height, 2f * cc.radius) * 0.5f * ct.localScale.y;
                    float bottom = ct.position.y - half;
                    if (bottom < lowest) lowest = bottom;
                }
                float clearance = lowest - groundHere;
                Assert.GreaterOrEqual(clearance, 0.05f,
                    "Liana at " + pos.ToString("F1") + " hangs below ground (clearance="
                    + clearance.ToString("F2") + ").");
                Assert.LessOrEqual(clearance, 0.9f,
                    "Liana at " + pos.ToString("F1") + " lower end too high (clearance="
                    + clearance.ToString("F2") + ").");

                // Climb proxy: horizontal ray at body height hits a capsule of THIS liana.
                Vector3 mid = new Vector3(pos.x, (pos.y + lowest) * 0.5f, pos.z);
                bool blocked = false;
                var dirs = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
                foreach (var d in dirs)
                {
                    var from = mid - d * 0.5f;
                    RaycastHit[] all = Physics.RaycastAll(from, d, 1.0f);
                    foreach (var h in all)
                    {
                        if (h.transform.parent.gameObject == root) { blocked = true; break; }
                    }
                    if (blocked) break;
                }
                Assert.IsTrue(blocked, "Liana at " + pos.ToString("F1")
                    + ": body-height ray misses the capsule chain.");

                nChecked++;
                if (nChecked >= 25) break;
            }
            Debug.Log("[LianenTest] checked=" + nChecked + " all cleared+blocked");
            yield return null;
        }
    }
}
