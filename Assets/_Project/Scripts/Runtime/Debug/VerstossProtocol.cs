using System;
using System.IO;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// P1 Waechter: Verstoss-Protokoll. Appends one JSON line per violation to
    /// Logs/playtest/verstoesse.jsonl with time, seed, position and rule.
    /// At most ONE entry per rule and second (dedup window).
    /// </summary>
    public class VerstossProtocol
    {
        private readonly float[] _lastRuleTime = new float[16];
        private readonly bool[] _hasEntry = new bool[16];
        private string _path;
        private int _written;

        public int WrittenCount { get; private set; }

        /// <summary>Starts a run: truncates the log file and resets the dedup window.</summary>
        public void BeginRun(string relativePath)
        {
            _path = relativePath;
            for (int i = 0; i < _hasEntry.Length; i++)
            {
                _hasEntry[i] = false;
            }

            WrittenCount = 0;
            DirectoryEnsure();
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        /// <summary>
        /// Logs one violation. ruleIndex 0..5 maps to R1..R6. Returns true when the
        /// line was written (false = suppressed by the 1-per-second dedup window).
        /// </summary>
        public bool Report(int ruleIndex, float timeS, int seed, float x, float y, float z)
        {
            if (ruleIndex < 0 || ruleIndex >= _hasEntry.Length || _path == null)
            {
                return false;
            }

            if (_hasEntry[ruleIndex] && timeS - _lastRuleTime[ruleIndex] < 1f)
            {
                return false;
            }

            _hasEntry[ruleIndex] = true;
            _lastRuleTime[ruleIndex] = timeS;

            string line = "{\"t\":" + Num(timeS) + ",\"seed\":" + seed
                + ",\"pos\":[" + Num(x) + "," + Num(y) + "," + Num(z) + "]"
                + ",\"rule\":\"R" + (ruleIndex + 1) + "\"}";
            File.AppendAllText(_path, line + "\n");
            WrittenCount++;
            return true;
        }

        /// <summary>Locale-safe number formatting; NaN/inf become 0 so the JSON stays valid.</summary>
        private static string Num(float v)
        {
            if (!(v == v) || v == float.PositiveInfinity || v == float.NegativeInfinity)
            {
                return "0";
            }

            return v.ToString("0.###").Replace(',', '.');
        }

        private void DirectoryEnsure()
        {
            int slash = _path.LastIndexOf('\\');
            int slash2 = _path.LastIndexOf('/');
            int cut = Math.Max(slash, slash2);
            if (cut > 0)
            {
                Directory.CreateDirectory(_path.Substring(0, cut));
            }
        }
    }
}

