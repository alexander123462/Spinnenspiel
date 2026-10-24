using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>T5 Vogelruf als Warnung: alarm call at the pre-warning, once per warning,
    /// descending tones, volume from GameSettings (0 -> silence).</summary>
    public class BirdAlarmCallTests
    {
        private const string SceneName = "Klettertest";

        [Test]
        public void AlarmCall_has_descending_tones()
        {
            float start = Spinnenspiel.ProceduralAudio.AlarmFrequencyAt(0f);
            float mid = Spinnenspiel.ProceduralAudio.AlarmFrequencyAt(0.5f);
            float end = Spinnenspiel.ProceduralAudio.AlarmFrequencyAt(0.99f);

            Assert.Greater(start, mid, "Tone 1 must start above the tone boundary");
            Assert.Greater(mid, end, "Tone 2 must end lower than the boundary (descending)");
            Assert.Greater(start, 400f, "Call must be in a audible bird-call range (Hz)");

            var clip = Spinnenspiel.ProceduralAudio.BirdAlarmCall();
            Assert.IsNotNull(clip, "Alarm call clip must exist");
            Assert.Greater(clip.length, 0.1f, "Clip must have samples (length in seconds)");
        }

        [UnityTest]
        public IEnumerator Vorwarnung_plays_the_alarm_call_exactly_once()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            BirdThreat threat = Object.FindFirstObjectByType<BirdThreat>();
            Assert.IsNotNull(threat, "BirdThreat missing on the spider");
            BirdAlarmCall alarm = Object.FindFirstObjectByType<BirdAlarmCall>();
            Assert.IsNotNull(alarm, "BirdAlarmCall missing on the spider");

            float t = 0f;
            while (t < 15f && !threat.Warned)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.That(threat.Warned, "Vorwarnung did not fire within 15 s");
            Assert.That(alarm.CallsPlayed == 1, "Alarm call must play exactly once per pre-warning");

            // Stay exposed longer: the call must not repeat while the bird circles on.
            for (int i = 0; i < 120; i++)
            {
                yield return null;
            }
            Assert.That(alarm.CallsPlayed == 1, "Alarm call repeated during one pre-warning");
            Debug.Log(string.Format("T5 alarm: calls={0} warned={1}", alarm.CallsPlayed, threat.Warned));
        }

        [UnityTest]
        public IEnumerator Volume_zero_means_silent_call()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameSettings settings = Object.FindFirstObjectByType<GameSettings>();
            Assert.IsNotNull(settings, "GameSettings missing in the scene");
            BirdThreat threat = Object.FindFirstObjectByType<BirdThreat>();
            Assert.IsNotNull(threat, "BirdThreat missing on the spider");
            BirdAlarmCall alarm = Object.FindFirstObjectByType<BirdAlarmCall>();
            Assert.IsNotNull(alarm, "BirdAlarmCall missing on the spider");

            settings.SetVolume(0f);

            float t = 0f;
            while (t < 15f && !threat.Warned)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.That(threat.Warned, "Vorwarnung did not fire within 15 s");
            Assert.GreaterOrEqual(alarm.CallsPlayed, 1, "Call still counted (silent)");
            Assert.That(alarm.LastCallVolume == 0f, "Call volume must be 0 when the setting is 0");

            settings.SetVolume(0.8f);
            settings.DeleteSettingsFile();
            Debug.Log(string.Format("T5 alarm volume-off: calls={0} lastVolume=0", alarm.CallsPlayed));
        }
    }
}
