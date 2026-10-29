using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Thin binder from the marble grand prix (<see cref="IRaceState"/>) to the format-agnostic presentation pieces: the top-5
    /// leaderboard, the caption (question, 3-2-1-GO, LAP and FINAL LAP banners, the winner and podium), the camera director, the
    /// sound, the number tags and the win confetti. Reads simulation state in <c>Update</c> and never writes it. Events fire in
    /// <c>FixedUpdate</c>, so they are only recorded and acted on in <c>Update</c>.
    /// </summary>
    public sealed class RacePresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] MonoBehaviour _simulation; // must implement IRaceState
        [SerializeField] LeaderboardHud _leaderboard;
        [SerializeField] CaptionHud _caption;
        [SerializeField] TourCameraRig _cameraRig;
        [SerializeField] RaceCameraDirector _director;
        [SerializeField] RaceAudio _audio;
        [SerializeField] Camera _camera;

        [Header("Text")]
        [Tooltip("Hook, stated as a question. {0} is replaced with the lap count.")]
        public string questionFormat = "WHICH MARBLE WINS {0} LAPS?";
        public string boardTitle = "RACE ORDER";

        [Header("Board")]
        [Tooltip("Rows on the leaderboard (capped by the rows the HUD canvas was built with).")]
        [Min(3)] public int boardRows = 16;

        [Header("Countdown")]
        [Tooltip("Mirror of MachineRaceConfig.countdownSeconds (the interface does not expose it). Only drives the 3-2-1 numbers.")]
        [Min(1f)] public float countdownSeconds = 3f;
        [Min(0.2f)] public float goBannerSeconds = 1.2f;

        [Header("Banners")]
        [Min(0.5f)] public float lapBannerSeconds = 3f;
        [Min(0.5f)] public float finalLapBannerSeconds = 4f;
        [Min(0.5f)] public float winBannerSeconds = 14f;
        [Min(0)] public int confettiPerBurst = 220;

        [Header("Tags")]
        public bool spawnTags = true;

        static readonly Color LapColour = new Color(1f, 0.92f, 0.45f, 1f);
        static readonly Color FinalLapColour = new Color(1f, 0.62f, 0.12f, 1f);

        IRaceState _state;
        string _question = string.Empty;
        bool _ready;
        int _slots;
        int[] _slotRacer;
        RacerTag[] _tags;

        // Recorded from events (FixedUpdate), consumed in Update.
        RacePhase _recordedPhase = RacePhase.Grid;
        float _phaseStartSimTime;
        int _recordedLapDone;
        int _wonRacer = -1;

        // Shown state.
        RacePhase _shownPhase = RacePhase.Grid;
        int _shownLapDone;
        int _shownCountdownNumber = -1;
        float _bannerUntil;
        bool _winShown;
        int _burstsLeft;
        float _burstTimer;
        Color _burstColour;
        ConfettiBurst3D _confetti;

        /// <summary>Edit-time wiring from the scene builder. Adds or finds the director and the audio on this object.</summary>
        public void Bind(MonoBehaviour simulation, LeaderboardHud leaderboard, CaptionHud caption, TourCameraRig cameraRig)
        {
            _simulation = simulation;
            _leaderboard = leaderboard;
            _caption = caption;
            _cameraRig = cameraRig;

            _camera = cameraRig != null ? cameraRig.GetComponent<Camera>() : null;

            _director = GetComponent<RaceCameraDirector>();
            if (_director == null)
            {
                _director = gameObject.AddComponent<RaceCameraDirector>();
            }

            _director.Bind(simulation, cameraRig, _camera);

            _audio = GetComponent<RaceAudio>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<RaceAudio>();
            }

            _audio.Bind(simulation, _camera != null ? _camera.transform : null);
        }

        void Start()
        {
            _state = _simulation as IRaceState;
            if (_state == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = _cameraRig != null ? _cameraRig.GetComponent<Camera>() : Camera.main;
            }

            _state.PhaseChanged += OnPhaseChanged;
            _state.LapCompleted += OnLapCompleted;
            _state.Won += OnWon;
        }

        void OnDestroy()
        {
            if (_state != null)
            {
                _state.PhaseChanged -= OnPhaseChanged;
                _state.LapCompleted -= OnLapCompleted;
                _state.Won -= OnWon;
            }
        }

        void OnPhaseChanged(RacePhase phase)
        {
            _recordedPhase = phase;
            _phaseStartSimTime = _state.SimTime;
        }

        void OnLapCompleted(int racer, int lap)
        {
            if (lap > _recordedLapDone)
            {
                _recordedLapDone = lap;
            }
        }

        void OnWon(int racer) { _wonRacer = racer; }

        void Update()
        {
            if (_state == null)
            {
                return;
            }

            if (!_ready)
            {
                TrySetup();
                if (!_ready)
                {
                    return;
                }
            }

            UpdateBoard();
            UpdateBanners();
            UpdateConfetti();
        }

        void TrySetup()
        {
            int n = _state.RacerCount;
            if (n <= 0)
            {
                return;
            }

            _question = string.Format(questionFormat, _state.Laps);
            if (_caption != null)
            {
                _caption.SetCaption(_question);
                _caption.SetSubline(n + " RACERS  |  " + _state.Laps + " LAPS");
                _caption.SetFooter(string.Empty);
            }

            if (_leaderboard != null)
            {
                _slots = Mathf.Min(boardRows, n, _leaderboard.RowCapacity);
                _leaderboard.Configure(_slots);
                _leaderboard.SetTitle(boardTitle);
                _leaderboard.HideTimer();
                _slotRacer = new int[_slots];
                for (int k = 0; k < _slots; k++)
                {
                    _slotRacer[k] = -1;
                    _leaderboard.SetRank(k, k);
                }
            }

            if (spawnTags)
            {
                SpawnTags(n);
            }

            _recordedPhase = _state.Phase;
            _shownPhase = _recordedPhase;
            _ready = true;
        }

        void SpawnTags(int n)
        {
            var root = new GameObject("RacerTags");
            root.transform.SetParent(transform, false);
            _tags = new RacerTag[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Tag");
                go.transform.SetParent(root.transform, false);
                RacerTag tag = go.AddComponent<RacerTag>();
                tag.Bind(_state, i, _camera);
                _tags[i] = tag;
            }
        }

        // Board -----------------------------------------------------------------------------------------------------

        /// <summary>Row k is rank k+1: when the order changes the row's racer changes, so the board re-sorts without gaps.</summary>
        void UpdateBoard()
        {
            if (_leaderboard == null)
            {
                return;
            }

            int laps = _state.Laps;
            int leader = _state.RacerAtRank(1);
            int leaderLap = leader >= 0 ? _state.LapOf(leader) : 1;
            for (int k = 0; k < _slots; k++)
            {
                int racer = _state.RacerAtRank(k + 1);
                if (racer < 0)
                {
                    continue;
                }

                if (_slotRacer[k] != racer)
                {
                    _slotRacer[k] = racer;
                    _leaderboard.SetContender(k, ColourNameOf(racer), _state.RacerColour(racer));
                    _leaderboard.SetRank(k, k);
                }

                _leaderboard.SetNote(k, NoteFor(racer, k == 0, leaderLap, laps));
            }

            _leaderboard.HideTimer();
        }

        string NoteFor(int racer, bool isLeader, int leaderLap, int laps)
        {
            if (_state.FinishTime(racer) >= 0f)
            {
                return "DONE";
            }

            // Every row shows its own lap out of the total, so no row is ever blank and rows compare directly.
            int lap = Mathf.Clamp(_state.LapOf(racer), 1, laps);
            return "LAP " + lap + "/" + laps;
        }

        // Banners ---------------------------------------------------------------------------------------------------

        void UpdateBanners()
        {
            if (_caption == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            RacePhase phase = _recordedPhase;
            if (phase != _shownPhase)
            {
                _shownPhase = phase;
                OnShownPhase(phase, now);
            }

            if (_shownPhase == RacePhase.Countdown)
            {
                float left = countdownSeconds - (_state.SimTime - _phaseStartSimTime);
                int number = Mathf.Max(1, Mathf.CeilToInt(left));
                if (number != _shownCountdownNumber)
                {
                    _shownCountdownNumber = number;
                    _caption.SetCaption(number.ToString());
                    _caption.SetSubline("GET READY");
                }
            }

            // Lap banners, once per lap, when the first racer completes it. The last lap has its own FINAL LAP banner.
            if (_recordedLapDone > _shownLapDone)
            {
                _shownLapDone = _recordedLapDone;
                int next = _shownLapDone + 1;
                if (next < _state.Laps && _shownPhase == RacePhase.Racing)
                {
                    ShowTimed("LAP " + next, LeaderName() + " LEADS", lapBannerSeconds, now);
                }
            }

            if (_bannerUntil > 0f && now >= _bannerUntil && _shownPhase != RacePhase.Finished)
            {
                _bannerUntil = 0f;
                _caption.SetCaption(_question);
                _caption.SetSubline(string.Empty);
            }

            if (!_winShown && (_wonRacer >= 0 || _state.WinnerIndex >= 0))
            {
                ShowWin(_wonRacer >= 0 ? _wonRacer : _state.WinnerIndex);
            }

            if (_winShown)
            {
                _caption.SetSubline(PodiumText());
            }
        }

        void OnShownPhase(RacePhase phase, float now)
        {
            switch (phase)
            {
                case RacePhase.Countdown:
                    _shownCountdownNumber = -1;
                    break;
                case RacePhase.Racing:
                    ShowTimed("GO!", string.Empty, goBannerSeconds, now);
                    break;
                case RacePhase.FinalLap:
                    ShowTimed("FINAL LAP", LeaderName() + " LEADS", finalLapBannerSeconds, now);
                    break;
            }
        }

        void ShowTimed(string caption, string subline, float seconds, float now)
        {
            _caption.SetCaption(caption);
            _caption.SetSubline(subline);
            _bannerUntil = now + seconds;
        }

        void ShowWin(int racer)
        {
            _winShown = true;
            Color colour = _state.RacerColour(racer);
            _caption.SetCaption(ColourNameOf(racer) + " WINS!");
            _caption.SetSubline(PodiumText());
            _caption.SetFooter(string.Empty);
            _bannerUntil = 0f;
            if (_leaderboard != null)
            {
                _leaderboard.ShowBanner(ColourNameOf(racer), colour, winBannerSeconds, 1f);
            }

            _burstColour = colour;
            _burstsLeft = 3;
            _burstTimer = 0f;
        }

        string PodiumText()
        {
            var sb = new System.Text.StringBuilder(48);
            string[] labels = { "1ST", "2ND", "3RD" };
            int places = Mathf.Min(3, _state.RacerCount);
            for (int p = 0; p < places; p++)
            {
                int racer = _state.RacerAtRank(p + 1);
                if (racer < 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append("   ");
                }

                sb.Append(labels[p]).Append(" ").Append(ColourNameOf(racer));
            }

            return sb.ToString();
        }

        string LeaderName()
        {
            int leader = _state.LeaderIndex;
            return leader >= 0 ? ColourNameOf(leader) : string.Empty;
        }

        static readonly string[] ColourNames =
        {
            "RED", "BLUE", "YELLOW", "GREEN", "ORANGE", "PURPLE", "CYAN", "PINK",
            "BROWN", "WHITE", "BLACK", "LIME", "NAVY", "MAGENTA", "PEACH", "GREY"
        };
        static Color[] _paletteColours;

        /// <summary>The board names racers by colour, not number: the nearest of 16 named hues to the racer's colour.</summary>
        string ColourNameOf(int racer)
        {
            if (_paletteColours == null)
            {
                MachineColour[] defaults = MachineRaceConfig.DefaultRacerColours();
                _paletteColours = new Color[defaults.Length];
                for (int i = 0; i < defaults.Length; i++)
                {
                    _paletteColours[i] = defaults[i].color;
                }
            }

            Color c = _state.RacerColour(racer);
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _paletteColours.Length && i < ColourNames.Length; i++)
            {
                Color p = _paletteColours[i];
                float d = (p.r - c.r) * (p.r - c.r) + (p.g - c.g) * (p.g - c.g) + (p.b - c.b) * (p.b - c.b);
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }

            return ColourNames[best];
        }

        // Confetti --------------------------------------------------------------------------------------------------

        void UpdateConfetti()
        {
            if (_burstsLeft <= 0)
            {
                return;
            }

            _burstTimer -= Time.deltaTime;
            if (_burstTimer > 0f)
            {
                return;
            }

            int winner = _wonRacer >= 0 ? _wonRacer : _state.WinnerIndex;
            if (winner < 0)
            {
                return;
            }

            if (_confetti == null)
            {
                var go = new GameObject("WinConfetti");
                go.transform.SetParent(transform, false);
                _confetti = go.AddComponent<ConfettiBurst3D>();
            }

            // The winner keeps rolling after the line, so aim at where it is now.
            _confetti.Burst(_state.RacerPosition(winner) + Vector3.up * 0.3f, _burstColour, confettiPerBurst);
            _burstsLeft--;
            _burstTimer = 0.35f;
        }
    }
}
