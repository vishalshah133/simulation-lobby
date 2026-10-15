using SimulationLobby.Core;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Race
{
    /// <summary>
    /// Thin binder from the race to the format-agnostic presentation pieces: the leaderboard, the chase
    /// camera and the audio. Reads simulation state in <c>Update</c> and never writes it.
    /// </summary>
    [RequireComponent(typeof(RaceSimulation))]
    public sealed class RacePresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] LeaderboardHud _hud;
        [SerializeField] ChaseCameraRig _camera;
        [SerializeField] MaterialContactAudio _audio;
        [SerializeField] AudioSource _rollSource;

        [Header("Text")]
        public string title = "GRAND PRIX";

        [Header("Camera")]
        [Tooltip("The chase camera only changes target after the new leader has held the front this long. Stops it flicking between two marbles that are swapping places.")]
        [Min(0f)] public float leaderHoldSeconds = 0.9f;

        [Min(0f)] public float finishCameraSeconds = 5f;

        [Header("Audio")]
        [Range(0f, 1f)] public float rollVolume = 0.22f;
        [Min(1)] public int maxClacksPerFrame = 3;
        [Min(0.1f)] public float minClackSpeed = 0.9f;

        RaceSimulation _simulation;
        RaceEntry _cameraTarget;
        RaceEntry _candidate;
        float _candidateSince;
        int _lastCountdown = -1;
        bool _finishCameraActive;
        float _finishCameraUntil;
        bool _winnerShown;
        bool _complete;

        static readonly Color Ivory = new Color(0.95f, 0.93f, 0.89f, 1f);
        static readonly Color Brass = new Color(0.93f, 0.71f, 0.38f, 1f);

        public void Bind(LeaderboardHud hud, ChaseCameraRig camera, MaterialContactAudio audio, AudioSource rollSource)
        {
            _hud = hud;
            _camera = camera;
            _audio = audio;
            _rollSource = rollSource;
        }

        void Start()
        {
            _simulation = GetComponent<RaceSimulation>();
            var entries = _simulation.Entries;
            if (entries.Count == 0)
            {
                Debug.LogError("[race] The simulation has no marbles. Check the config and the SimulationRunner.", this);
                enabled = false;
                return;
            }

            _hud.Configure(entries.Count);
            _hud.SetTitle(title);
            for (int i = 0; i < entries.Count; i++)
            {
                var identity = entries[i].identity;
                _hud.SetContender(identity.Index, identity.DisplayName, identity.Color);
            }

            // Synthesising clips on first use would stutter the first clack, so make them up front.
            var degrees = new int[10];
            for (int i = 0; i < degrees.Length; i++)
            {
                degrees[i] = i;
            }

            _audio.Prewarm(new[] { 0f }, 1.2f, degrees);

            if (_rollSource != null)
            {
                _rollSource.clip = ProceduralToneBank.CreateRumbleLoop(2f, 11, "MarbleRoll");
                _rollSource.loop = true;
                _rollSource.volume = 0f;
                _rollSource.spatialBlend = 0f;
                _rollSource.Play();
            }

            _camera.Orbit(_simulation.StartOrigin.position + Vector3.up * 0.4f - _simulation.StartOrigin.forward * 0.5f);
            _camera.Snap();

            _simulation.Released += OnReleased;
            _simulation.MarbleFinished += OnMarbleFinished;
            _simulation.LeaderChanged += OnLeaderChanged;
        }

        void OnDestroy()
        {
            if (_simulation != null)
            {
                _simulation.Released -= OnReleased;
                _simulation.MarbleFinished -= OnMarbleFinished;
                _simulation.LeaderChanged -= OnLeaderChanged;
            }
        }

        void Update()
        {
            if (_simulation == null || !enabled)
            {
                return;
            }

            var entries = _simulation.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                _hud.SetRank(entries[i].identity.Index, entries[i].rank);
            }

            switch (_simulation.Phase)
            {
                case RacePhase.Hold:
                    UpdateCountdown();
                    break;

                case RacePhase.Racing:
                    _hud.SetTimer(FormatTime(_simulation.ElapsedSeconds));
                    UpdateCamera();
                    UpdateAudio(entries);
                    break;

                case RacePhase.Complete:
                    if (!_complete)
                    {
                        _complete = true;
                        FinishUp();
                    }

                    UpdateAudio(entries);
                    break;
            }
        }

        // ------------------------------------------------------------------ beats

        void UpdateCountdown()
        {
            _hud.SetTimer(FormatTime(0f));
            int remaining = Mathf.CeilToInt(_simulation.SecondsUntilRelease);
            if (remaining <= 3 && remaining >= 1 && remaining != _lastCountdown)
            {
                _lastCountdown = remaining;
                _hud.ShowBanner(remaining.ToString(), Ivory, 0.85f, 0.25f);
                _audio.PlayNote(remaining == 1 ? 4 : 2);
            }
        }

        void OnReleased()
        {
            _hud.ShowBanner("GO", Brass, 0.9f, 0.4f);
            _audio.PlayWhoosh(0.9f);
            _audio.PlayNote(7);
            _cameraTarget = _simulation.Leader;
            if (_cameraTarget != null)
            {
                _camera.Chase(_cameraTarget.transform);
            }
        }

        void OnLeaderChanged(RaceEntry leader)
        {
            _audio.PlayNote(5);
        }

        void OnMarbleFinished(RaceEntry entry, int place)
        {
            _hud.SetNote(entry.identity.Index, FormatTime(entry.finishTime));
            _audio.PlayNote(Mathf.Max(0, 9 - place));

            if (place == 1)
            {
                // The winner: hold a wide frame on the line, and name them.
                Transform line = _simulation.FinishLine;
                Vector3 position = line.position - line.forward * 7f + line.right * 5f + Vector3.up * 3f;
                _camera.Hold(position, line.position + Vector3.up * 0.6f);
                _finishCameraActive = true;
                _finishCameraUntil = Time.time + finishCameraSeconds;
                _hud.ShowBanner(entry.identity.DisplayName + " WINS", entry.identity.Color, 0f);
                _winnerShown = true;
            }
        }

        void FinishUp()
        {
            if (!_winnerShown && _simulation.Leader != null)
            {
                _hud.ShowBanner(_simulation.Leader.identity.DisplayName + " LEADS", Ivory, 0f);
            }

            _hud.SetTimer(FormatTime(_simulation.Result != null ? _simulation.Result.GetMetric("winner_time_s") : 0f));
        }

        // ------------------------------------------------------------------ camera + audio

        void UpdateCamera()
        {
            if (_finishCameraActive)
            {
                if (Time.time < _finishCameraUntil)
                {
                    return;
                }

                // After the winner moment, go back to following the front of whoever is still racing.
                _finishCameraActive = false;
                _cameraTarget = null;
            }

            RaceEntry leader = FrontRunner();
            if (leader == null)
            {
                return;
            }

            if (_cameraTarget == null)
            {
                _cameraTarget = leader;
                _camera.Chase(leader.transform);
                return;
            }

            if (leader == _cameraTarget)
            {
                _candidate = null;
                return;
            }

            if (_candidate != leader)
            {
                _candidate = leader;
                _candidateSince = Time.time;
            }
            else if (Time.time - _candidateSince >= leaderHoldSeconds)
            {
                _cameraTarget = leader;
                _camera.Chase(leader.transform);
            }
        }

        /// <summary>The marble furthest along that has not yet crossed the line.</summary>
        RaceEntry FrontRunner()
        {
            RaceEntry best = null;
            var entries = _simulation.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                RaceEntry entry = entries[i];
                if (entry.finished)
                {
                    continue;
                }

                if (best == null || entry.progress > best.progress)
                {
                    best = entry;
                }
            }

            return best ?? _simulation.Leader;
        }

        void UpdateAudio(System.Collections.Generic.IReadOnlyList<RaceEntry> entries)
        {
            int clacks = 0;
            float speedSum = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                RaceEntry entry = entries[i];
                speedSum += entry.body.linearVelocity.magnitude;
                if (entry.detector == null)
                {
                    continue;
                }

                int count = entry.detector.ConsumeCollisionCount();
                if (count > 0 && clacks < maxClacksPerFrame && entry.detector.LastImpactSpeed >= minClackSpeed)
                {
                    _audio.PlayContact(entry.detector.LastImpactSpeed * 0.5f, 0f);
                    clacks++;
                }
            }

            if (_rollSource != null)
            {
                float mean = entries.Count > 0 ? speedSum / entries.Count : 0f;
                float target = _simulation.Phase == RacePhase.Racing ? Mathf.Clamp01(mean / 7f) * rollVolume : 0f;
                _rollSource.volume = Mathf.MoveTowards(_rollSource.volume, target, Time.deltaTime * 0.5f);
                _rollSource.pitch = Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(mean / 8f));
            }
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes}:{rest:00.0}";
        }
    }
}
