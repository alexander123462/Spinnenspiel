using NUnit.Framework;
using System.Collections;

namespace Spinnenspiel.Tests
{
    /// <summary>J4: Vogel-Predator — Vorwarnung-Fensterrule, Deckung-rettet, Unter-Blatt-no-trigger.</summary>
    public class BirdThreatTests
    {
        private float dt = 0.1f;

        [Test]
        public void Vorwarnung_comes_between_7_5_and_8_5_seconds()
        {
            var model = new BirdThreatModel();
            float exposureAtWarn = -1f;
            for (int i = 0; i < 200; i++)
            {
                bool before = model.Warned;
                model.Tick(true, dt);
                if (!before && model.Warned)
                {
                    exposureAtWarn = model.ExposureSeconds;
                    break;
                }
            }
            Assert.That(exposureAtWarn >= 7.5f && exposureAtWarn <= 8.5f,
                "Vorwarnung not in [7.5, 8.5] s");
        }

        [Test]
        public void Cover_within_3_seconds_retreats_in_10_of_10()
        {
            int retreated = 0;
            for (int trial = 0; trial < 10; trial++)
            {
                var model = new BirdThreatModel();
                // Sitze im Freien until the pre-warning fires.
                int guard = 0;
                while (!model.Warned && guard < 200)
                {
                    model.Tick(true, dt);
                    guard++;
                }
                Assert.That(model.Warned, "Vorwarnung did not fire after 8 s");

                // Reach cover within the 3 s window (attack would be at warn+3 s).
                for (int i = 0; i < 25; i++)
                {
                    model.Tick(false, dt);
                }
                Assert.That(!model.Attacked && !model.Warned, "Retreat failed");
                if (!model.Attacked)
                {
                    retreated++;
                }
            }
            Assert.That(retreated == 10, "Retreat not in 10/10 cases");
        }

        [Test]
        public void Under_leaves_never_triggers_attack_in_10_of_10()
        {
            int noAttack = 0;
            for (int trial = 0; trial < 10; trial++)
            {
                var model = new BirdThreatModel();
                // Laengere Zeit under Blaehtern: never exposed.
                for (int i = 0; i < 200; i++)
                {
                    model.Tick(false, dt);
                }
                Assert.That(!model.Attacked && !model.Warned, "Attack fired under cover");
                if (!model.Attacked)
                {
                    noAttack++;
                }
            }
            Assert.That(noAttack == 10, "Attack triggered in some covered cases");
        }
    }
}
