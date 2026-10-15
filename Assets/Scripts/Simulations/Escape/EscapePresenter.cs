using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Escape
{
    /// <summary>
    /// Drives the scoreboard and the bounce melody from escape-run state. Lives in the format because
    /// the dependency only points this way — the scoreboard and the melody player stay ignorant of
    /// balls and gaps so other formats can drive the same components.
    /// </summary>
    /// <remarks>
    /// Runs in <c>LateUpdate</c>: presentation reads state after the fixed tick has settled it, and
    /// never inside <c>FixedUpdate</c> where it could influence the simulation. Nothing here writes to
    /// the run.
    /// </remarks>
    [RequireComponent(typeof(EscapeSimulation))]
    public sealed class EscapePresenter : MonoBehaviour
    {
        [SerializeField] EscapeSimulation _simulation;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] BounceMelodyPlayer _melody;

        [Tooltip("Matchup headline, above the question.")]
        [SerializeField] string _title = "WALL VS BALL";

        [Tooltip("The hook, stated as a question the viewer wants answered.")]
        [SerializeField] string _question = "WHO WILL WIN?";

        [Tooltip("Restart the melody at each new round. Turn off when the melody is a song, so the tune " +
                 "carries on across rounds instead of replaying its first line every round.")]
        [SerializeField] bool _restartMelodyEachRound = true;

        int _lastBounceCount;
        int _lastAttemptsPlayed;
        Transform _gapMarker;

        void Reset()
        {
            _simulation = GetComponent<EscapeSimulation>();
        }

        void Start()
        {
            if (_hud != null)
            {
                // A ramped series states its rule instead of the generic question: a gap a few degrees
                // narrower each round is invisible unless the viewer is told to look for it.
                string explainer = _simulation != null && _simulation.Config != null
                    ? _simulation.Config.RampExplainer()
                    : null;

                _hud.SetTitle(_title);
                _hud.SetQuestion(explainer ?? _question);
                _hud.SetSides("WALL", "BALL");
            }

            // Built by EscapeSceneBuilder as a child of the wall, so existing scenes need no rewiring.
            Transform boundary = _simulation != null ? _simulation.BoundaryTransform : null;
            _gapMarker = boundary != null ? boundary.Find("GapMarker") : null;
        }

        void LateUpdate()
        {
            if (_simulation == null)
            {
                return;
            }

            UpdateAudio();
            UpdateScoreboard();
            UpdateGapMarker();
        }

        /// <summary>Keeps the marker spanning the exit when a series ramp narrows it between rounds.</summary>
        void UpdateGapMarker()
        {
            if (_gapMarker == null)
            {
                return;
            }

            Vector3 scale = _gapMarker.localScale;
            float width = _simulation.GapChordWidth;
            if (width > 0f && !Mathf.Approximately(scale.x, width))
            {
                _gapMarker.localScale = new Vector3(width, scale.y, scale.z);
            }
        }

        void UpdateAudio()
        {
            if (_melody == null)
            {
                return;
            }

            // A new round restarts the melody at the root, so each round climbs from the bottom.
            if (_simulation.AttemptsPlayed != _lastAttemptsPlayed)
            {
                _lastAttemptsPlayed = _simulation.AttemptsPlayed;
                _lastBounceCount = 0;
                if (_restartMelodyEachRound)
                {
                    _melody.ResetMelody();
                }
            }

            int bounces = _simulation.BounceCount;
            if (bounces < _lastBounceCount)
            {
                // Counter reset under us (new attempt) — resync rather than play a burst of notes.
                _lastBounceCount = bounces;
                return;
            }

            // One note per bounce, even if several landed inside a single rendered frame. Notes played
            // must equal bounces: a missing note means a missed collision, which is the cheapest
            // tunneling detector this format has.
            int pending = bounces - _lastBounceCount;
            _lastBounceCount = bounces;

            if (pending <= 0)
            {
                return;
            }

            float speed = Mathf.Max(0.01f, _simulation.AttemptSpeed);
            float velocity01 = Mathf.Clamp01(_simulation.LastImpactSpeed / (speed * 2f));

            for (int i = 0; i < pending; i++)
            {
                _melody.PlayNote(velocity01);
            }
        }

        void UpdateScoreboard()
        {
            if (_hud == null)
            {
                return;
            }

            _hud.SetScore(_simulation.WallScore, _simulation.EscapeCount);
            string status = RampStatus();
            string round = $"ROUND {_simulation.AttemptNumber} / {_simulation.AttemptTotal}";
            _hud.SetRoundLabel(string.IsNullOrEmpty(status) ? round : $"{round}  ·  {status}");
            _hud.SetCounter("BOUNCES", _simulation.BounceCount);

            // The result only shows during the beat between rounds — that pause exists so the viewer
            // can read it, and leaving it up into the next round would misreport the live one.
            if (_simulation.IsResting)
            {
                switch (_simulation.LastOutcome)
                {
                    case AttemptOutcome.Escaped:
                        _hud.ShowResult("ESCAPED!", true);
                        break;
                    case AttemptOutcome.Trapped:
                        _hud.ShowResult("TOO BIG — WALL WINS", false);
                        break;
                    case AttemptOutcome.TimedOut:
                        _hud.ShowResult("TIME — WALL WINS", false);
                        break;
                    default:
                        _hud.ShowResult(null, false);
                        break;
                }
            }
            else
            {
                _hud.ShowResult(null, false);
            }
        }

        /// <summary>
        /// The round line's suffix for a ramped series, in units a viewer can check by eye: how many balls
        /// fit across the exit (live, so it counts down as the ball grows) and how long one lap of the exit
        /// takes. Empty for a config without a ramp.
        /// </summary>
        string RampStatus()
        {
            EscapeConfig config = _simulation.Config;
            if (config == null || (!config.HasGapRamp && !config.HasSpinRamp))
            {
                return string.Empty;
            }

            var parts = new System.Collections.Generic.List<string>(2);
            if (config.HasGapRamp)
            {
                // Floored, so the line never claims a fit the ball doesn't have.
                float fits = Mathf.Floor(_simulation.BallsAcrossExit * 10f) / 10f;
                parts.Add(fits >= 1f ? $"EXIT FITS {fits:0.0} BALLS" : "TOO BIG TO FIT");
            }

            if (config.HasSpinRamp && _simulation.ExitLapSeconds > 0f)
            {
                parts.Add($"1 LAP EVERY {Mathf.RoundToInt(_simulation.ExitLapSeconds)}s");
            }

            return string.Join("  ·  ", parts);
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(EscapeSimulation simulation, VersusScoreboardHud hud, BounceMelodyPlayer melody)
        {
            _simulation = simulation;
            _hud = hud;
            _melody = melody;
        }

        /// <summary>Assigned by the scene builder, for variants with their own copy.</summary>
        public void SetCopy(string title, string question, bool restartMelodyEachRound)
        {
            _title = title;
            _question = question;
            _restartMelodyEachRound = restartMelodyEachRound;
        }
    }
}
