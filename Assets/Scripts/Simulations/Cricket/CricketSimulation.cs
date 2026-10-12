using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Cricket
{
    public enum CricketPhase
    {
        /// <summary>The ball waits at the top. The board is set and readable.</summary>
        PreBall = 0,

        /// <summary>The ball is falling through the gates.</summary>
        Falling = 1,

        /// <summary>The ball is gone and its result is on screen.</summary>
        Result = 2,

        /// <summary>Between the innings.</summary>
        InningsBreak = 3,

        /// <summary>The match is decided; the result holds.</summary>
        Final = 4
    }

    /// <summary>The ball crossed a multiplier. Ticks, not seconds, so the log is reproducible.</summary>
    public readonly struct MultiplyEvent
    {
        public readonly int tick;
        public readonly int column;
        public readonly int lane;
        public readonly float value;
        public readonly float distanceBefore;
        public readonly float distanceAfter;
        public readonly Vector2 position;

        public MultiplyEvent(int tick, int column, int lane, float value, float before, float after, Vector2 position)
        {
            this.tick = tick;
            this.column = column;
            this.lane = lane;
            this.value = value;
            distanceBefore = before;
            distanceAfter = after;
            this.position = position;
        }
    }

    /// <summary>One delivery, once it is over.</summary>
    public readonly struct BallRecord
    {
        public readonly int delivery;
        public readonly int innings;
        public readonly int ballInInnings;
        public readonly BallOutcome outcome;
        public readonly float distance;

        /// <summary>Degrees clockwise from straight down the ground. Meaningless for a wicket.</summary>
        public readonly float angle;
        public readonly Vector2 position;

        public BallRecord(int delivery, int innings, int ballInInnings, BallOutcome outcome, float distance, float angle, Vector2 position)
        {
            this.delivery = delivery;
            this.innings = innings;
            this.ballInInnings = ballInInnings;
            this.outcome = outcome;
            this.distance = distance;
            this.angle = angle;
            this.position = position;
        }
    }

    /// <summary>
    /// A cricket match played on a flowing-ball strip. Each delivery launches one ball at the right end;
    /// it is pulled left through columns of gates: a multiplier scales the distance it carries, a
    /// release gate hits it (distance becomes runs) and an out gate is a wicket. Two sides bat in turn and the scorebook decides the winner.
    /// </summary>
    /// <remarks>
    /// Lane order is redrawn for every delivery, the ball's launch height is drawn too, and the angle of
    /// each shot comes from the height at which the ball left the strip, so the whole match is a function of the
    /// seed. The scorebook (<see cref="CricketMatch"/>) is separate and engine-free; this class only
    /// turns physics into one outcome per ball.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class CricketSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring")]
        [Tooltip("The one ball, reused for every delivery. Inactive between balls.")]
        [SerializeField] Rigidbody2D _ball;
        [Tooltip("Column-major: column 0 (met first, on the right) first, lanes top to bottom.")]
        [SerializeField] TriggerZone2D[] _gateZones;

        CricketConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        CricketMatch _match;
        CircleCollider2D _ballCollider;

        GateDef[][] _layout;
        readonly List<Collider2D> _entered = new List<Collider2D>();
        readonly List<Candidate> _candidates = new List<Candidate>();
        readonly List<MultiplyEvent> _multiplies = new List<MultiplyEvent>();
        readonly List<BallRecord> _deliveries = new List<BallRecord>();

        int _timerTicks;
        int _lastColumn;
        int _stillTicks;
        int _stuckTicks;
        int _inningsAtDelivery;

        struct Candidate
        {
            public int column;
            public int lane;
            public float offset;
        }

        public string FormatSlug => "cric";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        /// <summary>Changes every Initialize, so presentation can tell a new run started in the same scene.</summary>
        public int RunId { get; private set; }

        /// <summary>Counts up from 1 with every ball. A new value means the lane order changed.</summary>
        public int DeliveryId { get; private set; }

        public CricketPhase Phase { get; private set; }

        public CricketMatch Match => _match;

        public CricketConfig Config => _config;

        public int ColumnCount => _config != null ? _config.ColumnCount : 0;

        public int Lanes => _config != null ? _config.Lanes : 0;

        /// <summary>The distance the ball would score if it were hit now.</summary>
        public float Distance { get; private set; }

        public bool BallActive => _ball != null && _ball.gameObject.activeSelf;

        public Vector2 BallPosition => _ball != null ? _ball.position : Vector2.zero;

        public float BallRadius => _config != null ? _config.ballRadius : 0.14f;

        public IReadOnlyList<MultiplyEvent> Multiplies => _multiplies;

        public IReadOnlyList<BallRecord> Deliveries => _deliveries;

        public bool IsFinal => Phase == CricketPhase.Final || IsComplete;

        /// <summary>The side batting now, 0 or 1.</summary>
        public int BattingSide => _match != null ? _match.InningsIndex : 0;

        public GateDef GateAt(int column, int lane) => _layout[column][lane];

        public Vector2 GatePosition(int column, int lane) => _gateZones[column * Lanes + lane].transform.position;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (CricketConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 2048);
            RunId++;

            if (_gateZones == null || _gateZones.Length != _config.ColumnCount * _config.Lanes)
            {
                Debug.LogError($"[cric] Scene has {(_gateZones != null ? _gateZones.Length : 0)} gates but the config " +
                               $"wants {_config.ColumnCount * _config.Lanes}. Rebuild the scene from its menu.", this);
            }

            _ballCollider = _ball.GetComponent<CircleCollider2D>();
            _ballCollider.radius = _config.ballRadius;
            // The strip's pull is applied as a force toward the gates (see Fall), not as gravity.
            _ball.gravityScale = 0f;
            _ball.linearDamping = _config.linearDamping;
            HideBall();

            foreach (TriggerZone2D zone in _gateZones)
            {
                zone.Clear();
            }

            _match = new CricketMatch(_config.BallsPerInnings, _config.wicketsPerInnings, _config.ballsPerOver);
            _multiplies.Clear();
            _deliveries.Clear();
            _entered.Clear();
            _candidates.Clear();

            IsComplete = false;
            DeliveryId = 0;
            _stuckTicks = Mathf.Max(1, Mathf.RoundToInt(_config.stuckSeconds / _config.fixedTimestep));

            _recorder.SetMetric("balls_per_innings", _config.BallsPerInnings);
            _recorder.LogEvent("run_start", _config.BallsPerInnings);
            BeginDelivery();
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            switch (Phase)
            {
                case CricketPhase.PreBall:
                    if (--_timerTicks <= 0)
                    {
                        Launch();
                    }

                    break;

                case CricketPhase.Falling:
                    Fall();
                    break;

                case CricketPhase.Result:
                    if (--_timerTicks <= 0)
                    {
                        AfterResult();
                    }

                    break;

                case CricketPhase.InningsBreak:
                    if (--_timerTicks <= 0)
                    {
                        BeginDelivery();
                    }

                    break;

                case CricketPhase.Final:
                    if (--_timerTicks <= 0)
                    {
                        Finish();
                    }

                    break;
            }
        }

        void BeginDelivery()
        {
            DeliveryId++;
            _inningsAtDelivery = _match.InningsIndex;
            Distance = _config.baseDistance;

            // Draw order is fixed: one shuffle per column, first column first, then the launch height.
            _layout = new GateDef[_config.ColumnCount][];
            for (int column = 0; column < _config.ColumnCount; column++)
            {
                _layout[column] = (GateDef[])_config.columns[column].gates.Clone();
                if (_config.shuffleLanesPerDelivery)
                {
                    _random.Shuffle(_layout[column]);
                }
            }

            foreach (TriggerZone2D zone in _gateZones)
            {
                zone.Clear();
            }

            Phase = CricketPhase.PreBall;
            _timerTicks = Ticks(_config.preBallHoldSeconds);
        }

        void Launch()
        {
            float half = _config.StripHalfHeight * _config.launchSpread;
            var start = new Vector2(_config.SpawnX, _config.stripCenter.y + _random.NextFloat(-half, half));

            _ball.gameObject.SetActive(true);
            _ball.simulated = true;
            _ball.position = start;
            _ball.transform.position = start;
            _ball.rotation = 0f;
            _ball.angularVelocity = 0f;
            _ball.linearVelocity = new Vector2(-0.3f, _random.NextFloat(-0.4f, 0.4f));

            _lastColumn = -1;
            _stillTicks = 0;
            Phase = CricketPhase.Falling;
        }

        void Fall()
        {
            // The strip pulls the ball toward the gates, on the left.
            _ball.AddForce(Vector2.left * (_config.flowAcceleration * _ball.mass));
            ConstantSpeed2D.ClampTo(_ball, _config.maxSpeed);

            if (ResolveGates())
            {
                return;
            }

            if (_ball.position.x < _config.ExitX)
            {
                // Through every gate without being hit: it is released at whatever it has carried.
                EndDelivery(_config.OutcomeFor(Distance));
                return;
            }

            // Nudge a ball that has stopped on a peg. Seeded, so it is reproducible.
            if (_ball.linearVelocity.sqrMagnitude < 0.0025f)
            {
                if (++_stillTicks >= _stuckTicks)
                {
                    _stillTicks = 0;
                    _ball.linearVelocity = new Vector2(_random.NextFloat(-1.2f, 1.2f), 0.3f);
                }
            }
            else
            {
                _stillTicks = 0;
            }
        }

        /// <summary>Applies the gates the ball entered this tick. Returns true if the delivery ended.</summary>
        bool ResolveGates()
        {
            _entered.Clear();
            _candidates.Clear();
            int lanes = _config.Lanes;
            float y = _ball.position.y;

            for (int i = 0; i < _gateZones.Length; i++)
            {
                int before = _entered.Count;
                _gateZones[i].Drain(_entered);
                if (_entered.Count == before)
                {
                    continue;
                }

                for (int e = before; e < _entered.Count; e++)
                {
                    if (_entered[e] != _ballCollider)
                    {
                        continue;
                    }

                    int column = i / lanes;
                    int lane = i % lanes;
                    _candidates.Add(new Candidate
                    {
                        column = column,
                        lane = lane,
                        offset = Mathf.Abs(y - _config.LaneCenterY(lane))
                    });
                }
            }

            if (_candidates.Count == 0)
            {
                return false;
            }

            // A ball straddling two lanes touches both triggers in one tick. It is counted in the lane
            // its centre is nearest, first column first, so the result does not depend on callback order.
            _candidates.Sort((a, b) =>
            {
                int byColumn = a.column.CompareTo(b.column);
                return byColumn != 0 ? byColumn : a.offset.CompareTo(b.offset);
            });

            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate c = _candidates[i];
                if (c.column <= _lastColumn)
                {
                    continue;
                }

                _lastColumn = c.column;
                GateDef gate = _layout[c.column][c.lane];

                switch (gate.kind)
                {
                    case GateKind.Multiply:
                        float before = Distance;
                        Distance = before * gate.value;
                        _multiplies.Add(new MultiplyEvent(_recorder.CurrentTick, c.column, c.lane, gate.value, before, Distance, _ball.position));
                        _recorder.LogEvent($"mult_c{c.column + 1}", Distance);
                        break;

                    case GateKind.Release:
                        EndDelivery(_config.OutcomeFor(Distance));
                        return true;

                    case GateKind.Out:
                        EndDelivery(BallOutcome.Wicket);
                        return true;
                }
            }

            return false;
        }

        void EndDelivery(BallOutcome outcome)
        {
            Vector2 position = _ball.position;
            int innings = _match.InningsIndex;
            int ballInInnings = _match.CurrentBalls + 1;

            float angle = 0f;
            if (outcome != BallOutcome.Wicket)
            {
                // Top of the strip is one side of the ground, the bottom the other.
                float edge = Mathf.Clamp((position.y - _config.stripCenter.y) / _config.StripHalfHeight, -1f, 1f);
                angle = edge * _config.maxShotAngle + _random.NextFloat(-_config.angleJitter, _config.angleJitter);
            }

            var record = new BallRecord(DeliveryId, innings, ballInInnings, outcome, Distance, angle, position);
            _deliveries.Add(record);
            _recorder.LogEvent($"ball_i{innings + 1}_{outcome}", Distance);

            HideBall();
            _match.Record(outcome);

            Phase = CricketPhase.Result;
            _timerTicks = Ticks(_config.resultHoldSeconds);
        }

        void AfterResult()
        {
            if (_match.IsMatchOver)
            {
                Phase = CricketPhase.Final;
                _timerTicks = Ticks(_config.finishHoldSeconds);
                _recorder.LogEvent("match_over", _match.Winner);
                return;
            }

            if (_match.InningsIndex != _inningsAtDelivery)
            {
                Phase = CricketPhase.InningsBreak;
                _timerTicks = Ticks(_config.inningsBreakSeconds);
                _recorder.LogEvent("innings_break", _match.Runs(0));
                return;
            }

            BeginDelivery();
        }

        void HideBall()
        {
            _ball.linearVelocity = Vector2.zero;
            _ball.simulated = false;
            _ball.gameObject.SetActive(false);
        }

        int Ticks(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds / _config.fixedTimestep));

        void Finish()
        {
            _recorder.SetMetric("runs_a", _match.Runs(0));
            _recorder.SetMetric("wickets_a", _match.Wickets(0));
            _recorder.SetMetric("runs_b", _match.Runs(1));
            _recorder.SetMetric("wickets_b", _match.Wickets(1));
            _recorder.SetMetric("winner", _match.Winner);
            _recorder.SetStandings(new[]
            {
                $"{_config.TeamName(0)} {_match.Runs(0)}/{_match.Wickets(0)}",
                $"{_config.TeamName(1)} {_match.Runs(1)}/{_match.Wickets(1)}",
                _match.ResultText(_config.TeamName(0), _config.TeamName(1))
            });
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
