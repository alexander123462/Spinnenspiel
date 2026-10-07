using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Z6 Netzbau-Komfort: Esc aborts a running build; red preview when the spot went bad.</summary>
    public class NetbuildComfortTests
    {
        private const string SceneName = "Testwelt";

        private static GameObject LoadedSpider()
        {
            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            return spider;
        }

        // PLACEHOLDER_TESTS
        [UnityTest]
        public IEnumerator CancelBuild_StopsRunningBuild()
        {
            SceneManager.LoadScene("Klettertest");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderWebBuilder builder = spider.GetComponent<SpiderWebBuilder>();
            Assert.IsNotNull(builder, "SpiderWebBuilder fehlt");

            // Netze like Builder_BuildsWebInOrder.
            GameObject webGo = new GameObject("CancelWeb");
            OrbWeb web = webGo.AddComponent<OrbWeb>();
            Vector3[] anchors =
            {
                new Vector3(5.02f, 1.15f, 2.0f),
                new Vector3(5.78f, 1.15f, 2.0f),
                new Vector3(5.72f, 0.02f, 2.0f),
                new Vector3(5.08f, 0.02f, 2.0f)
            };
            OrbWebLayout layout = new OrbWebLayout(anchors, 16, 0.02f, 0.03f, 7);
            web.Build(layout);

            movement.SetScripted(true);
            movement.Teleport(new Vector3(5.4f, 0.6f, 1.9f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            builder.durationScale = 0.1f;
            builder.StartBuild(web);
            Assert.IsTrue(builder.IsBuilding, "StartBuild hat nicht gestartet");

            // Run the build for ~1.2 s, then abort (the Esc path via Placer calls CancelBuild).
            float elapsed = 0f;
            while (elapsed < 1.2f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            builder.CancelBuild();
            Assert.IsFalse(builder.IsBuilding, "CancelBuild must stop the build");
            Assert.AreNotEqual(OrbWeb.WebStage.Done, builder.CurrentStage,
                "An aborted build must not be Done (stage: " + builder.CurrentStage + ")");
            Assert.IsFalse(movement.IsScripted, "After CancelBuild the spider must be free (IsScripted false)");
            OrbWeb.WebStage stageAtCancel = builder.CurrentStage;

            // The coroutine must have stopped: no further progress after 1 s.
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(stageAtCancel, builder.CurrentStage,
                "Cancelled build must not advance further (was " + stageAtCancel + ")");
            Assert.IsFalse(builder.IsSittingInWeb, "A cancelled build must not sit in the web");

            Object.DestroyImmediate(webGo);
        }

        [UnityTest]
        public IEnumerator RedPreview_WhenSpotGoesBad()
        {
            SceneManager.LoadScene("Testwelt");
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement movement = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(movement, "SpiderMovement fehlt");
            SpiderWebPlacer placer = spider.GetComponent<SpiderWebPlacer>();
            Assert.IsNotNull(placer, "SpiderWebPlacer fehlt");

            // Two trunks 1.5-4 m apart (like WorldWebTests).
            Transform a = null;
            Transform b = null;
            float best = float.MaxValue;
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name != "Trunk" || go.GetComponent<MeshCollider>() == null) continue;
                Transform other = null;
                foreach (GameObject go2 in Object.FindObjectsOfType<GameObject>())
                {
                    if (go2 == go || go2.name != "Trunk" || go2.GetComponent<MeshCollider>() == null) continue;
                    other = go2.transform;
                    break;
                }
                if (other == null) continue;
                float dx = go.transform.position.x - other.position.x;
                float dz = go.transform.position.z - other.position.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist >= 1.5f && dist <= 4.0f && dist < best)
                {
                    best = dist;
                    a = go.transform;
                    b = other;
                }
            }
            Assert.IsNotNull(a, "No trunk pair 1.5-4 m apart");

            // Slanted thread: high (2 m) at A, low (0.2 m) at B.
            float groundA, groundB;
            GroundBelow(a.position, spider.transform, out groundA);
            GroundBelow(b.position, spider.transform, out groundB);
            Vector3 pa = new Vector3(a.position.x, groundA + 2f, a.position.z);
            Vector3 pb = new Vector3(b.position.x, groundB + 0.2f, b.position.z);
            GameObject threadGo = new GameObject("Z6SlantedThread");
            SilkThread faden = threadGo.AddComponent<SilkThread>();
            faden.Init(pa, pb, 0.02f);
            yield return new WaitForSeconds(0.3f);

            // Attach high: a valid spot exists -> white preview.
            movement.AttachToThread(faden, 0.1f);
            float t = 0f;
            while (!placer.CanBuild && t < 3f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(placer.CanBuild, "No spot at the high end (BlockReason: " + placer.BlockReason + ")");
            Assert.IsTrue(placer.PreviewVisible, "Preview not visible at the high end");
            Assert.IsFalse(placer.PreviewBlocked, "Preview must be white when a spot exists");

            // Move to the low end: no room under the thread -> red preview.
            movement.AttachToThread(faden, 0.9f);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(placer.CanBuild, "Low end still offers a spot");
            Assert.IsTrue(placer.PreviewVisible, "Red preview must stay visible at the bad spot");
            Assert.IsTrue(placer.PreviewBlocked, "Preview not red-blocked at the bad spot");

            Object.DestroyImmediate(threadGo);
        }

        /// <summary>Ground height under a trunk: vertical downcast, skip spider and Wood.</summary>
        private static bool GroundBelow(Vector3 trunkPos, Transform spiderRoot, out float groundY)
        {
            var hits = Physics.RaycastAll(trunkPos + Vector3.up * 30f, Vector3.down, 100f);
            foreach (RaycastHit hit in hits)
            {
                Transform tr = hit.collider.transform;
                bool underSpider = false;
                for (Transform x = tr; x != null; x = x.parent)
                {
                    if (x == spiderRoot) { underSpider = true; break; }
                }
                if (underSpider) continue;
                SurfaceTag tag = tr.GetComponentInParent<SurfaceTag>();
                if (tag != null && tag.surfaceType == SurfaceType.Wood) continue;
                groundY = hit.point.y;
                return true;
            }
            groundY = trunkPos.y;
            return false;
        }
    }
}