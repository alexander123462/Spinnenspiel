namespace Spinnenspiel
{
    /// <summary>Zentrale Layer-Konstanten für das Spiel.</summary>
    public static class GameLayers
    {
        /// <summary>Layer für Pflanzen (Spinnennähe, Netz-Anker). Bewegung/Beine/Kamera ignorieren ihn.</summary>
        public const int Plants = 8;

        /// <summary>Masken-Bit für Pflanzen.</summary>
        public const int PlantMask = 1 << Plants;
    }
}
