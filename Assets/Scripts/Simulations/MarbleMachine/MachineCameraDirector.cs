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
            GateWatch,
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

        [Header("Opening")]
        [Tooltip("Length of the whole opening sequence (zoom out to the full track, zoom in on the silo, orbit it). The gate opens when it ends, so it must equal MachineConfig.holdSeconds.")]
        [Min(8f)] public float openingSequenceSeconds = 20f;
        [Tooltip("Pull back from the opening hero shot to a view of the entire track.")]
        [Min(1f)] public float openingZoomOutSeconds = 5f;
        [Min(0f)] public float openingWideHoldSeconds = 2f;
        [Tooltip("Glide from the wide view in to the start of the silo orbit.")]
        [Min(0.5f)] public float openingGlideInSeconds = 2.5f;

        [Header("Gate shot")]
        [Tooltip("The camera reaches the front of the gate this long before it opens.")]
        [Min(0.5f)] public float gateLeadSeconds = 3f;
        [Tooltip("How long the camera stays on the gate after it opens, before it starts following the lead ball.")]
        [Min(1f)] public float gateWatchSeconds = 6f;
        [Tooltip("Camera offset from the gate opening in the silo's frame: x to the side, y up, z out in front of the gate.")]
        public Vector3 gateCameraOffset = new Vector3(3.2f, 2.6f, 6.5f);

        [Header("Tour")]
        [Tooltip("Seconds spent on each station, in flow order.")]
        public Vector2 stationDwellSeconds = new Vector2(5f, 10f);
        public Vector2 glideSeconds = new Vector2(2.5f, 4f);
        public Vector2 checkInEvery = new Vector2(45f, 60f);
        [Min(1f)] public float checkInDwell = 6f;
        [Min(1f)] public float leaderCheckInGap = 20f;
        [Min(1)] public int tourSeed = 77;
        [Tooltip("A tour stop is only entered once its station holds this many balls (random in the range, drawn once per stop).")]
        public Vector2 minBallsToVisit = new Vector2(100f, 150f);
        [Tooltip("Safety: if the station never reaches the threshold, move on after this many sim seconds of waiting.")]
        [Min(5f)] public float maxWaitSeconds = 60f;

        [Header("Finale and win")]
        [Min(5f)] public float finaleDollySeconds = 40f;
        [Min(1f)] public float winnerDwell = 5f;
        [Min(1f)] public float outroGlideSeconds = 9f;

        [Header("Framing")]
        [Tooltip("Opening: how far the camera pushes in over the hold (1 = no push).")]
        [Range(0.6f, 1f)] public float openingPushIn = 0.93f;
        [Tooltip("Opening: if framing silo + tubes needs more than this multiple of the tubes-only distance, drop the silo to the edge of frame.")]
        [Min(1f)] public float openingMaxStretch = 2.8f;
        [Tooltip("Station shots never sit farther than this from their focus, so small balls stay readable.")]
        [Min(2f)] public float maxStationDistance = 13f;
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
        Vector3 _orbitCenter;
        float _orbitRadius;
        float _orbitHeight;
        bool _orbitGlidedIn;
        bool _waitingForBalls;
        int _checkInThreshold = -1;

        // Recorded from events (FixedUpdate), consumed in Update.
        bool _leaderChanged;
        int _wonColour = -1;

        int _sorterIndex = -1;
        int _tubeRackIndex = -1;
        float _lastGlideTime;
        float _currentDwell;

        // Tour.
        int _stationCursor = 1;
        StopKind _pendingStop;
        MachineStation _pendingStation;
        int _gateThreshold = -1;
        float _gateWaitStart;
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
                _state.Won += OnWon;
            }
        }

        void OnDestroy()
        {
            if (_state != null)
            {
                _state.LeaderChanged -= OnLeaderChanged;
                _state.Won -= OnWon;
            }
        }

        void OnLeaderChanged(int colour) { _leaderChanged = true; }
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
                    UpdateOpening(stations, now);
                    break;
                case Mode.GateWatch:
                    if (now >= openingSequenceSeconds + gateWatchSeconds)
                    {
                        _mode = Mode.Tour;
                        _shotEnd = now;
                    }

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

        /// <summary>Silo orbit: a circle round the storage cylinder at a distance that fits it, starting on the camera side.</summary>
        void ComputeOrbit(IReadOnlyList<MachineStation> stations)
        {
            Bounds b = stations[0] is ISilo silo && silo.StorageBounds.size.sqrMagnitude > 1e-4f ? silo.StorageBounds : stations[0].activityBounds;
            _orbitCenter = b.center;
            _orbitRadius = Mathf.Max(8f, FitDistance(Mathf.Max(b.size.x, b.size.z) + 2f, b.size.y + 3f, 1.05f));
            _orbitHeight = b.center.y + _orbitRadius * 0.3f;
        }

        Vector3 OrbitPosition(float u)
        {
            float a = u * Mathf.PI * 2f;
            return new Vector3(_orbitCenter.x + Mathf.Sin(a) * _orbitRadius, _orbitHeight, _orbitCenter.z - Mathf.Cos(a) * _orbitRadius);
        }

        /// <summary>
        /// Opening: zoom out from the hero shot to the entire track, hold, glide in to the silo, orbit it once, then go to the
        /// silo exit as the gate opens (the sequence ends when it does).
        /// </summary>
        void UpdateOpening(IReadOnlyList<MachineStation> stations, float now)
        {
            float glideInAt = openingZoomOutSeconds + openingWideHoldSeconds;
            float orbitStart = glideInAt + openingGlideInSeconds;
            float gateAt = openingSequenceSeconds - gateLeadSeconds;
            if (now >= gateAt)
            {
                GlideToGate(stations[0], now);
                _mode = Mode.GateWatch;
                return;
            }

            if (!_orbitGlidedIn && now >= glideInAt)
            {
                _orbitGlidedIn = true;
                _rig.SetDrift(0f, 0f);
                _rig.GlideTo(OrbitPosition(0f), _orbitCenter, openingGlideInSeconds);
            }
            else if (_orbitGlidedIn && now >= orbitStart)
            {
                // One full turn, posed every frame so it is smooth and exact; ends where it started, on the camera side.
                _rig.SnapTo(OrbitPosition((now - orbitStart) / Mathf.Max(0.5f, gateAt - orbitStart)), _orbitCenter);
            }
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
            ComputeOrbit(stations);
            _rig.GlideTo(_widePos, _wideLook, openingZoomOutSeconds); // zoom out from the hero shot to the entire track
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
            if (phase == MachinePhase.Finale)
            {
                StartFinale(now);
            }
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

        // Station tour ----------------------------------------------------------------------------------------------

        enum StopKind
        {
            None,
            Station,
            FrontView,
            VortexExit
        }

        /// <summary>
        /// Visits the stations in flow order: silo exit (gate shot), peg wall, a front view of wall + silo drop, vortex,
        /// vortex exit, wave floor, then the rest, then the tubes and the lap starts again. A stop is only entered once
        /// its station holds enough balls (random 100..150 per stop, drawn once, so it never flickers); after
        /// <see cref="maxWaitSeconds"/> it moves on anyway. Tube check-ins can interrupt it and are never gated.
        /// </summary>
        void UpdateTour(IReadOnlyList<MachineStation> stations, float now)
        {
            bool leaderWants = _leaderChanged && now - _lastCheckIn >= leaderCheckInGap && _state.LeaderIndex >= 0;
            if (!leaderWants && now < _shotEnd)
            {
                return;
            }

            // Work out the next stop without committing to it.
            StopKind kind = _pendingStop;
            MachineStation station = _pendingStation;
            int cursorAfter = _stationCursor;
            if (kind == StopKind.None)
            {
                // Next stop in flow order; the tube rack and the cannon are skipped (the tubes get their own shot at the end of a lap).
                while (cursorAfter < stations.Count && (cursorAfter == _tubeRackIndex || stations[cursorAfter] is CannonStation))
                {
                    cursorAfter++;
                }

                if (cursorAfter >= stations.Count)
                {
                    // End of a lap: show the bins only if they hold something, otherwise just start the next lap.
                    _stationCursor = 1;
                    _pendingStop = StopKind.None;
                    _gateThreshold = -1;
                    if (TubesReady())
                    {
                        GlideToTubes(now);
                        _shotEnd = now + checkInDwell;
                        _checkInThreshold = -1;
                    }

                    return;
                }

                station = stations[cursorAfter++];
                kind = StopKind.Station;
            }

            // Gate: the stop's station must hold enough balls, otherwise stay on the current shot and re-check next frame.
            MachineStation pegWall = FindStation("peg", 1);
            MachineStation gateStation = kind == StopKind.FrontView ? pegWall : station;
            if (_gateThreshold < 0)
            {
                _gateThreshold = Mathf.RoundToInt(Range(minBallsToVisit));
                _gateWaitStart = now;
            }

            if (CountBallsIn(gateStation.activityBounds) < _gateThreshold && now - _gateWaitStart < maxWaitSeconds)
            {
                _waitingForBalls = true;
                return;
            }

            _waitingForBalls = false;

            // A tube check-in may only cut away once the next stop is ready, i.e. never while the camera is holding for balls: the gate
            // is judged first, so a due check-in cannot fire in the frame the dwell ends and drag the camera to the bins.
            // The bins get the same "never empty" rule as every station: a check-in (timed or on a lead change) only happens once enough
            // balls have been sorted. Before that the timer simply keeps running and the camera carries on with the stations. This was the
            // real reason the camera jumped to the sorter and bins around a minute in, while they were still empty.
            if ((leaderWants || now >= _nextCheckIn) && TubesReady())
            {
                GlideToTubes(now);
                _shotEnd = now + checkInDwell;
                _lastCheckIn = now;
                _nextCheckIn = now + Range(checkInEvery);
                _gateWaitStart = now;
                _checkInThreshold = -1;
                return;
            }

            // Commit.
            _gateThreshold = -1;
            _stationCursor = cursorAfter;
            _pendingStop = StopKind.None;
            _pendingStation = null;
            switch (kind)
            {
                case StopKind.FrontView:
                    GlideToFrontView(stations, now);
                    _shotEnd = now + Range(stationDwellSeconds);
                    break;
                case StopKind.VortexExit:
                    GlideToExit(station, now);
                    _shotEnd = now + Range(stationDwellSeconds);
                    break;
                default:
                    GlideToStation(station, now);
                    _shotEnd = now + Range(stationDwellSeconds);
                    if (station is VortexFunnelStation)
                    {
                        _pendingStop = StopKind.VortexExit;
                        _pendingStation = station;
                    }
                    else if (station == pegWall)
                    {
                        _pendingStop = StopKind.FrontView;
                        _pendingStation = station;
                    }

                    break;
            }
        }

        /// <summary>True once enough balls have been sorted (a random 100..150, drawn once per check-in) for the bins to be worth showing.</summary>
        bool TubesReady()
        {
            if (_checkInThreshold < 0)
            {
                _checkInThreshold = Mathf.RoundToInt(Range(minBallsToVisit));
            }

            return _state.TotalSorted >= _checkInThreshold;
        }

        /// <summary>Active balls inside <paramref name="bounds"/>. Read-only; the ball list is the sim's dynamic balls.</summary>
        int CountBallsIn(Bounds bounds)
        {
            IReadOnlyList<MachineBall> balls = _state.Balls;
            int n = 0;
            for (int i = 0; i < balls.Count; i++)
            {
                MachineBall b = balls[i];
                if (b != null && !b.counted && bounds.Contains(b.transform.position))
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>
        /// Front view of the peg wall: the camera stands out in front of the board (on the side its face and glass point to, i.e. ahead
        /// of the flow), a little to one side and above, looking back at the board and the silo exit behind it, so the balls dropping
        /// out of the silo and the wall they land on are both in frame.
        /// </summary>
        void GlideToFrontView(IReadOnlyList<MachineStation> stations, float now)
        {
            MachineStation silo = stations[0];
            MachineStation peg = FindStation("peg", 1);
            Bounds region = peg.activityBounds;
            Vector3 siloExit = silo.exit != null ? silo.exit.position : silo.activityBounds.center;
            region.Encapsulate(siloExit);
            region.Encapsulate(siloExit + Vector3.down * 1.5f);

            // Station frame: forward is the flow direction, which the board's face looks toward.
            Vector3 forward = peg.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            const float azimuth = 0f; // straight on, not from the side
            const float elevation = 24f * Mathf.Deg2Rad; // high enough to see the silo exit over the top of the board
            Vector3 dir = (forward * Mathf.Cos(azimuth) + right * Mathf.Sin(azimuth)) * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
            float extent = Mathf.Max(region.size.x, region.size.z);
            float fit = FitDistance(extent + 1f, region.size.y + 1f, 1.05f) + extent * 0.35f;
            _rig.GlideTo(region.center + dir * fit, region.center, Range(glideSeconds));
            _rig.SetLookTracking(null);
            _rig.SetDrift(0.15f, 0.2f);
            _lastGlideTime = now;
        }

        /// <summary>
        /// Stands past the end of a station's exit, a little to one side and above, and faces back along the flow at the exit, so
        /// the marbles coming off the end of the vortex spiral run toward the camera.
        /// </summary>
        void GlideToExit(MachineStation station, float now)
        {
            Vector3 exit = station.exit != null ? station.exit.position : station.activityBounds.center;
            Vector3 forward = station.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 look = exit - forward * 1.5f + Vector3.up * 0.2f;
            Vector3 offset = station.transform.rotation * new Vector3(1.5f, 2.2f, 4.5f);
            _rig.GlideTo(exit + offset, look, Range(glideSeconds));
            _rig.SetLookTracking(null);
            _rig.SetDrift(0.15f, 0.2f);
            _lastGlideTime = now;
        }

        // Poses -----------------------------------------------------------------------------------------------------

        void GlideToStation(MachineStation station, float now)
        {
            Vector3 focus = station.cameraFocus != null ? station.cameraFocus.position : station.activityBounds.center;
            _rig.GlideTo(focus + LimitOffset(station.cameraOffset), focus, Range(glideSeconds));
            _rig.SetLookTracking(null);
            _rig.SetDrift(0.2f, 0.25f);
            _lastGlideTime = now;
            _currentDwell = station.tourSeconds;
        }

        /// <summary>Front of the flood gate, a little to one side and above, so the balls pouring out are in frame.</summary>
        void GlideToGate(MachineStation silo, float now)
        {
            Transform t = silo.transform;
            Vector3 gate = silo is SiloStation s
                ? t.TransformPoint(s.plateRestPosition + new Vector3(0f, 0.3f, -0.3f))
                : silo.exit != null ? silo.exit.position : silo.activityBounds.center;
            _rig.GlideTo(gate + t.rotation * gateCameraOffset, gate, gateLeadSeconds);
            _rig.SetDrift(0.1f, 0.25f);
            _lastGlideTime = now;
        }

        /// <summary>Valve opening: glide in on the outlet / star feeder so the first balls dropping are big on screen.</summary>
        void GlideToValve(MachineStation station, float now)
        {
            Vector3 focus = station.exit != null ? station.exit.position
                : station.cameraFocus != null ? station.cameraFocus.position : station.activityBounds.center;
            Vector3 offset = LimitOffset(station.cameraOffset);
            float dist = Mathf.Clamp(offset.magnitude, 3.5f, 7f);
            Vector3 dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : new Vector3(0f, 0.2f, -1f);
            _rig.GlideTo(focus + dir * dist, focus, 3f);
            _rig.SetDrift(0.12f, 0.25f);
            _lastGlideTime = now;
            _currentDwell = station.tourSeconds;
        }

        /// <summary>Caps a station camera offset so balls stay large enough on screen.</summary>
        Vector3 LimitOffset(Vector3 offset)
        {
            // Never lower than ~27 degrees above the horizontal, so the balls on the track are seen from above.
            float flat = new Vector2(offset.x, offset.z).magnitude;
            offset.y = Mathf.Max(offset.y, flat * 0.5f);
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
