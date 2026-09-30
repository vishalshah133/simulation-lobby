using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Escape
{
    /// <summary>How a single attempt ended.</summary>
    public enum AttemptOutcome
    {
        Running = 0,
        Escaped = 1,

        /// <summary>Grew past the gap by the config's margin — escape became impossible.</summary>
        Trapped = 2,

        /// <summary>Hit the per-attempt cap while still able to fit. Usually means tuning is too slow.</summary>
        TimedOut = 3
    }

    /// <summary>
    /// A ball bounces inside a circle and grows every bounce. Does it escape through the gap, or get
    /// stuck? Played as a short series of attempts with a running score.
    /// </summary>
    /// <remarks>
    /// One run == one video == the whole series, so <see cref="RunResult"/> stays "the outcome of this
    /// video". The series lives here rather than in a general orchestrator because nothing else needs
    /// one yet — if a second format wants series structure, that is the signal to promote it into
    /// <c>Shared</c>, the same way code travels between formats everywhere else.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class EscapeSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring")]
        [SerializeField] Rigidbody2D _ball;
        [SerializeField] CircleCollider2D _ballCollider;
        [SerializeField] BounceDetector2D _bounceDetector;
        [SerializeField] CircularBoundary2D _boundary;

        EscapeConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        EscalationRule _growth;
        SizeEscalationTarget2D _sizeTarget;

        readonly List<AttemptOutcome> _outcomes = new List<AttemptOutcome>();

        float _seriesSizeFraction;
        float _attemptSpeed;
        float _gapAngularSpeed;
        int _attemptTick;
        int _restTicksRemaining;
        int _nearMissCount;
        bool _wasNearGap;

        public string FormatSlug => "esc";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by the HUD. Presentation observes; it must never write. ---

        /// <summary>1-based attempt currently on screen.</summary>
        public int AttemptNumber => Mathf.Min(_outcomes.Count + 1, AttemptTotal);

        public int AttemptTotal => _config != null ? _config.attemptCount : 0;

        /// <summary>Escapes so far — the ball's score.</summary>
        public int EscapeCount { get; private set; }

        /// <summary>Attempts resolved so far.</summary>
        public int AttemptsPlayed => _outcomes.Count;

        /// <summary>
        /// The wall's score: every attempt that did not escape. A timeout counts for the wall — the
        /// ball failed to get out, which is what the question asked.
        /// </summary>
        public int WallScore => _outcomes.Count - EscapeCount;

        /// <summary>Bounces in the current attempt.</summary>
        public int BounceCount => _bounceDetector != null ? _bounceDetector.TotalBounces : 0;

        public int NearMissCount => _nearMissCount;

        /// <summary>Impact speed of the most recent bounce, for note loudness.</summary>
        public float LastImpactSpeed => _bounceDetector != null ? _bounceDetector.LastImpactSpeed : 0f;

        /// <summary>Speed this attempt is running at — the scale <see cref="LastImpactSpeed"/> sits on.</summary>
        public float AttemptSpeed => _attemptSpeed;

        /// <summary>True during the beat between attempts, so the HUD can hold the result on screen.</summary>
        public bool IsResting => _restTicksRemaining > 0;

        /// <summary>Outcome of the attempt just finished, for the HUD's result flash.</summary>
        public AttemptOutcome LastOutcome { get; private set; }

        /// <summary>How close the ball is to being too big: 0 at spawn, 1 at the trapped threshold.</summary>
        public float TrappedProgress
        {
            get
            {
                if (_boundary == null || _sizeTarget == null)
                {
                    return 0f;
                }

                float limit = TrappedDiameter;
                return limit <= 0f ? 0f : Mathf.Clamp01(_sizeTarget.WorldRadius * 2f / limit);
            }
        }

        float TrappedDiameter => _boundary.GapChordWidth * (1f + _config.trappedMarginFraction);

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (EscapeConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random);

            _outcomes.Clear();
            EscapeCount = 0;
            LastOutcome = AttemptOutcome.Running;
            IsComplete = false;

            _boundary.gapDegrees = _config.gapDegrees;
            _boundary.gapCenterDegrees = _config.gapCenterDegrees;
            _boundary.Rebuild();

            // Drawn once for the whole series so every attempt starts on equal terms — with a score on
            // screen the attempts read as a fair contest, and unequal starting sizes would make the
            // comparison dishonest. randomizeSizePerAttempt overrules this deliberately.
            _seriesSizeFraction = _random.NextFloat(_config.initialSizeFractionMin, _config.initialSizeFractionMax);

            _recorder.SetMetric("gap_chord_width", _boundary.GapChordWidth);
            _recorder.SetMetric("trapped_diameter", _boundary.GapChordWidth * (1f + _config.trappedMarginFraction));
            _recorder.SetMetric("attempts_planned", _config.attemptCount);

            BeginAttempt();
        }

        void BeginAttempt()
        {
            float sizeFraction = _config.randomizeSizePerAttempt
                ? _random.NextFloat(_config.initialSizeFractionMin, _config.initialSizeFractionMax)
                : _seriesSizeFraction;

            // Draw order is fixed — size, then speed, then direction, then gap-rotation sign — so a
            // seed replays identically. Adding a draw here for esc-001 configs (gapAngularSpeed == 0)
            // would shift every later draw's position for no visible effect, so it's skipped entirely
            // rather than drawn-and-ignored.
            _attemptSpeed = _random.NextFloat(_config.speedMin, _config.speedMax);
            Vector2 direction = _random.NextDirection2D();

            _gapAngularSpeed = 0f;
            if (_config.gapAngularSpeed != 0f)
            {
                float sign = _config.gapDirectionSeeded
                    ? (_random.NextBool() ? 1f : -1f)
                    : Mathf.Sign(_config.gapAngularSpeed);
                _gapAngularSpeed = sign * Mathf.Abs(_config.gapAngularSpeed);
            }

            _boundary.transform.rotation = Quaternion.identity;

            float targetRadius = _boundary.radius * sizeFraction;
            float scale = targetRadius / _ballCollider.radius;
            _ball.transform.localScale = new Vector3(scale, scale, 1f);

            EscalationSettings growthSettings = _config.growth.Clone();
            growthSettings.startValue = scale;
            growthSettings.maxValue = scale * _config.growth.maxValue;
            growthSettings.minValue = scale * _config.growth.minValue;

            _sizeTarget = new SizeEscalationTarget2D(_ball.transform, _ballCollider);
            _growth = new EscalationRule(growthSettings, _sizeTarget);

            _bounceDetector.Reset();
            _ball.position = Vector2.zero;
            _ball.transform.position = Vector3.zero;
            _ball.linearVelocity = direction * _attemptSpeed;

            _attemptTick = 0;
            _nearMissCount = 0;
            _wasNearGap = false;
            LastOutcome = AttemptOutcome.Running;

            _recorder.LogEvent($"attempt_{AttemptNumber}_start", _attemptSpeed);
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            // The beat between attempts. The ball is parked so the viewer can read the result before
            // the reset yanks it away.
            if (_restTicksRemaining > 0)
            {
                _restTicksRemaining--;
                _ball.linearVelocity = Vector2.zero;

                if (_restTicksRemaining == 0)
                {
                    if (_outcomes.Count >= _config.attemptCount)
                    {
                        FinishSeries();
                    }
                    else
                    {
                        BeginAttempt();
                    }
                }

                return;
            }

            _attemptTick++;

            if (_gapAngularSpeed != 0f)
            {
                // Simulation state, not presentation — ticks with the fixed step so the gap's position
                // is exactly reproducible for a given seed, same as everything else in this method.
                _boundary.transform.Rotate(0f, 0f, _gapAngularSpeed * fixedDelta);
            }

            if (_bounceDetector.ConsumeBounce())
            {
                _growth.Step();
                _recorder.LogEvent("bounce", _sizeTarget.WorldRadius * 2f);
                RegisterNearMissIfAny();
            }

            ConstantSpeed2D.Apply(_ball, _attemptSpeed);

            EvaluateAttempt(fixedDelta);
        }

        /// <summary>
        /// A near miss is the ball lining up with the gap while too big to fit — the moment the viewer
        /// thinks it is about to escape. Edge-triggered, so lingering near the gap counts once.
        /// </summary>
        void RegisterNearMissIfAny()
        {
            Vector2 fromCentre = _ball.position;
            bool aimedAtGap = _boundary.IsWithinGap(fromCentre);
            bool tooBig = _sizeTarget.WorldRadius * 2f > _boundary.GapChordWidth;
            bool nearWall = fromCentre.magnitude > _boundary.radius * (1f - _config.nearMissGapWidths * 0.1f);

            bool isNearMiss = aimedAtGap && tooBig && nearWall;
            if (isNearMiss && !_wasNearGap)
            {
                _nearMissCount++;
                _recorder.LogEvent("near_miss", _nearMissCount);
            }

            _wasNearGap = isNearMiss;
        }

        void EvaluateAttempt(float fixedDelta)
        {
            Vector2 position = _ball.position;
            float distance = position.magnitude;

            if (distance > _boundary.radius + _boundary.thickness)
            {
                if (_boundary.IsWithinGap(position))
                {
                    EndAttempt(AttemptOutcome.Escaped);
                    return;
                }

                // The containment assertion. A ball outside the wall anywhere but the gap went
                // *through* solid geometry — that invalidates the video's whole premise, so fail the
                // run loudly rather than let a tunneled attempt reach a scan's shortlist.
                Debug.LogError(
                    $"[esc] TUNNELING at tick {_recorder.CurrentTick}: ball left the arena outside the gap " +
                    $"(pos {position}, dist {distance:0.00}, radius {_boundary.radius}). " +
                    "Lower the timestep or thicken the wall — this seed is unusable.", this);

                _recorder.SetMetric("tunneled", 1f);
                _recorder.LogEvent("tunneling_detected", distance);
                FinishSeries(CompletionReason.Failed);
                return;
            }

            // Resolve the moment escape becomes impossible, plus a margin that still allows a couple of
            // "only just too big" near-misses. Waiting for growth to clamp instead would leave a
            // hopeless ball bouncing for many seconds of footage nobody wants to watch.
            if (_sizeTarget.WorldRadius * 2f > TrappedDiameter)
            {
                EndAttempt(AttemptOutcome.Trapped);
                return;
            }

            if (_attemptTick * fixedDelta >= _config.maxAttemptSeconds)
            {
                EndAttempt(AttemptOutcome.TimedOut);
            }
        }

        void EndAttempt(AttemptOutcome outcome)
        {
            _outcomes.Add(outcome);
            LastOutcome = outcome;

            if (outcome == AttemptOutcome.Escaped)
            {
                EscapeCount++;
            }

            _recorder.LogEvent($"attempt_{_outcomes.Count}_{outcome.ToString().ToLowerInvariant()}", BounceCount);
            _recorder.SetMetric($"attempt_{_outcomes.Count}_bounces", BounceCount);
            _recorder.SetMetric($"attempt_{_outcomes.Count}_near_misses", _nearMissCount);

            _restTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(_config.interAttemptSeconds / _config.fixedTimestep));
        }

        void FinishSeries(CompletionReason reason = CompletionReason.Finished)
        {
            _recorder.SetMetric("escapes", EscapeCount);
            _recorder.SetMetric("attempts", _outcomes.Count);

            var standings = new List<string> { $"{EscapeCount}/{_outcomes.Count} escaped" };
            for (int i = 0; i < _outcomes.Count; i++)
            {
                standings.Add($"attempt {i + 1}: {_outcomes[i]}");
            }

            _recorder.SetStandings(standings);
            _recorder.Finish(reason);
            IsComplete = true;
        }
    }
}
