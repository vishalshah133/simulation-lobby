using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Escape
{
    /// <summary>
    /// 16:9 compilation wrapper around an <c>esc</c> run (comp-001 segment 2). Does two jobs the
    /// 9:16 scenes never needed:
    /// <list type="bullet">
    /// <item>Holds the run back until the segment card and the start of the "HOW IT WORKS" panel have
    /// played, so round 1 is not spent behind an opaque card.</item>
    /// <item>Pushes the right-margin counter and the bottom caption into the shared
    /// <see cref="SegmentCardPresenter"/> from the simulation's read-only state.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Presentation timing only: the delay changes <em>when</em> the run begins, never what it does, so
    /// the seed still replays identically. Reads the simulation in <c>LateUpdate</c> and never writes
    /// to it; the single call into the run is <see cref="SimulationRunner.Begin"/>.
    /// </remarks>
    [RequireComponent(typeof(EscapeSimulation))]
    public sealed class EscapeWideSegment : MonoBehaviour
    {
        [SerializeField] SimulationRunner _runner;
        [SerializeField] EscapeSimulation _simulation;
        [SerializeField] SegmentCardPresenter _card;

        [Tooltip("Wall-clock seconds before the run begins: segment card + the start of the explainer.")]
        [Min(0f)] [SerializeField] float _startDelaySeconds = 4.5f;

        [Tooltip("Draw a fresh seed each Play (logged, so a good take can be re-rendered by pasting it back).")]
        [SerializeField] bool _randomizeSeed = true;

        [Tooltip("Seed used when Randomize Seed is off.")]
        [SerializeField] int _seed = 1;

        [Tooltip("Closing caption: the one-line why, shown from the last round's result onwards.")]
        [SerializeField] string _whyCaption = "A SMALLER GAP NEEDS MORE BOUNCES TO FIND";

        [Tooltip("Seconds of the last round's rest spent on its result before the why line takes over.")]
        [Min(0f)] [SerializeField] float _lastResultSeconds = 1.1f;

        float _elapsed;
        bool _started;
        float _lastRestStart = -1f;

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(SimulationRunner runner, EscapeSimulation simulation, SegmentCardPresenter card,
            float startDelaySeconds, bool randomizeSeed, int seed)
        {
            _runner = runner;
            _simulation = simulation;
            _card = card;
            _startDelaySeconds = startDelaySeconds;
            _randomizeSeed = randomizeSeed;
            _seed = seed;
        }

        void Start()
        {
            if (_card != null)
            {
                // The card must keep its own clock: once the runner starts, its SimulatedSeconds
                // would reset to zero and replay the card over the live round.
                _card.SetTimeSource(() => _elapsed);
            }
        }

        void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            if (!_started && _elapsed >= _startDelaySeconds && _runner != null)
            {
                _started = true;
                int seed = _randomizeSeed ? System.Guid.NewGuid().GetHashCode() : _seed;
                if (_randomizeSeed)
                {
                    Debug.Log($"[esc-wide] Random seed {seed}. To re-render this exact take: set Seed " +
                              "on the Escape Wide Segment component to this value and untick Randomize Seed.", this);
                }

                _runner.Begin(seed);
            }
        }

        void LateUpdate()
        {
            if (_card == null || _simulation == null)
            {
                return;
            }

            EscapeConfig config = _simulation.Config;
            if (!_started || config == null)
            {
                _card.SetCounter(string.Empty);
                _card.SetResultCaption(string.Empty);
                return;
            }

            _card.SetCounter(CounterText(config));
            _card.SetResultCaption(CaptionText(config));
        }

        /// <summary>
        /// "BOUNCES n / ~N": N is the bounce at which the ball will have grown past the exit, computed
        /// from the current size so it is exact for this round rather than a guess for the series.
        /// </summary>
        string CounterText(EscapeConfig config)
        {
            string round = $"<size=55%>ROUND {_simulation.AttemptNumber}/{_simulation.AttemptTotal}</size>";
            string bounces = _simulation.BounceCount.ToString();

            float ballsAcross = _simulation.BallsAcrossExit;
            float magnitude = config.growth.magnitude;
            if (config.growth.mode == EscalationMode.Multiplicative && magnitude > 1f && ballsAcross > 0f)
            {
                // Bounces left until diameter exceeds chord * (1 + margin), the sim's own "trapped" test.
                float remaining = Mathf.Log(ballsAcross * (1f + config.trappedMarginFraction)) / Mathf.Log(magnitude);
                int ceiling = _simulation.BounceCount + Mathf.Max(0, Mathf.CeilToInt(remaining));
                bounces = $"{_simulation.BounceCount} / ~{ceiling}";
            }

            return $"<size=55%>BOUNCES</size>\n{bounces}\n{round}";
        }

        string CaptionText(EscapeConfig config)
        {
            if (_simulation.IsResting || _simulation.IsComplete)
            {
                bool lastRound = _simulation.AttemptsPlayed >= _simulation.AttemptTotal;
                if (lastRound)
                {
                    if (_lastRestStart < 0f)
                    {
                        _lastRestStart = _elapsed;
                    }

                    if (_elapsed - _lastRestStart >= _lastResultSeconds || _simulation.IsComplete)
                    {
                        return _whyCaption;
                    }
                }

                switch (_simulation.LastOutcome)
                {
                    case AttemptOutcome.Escaped: return "ESCAPED!";
                    case AttemptOutcome.Trapped: return "TOO BIG FOR THE GAP - WALL WINS";
                    case AttemptOutcome.TimedOut: return "OUT OF TIME - WALL WINS";
                }
            }

            // Live: say what is different about this round, since a few degrees is hard to see.
            float gap = config.GapDegreesForAttempt(_simulation.AttemptNumber - 1);
            float fits = Mathf.Floor(_simulation.BallsAcrossExit * 10f) / 10f;
            string fit = fits >= 1f ? $"EXIT FITS {fits:0.0} BALLS" : "TOO BIG TO FIT";
            return $"GAP {gap:0}°  ·  {fit}";
        }
    }
}
