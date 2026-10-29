using System;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// P1 Waechter: kleine Komponente, NUR aktiv in Tests (forceActive set by a
    /// test) oder when the command line carries -playtest. Checks the fixed rules
    /// (VerstossRulesModel) every frame and writes violations via VerstossProtocol
    /// to Logs/playtest/verstoesse.jsonl (max 1 per rule and second).
    /// While inactive its Update costs nothing (CheckedFrames stays 0).
    /// </summary>
    public class SpielWaechter : MonoBehaviour
    {
        [Tooltip("Set by tests; otherwise only -playtest on the command line activates.")]
        public bool forceActive;

        public bool IsActive { get; private set; }
        public int CheckedFrames { get; private set; }

        // P3: how many violations this run actually wrote (image trigger for the bot test).
        public int ReportsSeen { get; private set; }

        private const string LogPath = "Logs/playtest/verstoesse.jsonl";
        private const float R6IntervalS = 0.25f;
        private const float R6ProbeLen = 0.8f;
        private const int R6MinWoodHits = 4;

        private VerstossProtocol _protocol;
        private SpiderMovement _move;
        private JungleWorldGenerator _worldGen;
        private SpiderInput _silkInput;
        private Vector3 _idlePos = Vector3.zero;
        private float _idleStart = float.NegativeInfinity;
        private bool _inputWasActive;
        private float _r6Timer;

        private void Awake()
        {
            _protocol = new VerstossProtocol();
            _protocol.BeginRun(LogPath);
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            CheckedFrames++;

            // First frames after a scene load are load-hitches and unstable poses:
            // the waechter starts checking after 3 frames.
            if (CheckedFrames <= 3)
            {
                return;
            }

            if (_move == null)
            {
                _move = FindFirstObjectByType<SpiderMovement>();
                if (_move == null)
                {
                    return;
                }
            }

            if (_worldGen == null)
            {
                _worldGen = FindFirstObjectByType<JungleWorldGenerator>();
            }

            if (_silkInput == null)
            {
                _silkInput = FindFirstObjectByType<SpiderInput>();
            }

            Vector3 pos = _move.transform.position;
            float t = Time.time;
            int seed = _worldGen != null ? _worldGen.Seed : 0;

            Terrain terr = Terrain.activeTerrain;
            if (terr != null)
            {
                float terrainY = terr.SampleHeight(pos) + terr.transform.position.y;
                if (VerstossRulesModel.R1_BelowTerrain(pos.y, terrainY))
                {
                    ReportViolation(0, t, seed, pos);
                }
            }

            bool climbAllowed = _move.IsGrounded || _move.IsOnRope || _move.CurrentThread != null || _move.CurrentWeb != null;
            float upDot = Vector3.Dot(_move.transform.up, _move.SurfaceNormal);
            if (VerstossRulesModel.R2_BadSlope(upDot, climbAllowed))
            {
                                    ReportViolation(1, t, seed, pos);
            }

            // R3: input active but no progress for more than 5 s.
            Vector2 input = _silkInput != null ? _silkInput.Move : Vector2.zero;
            bool inputActive = input.SqrMagnitude() > 0.001f;
            if (inputActive)
            {
                if (!_inputWasActive)
                {
                    _idleStart = t;
                    _idlePos = pos;
                }

                float travelled = Vector3.Distance(pos, _idlePos);
                if (VerstossRulesModel.R3_NoProgress(input.SqrMagnitude(), travelled, t - _idleStart))
                {
                                        ReportViolation(2, t, seed, pos);
                }
            }

            _inputWasActive = inputActive;

            // R4: NaN/inf position or fall speed over 30 m/s.
            float fallSpeed = _move.IsGrounded ? 0f : Mathf.Max(0f, -_move.Velocity.y);
            if (VerstossRulesModel.R4_PositionOrSpeedInvalid(pos.x, pos.y, pos.z, fallSpeed))
            {
                                    ReportViolation(3, t, seed, pos);
            }

            // R5: frame time spike over 100 ms.
            float frameMs = Time.deltaTime * 1000f;
            if (VerstossRulesModel.R5_FrameSpike(frameMs))
            {
                                    ReportViolation(4, t, seed, pos);
            }

            // R6: body centre inside a tree collider (downcast into wood colliders).
            _r6Timer -= Time.deltaTime;
            if (_r6Timer <= 0f)
            {
                _r6Timer = R6IntervalS;
                if (InsideTreeCollider(pos))
                {
                                        ReportViolation(5, t, seed, pos);
                }
            }
        }

        private bool InsideTreeCollider(Vector3 pos)
        {
            // Four rays around the body centre; a wood collider (tree trunk, SurfaceTag Wood)
            // hit in all four directions means the centre is inside it. Trunk colliders sit on
            // layer 0 with a convex capsule blocker (see JungleWorldGenerator).
            int hits = 0;
            if (WoodHit(pos, Vector3.up)) { hits++; }
            if (WoodHit(pos, Vector3.down)) { hits++; }
            if (WoodHit(pos, Vector3.right)) { hits++; }
            if (WoodHit(pos, Vector3.left)) { hits++; }
            return hits >= R6MinWoodHits;
        }

        private static bool WoodHit(Vector3 from, Vector3 dir)
        {
            var ray = new Ray(from, dir);
            var hit = new RaycastHit();
            if (!Physics.Raycast(ray, out hit, R6ProbeLen, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Transform t = hit.transform;
            while (t != null)
            {
                var tag = t.GetComponent<SurfaceTag>();
                if (tag != null)
                {
                    return tag.surfaceType == SurfaceType.Wood;
                }

                t = t.parent;
            }

            return false;
        }

        private static void Unused() { }

        /// <summary>P3: counts written violations so the bot test can trigger images.</summary>
        private bool ReportViolation(int ruleIndex, float t, int seed, Vector3 pos)
        {
            bool written = _protocol.Report(ruleIndex, t, seed, pos.x, pos.y, pos.z);
            if (written) ReportsSeen++;
            return written;
        }

        private void Start()
        {
            TryActivate();
        }

        /// <summary>Active only in tests (forceActive set by the test) or with -playtest on the command line.</summary>
        public void TryActivate()
        {
            if (IsActive)
            {
                return;
            }

            if (forceActive)
            {
                IsActive = true;
                return;
            }

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-playtest")
                {
                    IsActive = true;
                    Debug.Log("[Waechter] activated by -playtest");
                    return;
                }
            }
        }
    }
}
