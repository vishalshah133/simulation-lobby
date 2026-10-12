using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.ReleaseOrMultiply
{
    /// <summary>A marble crossed a gate. Ticks, not seconds, so the log is reproducible.</summary>
    public readonly struct GateEvent
    {
        public readonly int tick;
        public readonly int row;
        public readonly int lane;
        public readonly GateDef gate;
        public readonly long before;
        public readonly long after;
        public readonly int clonesAdded;
        public readonly Vector2 position;

        public GateEvent(int tick, int row, int lane, GateDef gate, long before, long after, int clonesAdded, Vector2 position)
        {
            this.tick = tick;
            this.row = row;
            this.lane = lane;
            this.gate = gate;
            this.before = before;
            this.after = after;
            this.clonesAdded = clonesAdded;
            this.position = position;
        }
    }

    /// <summary>
    /// Marbles are released at the top and fall through rows of gates that multiply, add, halve or wipe
    /// the count they carry. The number at the bottom is the video.
    /// </summary>
    /// <remarks>
    /// The count is exact and the body count is capped: a marble stands for <see cref="Marble2D.Weight"/>
    /// marbles, clones are added only while the budget allows, and past it a multiplier just makes the
    /// marble heavier. Gate effects are applied in marble-serial order and every random choice (lane
    /// order, drop positions, clone scatter, nudges) is a draw from the run's seeded generator, so the
    /// same seed gives the same run.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class RomSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring")]
        [Tooltip("Inactive template the pool clones.")]
        [SerializeField] GameObject _marbleTemplate;
        [SerializeField] Transform _marbleParent;
        [Tooltip("Row-major: row 0 first, lanes left to right.")]
        [SerializeField] TriggerZone2D[] _gateZones;

        RomConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        BodyPool2D _pool;

        GateDef[][] _layout;
        readonly List<Marble2D> _marbles = new List<Marble2D>();
        readonly List<Collider2D> _entered = new List<Collider2D>();
        readonly List<Crossing> _crossings = new List<Crossing>();
        readonly List<GateEvent> _events = new List<GateEvent>();

        int _nextSerial;
        int _toDrop;
        int _dropTicks;
        int _holdTicksRemaining;
        long _banked;
        long _inFlight;
        long _peak;
        int _wipes;
        int _stuckTicks;

        struct Crossing
        {
            public Marble2D marble;
            public int row;
            public int lane;
        }

        public string FormatSlug => "rom";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        /// <summary>Changes every Initialize, so presentation can tell a new run started in the same scene.</summary>
        public int RunId { get; private set; }

        public int RowCount => _config != null ? _config.RowCount : 0;

        public int Lanes => _config != null ? _config.Lanes : 0;

        /// <summary>Everything counted so far: marbles still falling plus marbles that have landed.</summary>
        public long TotalCount => _banked + _inFlight;

        public long Banked => _banked;

        public long PeakCount => _peak;

        /// <summary>The run is over and the number is final.</summary>
        public bool IsFinal => IsComplete || _holdTicksRemaining >= 0;

        /// <summary>Marbles still waiting to be dropped.</summary>
        public int DropsRemaining => _toDrop;

        public int ActiveBodies => _marbles.Count;

        public float MarbleRadius => _config != null ? _config.marbleRadius : 0.1f;

        public int BodyBudget => _config != null ? _config.maxBodies : 0;

        public IReadOnlyList<Marble2D> Marbles => _marbles;

        public IReadOnlyList<GateEvent> Events => _events;

        /// <summary>The gate in a lane for this run. Lane order is drawn from the seed.</summary>
        public GateDef GateAt(int row, int lane) => _layout[row][lane];

        public Vector2 GatePosition(int row, int lane) => _gateZones[row * Lanes + lane].transform.position;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (RomConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 8192);
            RunId++;

            if (_gateZones == null || _gateZones.Length != _config.RowCount * _config.Lanes)
            {
                Debug.LogError($"[rom] Scene has {(_gateZones != null ? _gateZones.Length : 0)} gates but the config " +
                               $"wants {_config.RowCount * _config.Lanes}. Rebuild the scene from its menu.", this);
            }

            if (_pool == null || _pool.Capacity != _config.maxBodies)
            {
                _pool = new BodyPool2D(_marbleTemplate, _marbleParent, _config.maxBodies);
            }

            _pool.ReleaseAll();
            _marbles.Clear();
            _events.Clear();
            _crossings.Clear();
            foreach (TriggerZone2D zone in _gateZones)
            {
                zone.Clear();
            }

            IsComplete = false;
            _nextSerial = 0;
            _toDrop = _config.startMarbles;
            _dropTicks = 0;
            _holdTicksRemaining = -1;
            _banked = 0;
            _inFlight = 0;
            _peak = 0;
            _wipes = 0;
            _stuckTicks = Mathf.Max(1, Mathf.RoundToInt(_config.stuckSeconds / _config.fixedTimestep));

            // Draw order is fixed: one shuffle per row, top row first, then drops as they happen.
            _layout = new GateDef[_config.RowCount][];
            for (int row = 0; row < _config.RowCount; row++)
            {
                _layout[row] = (GateDef[])_config.rows[row].gates.Clone();
                if (_config.shuffleGatesPerRun)
                {
                    _random.Shuffle(_layout[row]);
                }
            }

            _recorder.SetMetric("rows", _config.RowCount);
            _recorder.SetMetric("start_marbles", _config.startMarbles);
            _recorder.LogEvent("run_start", _config.startMarbles);
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            if (_holdTicksRemaining >= 0)
            {
                if (_holdTicksRemaining-- == 0)
                {
                    Finish();
                }

                return;
            }

            Release();
            ResolveGates();
            MoveAndBank();

            if (_toDrop == 0 && _marbles.Count == 0)
            {
                _holdTicksRemaining = Mathf.RoundToInt(_config.finishHoldSeconds / _config.fixedTimestep);
                _recorder.SetMetric("final_count", _banked);
                _recorder.LogEvent("all_landed", _banked);
            }
        }

        void Release()
        {
            if (_toDrop <= 0)
            {
                return;
            }

            if (_dropTicks-- > 0)
            {
                return;
            }

            _dropTicks = Mathf.Max(1, Mathf.RoundToInt(_config.dropIntervalSeconds / _config.fixedTimestep));

            float half = _config.FieldHalfWidth * _config.dropSpread;
            Marble2D marble = Spawn(new Vector2(_random.NextFloat(-half, half), _config.spawnY), 1L, -1);
            if (marble == null)
            {
                return;
            }

            marble.Body.linearVelocity = new Vector2(_random.NextFloat(-0.4f, 0.4f), 0f);
            _toDrop--;
            _inFlight += 1;
            Track();
        }

        void ResolveGates()
        {
            _entered.Clear();
            for (int i = 0; i < _gateZones.Length; i++)
            {
                int before = _entered.Count;
                _gateZones[i].Drain(_entered);

                for (int e = before; e < _entered.Count; e++)
                {
                    if (!_entered[e].TryGetComponent(out Marble2D marble) || !marble.gameObject.activeSelf)
                    {
                        continue;
                    }

                    int row = i / _config.Lanes;
                    if (marble.LastGateRow >= row)
                    {
                        continue;
                    }

                    _crossings.Add(new Crossing { marble = marble, row = row, lane = i % _config.Lanes });
                }
            }

            if (_crossings.Count == 0)
            {
                return;
            }

            // Physics callback order isn't stable, so the order effects land in is made stable here:
            // by marble serial, then row, then lane. Each effect can spend random draws and body budget.
            _crossings.Sort((a, b) =>
            {
                int bySerial = a.marble.Serial.CompareTo(b.marble.Serial);
                if (bySerial != 0)
                {
                    return bySerial;
                }

                int byRow = a.row.CompareTo(b.row);
                return byRow != 0 ? byRow : a.lane.CompareTo(b.lane);
            });

            for (int i = 0; i < _crossings.Count; i++)
            {
                Crossing crossing = _crossings[i];
                if (!crossing.marble.gameObject.activeSelf || crossing.marble.LastGateRow >= crossing.row)
                {
                    continue;
                }

                Apply(crossing.marble, crossing.row, crossing.lane);
            }

            _crossings.Clear();
        }

        void Apply(Marble2D marble, int row, int lane)
        {
            GateDef gate = _layout[row][lane];
            long before = marble.Weight;
            marble.LastGateRow = row;

            long total;
            int wantedBodies;
            switch (gate.kind)
            {
                case GateKind.Multiply:
                    total = SaturatingMultiply(before, gate.value);
                    wantedBodies = gate.value;
                    break;

                case GateKind.Add:
                    total = before + gate.value;
                    wantedBodies = 1 + gate.value;
                    break;

                case GateKind.Halve:
                    // A lone marble can't be halved, so it lives or dies on a seeded flip.
                    total = before > 1 ? before / 2 : (_random.Chance(0.5f) ? 1L : 0L);
                    wantedBodies = 1;
                    break;

                default:
                    total = 0;
                    wantedBodies = 0;
                    break;
            }

            total = Math.Min(total, Math.Max(before, _config.countCap - (TotalCount - before)));
            _inFlight += total - before;
            Track();

            Vector2 position = marble.Body.position;

            if (total <= 0)
            {
                _wipes++;
                Remove(marble);
                AddEvent(row, lane, gate, before, 0, 0, position);
                return;
            }

            // One body per wanted marble while the budget holds; past it the weight is shared out
            // among whatever bodies exist.
            int extra = Math.Min(wantedBodies - 1, Math.Min(_config.maxClonesPerGate, _pool.FreeCount));
            extra = (int)Math.Min(extra, total - 1);
            extra = Math.Max(0, extra);

            int bodies = 1 + extra;
            long share = total / bodies;
            marble.Weight = share + total % bodies;

            Vector2 velocity = marble.Body.linearVelocity;
            for (int c = 0; c < extra; c++)
            {
                Vector2 offset = _random.NextDirection2D() * (_config.marbleRadius * 1.5f);
                Marble2D clone = Spawn(position + offset, share, row);
                if (clone == null)
                {
                    // The budget moved under us; give the weight back rather than lose count.
                    marble.Weight += share;
                    continue;
                }

                clone.Body.linearVelocity = velocity + _random.NextDirection2D() * _random.NextFloat(0.4f, 1.6f);
            }

            AddEvent(row, lane, gate, before, total, extra, position);
        }

        void MoveAndBank()
        {
            for (int i = _marbles.Count - 1; i >= 0; i--)
            {
                Marble2D marble = _marbles[i];
                Rigidbody2D body = marble.Body;

                ConstantSpeed2D.ClampTo(body, _config.maxFallSpeed);

                if (body.position.y < _config.BottomY)
                {
                    _banked += marble.Weight;
                    _inFlight -= marble.Weight;
                    Remove(marble);
                    continue;
                }

                // Nudge anything that has stopped on a peg. Seeded, in list order, so it is reproducible.
                if (body.linearVelocity.sqrMagnitude < 0.0025f)
                {
                    if (++marble.StillTicks >= _stuckTicks)
                    {
                        marble.StillTicks = 0;
                        body.linearVelocity = new Vector2(_random.NextFloat(-1.2f, 1.2f), 0.3f);
                    }
                }
                else
                {
                    marble.StillTicks = 0;
                }
            }
        }

        Marble2D Spawn(Vector2 position, long weight, int gateRow)
        {
            GameObject body = _pool.TryGet(position);
            if (body == null)
            {
                return null;
            }

            Marble2D marble = body.GetComponent<Marble2D>();
            marble.Serial = _nextSerial++;
            marble.Weight = weight;
            marble.LastGateRow = gateRow;
            marble.StillTicks = 0;
            marble.StyledWeight = 0;

            marble.Collider.radius = _config.marbleRadius;
            Rigidbody2D rigidbody = marble.Body;
            rigidbody.gravityScale = _config.gravityScale;
            rigidbody.linearDamping = _config.linearDamping;

            _marbles.Add(marble);
            return marble;
        }

        void Remove(Marble2D marble)
        {
            _marbles.Remove(marble);
            _pool.Release(marble.gameObject);
        }

        void Track()
        {
            _peak = Math.Max(_peak, TotalCount);
        }

        static long SaturatingMultiply(long a, long b)
        {
            return a > long.MaxValue / Math.Max(1L, b) ? long.MaxValue : a * b;
        }

        void AddEvent(int row, int lane, GateDef gate, long before, long after, int clones, Vector2 position)
        {
            _events.Add(new GateEvent(_recorder.CurrentTick, row, lane, gate, before, after, clones, position));
            _recorder.LogEvent($"gate_r{row + 1}_{gate.kind}", after - before);
        }

        void Finish()
        {
            _recorder.SetMetric("final_count", _banked);
            _recorder.SetMetric("peak_count", _peak);
            _recorder.SetMetric("wipes", _wipes);
            _recorder.SetMetric("gate_crossings", _events.Count);
            _recorder.SetStandings(new[] { $"{_banked:N0} marbles" });
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
