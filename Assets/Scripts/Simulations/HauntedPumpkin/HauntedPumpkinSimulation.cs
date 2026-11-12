using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.HauntedPumpkin
{
    /// <summary>How the run ended.</summary>
    public enum HauntedOutcome
    {
        Running = 0,

        /// <summary>A ghost broke out through the outer ring before the candle died.</summary>
        Escaped = 1,

        /// <summary>The candle burned out with the outer ring still holding. The pumpkin wins.</summary>
        CandleOut = 2
    }

    public enum SegmentEventKind
    {
        /// <summary>Touched, lost a hit point, still standing.</summary>
        Cracked = 0,

        /// <summary>Touched and broken: a hole opened.</summary>
        Broken = 1
    }

    /// <summary>One segment event, in ticks so the log is reproducible.</summary>
    public readonly struct SegmentEvent
    {
        public readonly int tick;
        public readonly int ring;
        public readonly int segment;
        public readonly SegmentEventKind kind;
        public readonly Vector2 position;
        public readonly Vector2 outward;
        public readonly int hitPointsLeft;

        /// <summary>The break split the ghost that caused it (always false for a crack).</summary>
        public readonly bool split;

        public SegmentEvent(int tick, int ring, int segment, SegmentEventKind kind, Vector2 position,
                            Vector2 outward, int hitPointsLeft, bool split)
        {
            this.tick = tick;
            this.ring = ring;
            this.segment = segment;
            this.kind = kind;
            this.position = position;
            this.outward = outward;
            this.hitPointsLeft = hitPointsLeft;
            this.split = split;
        }
    }

    /// <summary>A ghost slipped through the outer ring and became a bat.</summary>
    public readonly struct BatEvent
    {
        public readonly int tick;
        public readonly Vector2 position;
        public readonly int weight;

        public BatEvent(int tick, Vector2 position, int weight)
        {
            this.tick = tick;
            this.position = position;
            this.weight = weight;
        }
    }

    /// <summary>
    /// Ghosts bounce around inside a jack-o'-lantern of nested rings. A ghost damages the segment it touches;
    /// at 0 hit points the segment breaks and, with <c>cloneChance</c>, the ghost splits in two. A ghost that
    /// gets out through the outer ring becomes a bat. The first bat wins the race; if the candle burns out
    /// (<c>timeLimitSeconds</c>) before any ghost clears the outer ring, the pumpkin wins.
    /// </summary>
    /// <remarks>
    /// Determinism: ghosts are processed in list order (birth order), clones are appended, and every random
    /// choice (spin sign, launch direction, split chance, split turn and side) is a draw from the run's seeded
    /// generator in that order. A split always consumes the same draws whether or not the pool had room.
    /// Holes come only from physics: a ghost may only leave a ring through a broken segment (or a neighbour of
    /// one, since the ghost is wider than the seam it straddles). Anything else is a leak: removed uncounted and
    /// warned about, never counted as a bat.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class HauntedPumpkinSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring")]
        [Tooltip("Inactive template the pool clones.")]
        [SerializeField] GameObject _ghostTemplate;
        [SerializeField] Transform _ghostParent;
        [Tooltip("Innermost first.")]
        [SerializeField] SegmentedRing2D[] _rings;

        enum Phase
        {
            Hold,
            Running,
            Finishing,
            Done
        }

        HauntedPumpkinConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        BodyPool2D _pool;

        readonly List<GhostBody> _ghosts = new List<GhostBody>();
        readonly List<GhostBody> _scratch = new List<GhostBody>();
        readonly List<Collider2D> _hitScratch = new List<Collider2D>();
        readonly List<SegmentEvent> _events = new List<SegmentEvent>();
        readonly List<BatEvent> _batEvents = new List<BatEvent>();

        int[][] _hitPoints;
        float[] _ringAngle;
        float[] _ringSpin;

        Phase _phase;
        int _phaseTicks;
        int _clockTicks;
        int _decidedClockTicks;
        int _nextSerial;
        int _leaks;
        float[] _ringOpenSeconds;
        Vector2 _pendingLaunch;

        public string FormatSlug => "hall";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        /// <summary>Changes every Initialize, so presentation can tell a new run started in the same scene.</summary>
        public int RunId { get; private set; }

        public int RingCount => _rings != null ? _rings.Length : 0;

        /// <summary>Rings a ghost has got through so far. Rises one at a time, innermost first.</summary>
        public int RingsBroken { get; private set; }

        /// <summary>Ghosts alive inside the pumpkin, counting the extra weight a capped body stands for.</summary>
        public int GhostCount { get; private set; }

        public int PeakGhosts { get; private set; }

        public int BatsOut { get; private set; }

        public int Splits { get; private set; }

        public int Hits { get; private set; }

        public int SegmentsBroken { get; private set; }

        public HauntedOutcome Outcome { get; private set; }

        public bool IsHolding => _phase == Phase.Hold;

        public bool IsDecided => Outcome != HauntedOutcome.Running;

        public float TimeLimit => _config != null ? _config.timeLimitSeconds : 0f;

        /// <summary>Seconds the candle has burned. Frozen at the moment the run is decided.</summary>
        public float SecondsBurned => (IsDecided ? _decidedClockTicks : _clockTicks) * Step;

        public float SecondsLeft => Mathf.Max(0f, TimeLimit - SecondsBurned);

        /// <summary>1 = fresh candle, 0 = out.</summary>
        public float CandleFraction => TimeLimit > 0f ? Mathf.Clamp01(SecondsLeft / TimeLimit) : 0f;

        public float GhostRadius => _config != null ? _config.ghostRadius : 0.18f;

        public IReadOnlyList<GhostBody> Ghosts => _ghosts;

        public IReadOnlyList<SegmentEvent> Events => _events;

        public IReadOnlyList<BatEvent> BatEvents => _batEvents;

        public SegmentedRing2D Ring(int index) => _rings[index];

        public int MaxHitPoints(int ring) => _config != null ? _config.HitPointsForRing(ring) : 1;

        float Step => _config != null ? _config.fixedTimestep : 0.01f;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (HauntedPumpkinConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 8192);
            RunId++;

            if (_rings == null || _rings.Length != _config.ringCount)
            {
                Debug.LogError($"[hall] Scene has {RingCount} rings but the config wants {_config.ringCount}. " +
                               "Rebuild the scene from its menu.", this);
            }

            if (_pool == null || _pool.Capacity != _config.bodyCap)
            {
                _pool = new BodyPool2D(_ghostTemplate, _ghostParent, _config.bodyCap);
            }

            _pool.ReleaseAll();
            _ghosts.Clear();
            _scratch.Clear();
            _events.Clear();
            _batEvents.Clear();

            IsComplete = false;
            Outcome = HauntedOutcome.Running;
            RingsBroken = 0;
            GhostCount = 0;
            PeakGhosts = 1;
            BatsOut = 0;
            Splits = 0;
            Hits = 0;
            SegmentsBroken = 0;
            _leaks = 0;
            _nextSerial = 0;
            _clockTicks = 0;
            _decidedClockTicks = 0;

            Debug.Log($"[hall] start: seed {seed}, {_config.ringCount} rings, split chance {_config.cloneChance:0.00}, " +
                      $"speed {_config.ghostSpeed}, candle {_config.timeLimitSeconds:0}s, cap {_config.bodyCap}", this);

            int ringCount = RingCount;
            _hitPoints = new int[ringCount][];
            _ringAngle = new float[ringCount];
            _ringSpin = new float[ringCount];
            _ringOpenSeconds = new float[ringCount];

            // Draw order is fixed: spin sign, launch direction, then split rolls as they happen.
            float spinSign = _random.NextBool() ? 1f : -1f;
            for (int ring = 0; ring < ringCount; ring++)
            {
                SegmentedRing2D segments = _rings[ring];
                int count = segments.SegmentCount;
                _hitPoints[ring] = new int[count];
                int hitPoints = _config.HitPointsForRing(ring);
                for (int i = 0; i < count; i++)
                {
                    _hitPoints[ring][i] = hitPoints;
                    segments.GetCollider(i).enabled = true;
                }

                float direction = _config.alternateDirections && ring % 2 == 1 ? -1f : 1f;
                _ringSpin[ring] = _config.ringAngularSpeed * direction * spinSign;
                _ringAngle[ring] = 0f;
                segments.transform.rotation = Quaternion.identity;
            }

            Vector2 launch = _random.NextDirection2D();

            // Off-centre, sideways to the launch: from dead centre the first ghost would bounce along one
            // diameter forever (the brk lesson).
            float room = _config.innerRadius - _config.ghostRadius;
            Vector2 start = new Vector2(-launch.y, launch.x) * (room * _config.startOffsetFraction);
            Spawn(start, Vector2.zero, 0);
            _pendingLaunch = launch;

            _phase = Phase.Hold;
            _phaseTicks = Ticks(_config.preLaunchHoldSeconds);
            _recorder.SetMetric("rings", ringCount);
            _recorder.LogEvent("start", launch.x);
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            switch (_phase)
            {
                case Phase.Hold:
                    if (--_phaseTicks <= 0)
                    {
                        if (_ghosts.Count > 0)
                        {
                            _ghosts[0].Body.linearVelocity = _pendingLaunch * _config.ghostSpeed;
                        }

                        _phase = Phase.Running;
                    }

                    return;

                case Phase.Done:
                    return;

                case Phase.Finishing:
                    // A win keeps the swarm pouring out; a loss is frozen so the losing picture reads still.
                    if (Outcome == HauntedOutcome.Escaped)
                    {
                        SpinRings(fixedDelta);
                        StepGhosts();
                    }

                    if (--_phaseTicks <= 0)
                    {
                        Finish();
                    }

                    return;
            }

            // Checked before anything moves, so a bat can't escape on the tick the candle dies.
            if (_clockTicks * Step >= _config.timeLimitSeconds)
            {
                Decide(HauntedOutcome.CandleOut);
                return;
            }

            _clockTicks++;
            SpinRings(fixedDelta);
            StepGhosts();

            // The first ghost through the outer ring breaks the pumpkin. The rest pour out during the finish hold.
            if (RingsBroken >= RingCount)
            {
                Decide(HauntedOutcome.Escaped);
            }
            else if (GhostCount <= 0)
            {
                // Every ghost leaked: nothing left that could ever break out.
                Decide(HauntedOutcome.CandleOut);
            }
        }

        void SpinRings(float fixedDelta)
        {
            for (int ring = 0; ring < RingCount; ring++)
            {
                _ringAngle[ring] += _ringSpin[ring] * fixedDelta;
                _rings[ring].transform.rotation = Quaternion.Euler(0f, 0f, _ringAngle[ring]);
            }
        }

        void StepGhosts()
        {
            // Snapshot: clones born this tick join the list but don't act until the next one.
            _scratch.Clear();
            _scratch.AddRange(_ghosts);
            _ghosts.Clear();

            for (int i = 0; i < _scratch.Count; i++)
            {
                GhostBody ghost = _scratch[i];
                Vector2 position = ghost.Body.position;

                if (CheckCrossing(ghost, position))
                {
                    _hitScratch.Clear();
                    ghost.DrainHits(_hitScratch);
                    continue;
                }

                _ghosts.Add(ghost);

                _hitScratch.Clear();
                ghost.DrainHits(_hitScratch);
                for (int h = 0; h < _hitScratch.Count; h++)
                {
                    ApplyHit(ghost, _hitScratch[h], position);
                }

                KeepSpeed(ghost);
            }

            if (GhostCount > PeakGhosts)
            {
                PeakGhosts = GhostCount;
            }
        }

        /// <summary>
        /// Moves a ghost between zones as its centre clears a ring, enforcing that it only does so through a
        /// hole. Returns true if the ghost left the swarm (became a bat, or leaked through solid wall).
        /// </summary>
        bool CheckCrossing(GhostBody ghost, Vector2 position)
        {
            float distance = position.magnitude;
            int zone = ghost.Zone;

            if (zone < RingCount && distance > _rings[zone].OuterRadius)
            {
                if (!ThroughHole(zone, position))
                {
                    Leak(ghost, distance);
                    return true;
                }

                ghost.Zone = zone + 1;
                if (ghost.Zone > RingsBroken)
                {
                    RingsBroken = ghost.Zone;
                    float seconds = _clockTicks * Step;
                    _ringOpenSeconds[RingsBroken - 1] = seconds;
                    _recorder.LogEvent($"ring_{RingsBroken}_open", seconds);
                    _recorder.SetMetric($"ring_{RingsBroken}_seconds", seconds);
                    // Logged as it happens, so a Play stopped early still says how the pacing went.
                    Debug.Log($"[hall] ring {RingsBroken}/{RingCount} open at {seconds:0.0}s, {GhostCount} ghosts, " +
                              $"candle {SecondsLeft:0.0}s left", this);
                }

                if (ghost.Zone >= RingCount)
                {
                    BecomeBat(ghost, position);
                    return true;
                }

                return false;
            }

            if (zone > 0 && distance < _rings[zone - 1].innerRadius)
            {
                if (!ThroughHole(zone - 1, position))
                {
                    Leak(ghost, distance);
                    return true;
                }

                ghost.Zone = zone - 1;
            }

            return false;
        }

        bool ThroughHole(int ring, Vector2 position)
        {
            SegmentedRing2D segments = _rings[ring];
            int at = segments.SegmentIndexAt(position);
            int count = segments.SegmentCount;
            for (int offset = -1; offset <= 1; offset++)
            {
                if (_hitPoints[ring][(at + offset + count) % count] <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        void Leak(GhostBody ghost, float distance)
        {
            _leaks++;
            _recorder.SetMetric("leaks", _leaks);
            if (_leaks == 1)
            {
                Debug.LogWarning($"[hall] A ghost left through solid wall at tick {_recorder.CurrentTick} " +
                                 $"(dist {distance:0.00}). Counted as a leak, not a bat.", this);
            }

            GhostCount -= ghost.Weight;
            _pool.Release(ghost.gameObject);
        }

        void BecomeBat(GhostBody ghost, Vector2 position)
        {
            BatsOut += ghost.Weight;
            GhostCount -= ghost.Weight;
            _batEvents.Add(new BatEvent(_recorder.CurrentTick, position, ghost.Weight));
            _recorder.LogEvent("bat", BatsOut);
            _pool.Release(ghost.gameObject);
        }

        void ApplyHit(GhostBody ghost, Collider2D collider, Vector2 ghostPosition)
        {
            if (collider == null)
            {
                return;
            }

            for (int ring = 0; ring < RingCount; ring++)
            {
                if (!_rings[ring].TryGetSegment(collider, out int segment))
                {
                    continue;
                }

                if (_hitPoints[ring][segment] <= 0)
                {
                    return;
                }

                Hits++;
                SegmentedRing2D segments = _rings[ring];
                int left = --_hitPoints[ring][segment];
                Vector2 center = segments.SegmentCenter(segment);
                Vector2 outward = segments.SegmentOutward(segment);

                if (left > 0)
                {
                    _events.Add(new SegmentEvent(_recorder.CurrentTick, ring, segment, SegmentEventKind.Cracked,
                        center, outward, left, false));
                    return;
                }

                collider.enabled = false;
                SegmentsBroken++;
                bool split = RollSplit(ghost, ghostPosition, ring);
                _events.Add(new SegmentEvent(_recorder.CurrentTick, ring, segment, SegmentEventKind.Broken,
                    center, outward, 0, split));
                _recorder.LogEvent($"break_r{ring + 1}", segment);
                return;
            }
        }

        /// <summary>
        /// A break may split the ghost that caused it. The clone is born a little to the same side of the wall
        /// as its parent (clear of the wall and of the parent) and turned by a seeded angle.
        /// </summary>
        bool RollSplit(GhostBody parent, Vector2 position, int ring)
        {
            // Only drawn below 1, so configs at 1 keep the exact sequence they always had.
            if (_config.cloneChance < 1f && _random.NextFloat() >= _config.cloneChance)
            {
                return false;
            }

            // Always drawn, so the sequence doesn't depend on whether the pool had room.
            float turn = _random.NextFloat(_config.cloneTurnDegrees.x, _config.cloneTurnDegrees.y);
            if (_random.NextBool())
            {
                turn = -turn;
            }

            Splits++;

            if (_pool.FreeCount == 0)
            {
                // Weight-based counting: the cap binds, so the parent stands for one more ghost.
                parent.Weight++;
                GhostCount++;
                _recorder.LogEvent("split_weight", GhostCount);
                return true;
            }

            Vector2 heading = parent.Body.linearVelocity;
            heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : -position.normalized;
            Vector2 cloneHeading = Rotate(heading, turn);

            // Same side of the wall as the parent: toward the centre when it hit from inside, away from it
            // when it hit from the corridor outside.
            SegmentedRing2D segments = _rings[ring];
            float midRadius = segments.innerRadius + segments.thickness * 0.5f;
            Vector2 radial = position.sqrMagnitude > 1e-6f ? position.normalized : Vector2.up;
            Vector2 away = position.magnitude < midRadius ? -radial : radial;
            Vector2 spawnAt = position + away * (_config.ghostRadius * 2.05f);

            GhostBody clone = Spawn(spawnAt, cloneHeading * _config.ghostSpeed, parent.Zone);
            if (clone == null)
            {
                return false;
            }

            _recorder.LogEvent("split", GhostCount);
            return true;
        }

        GhostBody Spawn(Vector2 position, Vector2 velocity, int zoneHint)
        {
            GameObject body = _pool.TryGet(position);
            if (body == null)
            {
                return null;
            }

            GhostBody ghost = body.GetComponent<GhostBody>();
            ghost.ResetState(_nextSerial++, ZoneAt(position.magnitude, zoneHint));
            ghost.Body.linearVelocity = velocity;
            _ghosts.Add(ghost);
            GhostCount++;
            return ghost;
        }

        /// <summary>Zone a point is in: the number of rings whose outer edge it is already past.</summary>
        int ZoneAt(float distance, int fallback)
        {
            int zone = 0;
            for (int ring = 0; ring < RingCount; ring++)
            {
                if (distance > _rings[ring].OuterRadius)
                {
                    zone = ring + 1;
                }
            }

            return RingCount > 0 ? zone : fallback;
        }

        void KeepSpeed(GhostBody ghost)
        {
            Rigidbody2D body = ghost.Body;
            if (body.linearVelocity.sqrMagnitude < 1e-6f)
            {
                // Stopped dead by a head-on hit. Restart it rather than leave a frozen ghost.
                body.linearVelocity = _random.NextDirection2D() * _config.ghostSpeed;
                return;
            }

            ConstantSpeed2D.Apply(body, _config.ghostSpeed);
        }

        void Decide(HauntedOutcome outcome)
        {
            Outcome = outcome;
            _decidedClockTicks = _clockTicks;
            _phase = Phase.Finishing;
            _phaseTicks = Ticks(_config.finishHoldSeconds);

            if (outcome == HauntedOutcome.CandleOut)
            {
                for (int i = 0; i < _ghosts.Count; i++)
                {
                    _ghosts[i].Body.linearVelocity = Vector2.zero;
                }
            }

            _recorder.LogEvent(outcome == HauntedOutcome.Escaped ? "escaped" : "candle_out", BatsOut);
        }

        void Finish()
        {
            _phase = Phase.Done;
            _recorder.SetMetric("won", Outcome == HauntedOutcome.Escaped ? 1f : 0f);
            _recorder.SetMetric("seconds", _decidedClockTicks * Step);
            _recorder.SetMetric("peak_ghosts", PeakGhosts);
            _recorder.SetMetric("rings_broken", RingsBroken);
            _recorder.SetMetric("bats_out", BatsOut);
            _recorder.SetMetric("splits", Splits);
            _recorder.SetMetric("hits", Hits);
            _recorder.SetMetric("segments_broken", SegmentsBroken);

            string verdict = Outcome == HauntedOutcome.Escaped
                ? $"THEY ESCAPED ({SecondsLeft:0.0}s left)"
                : "THE PUMPKIN WINS";
            _recorder.SetStandings(new[] { $"{RingsBroken}/{RingCount} rings broken, peak {PeakGhosts} ghosts - {verdict}" });
            _recorder.Finish(CompletionReason.Finished);

            var opens = new System.Text.StringBuilder();
            for (int ring = 0; ring < RingsBroken; ring++)
            {
                opens.Append(ring == 0 ? "" : " ").Append(_ringOpenSeconds[ring].ToString("0.0"));
            }

            Debug.Log($"[hall] round {Outcome}: {_decidedClockTicks * Step:0.0}s of {_config.timeLimitSeconds:0}s, " +
                      $"rings open at [{opens}], {RingsBroken}/{RingCount} rings, peak {PeakGhosts} ghosts, " +
                      $"{BatsOut} bats, {Splits} splits, {_leaks} leaks, seed {_random.Seed}", this);

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
