using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Thin binder from the marble machine to the format-agnostic presentation pieces: the tube leaderboard, the
    /// caption, the tour camera, the sound and the win confetti. Reads simulation state in <c>Update</c> and never
    /// writes it. Events fire in <c>FixedUpdate</c>, so they are only recorded and acted on in <c>Update</c>.
    /// </summary>
    public sealed class MachinePresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] MachineSimulation _simulation;
        [SerializeField] LeaderboardHud _leaderboard;
        [SerializeField] CaptionHud _caption;
        [SerializeField] TourCameraRig _cameraRig;
        [SerializeField] MachineCameraDirector _director;
        [SerializeField] MachineAudio _audio;

        [Header("Text")]
        public string question = "WHICH COLOUR FILLS ITS TUBE FIRST?";
        public string boardTitle = "TUBES";

        [Header("Banners")]
        [Min(0.5f)] public float floodBannerSeconds = 4f;
        [Min(0.5f)] public float finaleBannerSeconds = 4f;
        [Min(0.5f)] public float winBannerSeconds = 14f;
        [Min(0)] public int confettiPerBurst = 220;

        static readonly Color FloodColour = new Color(1f, 0.62f, 0.12f, 1f);
        static readonly Color FinaleColour = new Color(1f, 0.92f, 0.45f, 1f);

        IMachineState _state;
        bool _boardReady;
        int[] _rank;
        int _lastSecond = -1;

        MachinePhase _recordedPhase = MachinePhase.Hold;
        MachinePhase _shownPhase = MachinePhase.Hold;
        int _wonColour = -1;
        bool _winShown;
        int _burstsLeft;
        float _burstTimer;
        Vector3 _burstPosition;
        Color _burstColour;
        ConfettiBurst3D _confetti;

        /// <summary>Edit-time wiring from the scene builder. Adds or finds the director and the audio on this object.</summary>
        public void Bind(MachineSimulation simulation, LeaderboardHud leaderboard, CaptionHud caption, TourCameraRig cameraRig)
        {
            _simulation = simulation;
            _leaderboard = leaderboard;
            _caption = caption;
            _cameraRig = cameraRig;

            _director = GetComponent<MachineCameraDirector>();
            if (_director == null)
            {
                _director = gameObject.AddComponent<MachineCameraDirector>();
            }

            Camera camera = cameraRig != null ? cameraRig.GetComponent<Camera>() : null;
            _director.Bind(simulation, cameraRig, camera);

            _audio = GetComponent<MachineAudio>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<MachineAudio>();
            }

            _audio.Bind(simulation, camera != null ? camera.transform : null);
        }

        void Start()
        {
            _state = _simulation as IMachineState;
            if (_state == null)
            {
                return;
            }

            if (_caption != null)
            {
                _caption.SetCaption(question);
            }

            _state.PhaseChanged += OnPhaseChanged;
            _state.Won += OnWon;
        }

        void OnDestroy()
        {
            if (_state != null)
            {
                _state.PhaseChanged -= OnPhaseChanged;
                _state.Won -= OnWon;
            }
        }

        void OnPhaseChanged(MachinePhase phase) { _recordedPhase = phase; }
        void OnWon(int colour) { _wonColour = colour; }

        void Update()
        {
            if (_state == null || _leaderboard == null)
            {
                return;
            }

            if (!_boardReady)
            {
                SetupBoard();
                if (!_boardReady)
                {
                    return;
                }
            }

            UpdateBoard();
            UpdateBanners();
            UpdateConfetti();
        }

        void SetupBoard()
        {
            int n = _state.ColourCount;
            if (n <= 0)
            {
                return;
            }

            _leaderboard.Configure(n);
            _leaderboard.SetTitle(boardTitle);
            _rank = new int[n];
            for (int i = 0; i < n; i++)
            {
                _leaderboard.SetContender(i, _state.ColourName(i).ToUpperInvariant(), _state.ColourValue(i));
                _leaderboard.SetRank(i, i);
            }

            _boardReady = true;
        }

        void UpdateBoard()
        {
            int n = _rank.Length;
            int capacity = _state.TubeCapacity;
            for (int i = 0; i < n; i++)
            {
                int ci = _state.TubeCount(i);
                int rank = 0;
                for (int j = 0; j < n; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    int cj = _state.TubeCount(j);
                    if (cj > ci || (cj == ci && j < i))
                    {
                        rank++;
                    }
                }

                if (rank != _rank[i])
                {
                    _rank[i] = rank;
                    _leaderboard.SetRank(i, rank);
                }

                _leaderboard.SetNote(i, ci + " / " + capacity);
            }

            // No clock on this HUD.
            _leaderboard.HideTimer();
        }

        void UpdateBanners()
        {
            if (_recordedPhase != _shownPhase)
            {
                _shownPhase = _recordedPhase;
                if (_shownPhase == MachinePhase.Flood)
                {
                    _leaderboard.ShowBanner("FLOOD GATE OPEN!", FloodColour, floodBannerSeconds);
                }
                else if (_shownPhase == MachinePhase.Finale)
                {
                    _leaderboard.ShowBanner("FINAL STRETCH", FinaleColour, finaleBannerSeconds);
                }
            }

            if (_wonColour >= 0 && !_winShown)
            {
                _winShown = true;
                Color colour = _state.ColourValue(_wonColour);
                _leaderboard.ShowBanner(_state.ColourName(_wonColour).ToUpperInvariant() + " WINS!", colour, winBannerSeconds, 1f);

                _burstColour = colour;
                _burstPosition = _state.TubeTop(_wonColour);
                _burstsLeft = 3;
                _burstTimer = 0f;
            }
        }

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

            if (_confetti == null)
            {
                var go = new GameObject("WinConfetti");
                go.transform.SetParent(transform, false);
                _confetti = go.AddComponent<ConfettiBurst3D>();
            }

            _confetti.Burst(_burstPosition, _burstColour, confettiPerBurst);
            _burstsLeft--;
            _burstTimer = 0.35f;
        }
    }
}
