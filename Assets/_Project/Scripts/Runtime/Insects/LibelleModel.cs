namespace Spinnenspiel
{
    /// <summary>
    /// T2: Reine Mix-Logik for the libelle (dragonfly) — big, fast, risky day-prey
    /// (no Unity dependency), so tests can check the share and the net-tear chance
    /// deterministically. A roll below libelleShare spawns the very rare libelle;
    /// a caught libelle tears the net (durability to 0) with tearChance and frees itself.
    /// </summary>
    public class LibelleModel
    {
        /// <summary>Share of day-insects that are libelles (T2: 3 %, very rare, constant).</summary>
        public static readonly float libelleShare = 0.03f;

        /// <summary>Chance that a caught libelle tears the net and cuts itself free (40 %).</summary>
        public static readonly float netTearChance = 0.4f;

        /// <summary>Seconds the libelle hangs in an intact net otherwise (15 s).</summary>
        public static readonly float hangTime = 15f;

        /// <summary>True when this roll (0..1) from the spawner's RNG spawns a libelle.</summary>
        public static bool IsLibelleRoll(double roll)
        {
            return roll < libelleShare;
        }

        /// <summary>True when this roll (0..1) is a net-tear (drawn per catch).</summary>
        public static bool IsTearRoll(double roll)
        {
            return roll < netTearChance;
        }
    }
}
