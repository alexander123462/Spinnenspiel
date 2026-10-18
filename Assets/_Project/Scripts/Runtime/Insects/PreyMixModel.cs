namespace Spinnenspiel
{
    /// <summary>
    /// T1: Reine Mix-Logik for the day-spawner (no Unity dependency), so tests
    /// can check the beetle share deterministically. A roll below beetleShare
    /// spawns the rare, armored prey beetle instead of a fly/mosquito.
    /// </summary>
    public class PreyMixModel
    {
        /// <summary>Share of day-insects that are beetles (T1: 10 %, constant).</summary>
        public static readonly float beetleShare = 0.1f;

        /// <summary>True when this roll (0..1) from the spawner's RNG spawns a beetle.
        /// T2: the libelle band [0, libelleShare) is drawn first, so the beetle band starts above it.</summary>
        public static bool IsBeetleRoll(double roll)
        {
            return roll >= LibelleModel.libelleShare && roll < LibelleModel.libelleShare + beetleShare;
        }
    }
}
