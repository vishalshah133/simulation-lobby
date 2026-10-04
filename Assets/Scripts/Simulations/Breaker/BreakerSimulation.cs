using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Breaker
{
    /// <summary>What happened to a segment, for presentation to react to.</summary>
    public enum SegmentEventKind
    {
        /// <summary>Touched, lost a hit point, still standing.</summary>
        Cracked = 0,

        /// <summary>Touched and broken: a hole opened.</summary>
        Broken = 1,

        /// <summary>Destroyed because the ball escaped its ring — the ring-clear explosion.</summary>
        Shattered = 2
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

        public SegmentEvent(int tick, int ring, int segment, SegmentEventKind kind, Vector2 position, Vector2 outward, int hitPointsLeft)
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
    /// A ball starts inside nested rings. Every touch damages the segment it hit; a broken segment
    /// leaves a hole, and when the ball gets through a hole its ring explodes and it is on to the next
    /// one — a little faster. Can it break all of them?
    /// </summary>
    /// <remarks>
    /// Holes come only from physics: a segment breaks because the ball touched it, and the ball escapes
    /// only through an opening that exists. The tunneling check on every escape enforces that.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class BreakerSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring")]
        [SerializeField] Rigidbody2D _ball;
        [SerializeField] CircleCollider2D _ballCollider;
        [SerializeField] BounceDetector2D _bounceDetector;
        [Tooltip("Innermost first.")]
        [SerializeField] SegmentedRing2D[] _rings;

        BreakerConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;

        int[][] _hitPoints;
        float[] _ringAngle;
        float[] _ringSpin;
        int[] _ringHits;
        float _speed;
        int _holdTicksRemaining;
        float _decidedAtSeconds;

        readonly List<SegmentEvent> _events = new List<SegmentEvent>();

        public string FormatSlug => "brk";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        /// <summary>Changes every Initialize, so presentation can tell a new run started in the same scene.</summary>
        public int RunId { get; private set; }

        public int RingCount => _rings != null ? _rings.Length : 0;

        /// <summary>The ring the ball is currently inside (0 = innermost). Equals <see cref="RingCount"/> once free.</summary>
        public int CurrentRing { get; private set; }

        public int RingsBroken => CurrentRing;

        public bool IsFree => CurrentRing >= RingCount;

        /// <summary>The ball broke every ring inside the limit.</summary>
        public bool Won { get; private set; }

        /// <summary>The clock ran out with rings still standing.</summary>
        public bool TimeUp { get; private set; }

        public bool HasClock => _config != null && _config.HasClock;

        public float TimeLimit => HasClock ? _config.timeLimitSeconds : 0f;

        /// <summary>Simulated seconds left on the clock, frozen at the moment the run is decided.</summary>
        public float SecondsLeft => HasClock
            ? Mathf.Max(0f, _config.timeLimitSeconds - (_decidedAtSeconds >= 0f ? _decidedAtSeconds : SimulatedSeconds))
            : 0f;

        float SimulatedSeconds => _recorder != null ? _recorder.SimulatedSeconds : 0f;

        /// <summary>Every touch on a segment — one melody note each.</summary>
        public int Hits { get; private set; }

        public int SegmentsBroken { get; private set; }

        public float Speed => _speed;

        public float LastImpactSpeed => _bounceDetector != null ? _bounceDetector.LastImpactSpeed : 0f;

        public IReadOnlyList<SegmentEvent> Events => _events;

        public SegmentedRing2D Ring(int index) => _rings[index];

        public int MaxHitPoints(int ring) => _config != null ? _config.HitPointsForRing(ring) : 1;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (BreakerConfig)config;
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

            if (_rings == null || _rings.Length != _config.ringCount)
            {
                Debug.LogError($"[brk] Scene has {RingCount} rings but the config wants {_config.ringCount}. " +
                               "Rebuild the scene from its menu.", this);
            }

            int ringCount = RingCount;
            _hitPoints = new int[ringCount][];
            _ringAngle = new float[ringCount];
            _ringSpin = new float[ringCount];
            _ringHits = new int[ringCount];

            // Draw order is fixed: spin sign, start angle, start distance, launch angle, launch side.
            float spinSign = _config.spinDirectionSeeded ? (_random.NextBool() ? 1f : -1f) : 1f;

            for (int ring = 0; ring < ringCount; ring++)
            {
                SegmentedRing2D segments = _rings[ring];
                int count = segments.SegmentCount;
                int hitPoints = _config.HitPointsForRing(ring);

                _hitPoints[ring] = new int[count];
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

            float startAngle = _random.NextFloat(0f, 360f) * Mathf.Deg2Rad;
            float room = _config.innerRadius - _config.ballRadius;
            float startDistance = room * _random.NextFloat(_config.startOffsetMax * 0.35f, _config.startOffsetMax);
            Vector2 start = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * startDistance;

            // Launched across the ring rather than along a radius, so the first bounces sweep the wall.
            float launchOffset = _random.NextFloat(35f, 145f) * (_random.NextBool() ? 1f : -1f);
            float launchAngle = startAngle + launchOffset * Mathf.Deg2Rad;
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
                RegisterHit(_bounceDetector.LastCollider);
            }

            ConstantSpeed2D.Apply(_ball, _speed);

            CheckEscape();
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

                Hits++;
                _ringHits[ring]++;
                int left = --_hitPoints[ring][segment];

                if (left == 0)
                {
                    _rings[ring].GetCollider(segment).enabled = false;
                    SegmentsBroken++;
                    AddEvent(ring, segment, SegmentEventKind.Broken, 0);
                    _recorder.LogEvent($"break_r{ring + 1}", segment);
                }
                else
                {
                    AddEvent(ring, segment, SegmentEventKind.Cracked, left);
                }

                return;
            }
        }

        void CheckEscape()
        {
            if (IsFree)
            {
                return;
            }

            SegmentedRing2D ring = _rings[CurrentRing];
            Vector2 fromCentre = _ball.position - (Vector2)ring.transform.position;
            if (fromCentre.magnitude <= ring.OuterRadius + _config.ballRadius)
            {
                return;
            }

            // The containment assertion: the ball may only leave through a broken segment (or a
            // neighbour of one — the ball is wider than the seam it straddles on the way out).
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
                    $"[brk] TUNNELING at tick {_recorder.CurrentTick}: ball left ring {CurrentRing + 1} through " +
                    $"intact segment {at}. Lower the timestep or thicken the rings — this seed is unusable.", this);
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
                AddEvent(ring, i, SegmentEventKind.Shattered, 0);
            }

            _recorder.SetMetric($"ring_{ring + 1}_hits", _ringHits[ring]);
            _recorder.SetMetric($"ring_{ring + 1}_seconds", _recorder.SimulatedSeconds);
            _recorder.LogEvent($"ring_{ring + 1}_broken", _ringHits[ring]);

            CurrentRing++;

            if (IsFree)
            {
                Won = true;
                Decide();
                return;
            }

            _speed = _config.SpeedForRing(CurrentRing);
            _recorder.LogEvent($"ring_{CurrentRing + 1}_start", _speed);
        }

        /// <summary>
        /// Time's up: the ball freezes where it is and the rings stop, so the losing position reads as
        /// a still frame under the result.
        /// </summary>
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
            // ring it died on was mostly broken.
            float progress = 1f;
            if (!IsFree)
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

        void AddEvent(int ring, int segment, SegmentEventKind kind, int left)
        {
            SegmentedRing2D segments = _rings[ring];
            _events.Add(new SegmentEvent(_recorder.CurrentTick, ring, segment, kind,
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
