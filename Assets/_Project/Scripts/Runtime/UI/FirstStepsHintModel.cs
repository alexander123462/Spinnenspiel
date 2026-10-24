using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// S9 Einstieg: pure-Logic-Model for the one-time hints at the first start.
    /// Every hint shows exactly once when its situation happens (e.g. the net-hint after
    /// 20 s without a built web) and vanishes after the first use of that control.
    /// The used-hints are stored as a bitmask so GameSettings can persist them.
    /// </summary>
    public class FirstStepsHintModel
    {
        public enum HintId
        {
            Move,
            Jump,
            Silk,
            Net,
            Collect
        }

        public static readonly HintId[] AllHints =
        {
            HintId.Move, HintId.Jump, HintId.Silk, HintId.Net, HintId.Collect
        };

        /// <summary>The hint texts (index-aligned with HintId).</summary>
        public static readonly string[] HintText =
        {
            "WASD laufen",
            "Leertaste springen",
            "F Faden spinnen",
            "N Netz bauen",
            "E Beute holen"
        };

        /// <summary>The net-hint appears after this many seconds without a built web.</summary>
        public static readonly float netHintDelaySeconds = 20f;

        private static readonly int hintCount = AllHints.Length;

        private bool[] _shown = new bool[hintCount];
        private bool[] _used = new bool[hintCount];
        private float _timeWithoutNet;
        private bool _caughtInsectSeen;

        /// <summary>Glue sets this from GameSettings ("Hinweise aus" -> nothing shows).</summary>
        public bool HintsEnabled = true;

        /// <summary>Seconds elapsed without a built web (the trigger of the net-hint).</summary>
        public float GetTimeWithoutNet() => _timeWithoutNet;

        /// <summary>Advance the situation clock. While a web is built the wait does not count.</summary>
        public void AdvanceTime(float deltaTime, bool hasBuiltWeb)
        {
            if (!hasBuiltWeb)
            {
                _timeWithoutNet += deltaTime;
            }
            EvaluateConditions();
        }

        /// <summary>A caught insect hangs in a web: the collect-hint situation.</summary>
        public void NotifyCaughtInsect()
        {
            _caughtInsectSeen = true;
            EvaluateConditions();
        }

        private void EvaluateConditions()
        {
            foreach (HintId id in AllHints)
            {
                var i = (int)id;
                if (_used[i])
                {
                    continue;
                }
                switch (id)
                {
                    case HintId.Move:
                    case HintId.Jump:
                    case HintId.Silk:
                        _shown[i] = true; // first start: available right away
                        break;
                    case HintId.Net:
                        if (_timeWithoutNet >= netHintDelaySeconds)
                        {
                            _shown[i] = true;
                        }
                        break;
                    case HintId.Collect:
                        if (_caughtInsectSeen)
                        {
                            _shown[i] = true;
                        }
                        break;
                }
            }
        }

        /// <summary>True while the hint is on screen (shown, not yet used, hints on).</summary>
        public bool IsVisible(HintId id)
        {
            var i = (int)id;
            return HintsEnabled && _shown[i] && !_used[i];
        }

        /// <summary>The control was used: the hint vanishes and never comes back.</summary>
        public void MarkUsed(HintId id)
        {
            var i = (int)id;
            _used[i] = true;
            _shown[i] = false;
        }

        public bool IsUsed(HintId id) => _used[(int)id];

        /// <summary>How many hints are currently on screen.</summary>
        public int VisibleCount()
        {
            var n = 0;
            foreach (HintId id in AllHints)
            {
                if (IsVisible(id))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>Bitmask of used hints (bit = HintId), persisted by GameSettings.</summary>
        public int UsedMask()
        {
            var mask = 0;
            foreach (HintId id in AllHints)
            {
                if (_used[(int)id])
                {
                    mask |= 1 << (int)id;
                }
            }
            return mask;
        }

        /// <summary>Restore used hints from a persisted bitmask (reload: used stays used).</summary>
        public void LoadUsedMask(int mask)
        {
            foreach (HintId id in AllHints)
            {
                _used[(int)id] = (mask & (1 << (int)id)) != 0;
            }
        }
    }
}
