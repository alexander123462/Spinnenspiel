namespace Spinnenspiel
{
    /// <summary>
    /// J4: Reine Vogel-Bedanger-Logik, ohne Unity-Abhaenging, so EditMode/PlayMode-Tests
    /// pruefen it deterministically. The bird greps the spider if she sits laenge under
    /// open sky; reaching cover (leaves/thicket/web) resets exposure and withdraws the bird.
    /// </summary>
    public class BirdThreatModel
    {
        public float warnTime = 8f;
        public float attackDelay = 3f;

        public float ExposureSeconds;
        public float WarnAtExposure = -1f;
        public bool IsExposed;
        public bool Warned;
        public bool Attacked;
        public bool InThreatCycle;
        public float TimeSinceWarn = -1f;

        /// <summary>Advance one tick. exposed: spider currently under open sky.</summary>
        public void Tick(bool exposed, float dt)
        {
            IsExposed = exposed;
            if (exposed)
            {
                ExposureSeconds += dt;
            }
            else
            {
                ExposureSeconds = 0f;
                if (InThreatCycle)
                {
                    InThreatCycle = false;
                    Warned = false;
                    Attacked = false;
                    WarnAtExposure = -1f;
                    TimeSinceWarn = -1f;
                }
            }

            if (!exposed)
            {
                return;
            }

            if (!InThreatCycle && ExposureSeconds >= warnTime)
            {
                InThreatCycle = true;
                Warned = true;
                WarnAtExposure = ExposureSeconds;
                TimeSinceWarn = 0f;
            }

            if (InThreatCycle)
            {
                TimeSinceWarn += dt;
                if (!Attacked && TimeSinceWarn >= attackDelay)
                {
                    Attacked = true;
                }
            }
        }
    }
}
