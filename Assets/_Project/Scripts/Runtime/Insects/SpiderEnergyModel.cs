namespace Spinnenspiel
{
    /// <summary>
    /// J2a: Reine Energie-Logik der Spinne (0..100), ohne Unity-Abhaenging, so Tests
    /// pruefen it deterministically und SpiderEnergy (thin MonoBehaviour) merely feeds
    /// Time.deltaTime. Drains per activity level; feeding raises it, capped at max.
    /// </summary>
    public class SpiderEnergyModel
    {
        /// <summary>Vollstaendig Energie beim Neustart.</summary>
        public static readonly float startEnergy = 100f;

        /// <summary>Oberhaldaenergie (never exceeded by feeding).</summary>
        public static readonly float maxEnergy = 100f;

        /// <summary>Loss per second while walking.</summary>
        public static readonly float drainWalk = 1.0f;

        /// <summary>Loss per second while climbing / building web.</summary>
        public static readonly float drainClimb = 2.5f;

        /// <summary>Loss per second while standing still.</summary>
        public static readonly float drainIdle = 0.3f;

        /// <summary>Gain when eating a fly.</summary>
        public static readonly float feedFly = 25f;

        /// <summary>Gain when eating a moth.</summary>
        public static readonly float feedMoth = 40f;

        /// <summary>S1: difficulty multiplier on the drain rate (1 = Normal).</summary>
        public float difficultyMultiplier = 1f;

        /// <summary>Current energy (0..maxEnergy).</summary>
        public float Energy;

        /// <summary>Activity levels the drain-rate is chosen from.</summary>
        public enum Activity
        {
            Idle,
            Walk,
            Climb
        }

        /// <summary>Fresh partie: zero the sting counter too.</summary>
        public void Reset()
        {
            Energy = startEnergy;
            StingCount = 0;
        }

        /// <summary>Advance one tick by dt seconds at the given activity.</summary>
        public void Tick(Activity activity, float dt)
        {
            float rate = drainIdle;
            if (activity == Activity.Walk)
            {
                rate = drainWalk;
            }
            else if (activity == Activity.Climb)
            {
                rate = drainClimb;
            }

            Energy -= rate * difficultyMultiplier * dt;
            if (Energy < 0f)
            {
                Energy = 0f;
            }
            else if (Energy > maxEnergy)
            {
                Energy = maxEnergy;
            }
        }

        /// <summary>S7: Energy a wasp sting takes from the spider.</summary>
        public static readonly float stingEnergyCost = 20f;

        /// <summary>S8: how many stings hit the spider this partie (for the stingless-night goal).</summary>
        public int StingCount;

        /// <summary>S7: Sting damage — energy loss (not scaled by difficulty drain).</summary>
        public void ApplySting(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }
            StingCount++;

            Energy -= amount;
            if (Energy < 0f)
            {
                Energy = 0f;
            }
        }

        /// <summary>S6: Spend energy (e.g. repairing the web). False when not enough energy.</summary>
        public bool SpendEnergy(float amount)
        {
            if (amount <= 0f)
            {
                return false;
            }
            if (Energy < amount)
            {
                return false;
            }
            Energy -= amount;
            return true;
        }

        /// <summary>Raise energy by amount, capped at maxEnergy.</summary>
        public void Feed(float amount)
        {
            Energy += amount;
            if (Energy > maxEnergy)
            {
                Energy = maxEnergy;
            }
        }

        /// <summary>Credit arbitrary energy (used for the data-driven insect energyValue).</summary>
        public void FeedEnergy(float amount)
        {
            Feed(amount);
        }

        /// <summary>Eat a fly: +feedFly.</summary>
        public void FeedFly()
        {
            Feed(feedFly);
        }

        /// <summary>Eat a moth: +feedMoth.</summary>
        public void FeedMoth()
        {
            Feed(feedMoth);
        }

        /// <summary>Energy as fraction 0..1 (for a HUD bar).</summary>
        public float Fraction()
        {
            if (maxEnergy <= 0f)
            {
                return 0f;
            }
            return Energy / maxEnergy;
        }
    }
}
