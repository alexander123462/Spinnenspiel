using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace Spinnenspiel
{
    /// <summary>P2: one scripted bot input step (pure data, replayable).</summary>
    public class BotStep
    {
        public float TimeS;    // fire time, seconds since run start
        public int Action;     // 0 move-hold, 1 sprint-on, 2 jump-tap, 3 run-at-trunk, 4 net-build
        public float DirX;     // move-hold: world-ish x of the pressed WASD combo (-1..1)
        public float DirY;     // move-hold: y of the pressed WASD combo (-1..1)
        public float DurationF; // hold duration (seconds); taps use 0.12
        public bool Used;      // runtime flag: trunk-run/net-build already fired this step
    }

    /// <summary>
    /// P2 Bot spielt Probe: reine Script-Model OHNE Unity-Zugangs. A fixed int seed
    /// always yields the identical step list (deterministic LCG), so a recorded run
    /// replays identically. The test drives these steps with real device inputs.
    /// </summary>
    public class BotScriptModel
    {
        public static readonly int[] Seeds = { 7, 5, 4242, 99, 31, 2026 };
        public static readonly float RunSeconds = 60f;
        public static readonly float SegmentS = 5f;

        /// <summary>Eight WASD compass combos (N NE E SE S SW W NW).</summary>
        private static readonly int[] ComboX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] ComboY = { 1, 1, 0, -1, -1, -1, 0, 1 };

        private static uint _randState;

        private static uint NextRand()
        {
            // Numerical-Recipes LCG: identical on every platform and locale.
            _randState = 1664525u * _randState + 1013904223u;
            return _randState >> 8;
        }

        /// <summary>Builds the deterministic 60 s script for a seed: 12 segments of 5 s.
        /// Every segment holds a WASD direction (the 8 combos cycle, seed-shifted);
        /// Leertaste taps every 5 s; two targeted trunk-runs (k=4, k=9 -> climb on
        /// contact); sprint in k=2/7; one net build at t=25.</summary>
        public static List<BotStep> BuildScript(int seed)
        {
            _randState = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            var steps = new List<BotStep>();
            int segmentCount = (int)(RunSeconds / SegmentS);
            for (int k = 0; k < segmentCount; k++)
            {
                float tStart = k * SegmentS;
                int dir = (int)((NextRand() + (uint)seed + (uint)k) % 8u);
                steps.Add(MakeStep(tStart, 0, ComboX[dir], ComboY[dir], SegmentS * 0.8f));
                if (k == 2 || k == 7)
                {
                    steps.Add(MakeStep(tStart + 0.5f, 1, 0f, 0f, 3f));
                }

                // Leertaste every 5 s.
                steps.Add(MakeStep(tStart + 1f, 2, 0f, 0f, 0.12f));
                if (k == 4 || k == 9)
                {
                    steps.Add(MakeStep(tStart, 3, 0f, 0f, SegmentS * 0.8f));
                }

                if (k == 5)
                {
                    steps.Add(MakeStep(tStart, 4, 0f, 0f, 1f));
                }
            }

            return steps;
        }

        private static BotStep MakeStep(float t, int action, float dx, float dy, float dur)
        {
            var s = new BotStep();
            s.TimeS = t;
            s.Action = action;
            s.DirX = dx;
            s.DirY = dy;
            s.DurationF = dur;
            return s;
        }

        /// <summary>Saves the input sequence with timestamps to a replay file (one JSON
        /// line per step plus a header), so the run can be replayed from the file.</summary>
        public static bool SaveReplay(string path, int seed, List<BotStep> steps)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                var lines = new List<string>();
                lines.Add("{\"seed\":" + seed + ",\"run\":\"start\"}");
                foreach (BotStep s in steps)
                {
                    lines.Add("{\"t\":" + Num(s.TimeS) + ",\"a\":" + s.Action
                        + ",\"x\":" + Num(s.DirX) + ",\"y\":" + Num(s.DirY)
                        + ",\"d\":" + Num(s.DurationF) + "}");
                }

                File.WriteAllLines(path, lines);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Loads a replay file back into the step list (inverse of SaveReplay).</summary>
        public static List<BotStep> LoadReplay(string path)
        {
            var steps = new List<BotStep>();
            string all = File.ReadAllText(path);
            string[] lines = all.Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (!line.StartsWith("{\"t\""))
                {
                    continue;
                }

                float t = ReadField(line, "t");
                int a = (int)ReadField(line, "a");
                float x = ReadField(line, "x");
                float y = ReadField(line, "y");
                float d = ReadField(line, "d");
                steps.Add(MakeStep(t, a, x, y, d));
            }

            return steps;
        }

        private static string Num(float v)
        {
            if (!(v == v)) return "0";
            return v.ToString("0.####").Replace(',', '.');
        }

        /// <summary>Reads a numeric field from one JSON line (no full parser needed).</summary>
        private static float ReadField(string line, string key)
        {
            string needle = "\"" + key + "\":";
            int at = line.IndexOf(needle);
            if (at < 0) return 0f;
            int from = at + needle.Length;
            int to = from;
            while (to < line.Length && (line[to] == '-' || line[to] == '.' || (line[to] >= '0' && line[to] <= '9')))
            {
                to++;
            }

            if (to == from) return 0f;
            return ParseFloat(line.Substring(from, to - from));
        }

        public static float ParseFloat(string s)
        {
            // Own parser: locale-independent (avoids CultureInfo differences in CI).
            int i = 0;
            bool neg = false;
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
            {
                neg = s[i] == '-';
                i++;
            }

            double whole = 0.0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                whole = whole * 10.0 + (s[i] - '0');
                i++;
            }

            double frac = 0.0;
            double scale = 1.0;
            if (i < s.Length && s[i] == '.')
            {
                i++;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9')
                {
                    frac = frac * 10.0 + (s[i] - '0');
                    scale *= 10.0;
                    i++;
                }
            }

            double v = whole + frac / scale;
            if (neg) v = -v;
            return (float)v;
        }
    }
}
