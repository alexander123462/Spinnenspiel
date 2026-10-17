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

        // ---- J2b: HUD-Balken value-test (no screenshot possible in batch HDRP). ----

        [Test]
        public void Bar_fill_matches_energy_100_50_20()
        {
            float f100 = Spinnenspiel.SpiderHunger.BarFill(100f);
            float f50 = Spinnenspiel.SpiderHunger.BarFill(50f);
            float f20 = Spinnenspiel.SpiderHunger.BarFill(20f);
            Assert.That(f100 >= 0.99f && f100 <= 1.01f, "Fill at energy 100 not ~1.0");
            Assert.That(f50 >= 0.49f && f50 <= 0.51f, "Fill at energy 50 not ~0.5");
            Assert.That(f20 >= 0.19f && f20 <= 0.21f, "Fill at energy 20 not ~0.2");
        }

        [Test]
        public void Bar_color_green_yellow_red_at_energy_60_40_10()
        {
            int c60 = Spinnenspiel.SpiderHunger.BarColorIndex(Spinnenspiel.SpiderHunger.BarFill(60f));
            int c40 = Spinnenspiel.SpiderHunger.BarColorIndex(Spinnenspiel.SpiderHunger.BarFill(40f));
            int c10 = Spinnenspiel.SpiderHunger.BarColorIndex(Spinnenspiel.SpiderHunger.BarFill(10f));
            Assert.That(c60 == 0, "Energy 60 not green");
            Assert.That(c40 == 1, "Energy 40 not yellow");
            Assert.That(c10 == 2, "Energy 10 not red");
        }
    }
}
