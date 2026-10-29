using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// T3: Reine Frosch-Bedaenger-Logik, ohne Unity-Abaenging, so Tests pruefen it
    /// deterministically. The frog lurks on the ground near water. It lashes its tongue
    /// when the spider is low to the ground (heightAboveGround <= maxStrikeHeight) AND
    /// within strikeRange horizontally: first a 0.6 s telegraph (Aiming), then the strike
    /// lands (energy -25), then a 5 s pause before it can hunt again. Higher places
    /// (branches, trunks, web) never trigger it.
    /// </summary>
    public class FrogModel
    {
        /// <summary>Horizontal reach of the tongue strike (m).</summary>
        public static readonly float strikeRange = 0.8f;

        /// <summary>The spider must sit at/below this height above ground to be struck (m).</summary>
        public static readonly float maxStrikeHeight = 0.3f;

        /// <summary>Telegraph before the tongue lands (s).</summary>
        public static readonly float aimTime = 0.6f;

        /// <summary>Cooldown after a strike before it can hunt again (s).</summary>
        public static readonly float pauseTime = 5f;

        /// <summary>Energy the frog strike takes from the spider.</summary>
        public static readonly float energyCost = 25f;

        public enum State
        {
            Lurking,
            Aiming,
            Pause
        }

        public State state = State.Lurking;

        /// <summary>True once the strike has landed in the current cycle.</summary>
        public bool Struck;

        /// <summary>Seconds accumulated in the aiming telegraph (-1 before it).</summary>
        public float AimTimer = -1f;

        /// <summary>Seconds accumulated in the post-strike pause (-1 before it).</summary>
        public float PauseTimer = -1f;

        /// <summary>
        /// T3: Find one frog spot per water body (Sumpf/Bach) — placed on the bank at the
        /// water edge, on ground at/just above the water level. Deterministic over the seed,
        /// so tests can assert every returned spot has water within 3 m and vice versa.
        /// </summary>
        public static List<Vector3> FindFrogSpots(JungleWorld world)
        {
            var spots = new List<Vector3>();
            float size = world.Size;
            const float step = 2f;

            // Mark dry bank cells that have water within 3 m; then greedily pick at most one
            // spot per connected water cluster so we get "1 pro Sumpfzone".
            var candidates = new List<Vector2>();
            for (float x = step; x < size - step; x += step)
            {
                for (float z = step; z < size - step; z += step)
                {
                    if (world.IsUnderWater(x, z))
                    {
                        continue; // the frog sits on the bank, not in the water
                    }

                    if (!HasWaterNear(world, x, z, 3f))
                    {
                        continue;
                    }

                    candidates.Add(new Vector2(x, z));
                }
            }

            // Greedy min-distance so one swamp yields one frog (spread >= 6 m apart).
            const float minGap = 6f;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 c = candidates[i];
                bool tooClose = false;
                for (int j = 0; j < spots.Count; j++)
                {
                    if (Vector2.Distance(c, new Vector2(spots[j].x, spots[j].z)) < minGap)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    float y = world.Height(c.x, c.y);
                    spots.Add(new Vector3(c.x, y, c.y));
                }
            }

            return spots;
        }

        /// <summary>True when some point within radius (m) of (x,z) lies under water.</summary>
        public static bool HasWaterNear(JungleWorld world, float x, float z, float radius)
        {
            for (float dx = -radius; dx <= radius; dx += 1f)
            {
                for (float dz = -radius; dz <= radius; dz += 1f)
                {
                    if (world.IsUnderWater(x + dx, z + dz))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Advance one tick. dist: horizontal frog-spider distance; heightAboveGround: spider height over ground.</summary>
        public void Tick(float dist, float heightAboveGround, float dt)
        {
            bool inReach = dist <= strikeRange && heightAboveGround <= maxStrikeHeight;

            switch (state)
            {
                case State.Lurking:
                    if (inReach)
                    {
                        state = State.Aiming;
                        AimTimer = 0f;
                    }

                    break;

                case State.Aiming:
                    // Spider slipped out of reach or up before the tongue lands: abort.
                    if (!inReach)
                    {
                        state = State.Lurking;
                        AimTimer = -1f;
                        break;
                    }

                    AimTimer += dt;
                    if (AimTimer >= aimTime)
                    {
                        Struck = true;
                        AimTimer = -1f;
                        PauseTimer = 0f;
                        state = State.Pause;
                    }

                    break;

                case State.Pause:
                    PauseTimer += dt;
                    if (PauseTimer >= pauseTime)
                    {
                        PauseTimer = -1f;
                        Struck = false;
                        state = State.Lurking;
                    }

                    break;
            }
        }
    }
}
