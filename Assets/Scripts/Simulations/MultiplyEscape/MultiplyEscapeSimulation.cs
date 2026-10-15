using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.MultiplyEscape
{
    /// <summary>How a round ended.</summary>
    public enum RoundOutcome
    {
        Running = 0,

        /// <summary>The swarm reached the fill target. Multiply wins.</summary>
        Full = 1,

        /// <summary>Every ball got out. Escape wins.</summary>
        Empty = 2,

        /// <summary>Hit the per-round cap. Means the config is too balanced to resolve; retune.</summary>
        TimedOut = 3
    }

    /// <summary>
    /// One ball in a circle with a spinning exit. Every wall bounce adds a ball; every ball that gets
    /// out widens the exit. Does the circle fill up, or does everything escape? Played as a series of
    /// rounds with a running score.
    /// </summary>
    /// <remarks>
    /// Determinism: balls are processed in list order (birth order), clones are appended, and every
    /// random choice (launch direction, clone turn, spin direction) is a draw from the run's seeded
    /// generator in that order. The wall is opened by switching segments off, never rebuilt mid-round.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class MultiplyEscapeSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring")]
        [Tooltip("Inactive template the pool clones.")]
        [SerializeField] GameObject _ballTemplate;
        [SerializeField] Transform _ballParent;
        [SerializeField] CircularBoundary2D _boundary;

        enum Phase
        {
            Hold,
            Running,
            Rest,
            Done
        }

        MultiplyEscapeConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        BodyPool2D _pool;

        readonly List<SwarmBall> _inside = new List<SwarmBall>();
        readonly List<SwarmBall> _escaped = new List<SwarmBall>();
        readonly List<RoundOutcome> _outcomes = new List<RoundOutcome>();
        readonly List<SwarmBall> _scratch = new List<SwarmBall>();

        Phase _phase;
        int _phaseTicks;
        int _roundTick;
        int _nextSerial;
        float _gapSpin;
        int _leaks;
        Vector2 _pendingLaunch;

        public string FormatSlug => "mve";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public int RoundNumber => Mathf.Min(_outcomes.Count + 1, RoundTotal);

        public int RoundTotal => _config != null ? _config.roundCount : 0;

        public int RoundsPlayed => _outcomes.Count;

        public int FullScore { get; private set; }

        public int EmptyScore { get; private set; }

        public int InsideCount => _inside.Count;

        public int FillTarget => _config != null ? _config.fillTarget : 0;

        public int EscapedThisRound { get; private set; }

        /// <summary>Balls born this round, the first one included. Rises once per clone.</summary>
        public int BornThisRound => _nextSerial;

        /// <summary>Most balls inside at once this round, for "it got to 480 and still emptied".</summary>
        public int PeakThisRound { get; private set; }

        public float GapDegrees => _boundary != null ? _boundary.gapDegrees : 0f;

        public bool IsResting => _phase == Phase.Rest || _phase == Phase.Done;

        public bool IsHolding => _phase == Phase.Hold;

        public RoundOutcome LastOutcome { get; private set; }

        public IReadOnlyList<SwarmBall> Inside => _inside;

        public IReadOnlyList<SwarmBall> Escaping => _escaped;

        public float BallSpeed => _config != null ? _config.ballSpeed : 1f;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (MultiplyEscapeConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 8192);

            if (_pool == null || _pool.Capacity != _config.BodyBudget)
            {
                _pool = new BodyPool2D(_ballTemplate, _ballParent, _config.BodyBudget);
            }

            _boundary.keepGapSegments = true;
            _boundary.gapDegrees = _config.startGapDegrees;
            _boundary.gapCenterDegrees = _config.gapCenterDegrees;
            _boundary.Rebuild();

            float ballDiameter = _config.ballRadius * 2f;
            if (ballDiameter >= _boundary.GapChordWidth)
            {
                Debug.LogError($"[mve] Balls ({ballDiameter:0.00}) are wider than the starting exit " +
                               $"({_boundary.GapChordWidth:0.00}). Nothing can escape, so FULL is guaranteed.", this);
            }

            _outcomes.Clear();
            FullScore = 0;
            EmptyScore = 0;
            _leaks = 0;
            IsComplete = false;
            LastOutcome = RoundOutcome.Running;

            _recorder.SetMetric("fill_target", _config.fillTarget);
            _recorder.SetMetric("rounds_planned", _config.roundCount);

            BeginRound();
        }

        void BeginRound()
        {
            _pool.ReleaseAll();
            _inside.Clear();
            _escaped.Clear();

            _nextSerial = 0;
            _roundTick = 0;
            EscapedThisRound = 0;
            PeakThisRound = 1;
            LastOutcome = RoundOutcome.Running;

            // Draw order per round: spin sign, then launch direction, then clone turns as they happen.
            _gapSpin = (_random.NextBool() ? 1f : -1f) * _config.gapAngularSpeed;
            Vector2 launch = _random.NextDirection2D();

            _boundary.transform.rotation = Quaternion.identity;
            _boundary.gapDegrees = _config.startGapDegrees;
            _boundary.RefreshGap();

            // Off-centre, sideways to the launch: from dead centre the first ball would bounce along
            // one diameter forever (the brk lesson).
            Vector2 start = new Vector2(-launch.y, launch.x) * (_boundary.radius * 0.15f);
            SwarmBall first = Spawn(start, Vector2.zero);
            _pendingLaunch = launch;

            _phase = Phase.Hold;
            _phaseTicks = Ticks(_config.preLaunchHoldSeconds);
            _recorder.LogEvent($"round_{RoundNumber}_start", _gapSpin);

            if (first == null)
            {
                Debug.LogError("[mve] Pool is empty at round start.", this);
            }
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            switch (_phase)
            {
                case Phase.Hold:
                    if (--_phaseTicks <= 0)
                    {
                        if (_inside.Count > 0)
                        {
                            _inside[0].Body.linearVelocity = _pendingLaunch * _config.ballSpeed;
                        }

                        _phase = Phase.Running;
                    }

                    return;

                case Phase.Rest:
                    StepEscaped();
                    if (--_phaseTicks <= 0)
                    {
                        if (_outcomes.Count >= _config.roundCount)
                        {
                            FinishSeries(CompletionReason.Finished);
                        }
                        else
                        {
                            BeginRound();
                        }
                    }

                    return;

                case Phase.Done:
                    return;
            }

            _roundTick++;
            _boundary.transform.Rotate(0f, 0f, _gapSpin * fixedDelta);

            StepInside();
            StepEscaped();

            if (_inside.Count > PeakThisRound)
            {
                PeakThisRound = _inside.Count;
            }

            if (_inside.Count >= _config.fillTarget)
            {
                EndRound(RoundOutcome.Full);
            }
            else if (_inside.Count == 0)
            {
                EndRound(RoundOutcome.Empty);
            }
            else if (_roundTick * fixedDelta >= _config.maxRoundSeconds)
            {
                EndRound(RoundOutcome.TimedOut);
            }
        }

        void StepInside()
        {
            float radius = _boundary.radius;
            float outer = _boundary.radius + _boundary.thickness;
            int escapedBefore = EscapedThisRound;

            // Snapshot: clones born this tick join the list but don't act until the next one.
            _scratch.Clear();
            _scratch.AddRange(_inside);
            _inside.Clear();

            for (int i = 0; i < _scratch.Count; i++)
            {
                SwarmBall ball = _scratch[i];
                Vector2 position = ball.Body.position;
                float distance = position.magnitude;
                bool inGap = _boundary.IsWithinGap(position);

                if (distance <= radius)
                {
                    ball.Exiting = false;
                }
                else if (inGap)
                {
                    ball.Exiting = true;
                }

                if (distance > outer)
                {
                    if (ball.Exiting || inGap)
                    {
                        ball.Escaped = true;
                        _escaped.Add(ball);
                        EscapedThisRound++;
                        _recorder.LogEvent("escape", _inside.Count);
                    }
                    else
                    {
                        // Squeezed through solid wall by the crowd. Not an escape: removed uncounted,
                        // and the metric says how often, so a config that leaks gets retuned.
                        _leaks++;
                        _recorder.SetMetric("leaks", _leaks);
                        if (_leaks == 1)
                        {
                            Debug.LogWarning($"[mve] A ball left through the wall at tick {_recorder.CurrentTick} " +
                                             $"(dist {distance:0.00}). Counted as a leak, not an escape.", this);
                        }

                        _pool.Release(ball.gameObject);
                    }

                    ball.ConsumeWallHit();
                    continue;
                }

                _inside.Add(ball);

                bool hit = ball.ConsumeWallHit();
                if (hit && !ball.Exiting && distance <= radius + _config.ballRadius)
                {
                    Clone(ball, position);
                }

                KeepSpeed(ball);
            }

            int escapes = EscapedThisRound - escapedBefore;
            if (escapes > 0)
            {
                _boundary.gapDegrees = Mathf.Min(360f, _boundary.gapDegrees + escapes * _config.gapDegreesPerEscape);
                _boundary.RefreshGap();
            }
        }

        void StepEscaped()
        {
            float recycle = _config.recycleDistance;
            for (int i = _escaped.Count - 1; i >= 0; i--)
            {
                SwarmBall ball = _escaped[i];
                if (ball.Body.position.magnitude > recycle)
                {
                    _pool.Release(ball.gameObject);
                    _escaped.RemoveAt(i);
                }
            }
        }

        void Clone(SwarmBall parent, Vector2 position)
        {
            if (_inside.Count + 1 > _config.fillTarget || _pool.FreeCount == 0)
            {
                // Still drawn, so the sequence doesn't depend on whether the pool had room.
                _random.NextFloat();
                _random.NextBool();
                return;
            }

            float turn = _random.NextFloat(_config.cloneTurnDegrees.x, _config.cloneTurnDegrees.y);
            if (_random.NextBool())
            {
                turn = -turn;
            }

            Vector2 heading = parent.Body.linearVelocity;
            heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : -position.normalized;
            Vector2 cloneHeading = Rotate(heading, turn);

            // Born a little toward the centre, clear of the wall and of its parent.
            Vector2 inward = position.sqrMagnitude > 1e-6f ? -position.normalized : Vector2.up;
            Vector2 spawnAt = position + inward * (_config.ballRadius * 2.2f);

            SwarmBall clone = Spawn(spawnAt, cloneHeading * _config.ballSpeed);
            if (clone != null)
            {
                _recorder.LogEvent("clone", _inside.Count);
            }
        }

        SwarmBall Spawn(Vector2 position, Vector2 velocity)
        {
            GameObject body = _pool.TryGet(position);
            if (body == null)
            {
                return null;
            }

            SwarmBall ball = body.GetComponent<SwarmBall>();
            ball.Wall = _boundary.transform;
            ball.ResetState(_nextSerial++);
            ball.Body.linearVelocity = velocity;
            _inside.Add(ball);
            return ball;
        }

        void KeepSpeed(SwarmBall ball)
        {
            Rigidbody2D body = ball.Body;
            if (body.linearVelocity.sqrMagnitude < 1e-6f)
            {
                // Stopped dead by a head-on hit. Restart it rather than leave a frozen ball.
                body.linearVelocity = _random.NextDirection2D() * _config.ballSpeed;
                return;
            }

            ConstantSpeed2D.Apply(body, _config.ballSpeed);
        }

        void EndRound(RoundOutcome outcome)
        {
            _outcomes.Add(outcome);
            LastOutcome = outcome;

            if (outcome == RoundOutcome.Full)
            {
                FullScore++;
            }
            else if (outcome == RoundOutcome.Empty)
            {
                EmptyScore++;
            }

            int round = _outcomes.Count;
            _recorder.LogEvent($"round_{round}_{outcome.ToString().ToLowerInvariant()}", _inside.Count);
            _recorder.SetMetric($"round_{round}_peak", PeakThisRound);
            _recorder.SetMetric($"round_{round}_escaped", EscapedThisRound);
            _recorder.SetMetric($"round_{round}_born", _nextSerial);
            _recorder.SetMetric($"round_{round}_seconds", _roundTick * _config.fixedTimestep);
            _recorder.SetMetric($"round_{round}_final_gap", _boundary.gapDegrees);

            // Freeze the swarm so the result reads over a still picture.
            for (int i = 0; i < _inside.Count; i++)
            {
                _inside[i].Body.linearVelocity = Vector2.zero;
            }

            _phase = Phase.Rest;
            _phaseTicks = Ticks(_config.interRoundSeconds);
        }

        void FinishSeries(CompletionReason reason)
        {
            _phase = Phase.Done;
            _recorder.SetMetric("full", FullScore);
            _recorder.SetMetric("empty", EmptyScore);

            var standings = new List<string> { $"MULTIPLY {FullScore} - {EmptyScore} ESCAPE" };
            for (int i = 0; i < _outcomes.Count; i++)
            {
                standings.Add($"round {i + 1}: {_outcomes[i]}");
            }

            _recorder.SetStandings(standings);
            _recorder.Finish(reason);
            IsComplete = true;
        }

        int Ticks(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds / _config.fixedTimestep));
        }

        static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }
    }
}
