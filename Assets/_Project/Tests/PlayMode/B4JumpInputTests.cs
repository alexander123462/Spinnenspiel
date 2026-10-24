using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// B4b: Sprung per echter Leertaste messen. Nut die Harness aus B4a
    /// (InputTestFixture -> Press(kb.spaceKey)), aber der SPIDER IN DER SCENE
    /// M1_Testgelaende (SpiderInput + SpiderMovement im Scene), so der echte
    /// Input-Pfad (input.JumpPressed -> TryTriggerJump) get ueber die echten Tasten
    /// verlaesslichkeit. KEIN RequestJump().
    /// </summary>
    public class B4JumpInputTests : InputTestFixture
    {
        private const string SceneName = "M1_Testgelaende";

        // Criterion 1: Leertaste am ebenen Boden -> Anstieg >= 0.35 m in ~0.6 s.
        [UnityTest]
        public IEnumerator B4b_JumpOnGround_RisesViaRealSpacePress()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht found in M1_Testgelaende");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");
            SpiderInput input = spider.GetComponent<SpiderInput>();
            Assert.IsNotNull(input, "SpiderInput fehlt");

            // Geerdet on the ground before the jump.
            move.SetExternalMoveInput(Vector2.zero);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(move.IsGrounded, "Spinne not grounded before jump");

            float startY = spider.transform.position.y;

            // Echte Leertaste-Press via Fixture-Harness.
            var kb = InputSystem.AddDevice<Keyboard>();
            Press(kb.spaceKey);

            float peakY = startY;
            float t = 0f;
            while (t < 1.5f)
            {
                peakY = Mathf.Max(peakY, spider.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }

            float rise = peakY - startY;
            Debug.Log("B4b ground -> rise=" + rise.ToString("0.###") + " m -> criterion >=0.35 -> " + (rise >= 0.35f ? "OK" : "NOK"));
            Release(kb.spaceKey);
            Assert.GreaterOrEqual(rise, 0.35f, "Real space-press jump rise too low: " + rise + " m");
        }

        // Criterion 2: waehrend Laufen -> Anstieg >= 0.35 m.
        [UnityTest]
        public IEnumerator B4b_JumpWhileRunning_RisesViaRealSpacePress()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht found");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");
            SpiderInput input = spider.GetComponent<SpiderInput>();
            Assert.IsNotNull(input, "SpiderInput fehlt");

            // Laufen vorwaerts zuerst.
            move.SetExternalMoveInput(new Vector2(0f, 1f));
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(move.IsGrounded, "Spinne not grounded while running before jump");

            float startY = spider.transform.position.y;

            var kb = InputSystem.AddDevice<Keyboard>();
            Press(kb.spaceKey);

            float peakY = startY;
            float t = 0f;
            while (t < 1.5f)
            {
                peakY = Mathf.Max(peakY, spider.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }

            move.SetExternalMoveInput(Vector2.zero);
            float rise = peakY - startY;
            Debug.Log("B4b running -> rise=" + rise.ToString("0.###") + " m -> criterion >=0.35 -> " + (rise >= 0.35f ? "OK" : "NOK"));
            Release(kb.spaceKey);
            Assert.GreaterOrEqual(rise, 0.35f, "Jump-while-running rise too low: " + rise + " m");
        }

        // Criterion 4: zweite Leertaste < 0.6 s nach der ersten -> kein zweiter Sprung (Cooldown).
        [UnityTest]
        public IEnumerator B4b_SecondPressWithinCooldown_NoSecondJump()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht found");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");
            SpiderInput input = spider.GetComponent<SpiderInput>();
            Assert.IsNotNull(input, "SpiderInput fehlt");

            move.SetExternalMoveInput(Vector2.zero);
            yield return new WaitForSeconds(0.3f);
            float startY = spider.transform.position.y;

            var kb = InputSystem.AddDevice<Keyboard>();
            Press(kb.spaceKey);   // first press -> jumps (cooldown set to Time.time + 0.6)
            Release(kb.spaceKey);

            // Kurze laenge nach dem Absprung: die Spinne haetter wieder, still in Cooldown (< 0.6 s).
            yield return new WaitForSeconds(0.3f);
            float midY = spider.transform.position.y;

            Press(kb.spaceKey);   // second press within cooldown -> must NOT jump again
            Release(kb.spaceKey);

            float peakAfter = midY;
            float t = 0f;
            while (t < 0.5f)
            {
                peakAfter = Mathf.Max(peakAfter, spider.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }

            float secondRise = peakAfter - midY;
            Debug.Log("B4b cooldown -> secondRise=" + secondRise.ToString("0.###") + " m -> criterion <0.35 (no 2nd jump) -> " + (secondRise < 0.35f ? "OK" : "NOK"));
            Assert.Less(secondRise, 0.35f, "Second space-press within cooldown triggered a jump: rise " + secondRise + " m");
        }

        // Criterion 3: im Netz (CurrentWeb gesetzt) -> Leertaste bleibt "fallen lassen", KEIN Sprung.
        [UnityTest]
        public IEnumerator B4b_InWeb_SpaceDoesNotJump()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht found");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");

            // Ein echts builten Netz (Layout != null) direkt um die Spinne.
            Vector3 hub = spider.transform.position + new Vector3(0f, 0.6f, 1.2f);
            Vector3[] anchors =
            {
                hub + new Vector3(0f, 1.1f, -1.1f),
                hub + new Vector3(0f, 1.1f, 1.1f),
                hub + new Vector3(0f, -1.1f, 1.1f),
                hub + new Vector3(0f, -1.1f, -1.1f),
            };
            OrbWeb web = WebBuildDemo.CreateWebAt(anchors, null, "B4bNetz");
            Assert.IsNotNull(web, "CreateWebAt lieferte null");
            web.SetProgress(OrbWeb.WebStage.Done, 1f);

            // Spinne im Netz setzen (Update-Netz-Modus frueh-return -> TryTriggerJump never reached).
            move.AttachToWeb(web);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(web, move.CurrentWeb, "CurrentWeb not set after AttachToWeb");

            float startY = spider.transform.position.y;

            var kb = InputSystem.AddDevice<Keyboard>();
            Press(kb.spaceKey);

            float peakY = startY;
            float t = 0f;
            while (t < 1.0f)
            {
                peakY = Mathf.Max(peakY, spider.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }

            float rise = peakY - startY;
            Debug.Log("B4b inweb -> rise=" + rise.ToString("0.###") + " m -> criterion <0.35 (no jump in web) -> " + (rise < 0.35f ? "OK" : "NOK"));
            Release(kb.spaceKey);
            Assert.Less(rise, 0.35f, "Space-press jumped while in web: rise " + rise + " m");
        }
    }
}
