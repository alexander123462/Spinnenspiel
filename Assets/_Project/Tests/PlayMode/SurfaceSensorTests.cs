using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    public class SurfaceSensorTests
    {
        private const string SceneName = "Klettertest";
        private static SpiderSurfaceSensor Sensor = new SpiderSurfaceSensor();

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }

        // Boden und Wand-voraus (Strahl C).
        [UnityTest]
        public IEnumerator GroundAndWallAhead()
        {
            yield return LoadScene();

            // Boden: Spinne auf ebener Erde.
            Assert.IsTrue(Sensor.Sense(new Vector3(0f, 0f, 0f), Vector3.up, Vector3.forward, out Vector3 point, out Vector3 normal, out bool wallAhead), "Boden: Sense lieferte false");
            Assert.LessOrEqual(Vector3.Distance(point, Vector3.zero), 0.1f, "Boden: Trefferpunkt falsch: " + point);
            Assert.Less(Vector3.Angle(normal, Vector3.up), 5f, "Boden: Normal zeigt nicht nach oben: " + normal);
            Assert.IsFalse(wallAhead, "Boden: waehlt faelschlich Wand voraus");

            // Wand voraus: Spinne steht 10 cm vor der Wand bei z=2.85.
            Assert.IsTrue(Sensor.Sense(new Vector3(0f, 0f, 2.75f), Vector3.up, Vector3.forward, out point, out normal, out wallAhead), "Wand: Sense lieferte false");
            Assert.IsTrue(wallAhead, "Wand: wallAhead ist false");
            Assert.Less(Vector3.Angle(normal, Vector3.back), 35f, "Wand: Normal zeigt nicht zur Wand: " + normal);
        }

        // Aussenkante: Spinne steht knapp ueber der Kante des Blocks und blickt nach +X.
        [UnityTest]
        public IEnumerator OuterEdgeBehindBlock()
        {
            yield return LoadScene();

            Assert.IsTrue(Sensor.Sense(new Vector3(3.35f, 0.4f, 0f), Vector3.up, Vector3.right, out Vector3 point, out Vector3 normal, out bool wallAhead), "Kante: Sense lieferte false");
            Assert.Greater(normal.x, 0.3f, "Kante: Normal hat keinen deutlichen +X-Anteil: " + normal);
        }
    }
}
