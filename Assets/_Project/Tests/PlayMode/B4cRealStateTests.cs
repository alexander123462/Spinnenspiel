using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// B4c: Ursache im echten Spiel — die Zustaeche Alex reported (Spinne am Seil/
    /// Faden, Netzbau skripted, im Netz) nachstellen and Leertaste. Per State must
    /// EITHER rise >= 0.35 m OR be a conscious, documented rejection (Leertaste =
    /// "fallen lassen" at thread/web/rope; nothing during scripted net build).
    /// Root cause found: at the Dragline rope the space press did NOTHING at all
    /// (TryTriggerJump refuses IsOnRope, SilkAiming only LetGo at thread/web) ->
    /// fixed: space = DropFromRope (fallen lassen), see SpiderMovement/SilkAiming.
    /// </summary>
    public class B4cRealStateTests : InputTestFixture
    {
        private const string SceneName = "M1_Testgelaende";

        // State 1: am Seil (Dragline) -> Leertaste = fallen lassen, NO jump rise,
        // rope released and the spider falls and lands again. Only in Urwald:
        // the M1 Testgelaende Spider has no Dragline component.
        [UnityTest]
        public IEnumerator B4c_OnRope_SpaceLetsGoAndLands()
        {
            // Seed fest wie in allen anderen Urwald-Tests (B3/A2/BotPlaytest): mit
            // zufaelligem Seed konnte ein PH-Baum-Ast (TreeCollider) direkt ueber dem
            // Spawnpunkt haengen — die Spinne landet dann korrekt am Ast und der
            // 0,3-s-Nachweis "haengt noch frei" schlug rot auf (Seed 356024, r103:
            // TreeCollider-Treffer bei y=9,50 m, 0,58 m unter dem Testfaden).
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht found in M1_Testgelaende");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");

            // Enter the rope state like the game does: stand on a thread high up,
            // then LetGo -> the spider catches on the dragline and dangles in the air.
            move.SetExternalMoveInput(Vector2.zero);
            yield return new WaitForSeconds(0.3f);
            Vector3 anchorBase = spider.transform.position;
            GameObject ropeThreadGo = new GameObject("B4cTestRopeThread");
            SilkThread ropeThread = ropeThreadGo.AddComponent<SilkThread>();
            ropeThread.Init(anchorBase + new Vector3(-1f, 1.2f, 0f), anchorBase + new Vector3(1f, 1.2f, 0f), 0.1f);
            move.AttachToThread(ropeThread, 0.5f);
            yield return new WaitForSeconds(0.3f);
            move.LetGo();
            yield return new WaitForSeconds(0.3f);
            GameObject.Destroy(ropeThreadGo);
            Assert.IsTrue(move.IsOnRope, "Seil-Status not set after LetGo/detach");

            float startY = spider.transform.position.y;

            var kb = InputSystem.AddDevice<Keyboard>();
            Press(kb.spaceKey);
            Release(kb.spaceKey);

            // After the fix the rope must be released within a few frames.
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(move.IsOnRope, "Space at rope did not release the rope (DropFromRope missing)");

            float peakY = startY;
            bool landed = false;
            float t = 0f;
            while (t < 2f && !landed)
            {
                peakY = Mathf.Max(peakY, spider.transform.position.y);
                landed = move.IsGrounded;
                t += Time.deltaTime;
                yield return null;
            }

            float rise = peakY - startY;
            Debug.Log("B4c onrope -> rise=" + rise.ToString("0.###") + " m -> criterion <0.35 (no jump, fallen lassen) -> " + (rise < 0.35f ? "OK" : "NOK"));
            Assert.Less(rise, 0.35f, "Space at rope jumped instead of letting go: rise " + rise + " m");
            Assert.IsTrue(landed, "Spider did not land after dropping from rope");
        }

        // State 2: am Faden (CurrentThread set) -> Leertaste = "fallen lassen" (LetGo),
        // NO jump rise; the spider catches on the dragline / falls.
        [UnityTest]
        public IEnumerator B4c_OnThread_SpaceLetsGoNoJump()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht found");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");

            // A real SilkThread next to the spider, attach at its middle.
            Vector3 anchorBase = spider.transform.position;
            GameObject threadGo = new GameObject("B4cTestFaden");
            SilkThread thread = threadGo.AddComponent<SilkThread>();
            thread.Init(anchorBase + new Vector3(-1f, 0.4f, 0f), anchorBase + new Vector3(1f, 0.4f, 0f), 0.1f);
            move.AttachToThread(thread, 0.5f);
            Assert.AreEqual(thread, move.CurrentThread, "CurrentThread not set");

            float startY = spider.transform.position.y;

            var kb = InputSystem.AddDevice<Keyboard>();
            Press(kb.spaceKey);
            Release(kb.spaceKey);

            yield return new WaitForSeconds(0.2f);
            Assert.IsNull(move.CurrentThread, "Space at thread did not let go (SilkAiming LetGo missing)");

            float peakY = startY;
            float t = 0f;
            while (t < 1.5f)
            {
                peakY = Mathf.Max(peakY, spider.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }

            float rise = peakY - startY;
            Debug.Log("B4c onthread -> rise=" + rise.ToString("0.###") + " m -> criterion <0.35 (no jump, fallen lassen) -> " + (rise < 0.35f ? "OK" : "NOK"));
            Assert.Less(rise, 0.35f, "Space at thread jumped instead of letting go: rise " + rise + " m");

            GameObject.Destroy(threadGo);
        }

        // State 3: Netzbau skriptmode (IsScripted, the spider is steered by the build
        // script) -> Leertaste consciously ignored: no rise, scripted state intact.
        [UnityTest]
        public IEnumerator B4c_ScriptedNetBuild_SpaceIgnored()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(1f);

            GameObject spider = GameObject.Find("Spider");
            Assert.IsNotNull(spider, "Spider nicht found");
            SpiderMovement move = spider.GetComponent<SpiderMovement>();
            Assert.IsNotNull(move, "SpiderMovement fehlt");

            move.SetExternalMoveInput(Vector2.zero);
            yield return new WaitForSeconds(0.3f);
            move.SetScripted(true);
            Assert.IsTrue(move.IsScripted, "SetScripted did not set the flag");

            float startY = spider.transform.position.y;

            var kb = InputSystem.AddDevice<Keyboard>();
            Press(kb.spaceKey);
            Release(kb.spaceKey);

            float peakY = startY;
            float t = 0f;
            while (t < 1f)
            {
                peakY = Mathf.Max(peakY, spider.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }

            float rise = peakY - startY;
            Debug.Log("B4c scripted -> rise=" + rise.ToString("0.###") + " m -> criterion <0.35 (conscious rejection) -> " + (rise < 0.35f ? "OK" : "NOK"));
            Assert.Less(rise, 0.35f, "Space jumped during scripted net build: rise " + rise + " m");
            Assert.IsTrue(move.IsScripted, "Scripted state must stay set (build script owns movement)");

            move.SetScripted(false);
        }
    }
}
