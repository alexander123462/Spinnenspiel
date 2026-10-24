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

        private static void Report(string label, float value)
        {
            Debug.Log("J2c -> " + label + ": " + value.ToString("0.####") + " -> checked by asserts above");
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

        [UnityTest]
        public IEnumerator Spider_JumpsFromGroundAndLands()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");

            move.SetExternalMoveInput(new Vector2(0f, 1f));
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(move.IsGrounded, "Spinne nicht geerdet before jump");

            float startY = spider.transform.position.y;
            Vector3 start = spider.transform.position;
            move.RequestJump();

            float peakY = startY;
            float t = 0f;
            while (t < 2f && spider.transform.position.y > startY - 0.02f)
            {
                peakY = Mathf.Max(peakY, spider.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }

            Assert.GreaterOrEqual(peakY - startY, 0.3f, "Spinne jumped not high enough: " + (peakY - startY));

            yield return new WaitForSeconds(1f);
            Assert.GreaterOrEqual(spider.transform.position.y, GroundHeightAt(spider.transform.position) - 0.02f, "Spinne steckt in Boden after jump");
            Assert.IsTrue(move.IsGrounded, "Spinne not grounded after landing");
            Vector3 delta = spider.transform.position - start;
            delta.y = 0f;
            Assert.Greater(delta.magnitude, 0.7f, "Spinne jumped not far enough: " + delta.magnitude);
        }

        [UnityTest]
        public IEnumerator ControlsHelp_Present()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht gefunden");
            ControlsHelp help = spider.GetComponent<ControlsHelp>();
            Assert.IsNotNull(help, "ControlsHelp fehlt an der Spinne");
            Assert.IsTrue(help.Visible, "ControlsHelp soll beim Start sichtbar sein");
        }

        [UnityTest]
        public IEnumerator Spider_EnergyLowCutsTempoToSixtyPercent()
        {
            // J2c value-test (no scene spider): build a spider with movement+energy, then
            // read the tempo factor under full vs. low energy. Criteria 'Wert -> Kriterium -> OK'.
            var go = new GameObject("J2cSpider");
            var move = go.AddComponent<SpiderMovement>();
            var energy = go.AddComponent<SpiderEnergy>();
            yield return null;

            var model = energy.GetModel();
            Assert.IsNotNull(model, "energy model fehlt");

            // Full energy -> factor 1 (no reduction).
            model.Energy = SpiderEnergyModel.startEnergy;
            yield return new WaitForSeconds(0.1f);
            var full = move.EnergyTempoFactor();
            Report("tempo factor at full energy", full);
            Assert.That(full > 0.99f && full < 1.01f, "full-energy tempo factor not ~1");

            // Below the threshold (25) -> factor must be the named constant 0.6.
            model.Energy = 20f;
            yield return new WaitForSeconds(0.1f);
            var low = move.EnergyTempoFactor();
            Report("tempo factor below threshold", low);
            Assert.That(low > 0.59f && low < 0.61f, "low-energy tempo factor not ~0.6");

            GameObject.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator Spider_EnergyZeroDiesAndShowsStats()
        {
            // J2c: energy 0 must trigger the existing death report (DeathStats/K6). Self-built
            // spider so the test does not depend on the scene spider's component set.
            var go = new GameObject("J2cDeathSpider");
            var stats = go.AddComponent<DeathStats>();
            var energy = go.AddComponent<SpiderEnergy>();
            yield return null;

            var model = energy.GetModel();
            Assert.IsNotNull(model, "energy model fehlt");
            Assert.IsFalse(stats.Stats.DeathShown, "not dead at start");

            model.Energy = 0f;
            yield return new WaitForSeconds(1f);
            Report("death shown after energy 0", stats.Stats.DeathShown ? 1f : 0f);
            Assert.IsTrue(stats.Stats.DeathShown, "energy 0 must die the spider within 1s");

            // N would start a fresh partie; verify the reset path directly.
            stats.Stats.StartNewPartie();
            energy.ResetEnergy();
            Assert.IsFalse(stats.Stats.DeathShown, "fresh partie clears the death flag");
            Report("energy after new partie", energy.Energy);
            Assert.That(energy.Energy > 99f && energy.Energy <= 100.1f,
                "new partie restores start energy ~100");

            GameObject.DestroyImmediate(go);
        }
    }
}
