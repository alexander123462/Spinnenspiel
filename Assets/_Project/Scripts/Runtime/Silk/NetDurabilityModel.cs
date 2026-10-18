namespace Spinnenspiel
{
    /// <summary>
    /// S6: Reine Haltbarkeits-Modell eines Radnetz (0..100 Prozent), ohne Unity-Abhaenging,
    /// so Tests pruefen it deterministically und OrbWeb (thin coupling) merely feeds
    /// catches, rain and the repair. Under 0 the net is torn (faeden disappear).
    /// </summary>
    public class NetDurabilityModel
    {
        /// <summary>Vollstaendig Haltbarkeits (Prozent).</summary>
        public static readonly float maxDurability = 100f;

        /// <summary>Haltbarkeits-Verlust per gefaengten Insekt (Prozent).</summary>
        public static readonly float catchCost = 8f;

        /// <summary>Haltbarkeits-Verlust per 10 s vollem Regen (Prozent).</summary>
        public static readonly float rainLossPerTenSeconds = 1f;

        /// <summary>Energy the spider spends for one repair.</summary>
        public static readonly float repairEnergyCost = 5f;

        /// <summary>Current durability in percent (can go below 0 → torn).</summary>
        public float Durability;

        /// <summary>Set durability back to full (fresh net).</summary>
        public void Reset()
        {
            Durability = maxDurability;
        }

        /// <summary>An insect got caught: the threads wear down.</summary>
        public void AddCatch()
        {
            Durability -= catchCost;
        }

        /// <summary>Rain wears the net: 1 % per 10 s at full rain (intensity 0..1).</summary>
        public void TickRain(float dt, float intensity)
        {
            if (dt <= 0f || intensity <= 0f)
            {
                return;
            }
            Durability -= rainLossPerTenSeconds * intensity * (dt / 10f);
        }

        /// <summary>Below 0 the net is torn and catches nothing anymore.</summary>
        public bool Torn => Durability < 0f;

        /// <summary>Repair at the web: back to 100 % (the energy cost is spent by the caller).</summary>
        public void Repair()
        {
            Durability = maxDurability;
        }
    }
}
