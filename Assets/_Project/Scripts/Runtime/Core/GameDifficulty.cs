namespace Spinnenspiel
{
    /// <summary>
    /// S1: Difficulty levels (Entspannt / Normal / Hart) as ONE configuration class.
    /// Multipliers are the single source of truth; game code asks here instead of
    /// scattering numbers. The selected level is set from the start menu (GameMenu),
    /// survives in the savegame (SaveGame) and in the settings (GameSettings).
    /// </summary>
    public class GameDifficulty
    {
        public enum Level
        {
            Relaxed,
            Normal,
            Hard
        }

        /// <summary>Energy drain per second multiplier (Entspannt / Normal / Hart).</summary>
        private static readonly float drainRelaxed = 0.7f;
        private static readonly float drainNormal = 1.0f;
        private static readonly float drainHard = 1.4f;

        /// <summary>Prey amount multiplier (Entspannt / Normal / Hart).</summary>
        private static readonly float preyRelaxed = 1.3f;
        private static readonly float preyNormal = 1.0f;
        private static readonly float preyHard = 0.7f;

        /// <summary>Bird danger-frequency multiplier (Entspannt / Normal / Hart).
        /// Higher = bird reacts faster, so the warning interval divides down.</summary>
        private static readonly float birdRelaxed = 0.5f;
        private static readonly float birdNormal = 1.0f;
        private static readonly float birdHard = 1.6f;

        /// <summary>T12: Frequency multiplier for the new hazards (Wespe / Frosch / Libelle).
        /// Higher = more of them out, so spawn intervals divide down and shares scale up.</summary>
        private static readonly float hazardFrequencyRelaxed = 0.5f;
        private static readonly float hazardFrequencyNormal = 1.0f;
        private static readonly float hazardFrequencyHard = 1.6f;

        /// <summary>T12: Damage multiplier for the new hazards (Wespe / Frosch sting, Frosch strike).
        /// Higher = each hit takes more energy.</summary>
        private static readonly float hazardDamageRelaxed = 0.7f;
        private static readonly float hazardDamageNormal = 1.0f;
        private static readonly float hazardDamageHard = 1.3f;

        /// <summary>Runtime-selected level (default Normal; set by menu/save/settings).</summary>
        public static Level selectedLevel = Level.Normal;

        public static float DrainMultiplierOf(Level level)
        {
            if (level == Level.Relaxed)
            {
                return drainRelaxed;
            }
            return level == Level.Hard ? drainHard : drainNormal;
        }

        public static float PreyMultiplierOf(Level level)
        {
            if (level == Level.Relaxed)
            {
                return preyRelaxed;
            }
            return level == Level.Hard ? preyHard : preyNormal;
        }

        public static float BirdThreatMultiplierOf(Level level)
        {
            if (level == Level.Relaxed)
            {
                return birdRelaxed;
            }
            return level == Level.Hard ? birdHard : birdNormal;
        }

        public static float HazardFrequencyMultiplierOf(Level level)
        {
            if (level == Level.Relaxed)
            {
                return hazardFrequencyRelaxed;
            }
            return level == Level.Hard ? hazardFrequencyHard : hazardFrequencyNormal;
        }

        public static float HazardDamageMultiplierOf(Level level)
        {
            if (level == Level.Relaxed)
            {
                return hazardDamageRelaxed;
            }
            return level == Level.Hard ? hazardDamageHard : hazardDamageNormal;
        }

        public static float DrainMultiplier()
        {
            return DrainMultiplierOf(selectedLevel);
        }

        public static float PreyMultiplier()
        {
            return PreyMultiplierOf(selectedLevel);
        }

        public static float BirdThreatMultiplier()
        {
            return BirdThreatMultiplierOf(selectedLevel);
        }

        public static float HazardFrequencyMultiplier()
        {
            return HazardFrequencyMultiplierOf(selectedLevel);
        }

        public static float HazardDamageMultiplier()
        {
            return HazardDamageMultiplierOf(selectedLevel);
        }

        /// <summary>Human-readable name of a level (menu display).</summary>
        public static string LevelName(Level level)
        {
            if (level == Level.Relaxed)
            {
                return "Entspannt";
            }
            return level == Level.Hard ? "Hart" : "Normal";
        }

        /// <summary>Stored value for save/settings files: Relaxed=1, Normal=2, Hard=3.
        /// 0 means "not stored" (older file) -> Normal.</summary>
        public static int LevelToStored(Level level)
        {
            if (level == Level.Relaxed)
            {
                return 1;
            }
            return level == Level.Hard ? 3 : 2;
        }

        /// <summary>Decode file value; out-of-range or legacy files fall back to Normal.</summary>
        public static Level LevelFromStored(int stored)
        {
            if (stored == 1)
            {
                return Level.Relaxed;
            }
            if (stored == 3)
            {
                return Level.Hard;
            }
            return Level.Normal;
        }
    }
}
