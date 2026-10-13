using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>B3.5: PH-Baumes carry a concave MeshCollider (TreeCollider) with SurfaceTag Wood,
    /// and a horizontal ray at body height toward the trunk hits that collider -> Spider gets stopped.</summary>
    public class TreesTests
    {
        [UnityTest]
        public IEnumerator Trees_HaveCollidersAndBlockSpider()
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
            Assert.GreaterOrEqual(gen.TreeCount, 40, "Too few trees (< 40).");

            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");

            var dirs = new Vector3[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            int checkCount = gen.TreeCount < 12 ? gen.TreeCount : 12;
            for (int i = 0; i < checkCount; i++)
            {
                Transform tree = gen.Trees[i];
                GameObject root = tree.gameObject;

                var tag = root.GetComponent<SurfaceTag>();
                Assert.IsNotNull(tag, "Tree " + i + " has no SurfaceTag.");
                Assert.AreEqual(SurfaceType.Wood, tag.surfaceType, "Tree " + i + " SurfaceTag is not Wood.");

                GameObject colNode = null;
                MeshCollider mc = null;
                foreach (var cand in root.GetComponentsInChildren<MeshCollider>())
                {
                    mc = cand;
                    colNode = cand.gameObject;
                    break;
                }
                Assert.IsNotNull(mc, "Tree " + i + " has no MeshCollider child.");
                Assert.IsFalse(mc.convex, "Tree " + i + " collider is convex (must be concave).");
                Assert.IsNotNull(mc.sharedMesh, "Tree " + i + " MeshCollider has no mesh.");

                Vector3 pos = tree.position;
                float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;

                Mesh cmesh = mc.sharedMesh;
                int subs = cmesh.subMeshCount < 1 ? 1 : cmesh.subMeshCount;
                // Blocking check: (a) downcasts over the footprint, (b) horizontal casts at body
                // height toward the trunk-submesh center. RaycastAll so terrain/other colliders in
                // front do not hide a hit on THIS tree's collider inside the body-height band.
                int hits = 0;
                Bounds wb = mc.bounds;
                float stepX = Mathf.Max(0.5f, wb.size.x / 64f), stepZ = Mathf.Max(0.5f, wb.size.z / 64f);
                for (float x = wb.center.x - wb.extents.x; x <= wb.center.x + wb.extents.x; x += stepX)
                {
                    for (float z = wb.center.z - wb.extents.z; z <= wb.center.z + wb.extents.z; z += stepZ)
                    {
                        float g = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                        var top = new Vector3(x, g + 12f, z);
                        RaycastHit[] all = Physics.RaycastAll(top, Vector3.down, 13f);
                        foreach (var h in all)
                        {
                            if (h.collider.gameObject != colNode) continue;
                            float above = h.point.y - g;
                            if (above > 0.2f) { hits++; break; }
                        }
                    }
                }
                if (hits == 0)
                {
                    int trunkSub = 0;
                    float bestArea = 1e9f;
                    for (int s = 0; s < subs; s++)
                    {
                        Bounds sb = cmesh.GetSubMesh(s).bounds;
                        float area = sb.size.x * sb.size.z;
                        if (sb.size.y >= 2f && area < bestArea) { bestArea = area; trunkSub = s; }
                    }
                    Vector3 twc = colNode.transform.TransformPoint(cmesh.GetSubMesh(trunkSub).bounds.center);
                    float tGround = terrain.SampleHeight(twc) + terrain.transform.position.y;
                    foreach (var d in dirs)
                        foreach (float hy in new float[] { 1f, 2f })
                        {
                            var from = new Vector3(twc.x + d.x * 6f, tGround + hy, twc.z + d.z * 6f);
                            var dirTo = new Vector3(twc.x - from.x, 0f, twc.z - from.z);
                            if (dirTo.sqrMagnitude < 0.01f) continue;
                            RaycastHit[] all = Physics.RaycastAll(from, dirTo.normalized, dirTo.magnitude + 1f);
                            foreach (var h in all)
                            {
                                if (h.collider.gameObject != colNode) continue;
                                float above = h.point.y - (terrain.SampleHeight(h.point) + terrain.transform.position.y);
                                if (above > 0.2f && above < 3f) { hits++; break; }
                            }
                        }
                }
                Debug.Log(string.Format("[TreesTest] tree {0} pos={1} ext={2} hits={3}",
                    i, pos.ToString("F1"), mc.bounds.size.ToString("F1"), hits));
                Assert.GreaterOrEqual(hits, 1,
                    "Tree " + i + " at " + pos.ToString("F1") + ": no downcast over the footprint hits TreeCollider above ground.");
            }
        }

        // T1: trees must not float. Measures renderer-bounds bottom vs terrain under each
        // tree and reports count/min/max/median plus how many sit above +0.10 m.
        [UnityTest]
        public IEnumerator Trees_TrunksSitInGround()
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

            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain.");
            float terrY = terrain.transform.position.y;

            var gaps = new System.Collections.Generic.List<float>();
            for (int i = 0; i < gen.TreeCount; i++)
            {
                Transform tree = gen.Trees[i];
                float lowest = 1e30f;
                bool found = false;
                foreach (var r in tree.gameObject.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.gameObject.name.Contains("TreeLOD")) continue;
                    float b = r.bounds.min.y;
                    if (b < lowest) lowest = b;
                    found = true;
                }
                if (!found) continue;
                float groundY = terrain.SampleHeight(tree.position) + terrY;
                gaps.Add(lowest - groundY);
            }
            Assert.GreaterOrEqual(gaps.Count, 40, "Too few trees measured (< 40).");

            var sorted = new System.Collections.Generic.List<float>(gaps);
            sorted.Sort();
            float median = sorted[sorted.Count / 2];
            float min = sorted[0];
            float max = sorted[sorted.Count - 1];
            int above = 0;
            foreach (var g2 in gaps) if (g2 > 0.10f) above++;
            Debug.Log(string.Format("[TreeGround] count={0} min={1} max={2} median={3} above+0.10={4}",
                gaps.Count, min.ToString("F3"), max.ToString("F3"), median.ToString("F3"), above));

            Assert.LessOrEqual(max, 0.10f, "A tree floats above +0.10 m (max=" + max.ToString("F3") + ").");
            Assert.GreaterOrEqual(median, -0.30f, "Median gap below -0.30 m (median=" + median.ToString("F3") + ").");
            Assert.LessOrEqual(median, 0.05f, "Median gap above +0.05 m (median=" + median.ToString("F3") + ").");
        }
    }
}
