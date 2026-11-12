using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.ReachCore
{
    /// <summary>What happened to a segment, for presentation to react to.</summary>
    public enum CoreSegmentEventKind
    {
        /// <summary>Touched, lost a hit point, still standing.</summary>
        Cracked = 0,

        /// <summary>Touched and broken: a hole opened.</summary>
        Broken = 1,

        /// <summary>Destroyed because the ball got through its ring: the ring-clear explosion.</summary>
        Shattered = 2
    }

    /// <summary>One segment event, in ticks so the log is reproducible.</summary>
    public readonly struct CoreSegmentEvent
    {
        public readonly int tick;
        public readonly int ring;
        public readonly int segment;
        public readonly CoreSegmentEventKind kind;
        public readonly Vector2 position;
        public readonly Vector2 outward;
        public readonly int hitPointsLeft;

        public CoreSegmentEvent(int tick, int ring, int segment, CoreSegmentEventKind kind, Vector2 position,
                                Vector2 outward, int hitPointsLeft)
        {
            this.tick = tick;
            this.ring = ring;
            this.segment = segment;
            this.kind = kind;
            this.position = position;
            this.outward = outward;
            this.hitPointsLeft = hitPointsLeft;
        }
    }

    /// <summary>
    /// A ball starts outside nested rings with a glowing core in the middle. Every touch damages the
    /// segment it hit; a broken segment leaves a hole, and when the ball gets through, that ring
    /// explodes, the containment wall closes in behind it and the ball is a little faster. Can it
    /// reach the core before the clock runs out?
    /// </summary>
    /// <remarks>
    /// <c>brk</c> inverted. The ball moves inward, so the arena needs a wall *outside* the current ring
    /// (a circle that steps in on every ring clear) and a core to reach. Holes come only from physics:
    /// the ball crosses a ring only through an opening that exists, and the tunneling check on every
    /// crossing enforces it. Rings are indexed outermost first.
    /// <para>
    /// The hit-point, clock and decide/finish logic mirrors <c>Breaker</c>. Formats may not reference
    /// each other, so it is duplicated here for now; when a third format needs it, promote it to
    /// <c>Shared</c>.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class CoreSimulation : MonoBehaviour, ISimulation
    {
        const int WallPoints = 96;

        [Header("Scene wiring")]
        [SerializeField] Rigidbody2D _ball;
        [SerializeField] CircleCollider2D _ballCollider;
        [SerializeField] BounceDetector2D _bounceDetector;
        [Tooltip("Outermost first.")]
        [SerializeField] SegmentedRing2D[] _rings;
        [SerializeField] EdgeCollider2D _wall;

        CoreConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;

        int[][] _hitPoints;
        int[][] _lastHitTick;
        float[] _ringAngle;
        float[] _ringSpin;
        int[] _ringHits;
        float _speed;
        int _holdTicksRemaining;
        float _decidedAtSeconds;
        int _lastTouchTick;
        readonly Vector2[] _wallPoints = new Vector2[WallPoints + 1];

        readonly List<CoreSegmentEvent> _events = new List<CoreSegmentEvent>();

        public string FormatSlug => "core";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        /// <summary>Changes every Initialize, so presentation can tell a new run started in the same scene.</summary>
        public int RunId { get; private set; }

        public int RingCount => _rings != null ? _rings.Length : 0;

        /// <summary>The ring the ball is working on (0 = outermost). Equals <see cref="RingCount"/> once inside.</summary>
        public int CurrentRing { get; private set; }

        public int RingsBroken => CurrentRing;

        public int RingsLeft => RingCount - CurrentRing;

        /// <summary>Every ring is gone: only the core is left to hit.</summary>
        public bool InFinalChamber => CurrentRing >= RingCount;

        /// <summary>The ball touched the core inside the limit.</summary>
        public bool Won { get; private set; }

        /// <summary>The clock ran out before the core was reached.</summary>
        public bool TimeUp { get; private set; }

        public bool HasClock => _config != null && _config.HasClock;

        public float TimeLimit => HasClock ? _config.timeLimitSeconds : 0f;

        public float SecondsLeft => HasClock
            ? Mathf.Max(0f, _config.timeLimitSeconds - (_decidedAtSeconds >= 0f ? _decidedAtSeconds : SimulatedSeconds))
            : 0f;

        float SimulatedSeconds => _recorder != null ? _recorder.SimulatedSeconds : 0f;

        /// <summary>Every touch on a segment: one melody note each.</summary>
        public int Hits { get; private set; }

        public int SegmentsBroken { get; private set; }

        public float Speed => _speed;

        public float CoreRadius => _config != null ? _config.coreRadius : 0f;

        /// <summary>Radius the containment wall is at right now. It steps in on every ring clear.</summary>
        public float WallRadius { get; private set; }

        public float LastImpactSpeed => _bounceDetector != null ? _bounceDetector.LastImpactSpeed : 0f;

        public IReadOnlyList<CoreSegmentEvent> Events => _events;

        public SegmentedRing2D Ring(int index) => _rings[index];

        public int MaxHitPoints(int ring) => _config != null ? _config.HitPointsForRing(ring) : 1;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (CoreConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random);
            RunId++;

            _events.Clear();
            IsComplete = false;
            CurrentRing = 0;
            Hits = 0;
            SegmentsBroken = 0;
            Won = false;
            TimeUp = false;
            _decidedAtSeconds = -1f;
            _holdTicksRemaining = -1;
            _lastTouchTick = 0;

            if (_rings == null || _rings.Length != _config.ringCount)
            {
                Debug.LogError($"[core] Scene has {RingCount} rings but the config wants {_config.ringCount}. " +
                               "Rebuild the scene from its menu.", this);
            }

            int ringCount = RingCount;
            _hitPoints = new int[ringCount][];
            _lastHitTick = new int[ringCount][];
            _ringAngle = new float[ringCount];
            _ringSpin = new float[ringCount];
            _ringHits = new int[ringCount];

            // Draw order is fixed: spin sign, start angle, start distance, launch side, launch offset.
            float spinSign = _config.spinDirectionSeeded ? (_random.NextBool() ? 1f : -1f) : 1f;

            for (int ring = 0; ring < ringCount; ring++)
            {
                SegmentedRing2D segments = _rings[ring];
                int count = segments.SegmentCount;
                int hitPoints = _config.HitPointsForRing(ring);

                _hitPoints[ring] = new int[count];
                _lastHitTick[ring] = new int[count];
                for (int i = 0; i < count; i++)
                {
                    _hitPoints[ring][i] = hitPoints;
                    _lastHitTick[ring][i] = int.MinValue / 2;
                    segments.GetCollider(i).enabled = true;
                }

                float direction = _config.alternateDirections && ring % 2 == 1 ? -1f : 1f;
                _ringSpin[ring] = _config.ringAngularSpeed * direction * spinSign;
                _ringAngle[ring] = 0f;
                segments.transform.rotation = Quaternion.identity;
            }

            SetWallRadius(_config.WallRadius(0));

            // Start in the corridor between the wall and the outer ring, never touching either.
            float startAngle = _random.NextFloat(0f, 360f) * Mathf.Deg2Rad;
            float outerEdge = _config.RingOuterRadius(0) + _config.ballRadius + 0.05f;
            float wallEdge = WallRadius - _config.ballRadius - 0.05f;
            float startDistance = Mathf.Lerp(outerEdge, wallEdge, 0.5f);
            Vector2 start = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * startDistance;

            // Launched mostly inward but across, so the first touch lands on the ring within a beat.
            float launchOffset = _random.NextFloat(25f, 75f) * (_random.NextBool() ? 1f : -1f);
            float launchAngle = startAngle + Mathf.PI + launchOffset * Mathf.Deg2Rad;
            Vector2 launch = new Vector2(Mathf.Cos(launchAngle), Mathf.Sin(launchAngle));

            float scale = _config.ballRadius / _ballCollider.radius;
            _ball.transform.localScale = new Vector3(scale, scale, 1f);
            _ball.transform.position = start;
            _ball.position = start;
            _ball.rotation = 0f;
            _ball.angularVelocity = 0f;

            _speed = _config.SpeedForRing(0);
            _ball.linearVelocity = launch * _speed;
            _bounceDetector.Reset();

            _recorder.SetMetric("rings", ringCount);
            _recorder.LogEvent("ring_1_start", _speed);
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            if (_holdTicksRemaining >= 0)
            {
                ConstantSpeed2D.Apply(_ball, _speed);
                if (_holdTicksRemaining-- == 0)
                {
                    Finish(CompletionReason.Finished);
                }

                return;
            }

            // Checked before anything moves, so a ring can't break on the tick the clock hits zero.
            if (HasClock && SimulatedSeconds >= _config.timeLimitSeconds)
            {
                RunOutOfTime();
                return;
            }

            for (int ring = CurrentRing; ring < RingCount; ring++)
            {
                _ringAngle[ring] += _ringSpin[ring] * fixedDelta;
                _rings[ring].transform.rotation = Quaternion.Euler(0f, 0f, _ringAngle[ring]);
            }

            if (_bounceDetector.ConsumeBounce())
            {
                Collider2D touched = _bounceDetector.LastCollider;
                if (touched == _wall)
                {
                    ShapePath();
                }
                else
                {
                    RegisterHit(touched);
                }
            }

            // A ball skimming round a thin gap never meets the ring: turn it toward the core.
            if (!InFinalChamber && _config.stallSeconds > 0f &&
                _recorder.CurrentTick - _lastTouchTick > Mathf.RoundToInt(_config.stallSeconds / fixedDelta))
            {
                ShapePath(true);
                _lastTouchTick = _recorder.CurrentTick;
            }

            ConstantSpeed2D.Apply(_ball, _speed);

            if (InFinalChamber)
            {
                CheckCore();
            }
            else
            {
                CheckCrossing();
            }
        }

        /// <summary>
        /// Keep the ball's path between "straight along a radius" and "skimming round the gap". A circular
        /// wall conserves angular momentum, so left alone a ball either orbits forever (path near
        /// tangent) or hammers one segment (path near radial). Outside the band it is rotated back to a
        /// seeded angle inside it; inside the band it is left untouched. Deterministic: the angle comes
        /// from the run's seeded RNG.
        /// </summary>
        void ShapePath(bool force = false)
        {
            Vector2 velocity = _ball.linearVelocity;
            Vector2 position = _ball.position;
            if (velocity.sqrMagnitude < Mathf.Epsilon || position.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            // Deviation from the radial line (0 = along it, 90 = tangent), and which way along it the ball goes.
            Vector2 inward = -position.normalized;
            float fromInward = Vector2.Angle(velocity, inward);
            Vector2 along = fromInward > 90f ? -inward : inward;
            float deviation = Mathf.Min(fromInward, 180f - fromInward);

            Vector2 band = _config.incidenceBandDegrees;
            if (!force && deviation >= band.x && deviation <= band.y)
            {
                return;
            }

            float signed = Vector2.SignedAngle(along, velocity);
            float side = signed < 0f ? -1f : 1f;
            float target = _random.NextFloat(band.x, band.y);
            _ball.linearVelocity = Quaternion.Euler(0f, 0f, side * target - signed) * velocity;
        }

        void RegisterHit(Collider2D collider)
        {
            if (collider == null)
            {
                return;
            }

            for (int ring = CurrentRing; ring < RingCount; ring++)
            {
                if (!_rings[ring].TryGetSegment(collider, out int segment))
                {
                    continue;
                }

                if (_hitPoints[ring][segment] <= 0)
                {
                    return;
                }

                // A repeat touch on the same segment inside the cooldown is a glance: no damage, no note.
                int cooldownTicks = Mathf.RoundToInt(_config.segmentCooldownSeconds / _config.fixedTimestep);
                if (_recorder.CurrentTick - _lastHitTick[ring][segment] < cooldownTicks)
                {
                    return;
                }

                _lastHitTick[ring][segment] = _recorder.CurrentTick;
                _lastTouchTick = _recorder.CurrentTick;
                Hits++;
                _ringHits[ring]++;
                int left = --_hitPoints[ring][segment];

                if (left == 0)
                {
                    _rings[ring].GetCollider(segment).enabled = false;
                    SegmentsBroken++;
                    AddEvent(ring, segment, CoreSegmentEventKind.Broken, 0);
                    _recorder.LogEvent($"break_r{ring + 1}", segment);
                }
                else
                {
                    AddEvent(ring, segment, CoreSegmentEventKind.Cracked, left);
                }

                return;
            }
        }

        /// <summary>The ball is through a ring once its whole body is inside the ring's inner surface.</summary>
        void CheckCrossing()
        {
            SegmentedRing2D ring = _rings[CurrentRing];
            Vector2 fromCentre = _ball.position - (Vector2)ring.transform.position;
            if (fromCentre.magnitude >= ring.innerRadius - _config.ballRadius * 0.5f)
            {
                return;
            }

            // The containment assertion: the ball may only cross through a broken segment (or a
            // neighbour of one: the ball is wider than the seam it straddles on the way in).
            int at = ring.SegmentIndexAt(_ball.position);
            int count = ring.SegmentCount;
            bool throughHole = false;
            for (int offset = -1; offset <= 1; offset++)
            {
                if (_hitPoints[CurrentRing][(at + offset + count) % count] <= 0)
                {
                    throughHole = true;
                    break;
                }
            }

            if (!throughHole)
            {
                Debug.LogError(
                    $"[core] TUNNELING at tick {_recorder.CurrentTick}: ball crossed ring {CurrentRing + 1} through " +
                    $"intact segment {at}. Lower the timestep or thicken the rings: this seed is unusable.", this);
                _recorder.SetMetric("tunneled", 1f);
                Finish(CompletionReason.Failed);
                return;
            }

            ClearRing(CurrentRing);
        }

        void ClearRing(int ring)
        {
            SegmentedRing2D segments = _rings[ring];
            for (int i = 0; i < segments.SegmentCount; i++)
            {
                if (_hitPoints[ring][i] <= 0)
                {
                    continue;
                }

                _hitPoints[ring][i] = 0;
                segments.GetCollider(i).enabled = false;
                AddEvent(ring, i, CoreSegmentEventKind.Shattered, 0);
            }

            _recorder.SetMetric($"ring_{ring + 1}_hits", _ringHits[ring]);
            _recorder.SetMetric($"ring_{ring + 1}_seconds", _recorder.SimulatedSeconds);
            _recorder.LogEvent($"ring_{ring + 1}_broken", _ringHits[ring]);
            Debug.Log($"[core] ring {ring + 1} cleared at {_recorder.SimulatedSeconds:0.0}s after {_ringHits[ring]} touches " +
                      $"({Hits} total, {SegmentsBroken} segments broken)", this);

            CurrentRing++;
            _lastTouchTick = _recorder.CurrentTick;

            // The arena closes in behind the ball: the cause the viewer sees for "no way back".
            SetWallRadius(InFinalChamber ? _config.FinalWallRadius : _config.WallRadius(CurrentRing));

            if (!InFinalChamber)
            {
                _speed = _config.SpeedForRing(CurrentRing);
                _recorder.LogEvent($"ring_{CurrentRing + 1}_start", _speed);
            }
            else
            {
                _recorder.LogEvent("final_chamber", _recorder.SimulatedSeconds);
            }
        }

        void CheckCore()
        {
            Vector2 fromCentre = _ball.position;
            if (fromCentre.magnitude > _config.coreRadius + _config.ballRadius)
            {
                return;
            }

            Won = true;
            _recorder.LogEvent("core_reached", Hits);
            Decide();
        }

        /// <summary>Time's up: ball and rings freeze where they are, so the losing position reads as a still frame.</summary>
        void RunOutOfTime()
        {
            TimeUp = true;
            _speed = 0f;
            _ball.linearVelocity = Vector2.zero;
            _recorder.LogEvent("time_up", RingsBroken);
            Decide();
        }

        void Decide()
        {
            _decidedAtSeconds = SimulatedSeconds;
            _holdTicksRemaining = Mathf.RoundToInt(_config.finishHoldSeconds / _config.fixedTimestep);

            // How close it came. A win is close when little time was left; a loss is close when the
            // ring it died on was mostly broken (1 once every ring is gone).
            float progress = 1f;
            if (!InFinalChamber)
            {
                int broken = 0;
                int[] ring = _hitPoints[CurrentRing];
                for (int i = 0; i < ring.Length; i++)
                {
                    if (ring[i] <= 0)
                    {
                        broken++;
                    }
                }

                progress = broken / (float)ring.Length;
            }

            _recorder.SetMetric("won", Won ? 1f : 0f);
            _recorder.SetMetric("seconds_left", Won ? SecondsLeft : 0f);
            _recorder.SetMetric("last_ring_progress", progress);
        }

        void SetWallRadius(float radius)
        {
            WallRadius = radius;
            for (int i = 0; i <= WallPoints; i++)
            {
                float angle = i / (float)WallPoints * Mathf.PI * 2f;
                _wallPoints[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            _wall.points = _wallPoints;
        }

        void AddEvent(int ring, int segment, CoreSegmentEventKind kind, int left)
        {
            SegmentedRing2D segments = _rings[ring];
            _events.Add(new CoreSegmentEvent(_recorder.CurrentTick, ring, segment, kind,
                segments.SegmentCenter(segment), segments.SegmentOutward(segment), left));
        }

        void Finish(CompletionReason reason)
        {
            _recorder.SetMetric("hits", Hits);
            _recorder.SetMetric("segments_broken", SegmentsBroken);
            _recorder.SetMetric("rings_broken", RingsBroken);
            string verdict = !HasClock ? "" : Won ? $" - BALL WINS ({SecondsLeft:0.0}s left)" : " - CLOCK WINS";
            _recorder.SetStandings(new[] { $"{RingsBroken}/{RingCount} rings broken{verdict}" });
            _recorder.Finish(reason);
            IsComplete = true;
        }
    }
}
