using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    public class SpiderMovementTests
    {
        private const string SceneName = "M1_Testgelaende";

        private static float GroundHeightAt(Vector3 pos)
        {
            Terrain t = Terrain.activeTerrain;
            Assert.IsNotNull(t, "Kein aktives Terrain in der Szene");
            return t.SampleHeight(pos) + t.transform.position.y;
        }

        [UnityTest]
        public IEnumerator Spider_LandsWalksAndStops()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");

            Assert.GreaterOrEqual(spider.transform.position.y, GroundHeightAt(spider.transform.position) - 0.02f, "Spinne steckt nach dem Landen im Boden");
            Assert.IsTrue(move.IsGrounded, "Spinne ist nach 1 s nicht geerdet");

            Vector3 start = spider.transform.position;
            move.SetExternalMoveInput(new Vector2(0f, 1f));
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                Assert.GreaterOrEqual(spider.transform.position.y, GroundHeightAt(spider.transform.position) - 0.02f, "Spinne sinkt beim Laufen in den Boden");
                yield return null;
            }

            Vector3 delta = spider.transform.position - start;
            delta.y = 0f;
            Assert.Greater(delta.magnitude, move.WalkSpeed * 3f * 0.8f, "Spinne ist in 3 s nicht weit genug gelaufen: " + delta.magnitude);
            Assert.LessOrEqual(move.Speed, move.SprintSpeed + 0.1f, "Spinne ist schneller als sprintSpeed");

            move.SetExternalMoveInput(Vector2.zero);
            yield return new WaitForSeconds(1f);
            Assert.Less(move.Speed, 0.05f, "Spinne bremst nicht ab: " + move.Speed);
        }
    }
}
