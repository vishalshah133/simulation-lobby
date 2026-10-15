using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MultiplyEscape
{
    /// <summary>
    /// Drives the scoreboard, the swarm's colours and the audio from run state. Reads in
    /// <c>LateUpdate</c> and never writes to the run.
    /// </summary>
    [RequireComponent(typeof(MultiplyEscapeSimulation))]
    public sealed class MultiplyEscapePresenter : MonoBehaviour
    {
        [SerializeField] MultiplyEscapeSimulation _simulation;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] BounceMelodyPlayer _cloneAudio;
        [SerializeField] BounceMelodyPlayer _escapeAudio;

        [SerializeField] string _title = "MULTIPLY VS ESCAPE";
        [SerializeField] string _question = "WILL IT FILL UP?";

        [Tooltip("Most notes per rendered frame. A swarm can make dozens of clones in one frame, and " +
                 "playing all of them is noise, not music.")]
        [Range(1, 6)] [SerializeField] int _maxNotesPerFrame = 2;

        [Tooltip("Golden-ratio hue steps from this start, so neighbours by birth order never share a colour.")]
        [Range(0f, 1f)] [SerializeField] float _hueStart = 0.95f;

        [Range(0f, 1f)] [SerializeField] float _saturation = 0.72f;

        int _lastBorn;
        int _lastEscaped;
        int _lastRoundsPlayed;

        void Reset()
        {
            _simulation = GetComponent<MultiplyEscapeSimulation>();
        }

        void Start()
        {
            if (_hud != null)
            {
                _hud.SetTitle(_title);
                _hud.SetQuestion(_question);
                _hud.SetSides("ESCAPE", "MULTIPLY");
            }
        }

        void LateUpdate()
        {
            if (_simulation == null)
            {
                return;
            }

            Colour(_simulation.Inside);
            Colour(_simulation.Escaping);
            UpdateAudio();
            UpdateScoreboard();
        }

        void Colour(IReadOnlyList<SwarmBall> balls)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                SwarmBall ball = balls[i];
                if (ball.StyledSerial == ball.Serial || ball.Renderer == null)
                {
                    continue;
                }

                ball.StyledSerial = ball.Serial;
                float hue = Mathf.Repeat(_hueStart + ball.Serial * 0.618034f, 1f);
                ball.Renderer.color = Color.HSVToRGB(hue, _saturation, 1f);
            }
        }

        void UpdateAudio()
        {
            if (_simulation.RoundsPlayed != _lastRoundsPlayed || _simulation.BornThisRound < _lastBorn)
            {
                // New round: resync the counters and start the climb from the bottom again.
                _lastRoundsPlayed = _simulation.RoundsPlayed;
                _lastBorn = _simulation.BornThisRound;
                _lastEscaped = _simulation.EscapedThisRound;
                if (_cloneAudio != null)
                {
                    _cloneAudio.ResetMelody();
                }

                if (_escapeAudio != null)
                {
                    _escapeAudio.ResetMelody();
                }
            }

            int born = _simulation.BornThisRound;
            int clones = born - _lastBorn;
            _lastBorn = born;
            Play(_cloneAudio, clones);

            int escaped = _simulation.EscapedThisRound;
            int escapes = escaped - _lastEscaped;
            _lastEscaped = escaped;
            Play(_escapeAudio, escapes);
        }

        void Play(BounceMelodyPlayer player, int pending)
        {
            if (player == null || pending <= 0)
            {
                return;
            }

            int notes = Mathf.Min(pending, _maxNotesPerFrame);
            for (int i = 0; i < notes; i++)
            {
                player.PlayNote(1f);
            }
        }

        void UpdateScoreboard()
        {
            if (_hud == null)
            {
                return;
            }

            _hud.SetScore(_simulation.EmptyScore, _simulation.FullScore);
            _hud.SetRound(_simulation.RoundNumber, _simulation.RoundTotal);

            int inside = _simulation.InsideCount;
            int target = _simulation.FillTarget;
            bool nearlyFull = target > 0 && inside >= target * 0.8f;
            _hud.SetCounterText($"INSIDE  {inside} / {target}", nearlyFull);

            if (_simulation.IsResting)
            {
                switch (_simulation.LastOutcome)
                {
                    case RoundOutcome.Full:
                        _hud.ShowResult("FULL! MULTIPLY WINS", true);
                        break;
                    case RoundOutcome.Empty:
                        _hud.ShowResult($"ALL OUT! PEAKED AT {_simulation.PeakThisRound}", false);
                        break;
                    case RoundOutcome.TimedOut:
                        _hud.ShowResult("TIME, NO WINNER", false);
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

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(MultiplyEscapeSimulation simulation, VersusScoreboardHud hud,
                         BounceMelodyPlayer cloneAudio, BounceMelodyPlayer escapeAudio)
        {
            _simulation = simulation;
            _hud = hud;
            _cloneAudio = cloneAudio;
            _escapeAudio = escapeAudio;
        }
    }
}
