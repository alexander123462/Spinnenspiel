using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>T6 Dynamische Musik: intensity model (0.2 baseline, 0.8 danger after 1 s,
    /// falls back under 0.3 after 10 s) and the music bed volume from GameSettings.</summary>
    public class DangerMusicTests
    {
        private const string SceneName = "Klettertest";

        [Test]
        public void DangerMusic_has_baseline_02_and_danger_08()
        {
            var model = new Spinnenspiel.DangerMusicModel();
            float idle = model.GetIntensity();
            Assert.That(Mathf.Abs(idle - 0.2f) <= 0.02f, "Baseline must be 0.2 +-0.02");

            // Danger active: after 1 s at least 0.7.
            for (int i = 0; i < 60; i++)
            {
                model.Tick(true, 1f / 60f);
            }
            float hot = model.GetIntensity();
            Assert.That(hot >= 0.7f, "1 s after danger start intensity must be at least 0.7");
            Assert.That(Mathf.Abs(Spinnenspiel.DangerMusicModel.dangerLevel - 0.8f) < 0.001f,
                "Danger level constant must be 0.8");
        }

        [Test]
        public void DangerMusic_falls_back_slowly_over_10s()
        {
            var model = new Spinnenspiel.DangerMusicModel();
            for (int i = 0; i < 60; i++)
            {
                model.Tick(true, 1f / 60f);
            }
            float hot = model.GetIntensity();

            // Danger over: fall back to baseline over 10 s.
            for (int i = 0; i < 600; i++)
            {
                model.Tick(false, 1f / 60f);
            }
            float cooled = model.GetIntensity();

            Debug.Log(FormatCriterion("Intensity 10 s after danger end",
                cooled.ToString("F3"), "< 0.3", cooled < 0.3f ? "OK" : "NOK"));
            Assert.That(cooled < 0.3f, "10 s after danger end intensity must be back under 0.3");
            Assert.That(hot >= 0.7f, "Danger peak reached before the fall");
        }

        [UnityTest]
        public IEnumerator Music_bed_follows_the_bird_prewarning()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            DangerMusic music = Object.FindFirstObjectByType<DangerMusic>();
            Assert.IsNotNull(music, "DangerMusic missing on the spider");
            BirdThreat threat = Object.FindFirstObjectByType<BirdThreat>();
            Assert.IsNotNull(threat, "BirdThreat missing on the spider");

            // Safety first: intensity at baseline.
            float idle = music.IntensityNow;
            Debug.Log(FormatCriterion("Intensity without danger",
                idle.ToString("F3"), "0.2 +-0.02", Mathf.Abs(idle - 0.2f) <= 0.02f ? "OK" : "NOK"));
            Assert.That(Mathf.Abs(idle - 0.2f) <= 0.02f, "Intensity in safety must be 0.2 +-0.02");
            // Wait for the pre-warning to fire.
            float t = 0f;
            while (t < 15f && !threat.Warned)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.That(threat.Warned, "Vorwarnung did not fire within 15 s");

            // 1 s after the danger began: intensity at least 0.7.
            yield return new WaitForSeconds(1f);
            float hot = music.IntensityNow;
            Debug.Log(FormatCriterion("Intensity 1 s after danger start",
                hot.ToString("F3"), ">= 0.7", hot >= 0.7f ? "OK" : "NOK"));
            Assert.That(hot >= 0.7f, "Music intensity must rise to at least 0.7 within 1 s of danger");
        }

        [UnityTest]
        public IEnumerator Volume_zero_means_no_music_output()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            GameSettings settings = Object.FindFirstObjectByType<GameSettings>();
            Assert.IsNotNull(settings, "GameSettings missing in the scene");
            DangerMusic music = Object.FindFirstObjectByType<DangerMusic>();
            Assert.IsNotNull(music, "DangerMusic missing on the spider");

            settings.SetVolume(0f);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Assert.That(music.LastVolume == 0f, "Music bed volume must be 0 when the setting is 0");

            settings.SetVolume(0.8f);
            settings.DeleteSettingsFile();
        }

        private static string FormatCriterion(string label, string value, string criterion, string result)
        {
            return label + ": " + value + " -> " + criterion + " -> " + result;
        }
    }
}
