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

        /// <summary>T2: Explicit tear flag (libelle tears the net to exactly 0 and rips it).</summary>
        private bool tornFlag;

        /// <summary>Set durability back to full (fresh net).</summary>
        public void Reset()
        {
            Durability = maxDurability;
            tornFlag = false;
        }

        /// <summary>An insect got caught: the threads wear down.</summary>
        public void AddCatch()
        {
            Durability -= catchCost;
        }

        /// <summary>S7: A wasp caught in the net tears more of it (arbitrary percent).</summary>
        public void AddDamage(float amount)
        {
            Durability -= amount;
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

        /// <summary>T2: A libelle tears the whole net: durability to exactly 0 and torn.</summary>
        public void TearNet()
        {
            Durability = 0f;
            tornFlag = true;
        }

        /// <summary>Below 0 the net is torn (or explicitly torn via TearNet) and catches nothing anymore.</summary>
        public bool Torn => tornFlag || Durability < 0f;

        /// <summary>Repair at the web: back to 100 % (the energy cost is spent by the caller).</summary>
        public void Repair()
        {
            Durability = maxDurability;
            tornFlag = false;
        }

        /// <summary>T9: restore a saved durability state exactly (value + torn flag), for the savegame.</summary>
        public void RestoreFromSave(float value, bool torn)
        {
            Durability = value;
            tornFlag = torn;
        }
    }
}
