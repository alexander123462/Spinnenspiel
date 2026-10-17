using System;
using NUnit.Framework;
using UnityEngine;

namespace Spinnenspiel.Tests
{
    // Zeitraffer fuer PlayMode-Tests: Jeder Frame liefert eine feste 20 ms
    // Spielzeit, unabhaengig davon, wie schnell der PC rechnet. Spinne, Welt
    // und Physik laufen damit so schnell wie die GPU kann, das
    // Physik-IK-Verhaeltnis bleibt wie im Spiel (Echtzeit-Tests: 03.10.2026).
    //
    // Um die Geschwindigkeit zu aendern, kann man SPINNEN_TEST_DT auf einen
    // anderen Wert setzen, z. B. 0.02f.
    [SetUpFixture]
    public class TestSpeed
    {
        // Standardwert: 1/50 s = 20 ms pro Frame.
        static float dt()
        {
            const string EnvName = "SPINNEN_TEST_DT";
            string s = Environment.GetEnvironmentVariable(EnvName);
            if (!string.IsNullOrWhiteSpace(s) && float.TryParse(s, out float v) && v >= 0f)
            {
                return v;
            }
            return 1f / 50f;
        }

        [OneTimeSetUp]
        public static void SetUp()
        {
            float v = dt();
            if (v > 0f)
            {
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
                Time.captureDeltaTime = v;
            }
        }

        [OneTimeTearDown]
        public static void TearDown()
        {
            Time.captureDeltaTime = 0;
        }
    }
}
