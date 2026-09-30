using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival
{
    /// <summary>
    /// Turns wall-survival state into the video: HUD copy, the audio cues, the slow-motion holds and
    /// the camera's push and shake. Lives in the format for the same reason <c>EscapePresenter</c>
    /// does — every component it drives stays ignorant of walls and balls, so other formats can drive
    /// them.
    /// </summary>
    /// <remarks>
    /// Runs in <c>LateUpdate</c>: presentation reads state after the fixed tick has settled it, never
    /// inside <c>FixedUpdate</c>. Nothing here writes to the run — the one global it touches is
    /// <c>Time.timeScale</c>, via <see cref="SlowMotionDirector"/>, which changes how long the ticks
    /// take to watch and not what they contain (see that class's remarks).
    /// </remarks>
    [RequireComponent(typeof(WallSurvivalSimulation))]
    public sealed class WallSurvivalPresenter : MonoBehaviour
    {
        [SerializeField] WallSurvivalSimulation _simulation;
        [SerializeField] WallSurvivalConfig _config;
        [SerializeField] ImpactAudioPlayer _audio;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] SlowMotionDirector _slowMotion;
        [SerializeField] ImpactCameraRig _cameraRig;

        int _lastImpactCount;
        int _lastEliminatedTotal;
        int _lastShotIndex = -1;
        bool _riserPlayedThisShot;
        bool _whooshPlayedThisShot;
        bool _slowMotionUsedThisShot;
        bool _wipeoutSlowMotionUsed;
        bool _finalReported;
        float _slowMotionSecondsLeft;
        WallSurvivalSimulation.RunPhase _lastPhase = WallSurvivalSimulation.RunPhase.Opening;

        void Reset()
        {
            _simulation = GetComponent<WallSurvivalSimulation>();
        }

        void Start()
        {
            if (_hud == null || _simulation == null)
            {
                return;
            }

            _hud.SetTitle("WALL vs BALL");
            _hud.SetSides("WALL", "BALL");
            _hud.SetQuestion(QuestionText(_simulation.StartingBlockCount));
            _hud.ShowResult(null, true);
        }

        /// <summary>The hook, shared with the scene builder so the baked canvas and the live one agree.</summary>
        public static string QuestionText(int blockCount) =>
            $"GUESS: how many HITS to destroy all {blockCount} BLOCKS?";

        void LateUpdate()
        {
            if (_simulation == null)
            {
                return;
            }

            TickShotCues();
            TickImpactCues();
            TickEliminationCues();
            TickScatterRumble();
            TickSlowMotion();
            TickHud();

            _lastPhase = _simulation.Phase;
            _lastShotIndex = _simulation.ShotIndex;
        }

        /// <summary>Riser on the hold, whoosh on the launch — one of each per shot.</summary>
        void TickShotCues()
        {
            if (_simulation.ShotIndex != _lastShotIndex)
            {
                _riserPlayedThisShot = false;
                _whooshPlayedThisShot = false;
                _slowMotionUsedThisShot = false;
            }

            if (_simulation.Phase == WallSurvivalSimulation.RunPhase.HitHold && !_riserPlayedThisShot)
            {
                _riserPlayedThisShot = true;
                if (_audio != null)
                {
                    _audio.PlayRiser(_simulation.Progress01);
                }
            }

            if (_simulation.HasLaunched && !_whooshPlayedThisShot)
            {
                _whooshPlayedThisShot = true;
                if (_audio != null)
                {
                    _audio.PlayWhoosh();
                }
            }
        }

        /// <summary>One knock per counted collision, plus a shake scaled by how hard it landed.</summary>
        void TickImpactCues()
        {
            int impacts = _simulation.ImpactCount;
            int pending = impacts - _lastImpactCount;
            _lastImpactCount = impacts;

            if (pending <= 0)
            {
                return;
            }

            float velocity01 = Mathf.Clamp01(_simulation.LastImpactSpeed / 20f);

            if (_audio != null)
            {
                // Even if several landed inside one rendered frame — same "notes played must equal
                // bounces" discipline EscapePresenter uses for its melody.
                for (int i = 0; i < pending; i++)
                {
                    _audio.PlayImpact(velocity01);
                }
            }

            if (_cameraRig != null)
            {
                // Burst size matters as much as speed: ploughing through twelve blocks at once is the
                // moment worth shaking for, not a single glancing touch.
                float burst01 = Mathf.Clamp01(pending / 8f);
                _cameraRig.Shake(Mathf.Max(velocity01 * 0.6f, burst01));
            }
        }

        /// <summary>
        /// Walks the chime ladder upward as the cascade removes blocks — the payoff sound. The ladder
        /// restarts from the bottom every hit, so each cascade is its own rising run and a big hit
        /// audibly climbs higher than a small one.
        /// </summary>
        void TickEliminationCues()
        {
            int eliminated = _simulation.EliminatedTotal;
            int pending = eliminated - _lastEliminatedTotal;
            _lastEliminatedTotal = eliminated;

            if (pending <= 0 || _audio == null)
            {
                return;
            }

            int firstDegree = _simulation.EliminatedThisShot - pending;
            for (int i = 0; i < pending; i++)
            {
                _audio.PlayChime(firstDegree + i);
            }
        }

        /// <summary>Feeds the audio how much of the wall is moving, so debris is audible as it scatters.</summary>
        void TickScatterRumble()
        {
            if (_audio != null)
            {
                _audio.SetScatterIntensity(_simulation.ScatterEnergy01);
            }
        }

        /// <summary>
        /// Holds slow-motion on each hit's first contact — longer during the last stand — and once more,
        /// longest of all, the moment the wall is confirmed to be coming down. Timed in unscaled seconds
        /// so a hold is the same length on screen however far the scale has dropped.
        /// </summary>
        void TickSlowMotion()
        {
            if (_slowMotion == null || _config == null)
            {
                return;
            }

            bool lastStand = _simulation.IsLastStand;
            bool eligible = _config.slowMotionOnEveryHit || lastStand;

            if (eligible && !_slowMotionUsedThisShot && _simulation.HasContactThisShot &&
                _simulation.Phase == WallSurvivalSimulation.RunPhase.HitFlight)
            {
                _slowMotionUsedThisShot = true;
                Hold(_config.slowMotionSeconds + (lastStand ? _config.lastStandSlowMotionBonus : 0f));
            }

            // The reveal. Which hit is the last is unknowable until it happens, so this keys off the
            // outcome — every remaining block on its way out — and extends whatever hold is running.
            if (!_wipeoutSlowMotionUsed && _simulation.WipeoutUnderway)
            {
                _wipeoutSlowMotionUsed = true;
                Hold(Mathf.Max(_slowMotionSecondsLeft, _config.wipeoutSlowMotionSeconds));
            }

            if (_slowMotionSecondsLeft <= 0f)
            {
                return;
            }

            _slowMotionSecondsLeft -= Time.unscaledDeltaTime;
            if (_slowMotionSecondsLeft <= 0f)
            {
                _slowMotion.Release();
            }
        }

        void Hold(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            _slowMotionSecondsLeft = seconds;
            _slowMotion.Engage();
        }

        void TickHud()
        {
            if (_cameraRig != null)
            {
                _cameraRig.SetProgress(_simulation.Progress01);
            }

            if (_hud == null)
            {
                return;
            }

            _hud.SetScore(_simulation.SurvivorCount, _simulation.EliminatedTotal);
            _hud.SetCounter("STANDING", _simulation.SurvivorCount);

            // The hit count is the thing being guessed: a live tally, ticking up on contact, with no
            // "/ total" — printing a total would give the answer away.
            _hud.SetRoundLabel($"HITS  {_simulation.HitsLanded}");

            // The hit result refreshes every frame rather than on the beat change alone: the phase
            // flips to HitResult *before* the cascade has removed anything, so a single write on entry
            // always reads "-0 BLOCKS". Refreshing lets the number count up with the chimes.
            if (_simulation.Phase == WallSurvivalSimulation.RunPhase.HitResult)
            {
                _hud.ShowResult(
                    _simulation.HasContactThisShot ? $"-{_simulation.EliminatedThisShot} BLOCKS" : "MISSED",
                    !_simulation.HasContactThisShot);
                return;
            }

            // Every other line is a beat, not a readout: rewrite it only when the run moves to a new
            // phase or a new shot, so it holds on screen long enough to be read.
            bool beatChanged = _simulation.Phase != _lastPhase || _simulation.ShotIndex != _lastShotIndex;
            if (!beatChanged)
            {
                return;
            }

            switch (_simulation.Phase)
            {
                case WallSurvivalSimulation.RunPhase.Opening:
                    _hud.ShowResult("LOCK IN YOUR GUESS", true);
                    break;

                case WallSurvivalSimulation.RunPhase.HitHold:
                    _hud.ShowResult(
                        _simulation.IsLastStand ? $"ONLY {_simulation.SurvivorCount} LEFT" : null,
                        true);
                    break;

                case WallSurvivalSimulation.RunPhase.HitFlight:
                    _hud.ShowResult(null, true);
                    break;

                case WallSurvivalSimulation.RunPhase.FinalHold:
                case WallSurvivalSimulation.RunPhase.Complete:
                    if (!_finalReported)
                    {
                        _finalReported = true;
                        bool wallDown = _simulation.SurvivorCount == 0;
                        // Destroyed is the ball winning, so it reads in the failure colour — the wall
                        // is the side the HUD lists first and the one being rooted for.
                        _hud.ShowResult(
                            wallDown
                                ? $"DESTROYED IN {_simulation.HitsLanded} HITS"
                                : $"WALL SURVIVED {_simulation.HitsLanded} HITS",
                            !wallDown);
                    }

                    break;
            }
        }

        /// <summary>Assigned by the scene builder, so nothing needs hand-wiring in the Inspector.</summary>
        public void Bind(WallSurvivalSimulation simulation, WallSurvivalConfig config, ImpactAudioPlayer audio,
            VersusScoreboardHud hud, SlowMotionDirector slowMotion, ImpactCameraRig cameraRig)
        {
            _simulation = simulation;
            _config = config;
            _audio = audio;
            _hud = hud;
            _slowMotion = slowMotion;
            _cameraRig = cameraRig;
        }
    }
}
