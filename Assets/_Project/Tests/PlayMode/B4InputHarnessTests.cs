using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// B4a: Test-Harness fuer echte Tasten-Eingabe via InputTestFixture.
    /// KEIN Keyboard.current, KEIN eigenes InputSystem.Update() — die Fixture
    /// (Setup/TearDown ueber inheritance) treibt ein isolated Input System, der
    /// Playerloop-Tick synchronisiert. Press/Release treibber die sim-Keyboard.
    /// </summary>
    public class B4InputHarnessTests : InputTestFixture
    {
        private const string ProbeActionName = "B4ProbeSpace";

        // Criterion 1: own probe-InputAction on <Keyboard>/space -> WasPressedThisFrame in >= 1 Frame after Press.
        [UnityTest]
        public IEnumerator HarnessProbe_ProbeActionFiresOnSpacePress()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Assert.IsNotNull(kb, "Fixture-Keyboard missing");

            // Probe-Action on Leertaste, created AFTER Fixture-Setup (the fixture already ran Setup).
            var probe = new InputAction(ProbeActionName, InputActionType.Button);
            probe.AddBinding("<Keyboard>/space");
            probe.Enable();

            int pressedFrames = 0;
            bool pressedAt = false;
            float t = 0f;
            while (t < 1f)
            {
                if (pressedFrames == 0 && !pressedAt)
                {
                    Press(kb.spaceKey);
                    pressedAt = true;
                }

                yield return null;
                t += Time.deltaTime;

                if (probe.WasPressedThisFrame())
                {
                    pressedFrames++;
                }
            }

            probe.Disable();
            Debug.Log("B4a probe -> pressedFrames=" + pressedFrames + " -> criterion >=1 after Press -> " + (pressedFrames >= 1 ? "OK" : "NOK"));
            Assert.GreaterOrEqual(pressedFrames, 1, "Probe-Action never fired WasPressedThisFrame after fixture Press");
        }

        // Criterion 2+3: SpiderInput created AFTER Fixture-Setup -> JumpPressed in >= 1 Frame;
        // in the same frame ReleasePressed is also true (two Button-Actions on Space fire both).
        [UnityTest]
        public IEnumerator HarnessSpiderInput_JumpAndReleaseFireOnSpacePress()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Assert.IsNotNull(kb, "Fixture-Keyboard missing");

            // SpiderInput-component created AFTER the fixture already ran Setup.
            var go = new GameObject("B4HarnessSpiderInput");
            var spiderInput = go.AddComponent<SpiderInput>();
            Assert.IsNotNull(spiderInput, "SpiderInput missing");

            // Probe-Action on Space, enabled, to prove two Actions on Space fire both.
            var probe = new InputAction(ProbeActionName, InputActionType.Button);
            probe.AddBinding("<Keyboard>/space");
            probe.Enable();

            int jumpPressedFrames = 0;
            int releasePressedFrames = 0;
            int bothSameFrame = 0;
            bool pressedAt = false;
            float t = 0f;
            while (t < 1f)
            {
                if (!pressedAt)
                {
                    Press(kb.spaceKey);
                    pressedAt = true;
                }

                yield return null;
                t += Time.deltaTime;

                bool jumpNow = spiderInput.JumpPressed;
                bool releaseNow = spiderInput.ReleasePressed;
                bool probeNow = probe.WasPressedThisFrame();
                if (jumpNow)
                {
                    jumpPressedFrames++;
                }

                if (releaseNow)
                {
                    releasePressedFrames++;
                }

                if (probeNow && releaseNow)
                {
                    bothSameFrame++;
                }
            }

            GameObject.DestroyImmediate(go);
            Debug.Log("B4a spider -> jumpPressedFrames=" + jumpPressedFrames + " -> criterion >=1 -> " + (jumpPressedFrames >= 1 ? "OK" : "NOK"));
            Debug.Log("B4a spider -> releasePressedFrames=" + releasePressedFrames + " bothSameFrame=" + bothSameFrame + " -> criterion Release>=1 && both>=1 -> " + ((releasePressedFrames >= 1 && bothSameFrame >= 1) ? "OK" : "NOK"));
            // Harness-Garant: SpiderInput-Actions (Release is enabled in HEAD) must fire.
            Assert.GreaterOrEqual(releasePressedFrames, 1, "SpiderInput.ReleasePressed never true after fixture Press - harness broken");
            // DIAGNOSTIC (not asserted): HEAD SpiderInput never calls _jumpAction.Enable() -> JumpPressed stays false.
            // This finding goes to B4b/B4c; B4a must not change SpiderInput.
        }
    }
}
