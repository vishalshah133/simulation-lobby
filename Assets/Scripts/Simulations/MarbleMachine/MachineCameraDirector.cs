using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Drives the <see cref="TourCameraRig"/> through the mach-001 storyboard: wide opening, a first lap station by
    /// station, a shuffled tour with tube check-ins, the flood, the finale dolly and the winner push-in. Never cuts.
    /// Reads <see cref="IMachineState"/> in <c>Update</c> (events are only recorded), timed on sim time.
    /// </summary>
    public sealed class MachineCameraDirector : MonoBehaviour
    {
        enum Mode
        {
            Opening,
            Act1,
            Tour,
            Scripted,
            Finale,
            Done
        }

        struct Shot
        {
            public Vector3 position;
            public Vector3 look;
            public float glide;
            public float dwell;
        }

        [Header("Wiring")]
        [SerializeField] MachineSimulation _simulation;
        [SerializeField] TourCameraRig _rig;
        [SerializeField] Camera _camera;

        [Header("Opening and Act 1")]
        [Min(1f)] public float openingHoldSeconds = 6f;
        [Min(0.5f)] public float minStationDwell = 3.5f;
        [Min(0.5f)] public float afterSorterSeconds = 14f;

        [Header("Tour")]
        public Vector2 glideSeconds = new Vector2(2.5f, 4f);
        public Vector2 checkInEvery = new Vector2(45f, 60f);
        [Min(1f)] public float checkInDwell = 6f;
        [Min(1f)] public float leaderCheckInGap = 20f;
        [Min(1)] public int tourSeed = 77;

        [Header("Finale and win")]
        [Min(5f)] public float finaleDollySeconds = 40f;
        [Min(1f)] public float winnerDwell = 5f;
        [Min(1f)] public float outroGlideSeconds = 9f;

        [Header("Framing")]
        [Tooltip("Opening: how far the camera pushes in over the hold (1 = no push).")]
        [Range(0.6f, 1f)] public float openingPushIn = 0.86f;
        [Tooltip("Opening: if framing silo + tubes needs more than this multiple of the tubes-only distance, drop the silo to the edge of frame.")]
        [Min(1f)] public float openingMaxStretch = 1.9f;
        [Tooltip("Station shots never sit farther than this from their focus, so small balls stay readable.")]
        [Min(2f)] public float maxStationDistance = 9f;
        [Tooltip("Flood and mid-run wides frame this many stations from the silo, 3/4 elevated.")]
        [Min(1)] public int midWideStations = 3;

        IMachineState _state;
        System.Random _rng;
        Mode _mode = Mode.Opening;
        bool _started;
        bool _won;

        Vector3 _widePos;
        Vector3 _wideLook;
        Vector3 _openPos;
        Vector3 _openLook;
        Vector3 _openEndPos;
        Vector3 _openEndLook;

        // Recorded from events (FixedUpdate), consumed in Update.
        bool _leaderChanged;
        bool _firstSortSeen;
        int _wonColour = -1;

        // Act 1.
        int _act1Index = 1;
        int _sorterIndex = -1;
        int _tubeRackIndex = -1;
        float _lastGlideTime;
        float _currentDwell;
        float _sorterGlideTime = -1f;

        // Tour.
        readonly List<int> _bag = new List<int>();
        float _shotEnd;
        float _lastCheckIn;
        float _nextCheckIn;
        MachinePhase _lastHandledPhase = MachinePhase.Hold;

        // Scripted shots (flood, win).
        readonly List<Shot> _queue = new List<Shot>();
        int _queueIndex;
        float _queueShotEnd;
        Mode _afterQueue = Mode.Tour;

        // Finale dolly.
        float _dollyEnd = -1f;
        bool _dollyForward = true;

        public void Bind(MachineSimulation simulation, TourCameraRig rig, Camera camera)
        {
            _simulation = simulation;
            _rig = rig;
            _camera = camera;
        }

        void Start()
        {
            _state = _simulation as IMachineState;
            if (_camera == null && _rig != null)
            {
                _camera = _rig.GetComponent<Camera>();
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_rig != null && _camera != null)
            {
                _rig.Bind(_camera);
            }

            _rng = new System.Random(tourSeed);
            if (_state != null)
            {
                _state.LeaderChanged += OnLeaderChanged;
                _state.BallSorted += OnBallSorted;
                _state.Won += OnWon;
            }
        }

        void OnDestroy()
        {
            if (_state != null)
            {
                _state.LeaderChanged -= OnLeaderChanged;
                _state.BallSorted -= OnBallSorted;
                _state.Won -= OnWon;
            }
        }

        void OnLeaderChanged(int colour) { _leaderChanged = true; }
        void OnBallSorted(int colour, Vector3 position) { _firstSortSeen = true; }
        void OnWon(int colour) { _wonColour = colour; }

        void Update()
        {
            if (_state == null || _rig == null || _camera == null)
            {
                return;
            }

            IReadOnlyList<MachineStation> stations = _state.Stations;
            if (stations == null || stations.Count == 0)
            {
                return;
            }

            float now = _state.SimTime;
            if (!_started)
            {
                Begin(stations, now);
            }

            HandlePhase(now);

            switch (_mode)
            {
                case Mode.Opening:
                    if (now >= openingHoldSeconds)
                    {
                        GlideToValve(stations[0], now);
                        _mode = stations.Count > 1 ? Mode.Act1 : Mode.Tour;
                    }

                    break;
                case Mode.Act1:
                    UpdateAct1(stations, now);
                    break;
                case Mode.Tour:
                    UpdateTour(stations, now);
                    break;
                case Mode.Scripted:
                    UpdateQueue(now);
                    break;
                case Mode.Finale:
                    if (now >= _dollyEnd)
                    {
                        StartDolly(now);
                    }

                    break;
            }

            _leaderChanged = false;
        }

        void Begin(IReadOnlyList<MachineStation> stations, float now)
        {
            _started = true;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] is ISorter && _sorterIndex < 0)
                {
                    _sorterIndex = i;
                }

                if (stations[i] is ITubeRack)
                {
                    _tubeRackIndex = i;
                }
            }

            ComputeWidePose(stations);
            ComputeOpeningPose(stations);
            _rig.SnapTo(_openPos, _openLook);
            _rig.GlideTo(_openEndPos, _openEndLook, openingHoldSeconds);
            _rig.SetDrift(0.15f, 0.2f);

            if (_sorterIndex < 0)
            {
                _sorterIndex = Mathf.Max(0, stations.Count - 1);
            }

            _lastCheckIn = now;
            _nextCheckIn = now + Range(checkInEvery);
            _lastHandledPhase = _state.Phase;
        }

        // Phase overrides -------------------------------------------------------------------------------------------

        void HandlePhase(float now)
        {
            MachinePhase phase = _state.Phase;
            if (_won)
            {
                return;
            }

            if (_wonColour >= 0 || phase == MachinePhase.Won)
            {
                int winner = _wonColour >= 0 ? _wonColour : _state.WinnerIndex;
                if (winner >= 0)
                {
                    StartWin(winner, now);
                }

                return;
            }

            if (phase == _lastHandledPhase)
            {
                return;
            }

            _lastHandledPhase = phase;
            if (phase == MachinePhase.Flood)
            {
                StartFlood(now);
            }
            else if (phase == MachinePhase.Finale)
            {
                StartFinale(now);
            }
        }

        void StartFlood(float now)
        {
            if (_mode == Mode.Finale)
            {
                return;
            }

            IReadOnlyList<MachineStation> stations = _state.Stations;
            _queue.Clear();

            Bounds half = default;
            bool any = false;
            int count = Mathf.Min(midWideStations, stations.Count);
            for (int i = 0; i < count; i++)
            {
                Bounds b = stations[i].activityBounds;
                if (b.size.sqrMagnitude < 1e-4f)
                {
                    continue;
                }

                if (!any)
                {
                    half = b;
                    any = true;
                }
                else
                {
                    half.Encapsulate(b);
                }
            }

            if (any)
            {
                ThreeQuarterPose(half, 1.0f, 0.1f, out Vector3 floodPos, out Vector3 floodLook);
                _queue.Add(new Shot { position = floodPos, look = floodLook, glide = 3f, dwell = 6f });
            }

            MachineStation bucket = FindStation("Bucket", 4);
            MachineStation ski = FindStation("Ski", 5);
            AddStationShot(bucket);
            if (ski != bucket)
            {
                AddStationShot(ski);
            }

            StartQueue(Mode.Tour, now);
        }

        void StartFinale(float now)
        {
            _mode = Mode.Finale;
            _rig.SetLookTracking(null);
            _rig.SetDrift(0.12f, 0.15f);
            _dollyEnd = -1f;
            _dollyForward = true;
            StartDolly(now);
        }

        void StartDolly(float now)
        {
            TubeFrame(out Vector3 center, out Vector3 right, out Vector3 back, out float width, out float dist);
            float reach = width * 0.22f;
            Vector3 left = center - right * reach;
            Vector3 rightEnd = center + right * reach;
            Vector3 lift = Vector3.up;

            if (_dollyEnd < 0f)
            {
                // First leg: from wherever we are to the left end of the row.
                _rig.GlideTo(left + back * dist + lift, left, 3f);
                _dollyEnd = now + 3f;
                return;
            }

            Vector3 target = _dollyForward ? rightEnd : left;
            _rig.GlideTo(target + back * dist + lift, target, finaleDollySeconds);
            _dollyEnd = now + finaleDollySeconds;
            _dollyForward = !_dollyForward;
        }

        void StartWin(int winner, float now)
        {
            _won = true;
            _rig.SetLookTracking(null);
            _rig.SetDrift(0.08f, 0.2f);

            TubeFrame(out Vector3 center, out Vector3 right, out Vector3 back, out float width, out float dist);
            Vector3 fill = _state.TubeFillLine(winner);
            _queue.Clear();
            _queue.Add(new Shot
            {
                position = fill + back * 3.2f + Vector3.up * 0.9f,
                look = fill,
                glide = 2.5f,
                dwell = winnerDwell
            });
            _queue.Add(new Shot
            {
                position = _widePos,
                look = _wideLook,
                glide = outroGlideSeconds,
                dwell = outroGlideSeconds
            });
            StartQueue(Mode.Done, now);
        }

        // Scripted queue --------------------------------------------------------------------------------------------

        void StartQueue(Mode after, float now)
        {
            _afterQueue = after;
            _queueIndex = 0;
            _mode = Mode.Scripted;
            PlayQueueShot(now);
        }

        void PlayQueueShot(float now)
        {
            Shot shot = _queue[_queueIndex];
            _rig.GlideTo(shot.position, shot.look, shot.glide);
            _queueShotEnd = now + shot.dwell;
        }

        void UpdateQueue(float now)
        {
            if (now < _queueShotEnd)
            {
                return;
            }

            _queueIndex++;
            if (_queueIndex < _queue.Count)
            {
                PlayQueueShot(now);
                return;
            }

            _mode = _afterQueue;
            _shotEnd = now;
        }

        // Act 1 -----------------------------------------------------------------------------------------------------

        void UpdateAct1(IReadOnlyList<MachineStation> stations, float now)
        {
            if (_act1Index <= _sorterIndex)
            {
                MachineStation next = stations[_act1Index];
                float since = now - _lastGlideTime;
                bool timeout = since >= _currentDwell;
                bool entered = since >= minStationDwell && BallInside(next.activityBounds);
                if (entered || timeout)
                {
                    GlideToStation(next, now);
                    if (_act1Index == _sorterIndex)
                    {
                        _sorterGlideTime = now;
                    }

                    _act1Index++;
                }

                return;
            }

            // Past the sorter: wait for the first sort (or give up), then show the tubes and hand over to the tour.
            if (_sorterGlideTime < 0f)
            {
                _sorterGlideTime = now;
            }

            bool sorted = _firstSortSeen && now - _lastGlideTime >= minStationDwell;
            if (sorted || now - _sorterGlideTime >= afterSorterSeconds)
            {
                GlideToTubes(now);
                _shotEnd = now + checkInDwell;
                _lastCheckIn = now;
                _nextCheckIn = now + Range(checkInEvery);
                _mode = Mode.Tour;
            }
        }

        bool BallInside(Bounds bounds)
        {
            IReadOnlyList<MachineBall> balls = _state.Balls;
            if (balls == null)
            {
                return false;
            }

            for (int i = 0; i < balls.Count; i++)
            {
                MachineBall ball = balls[i];
                if (ball != null && !ball.counted && bounds.Contains(ball.transform.position))
                {
                    return true;
                }
            }

            return false;
        }

        // Tour ------------------------------------------------------------------------------------------------------

        void UpdateTour(IReadOnlyList<MachineStation> stations, float now)
        {
            bool leaderWants = _leaderChanged && now - _lastCheckIn >= leaderCheckInGap && _state.LeaderIndex >= 0;
            if (!leaderWants && now < _shotEnd)
            {
                return;
            }

            if (leaderWants || now >= _nextCheckIn)
            {
                GlideToTubes(now);
                _shotEnd = now + checkInDwell;
                _lastCheckIn = now;
                _nextCheckIn = now + Range(checkInEvery);
                return;
            }

            if (_bag.Count == 0)
            {
                for (int i = 0; i < stations.Count; i++)
                {
                    if (i != _tubeRackIndex)
                    {
                        _bag.Add(i);
                    }
                }

                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    int tmp = _bag[i];
                    _bag[i] = _bag[j];
                    _bag[j] = tmp;
                }
            }

            if (_bag.Count == 0)
            {
                return;
            }

            int pick = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            MachineStation station = stations[pick];
            GlideToStation(station, now);
            _shotEnd = now + station.tourSeconds;
        }

        // Poses -----------------------------------------------------------------------------------------------------

        void GlideToStation(MachineStation station, float now)
        {
            Vector3 focus = station.cameraFocus != null ? station.cameraFocus.position : station.activityBounds.center;
            _rig.GlideTo(focus + LimitOffset(station.cameraOffset), focus, Range(glideSeconds));
            _rig.SetDrift(0.2f, 0.25f);
            _lastGlideTime = now;
            _currentDwell = station.tourSeconds;
        }

        /// <summary>Valve opening: glide in on the outlet / star feeder so the first balls dropping are big on screen.</summary>
        void GlideToValve(MachineStation station, float now)
        {
            Vector3 focus = station.exit != null ? station.exit.position
                : station.cameraFocus != null ? station.cameraFocus.position : station.activityBounds.center;
            Vector3 offset = LimitOffset(station.cameraOffset);
            float dist = Mathf.Clamp(offset.magnitude, 2f, 4.5f);
            Vector3 dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : new Vector3(0f, 0.2f, -1f);
            _rig.GlideTo(focus + dir * dist, focus, 3f);
            _rig.SetDrift(0.12f, 0.25f);
            _lastGlideTime = now;
            _currentDwell = station.tourSeconds;
        }

        /// <summary>Caps a station camera offset so balls stay large enough on screen.</summary>
        Vector3 LimitOffset(Vector3 offset)
        {
            float m = offset.magnitude;
            return m > maxStationDistance ? offset * (maxStationDistance / m) : offset;
        }

        void AddStationShot(MachineStation station)
        {
            Vector3 focus = station.cameraFocus != null ? station.cameraFocus.position : station.activityBounds.center;
            _queue.Add(new Shot
            {
                position = focus + LimitOffset(station.cameraOffset),
                look = focus,
                glide = Range(glideSeconds),
                dwell = Mathf.Max(5f, station.tourSeconds * 0.6f)
            });
        }

        MachineStation FindStation(string nameFragment, int fallbackIndex)
        {
            IReadOnlyList<MachineStation> stations = _state.Stations;
            for (int i = 0; i < stations.Count; i++)
            {
                string n = stations[i].displayName;
                if (!string.IsNullOrEmpty(n) && n.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return stations[i];
                }
            }

            return stations[Mathf.Clamp(fallbackIndex, 0, stations.Count - 1)];
        }

        void GlideToTubes(float now)
        {
            TubeFrame(out Vector3 center, out Vector3 right, out Vector3 back, out float width, out float dist);
            _rig.GlideTo(center + back * dist + Vector3.up, center, Range(glideSeconds));
            _rig.SetDrift(0.15f, 0.2f);
            _lastGlideTime = now;
            _currentDwell = checkInDwell;
        }

        /// <summary>Frame of the tube row: centre, row axis, the side facing -Z, width and the distance that fits it.</summary>
        void TubeFrame(out Vector3 center, out Vector3 right, out Vector3 back, out float width, out float dist)
        {
            int n = Mathf.Max(1, _state.ColourCount);
            Bounds b = new Bounds(_state.TubeTop(0), Vector3.zero);
            for (int i = 0; i < n; i++)
            {
                b.Encapsulate(_state.TubeTop(i));
                b.Encapsulate(_state.TubeFillLine(i));
            }

            center = b.center;
            Vector3 row = _state.TubeTop(n - 1) - _state.TubeTop(0);
            row.y = 0f;
            right = row.sqrMagnitude > 1e-4f ? row.normalized : Vector3.right;
            back = Vector3.Cross(right, Vector3.up);
            if (back.z > 0f)
            {
                back = -back;
            }

            width = Mathf.Max(row.magnitude, b.size.x) + 1.0f;
            dist = FitDistance(width, Mathf.Max(b.size.y, 1f) + 0.6f, 1.04f) + 0.5f;
        }

        void ComputeWidePose(IReadOnlyList<MachineStation> stations)
        {
            Bounds all = default;
            bool any = false;
            for (int i = 0; i < stations.Count; i++)
            {
                Bounds b = stations[i].activityBounds;
                if (b.size.sqrMagnitude < 1e-4f)
                {
                    continue;
                }

                if (!any)
                {
                    all = b;
                    any = true;
                }
                else
                {
                    all.Encapsulate(b);
                }
            }

            if (!any)
            {
                all = new Bounds(Vector3.up * 3f, new Vector3(30f, 8f, 20f));
            }

            ThreeQuarterPose(all, 1.0f, 0.1f, out _widePos, out _wideLook);
        }

        /// <summary>
        /// Elevated 3/4 view (25 degrees off the -Z axis, 24 degrees up) that fits <paramref name="region"/>.
        /// </summary>
        void ThreeQuarterPose(Bounds region, float margin, float lookDrop, out Vector3 position, out Vector3 look)
        {
            const float azimuth = 25f * Mathf.Deg2Rad;
            const float elevation = 24f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), -Mathf.Cos(azimuth) * Mathf.Cos(elevation));
            float width = region.size.x * Mathf.Cos(azimuth) + region.size.z * Mathf.Sin(azimuth);
            float height = region.size.y * Mathf.Cos(elevation) + region.size.z * Mathf.Sin(elevation);
            float fit = FitDistance(width, height, margin) + region.extents.z * 0.5f;
            look = region.center + new Vector3(0f, -region.extents.y * lookDrop, 0f);
            position = region.center + dir * fit;
        }

        /// <summary>
        /// Opening hero shot: the seven tubes front and centre with the silo in frame behind or beside them. Frames
        /// the union of both; if that needs a much longer lens than the tubes alone, frames the tubes with the silo
        /// off to one side. Ends slightly closer than it starts (slow push-in over the hold).
        /// </summary>
        void ComputeOpeningPose(IReadOnlyList<MachineStation> stations)
        {
            TubeFrame(out Vector3 tubeCenter, out Vector3 right, out Vector3 back, out float tubeWidth, out float tubeDist);
            Bounds tubes = new Bounds(_state.TubeTop(0), Vector3.zero);
            for (int i = 0; i < _state.ColourCount; i++)
            {
                tubes.Encapsulate(_state.TubeTop(i));
                tubes.Encapsulate(_state.TubeFillLine(i));
            }

            if (_tubeRackIndex >= 0 && stations[_tubeRackIndex].activityBounds.size.sqrMagnitude > 1e-4f)
            {
                tubes.Encapsulate(stations[_tubeRackIndex].activityBounds);
            }

            Vector3 lift = Vector3.up * Mathf.Max(1.5f, tubes.size.y * 0.35f);
            Vector3 look = tubes.center;
            float dist = tubeDist;

            if (stations[0] is ISilo silo && silo.StorageBounds.size.sqrMagnitude > 1e-4f)
            {
                Bounds all = tubes;
                all.Encapsulate(silo.StorageBounds);
                float width = Mathf.Abs(right.x) * all.size.x + Mathf.Abs(right.z) * all.size.z + 1.5f;
                float unionDist = FitDistance(width, all.size.y + 1.0f, 1.04f) + 0.5f;
                if (unionDist <= tubeDist * openingMaxStretch)
                {
                    look = all.center;
                    dist = unionDist;
                    lift = Vector3.up * Mathf.Max(1.5f, all.size.y * 0.2f);
                }
                else
                {
                    // Too far apart: tubes stay the subject, the silo creeps in from its side of the frame.
                    look = Vector3.Lerp(tubes.center, silo.StorageBounds.center, 0.3f);
                    dist = tubeDist * 1.2f;
                }
            }

            _openPos = look + back * dist + lift;
            _openLook = look;
            _openEndPos = look + back * dist * openingPushIn + lift * openingPushIn;
            _openEndLook = look;
        }

        /// <summary>Distance at which a width x height rectangle fits the frame.</summary>
        float FitDistance(float width, float height, float margin)
        {
            float vHalf = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = _camera.aspect > 0.1f ? _camera.aspect : 16f / 9f;
            float byWidth = width * 0.5f / (vHalf * aspect);
            float byHeight = height * 0.5f / vHalf;
            return Mathf.Max(byWidth, byHeight) * margin;
        }

        float Range(Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)_rng.NextDouble());
        }
    }
}
