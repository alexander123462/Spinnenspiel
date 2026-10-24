namespace Spinnenspiel
{
    /// <summary>
    /// S7: Reine Wespe-Bedaenger-Logik, ohne Unity-Abaenging, so Tests pruefen it
    /// deterministically. The wasp is NO prey: it searches (wanders), attacks the
    /// spider when she is noticed within noticeDistance AND exposed (open sky —
    /// cover like the bird protects), stings on contact, then withdraws and keeps
    /// a safe distance. In a web it tears the net (waspTearCost) and frees itself
    /// after escapeTime.
    /// </summary>
    public class WaspModel
    {
        /// <summary>How far the wasp notices the spider (m).</summary>
        public static readonly float noticeDistance = 5f;

        /// <summary>Energy the sting takes from the spider.</summary>
        public static readonly float stingEnergyCost = 20f;

        /// <summary>Stun duration on the spider after a sting (s).</summary>
        public static readonly float stunDuration = 1f;

        /// <summary>Distance the wasp keeps at least after the sting (m).</summary>
        public static readonly float withdrawMinDistance = 4f;

        /// <summary>Durability the wasp tears out of a net (Prozent).</summary>
        public static readonly float waspTearCost = 50f;

        /// <summary>Seconds the caught wasp needs to cut itself free.</summary>
        public static readonly float escapeTime = 3f;

        /// <summary>Distance the wasp stings from (m).</summary>
        public static readonly float stingRange = 0.12f;

        public enum State
        {
            Searching,
            Attack,
            Withdraw,
            Caught
        }

        public State state = State.Searching;

        /// <summary>True once the sting has landed in the current cycle.</summary>
        public bool Stung;

        /// <summary>Time since the sting (s), -1 before it.</summary>
        public float TimeSinceSting = -1f;

        /// <summary>True while the spider sits exposed (no cover).</summary>
        public bool IsExposed;

        /// <summary>Advance one tick. dist: wasp-spider distance; exposed: open sky above the spider.</summary>
        public void Tick(float dist, bool exposed, float dt)
        {
            IsExposed = exposed;

            switch (state)
            {
                case State.Searching:
                    // Cover protects: a covered spider is never noticed/attacked.
                    if (exposed && dist <= noticeDistance)
                    {
                        state = State.Attack;
                    }

                    break;

                case State.Attack:
                    if (!exposed)
                    {
                        // Spider slipped into cover: give up, withdraw calmly.
                        state = State.Withdraw;
                        TimeSinceSting = 0f;
                        break;
                    }

                    if (dist <= stingRange)
                    {
                        Stung = true;
                        TimeSinceSting = 0f;
                        state = State.Withdraw;
                    }

                    break;

                case State.Withdraw:
                    TimeSinceSting += dt;
                    if (TimeSinceSting >= escapeTime && dist >= withdrawMinDistance)
                    {
                        // Safe distance reached: back to searching.
                        state = State.Searching;
                        TimeSinceSting = -1f;
                    }

                    break;

                case State.Caught:
                    TimeSinceSting += dt;
                    break;
            }
        }

        /// <summary>Caught in a net: tear it and cut free after escapeTime (caller applies damage).</summary>
        public void CatchInWeb()
        {
            state = State.Caught;
            TimeSinceSting = 0f;
        }

        /// <summary>True once the caught wasp has freed itself (caller then sets Searching).</summary>
        public bool FreedAfterCatch
        {
            get
            {
                return state == State.Caught && TimeSinceSting >= escapeTime;
            }
        }
    }
}
