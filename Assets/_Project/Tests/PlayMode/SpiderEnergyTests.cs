using NUnit.Framework;

namespace Spinnenspiel.Tests
{
    /// <summary>J2a: Energie-Logik der Spinne (0..100), reine Modell ohne Szene.</summary>
    public class SpiderEnergyTests
    {
        private float dt = 0.1f;

        [Test]
        public void Hundred_seconds_walking_drains_to_zero_within_ten()
        {
            var model = new SpiderEnergyModel();
            model.Reset();
            for (int i = 0; i < 1000; i++)
            {
                model.Tick(SpiderEnergyModel.Activity.Walk, dt);
            }
            Assert.That(model.Energy >= 0f && model.Energy <= 10f,
                "After 100 s walking energy not in [0,10]");
        }

        [Test]
        public void Eating_a_fly_gives_about_twenty_five_capped_at_hundred()
        {
            var model = new SpiderEnergyModel();
            model.Reset();
            // Drain below the cap so a feed of 25 is measurable.
            for (int i = 0; i < 400; i++)
            {
                model.Tick(SpiderEnergyModel.Activity.Walk, dt);
            }
            float before = model.Energy;
            model.FeedFly();
            float gained = model.Energy - before;
            Assert.That(gained >= 24f && gained <= 26f, "Fly feed not ~25");
            Assert.That(model.Energy <= SpiderEnergyModel.maxEnergy, "Energy exceeded cap");
        }

        [Test]
        public void Sixty_seconds_idle_drops_energy_by_about_eighteen()
        {
            var model = new SpiderEnergyModel();
            model.Reset();
            float before = model.Energy;
            for (int i = 0; i < 600; i++)
            {
                model.Tick(SpiderEnergyModel.Activity.Idle, dt);
            }
            float dropped = before - model.Energy;
            Assert.That(dropped >= 17f && dropped <= 19f, "Idle drop not ~18");
        }

        [Test]
        public void Fresh_start_energy_is_hundred()
        {
            var model = new SpiderEnergyModel();
            model.Reset();
            Assert.That(model.Energy >= 99.9f && model.Energy <= 100.1f, "Start energy not 100");
        }
    }
}
