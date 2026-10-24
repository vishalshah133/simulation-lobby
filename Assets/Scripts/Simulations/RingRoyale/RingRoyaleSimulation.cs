using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.RingRoyale
{
    /// <summary>
    /// Several colours of marbles in one spinning ring. A marble that leaves through the EXIT gap is out; a
    /// wall hit inside the gold x2 arc clones it (same colour). From a set time the EXIT widens, so the
    /// game resolves. The last colour with marbles inside wins.
    /// </summary>
    /// <remarks>
    /// Determinism: marbles are processed in list order, clones are born into that list, and every random
    /// choice (spin direction, start positions, launch headings, clone turns) is a draw from the run's seeded
    /// generator in that order. A clone's draws are made even when the cap blocks it. The wall is opened by
    /// switching segments off, never rebuilt mid-game.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class RingRoyaleSimulation : MonoBehaviour, ISimulation
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
            Seal,
            Result,
            Done
        }

        RingRoyaleConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        BodyPool2D _pool;

        readonly List<RoyaleMarble> _inside = new List<RoyaleMarble>();
        readonly List<RoyaleMarble> _escaped = new List<RoyaleMarble>();
        readonly List<RoyaleMarble> _scratch = new List<RoyaleMarble>();
        readonly List<Vector2> _launchHeadings = new List<Vector2>();
        readonly List<int> _eliminationOrder = new List<int>();

        int[] _counts;
        int[] _previousCounts;
        bool[] _eliminated;

        Phase _phase;
        int _phaseTicks;
        int _runTicks;
        int _nextSerial;
        int _alive;
        int _leaks;
        int _capBlocked;
        int _leader;
        int _leadChanges;
        int _peakAlive;
        float _spin;
        float _sealFromDegrees;
        bool _timedOut;

        public string FormatSlug => "royale";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public int ColourCount => _counts != null ? _counts.Length : 0;

        /// <summary>Marbles inside the ring per colour.</summary>
        public IReadOnlyList<int> Counts => _counts;

        public bool IsEliminated(int colour)
        {
            return _eliminated != null && colour >= 0 && colour < _eliminated.Length && _eliminated[colour];
        }

        public int AliveTotal => _alive;

        /// <summary>Colour that won, or -1 while the game is on.</summary>
        public int Winner { get; private set; }

        /// <summary>Marbles the winner finished with.</summary>
        public int WinnerMarbles { get; private set; }

        public bool TimedOut => _timedOut;

        /// <summary>The config this run was started with, for presentation to read colours and names.</summary>
        public RingRoyaleConfig Config => _config;

        public bool IsHolding => _phase == Phase.Hold;

        public bool IsRunning => _phase == Phase.Running;

        /// <summary>The game is decided; the result is on screen.</summary>
        public bool IsResting => _phase == Phase.Seal || _phase == Phase.Result || _phase == Phase.Done;

        /// <summary>0 when the win is decided, 1 once the EXIT has closed behind the winners.</summary>
        public float SealProgress { get; private set; }

        /// <summary>The EXIT has closed: the victory is sealed.</summary>
        public bool IsSealed { get; private set; }

        public bool SuddenDeathActive { get; private set; }

        /// <summary>Seconds since launch.</summary>
        public float GameSeconds { get; private set; }

        /// <summary>Current EXIT width, degrees.</summary>
        public float ExitDegrees => _boundary != null ? _boundary.gapDegrees : 0f;

        /// <summary>EXIT centre in the ring's own frame (does not spin). Children of the ring use this.</summary>
        public float ExitCenterLocalDegrees => _boundary != null ? _boundary.gapCenterDegrees : 0f;

        /// <summary>EXIT centre in world space, including the ring's spin.</summary>
        public float ExitCenterDegrees => _boundary != null ? _boundary.EffectiveGapCenterDegrees : 0f;

        public float X2ArcDegrees => _config != null ? _config.x2ArcDegrees : 0f;

        public float X2CenterLocalDegrees => _config != null ? _config.x2CenterDegrees : 0f;

        /// <summary>x2 arc centre in world space, including the ring's spin.</summary>
        public float X2CenterDegrees => X2CenterLocalDegrees + (_boundary != null ? _boundary.transform.eulerAngles.z : 0f);

        /// <summary>Every wall hit inside the x2 arc, clone born or not. Rises once per hit.</summary>
        public int X2HitCount { get; private set; }

        public int LastX2Colour { get; private set; }

        /// <summary>Clones actually born.</summary>
        public int CloneCount { get; private set; }

        public int LastCloneColour { get; private set; }

        /// <summary>World angle of the last clone's wall contact, degrees.</summary>
        public float LastCloneAngle { get; private set; }

        /// <summary>Marbles that have left through the EXIT.</summary>
        public int ExitCount { get; private set; }

        public int LastExitColour { get; private set; }

        public Vector2 LastExitPoint { get; private set; }

        /// <summary>Colours eliminated so far.</summary>
        public int EliminationCount { get; private set; }

        public int LastEliminatedColour { get; private set; }

        public IReadOnlyList<RoyaleMarble> Inside => _inside;

        public IReadOnlyList<RoyaleMarble> Escaping => _escaped;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (RingRoyaleConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 8192);

            if (_pool == null || _pool.Capacity != _config.BodyBudget)
            {
                _pool = new BodyPool2D(_ballTemplate, _ballParent, _config.BodyBudget);
            }

            int colours = _config.ColourCount;
            _counts = new int[colours];
            _previousCounts = new int[colours];
            _eliminated = new bool[colours];

            _boundary.keepGapSegments = true;
            _boundary.gapDegrees = _config.exitStartDegrees;
            _boundary.gapCenterDegrees = _config.exitCenterDegrees;
            _boundary.transform.rotation = Quaternion.identity;
            _boundary.Rebuild();

            float ballDiameter = _config.ballRadius * 2f;
            if (ballDiameter >= _boundary.GapChordWidth)
            {
                Debug.LogError($"[royale] Marbles ({ballDiameter:0.00}) are wider than the starting exit " +
                               $"({_boundary.GapChordWidth:0.00}). Nothing can leave, so growth is unchecked until sudden death.",
                               this);
            }

            _pool.ReleaseAll();
            _inside.Clear();
            _escaped.Clear();
            _scratch.Clear();
            _launchHeadings.Clear();
            _eliminationOrder.Clear();

            _nextSerial = 0;
            _alive = 0;
            _leaks = 0;
            _capBlocked = 0;
            _leader = -1;
            _leadChanges = 0;
            _peakAlive = 0;
            _runTicks = 0;
            _timedOut = false;
            GameSeconds = 0f;
            SuddenDeathActive = false;
            SealProgress = 0f;
            IsSealed = false;
            _sealFromDegrees = 0f;
            Winner = -1;
            WinnerMarbles = 0;
            X2HitCount = 0;
            CloneCount = 0;
            ExitCount = 0;
            EliminationCount = 0;
            LastX2Colour = -1;
            LastCloneColour = -1;
            LastExitColour = -1;
            LastEliminatedColour = -1;
            IsComplete = false;

            // Draw order: spin sign, then start positions (colour by colour in serial order), then one
            // launch heading per marble, then clone turns as they happen.
            _spin = (_random.NextBool() ? 1f : -1f) * _config.ringAngularSpeed;

            int total = colours * _config.marblesPerColour;
            var positions = new List<Vector2>(total);
            float spawnRadius = _boundary.radius * _config.spawnRadiusFraction;
            float spacingSqr = _config.spawnSpacing * _config.spawnSpacing;
            for (int i = 0; i < total; i++)
            {
                Vector2 point = Vector2.zero;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    float angle = _random.NextFloat(0f, Mathf.PI * 2f);
                    float distance = spawnRadius * Mathf.Sqrt(_random.NextFloat());
                    point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                    bool clear = true;
                    for (int j = 0; j < positions.Count; j++)
                    {
                        if ((positions[j] - point).sqrMagnitude < spacingSqr)
                        {
                            clear = false;
                            break;
                        }
                    }

                    if (clear)
                    {
                        break;
                    }
                }

                positions.Add(point);
            }

            for (int i = 0; i < total; i++)
            {
                _launchHeadings.Add(_random.NextDirection2D());
            }

            for (int i = 0; i < total; i++)
            {
                Spawn(positions[i], Vector2.zero, i % colours);
            }

            _peakAlive = _alive;
            _phase = Phase.Hold;
            _phaseTicks = Ticks(_config.preLaunchHoldSeconds);

            _recorder.SetMetric("colours", colours);
            _recorder.SetMetric("marbles_start", total);
            _recorder.SetMetric("spin_dps", _spin);
            _recorder.LogEvent("hold", total);
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            switch (_phase)
            {
                case Phase.Hold:
                    if (--_phaseTicks <= 0)
                    {
                        Launch();
                        _phase = Phase.Running;
                    }

                    return;

                case Phase.Seal:
                    StepEscaped();
                    StepSeal();
                    return;

                case Phase.Result:
                    StepEscaped();
                    if (--_phaseTicks <= 0)
                    {
                        FinishRun();
                    }

                    return;

                case Phase.Done:
                    return;
            }

            _runTicks++;
            GameSeconds = _runTicks * fixedDelta;
            _boundary.transform.Rotate(0f, 0f, _spin * fixedDelta);
            WidenExit();

            StepInside();
            StepEscaped();
            Resolve();
        }

        void Launch()
        {
            for (int i = 0; i < _inside.Count; i++)
            {
                _inside[i].Body.linearVelocity = _launchHeadings[i] * _config.ballSpeed;
            }

            _recorder.LogEvent("launch", _alive);
        }

        void WidenExit()
        {
            bool active = GameSeconds >= _config.suddenDeathAtSeconds;
            if (active && !SuddenDeathActive)
            {
                _recorder.LogEvent("sudden_death", _alive);
            }

            SuddenDeathActive = active;
            if (!active)
            {
                return;
            }

            float target = Mathf.Min(_config.maxExitDegrees,
                _config.exitStartDegrees + (GameSeconds - _config.suddenDeathAtSeconds) * _config.suddenDeathDegreesPerSecond);
            if (target > _boundary.gapDegrees)
            {
                _boundary.gapDegrees = target;
                _boundary.RefreshGap();
            }
        }

        void StepInside()
        {
            float radius = _boundary.radius;
            float outer = _boundary.radius + _boundary.thickness;
            System.Array.Copy(_counts, _previousCounts, _counts.Length);

            // Snapshot: clones born this tick join the list but don't act until the next one.
            _scratch.Clear();
            _scratch.AddRange(_inside);
            _inside.Clear();

            for (int i = 0; i < _scratch.Count; i++)
            {
                RoyaleMarble marble = _scratch[i];
                Vector2 position = marble.Body.position;
                float distance = position.magnitude;
                bool inGap = _boundary.IsWithinGap(position);

                if (distance <= radius)
                {
                    marble.Exiting = false;
                }
                else if (inGap)
                {
                    marble.Exiting = true;
                }

                if (distance > outer)
                {
                    marble.Sensor.Consume(out _);
                    _counts[marble.Colour]--;
                    _alive--;

                    if (marble.Exiting || inGap)
                    {
                        marble.Escaped = true;
                        _escaped.Add(marble);
                        ExitCount++;
                        LastExitColour = marble.Colour;
                        LastExitPoint = position;
                        _recorder.LogEvent("exit", marble.Colour);
                    }
                    else
                    {
                        // Squeezed through solid wall by the crowd. Not an exit: removed uncounted, and
                        // the metric says how often, so a config that leaks gets retuned.
                        _leaks++;
                        _recorder.SetMetric("leaks", _leaks);
                        if (_leaks == 1)
                        {
                            Debug.LogWarning($"[royale] A marble left through the wall at tick {_recorder.CurrentTick} " +
                                             $"(dist {distance:0.00}). Counted as a leak, not an exit.", this);
                        }

                        _pool.Release(marble.gameObject);
                    }

                    continue;
                }

                _inside.Add(marble);

                bool hit = marble.Sensor.Consume(out Vector2 contact);
                if (hit && !marble.Exiting && distance <= radius + _config.ballRadius && IsInsideX2(contact))
                {
                    X2HitCount++;
                    LastX2Colour = marble.Colour;
                    Clone(marble, position, contact);
                }

                KeepSpeed(marble);
            }

            if (_alive > _peakAlive)
            {
                _peakAlive = _alive;
            }

            TrackLead();
        }

        /// <summary>Is a world-space point on the x2 arc? Accounts for the ring's spin.</summary>
        bool IsInsideX2(Vector2 worldPoint)
        {
            float angle = Mathf.Atan2(worldPoint.y, worldPoint.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(X2CenterDegrees, angle);
            return Mathf.Abs(delta) <= _config.x2ArcDegrees * 0.5f;
        }

        void StepEscaped()
        {
            float recycle = _config.recycleDistance;
            for (int i = _escaped.Count - 1; i >= 0; i--)
            {
                RoyaleMarble marble = _escaped[i];
                if (marble.Body.position.magnitude > recycle)
                {
                    _pool.Release(marble.gameObject);
                    _escaped.RemoveAt(i);
                }
            }
        }

        void Clone(RoyaleMarble parent, Vector2 position, Vector2 contact)
        {
            if (_alive + 1 > _config.bodyCap || _pool.FreeCount == 0)
            {
                // Still drawn, so the sequence doesn't depend on whether the cap or pool had room.
                _random.NextFloat();
                _random.NextBool();
                _recorder.SetMetric("cap_blocked", ++_capBlocked);
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

            RoyaleMarble clone = Spawn(spawnAt, cloneHeading * _config.ballSpeed, parent.Colour);
            if (clone != null)
            {
                CloneCount++;
                LastCloneColour = parent.Colour;
                LastCloneAngle = Mathf.Atan2(contact.y, contact.x) * Mathf.Rad2Deg;
                _recorder.LogEvent("clone", parent.Colour);
            }
        }

        RoyaleMarble Spawn(Vector2 position, Vector2 velocity, int colour)
        {
            GameObject body = _pool.TryGet(position);
            if (body == null)
            {
                Debug.LogError("[royale] Pool is empty.", this);
                return null;
            }

            RoyaleMarble marble = body.GetComponent<RoyaleMarble>();
            marble.Sensor.Wall = _boundary.transform;
            marble.ResetState(_nextSerial++, colour);
            marble.Body.bodyType = RigidbodyType2D.Dynamic;
            marble.Body.linearVelocity = velocity;
            _inside.Add(marble);
            _counts[colour]++;
            _alive++;
            return marble;
        }

        void KeepSpeed(RoyaleMarble marble)
        {
            Rigidbody2D body = marble.Body;
            if (body.linearVelocity.sqrMagnitude < 1e-6f)
            {
                // Stopped dead by a head-on hit. Restart it rather than leave a frozen marble.
                body.linearVelocity = _random.NextDirection2D() * _config.ballSpeed;
                return;
            }

            ConstantSpeed2D.Apply(body, _config.ballSpeed);
        }

        void TrackLead()
        {
            int best = -1;
            int bestCount = 0;
            bool tied = false;
            for (int c = 0; c < _counts.Length; c++)
            {
                if (_counts[c] > bestCount)
                {
                    best = c;
                    bestCount = _counts[c];
                    tied = false;
                }
                else if (_counts[c] == bestCount && bestCount > 0)
                {
                    tied = true;
                }
            }

            if (best >= 0 && !tied && best != _leader)
            {
                if (_leader >= 0)
                {
                    _leadChanges++;
                }

                _leader = best;
            }
        }

        void Resolve()
        {
            for (int c = 0; c < _counts.Length; c++)
            {
                if (!_eliminated[c] && _counts[c] <= 0)
                {
                    _eliminated[c] = true;
                    _eliminationOrder.Add(c);
                    EliminationCount++;
                    LastEliminatedColour = c;
                    _recorder.LogEvent("eliminated", c);
                    _recorder.SetMetric($"out_{_config.NameOf(c).ToLowerInvariant()}_s", GameSeconds);
                }
            }

            int remaining = 0;
            int last = -1;
            for (int c = 0; c < _eliminated.Length; c++)
            {
                if (!_eliminated[c])
                {
                    remaining++;
                    last = c;
                }
            }

            if (remaining == 1)
            {
                Decide(last, false);
            }
            else if (remaining == 0)
            {
                // The last marbles of two colours left in the same tick: the larger side a tick ago wins.
                int best = 0;
                for (int c = 1; c < _previousCounts.Length; c++)
                {
                    if (_previousCounts[c] > _previousCounts[best])
                    {
                        best = c;
                    }
                }

                _eliminated[best] = false;
                _eliminationOrder.Remove(best);
                _recorder.SetMetric("simultaneous_finish", 1f);
                Decide(best, false);
            }
            else if (GameSeconds >= _config.maxGameSeconds)
            {
                int best = 0;
                for (int c = 1; c < _counts.Length; c++)
                {
                    if (_counts[c] > _counts[best])
                    {
                        best = c;
                    }
                }

                Debug.LogWarning($"[royale] Hit maxGameSeconds ({_config.maxGameSeconds}). " +
                                 $"{_config.NameOf(best)} takes it on count; retune.", this);
                Decide(best, true);
            }
        }

        void Decide(int winner, bool timedOut)
        {
            Winner = winner;
            WinnerMarbles = _counts[winner];
            _timedOut = timedOut;

            // Freeze the survivors so the result reads over a still picture; the ring stops too. Kinematic,
            // so a winner sitting in the exit channel isn't shoved when the wall closes over it.
            for (int i = 0; i < _inside.Count; i++)
            {
                Rigidbody2D body = _inside[i].Body;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            _recorder.LogEvent(timedOut ? "timeout" : "winner", winner);
            _recorder.SetMetric("seconds", GameSeconds);
            _recorder.SetMetric("peak_marbles", _peakAlive);
            _recorder.SetMetric("clones", CloneCount);
            _recorder.SetMetric("exits", ExitCount);
            _recorder.SetMetric("x2_hits", X2HitCount);
            _recorder.SetMetric("lead_changes", _leadChanges);
            _recorder.SetMetric("final_exit_deg", _boundary.gapDegrees);
            _recorder.SetMetric("winner_marbles", WinnerMarbles);
            _recorder.SetMetric("eliminations", EliminationCount);

            var standings = new List<string> { _config.NameOf(winner) };
            var rest = new List<int>();
            for (int c = 0; c < _counts.Length; c++)
            {
                if (c != winner && !_eliminated[c])
                {
                    rest.Add(c);
                }
            }

            rest.Sort((a, b) => _counts[a] != _counts[b] ? _counts[b].CompareTo(_counts[a]) : a.CompareTo(b));
            for (int i = 0; i < rest.Count; i++)
            {
                standings.Add(_config.NameOf(rest[i]));
            }

            for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            {
                if (_eliminationOrder[i] != winner)
                {
                    standings.Add(_config.NameOf(_eliminationOrder[i]));
                }
            }

            _recorder.SetStandings(standings);

            _sealFromDegrees = _boundary.gapDegrees;
            _phase = Phase.Seal;
            _phaseTicks = 0;
        }

        /// <summary>Closes the EXIT behind the winners, easing out, then holds the result.</summary>
        void StepSeal()
        {
            int total = _config.sealSeconds > 0f ? Ticks(_config.sealSeconds) : 0;
            _phaseTicks++;
            float t = total > 0 ? Mathf.Clamp01((float)_phaseTicks / total) : 1f;
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            SealProgress = eased;
            _boundary.gapDegrees = Mathf.Lerp(_sealFromDegrees, 0f, eased);
            _boundary.RefreshGap();

            if (t >= 1f)
            {
                IsSealed = true;
                _recorder.LogEvent("sealed", Winner);
                _phase = Phase.Result;
                _phaseTicks = Ticks(_config.resultHoldSeconds);
            }
        }

        void FinishRun()
        {
            _phase = Phase.Done;
            _recorder.Finish(_timedOut ? CompletionReason.TimedOut : CompletionReason.Finished);
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
