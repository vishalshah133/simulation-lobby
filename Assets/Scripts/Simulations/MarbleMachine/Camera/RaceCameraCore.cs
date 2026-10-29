using System;
using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>One frame of race state, as the camera reads it. Arrays are owned by the caller and reused.</summary>
    public struct RaceCameraInput
    {
        public RacePhase phase;
        public float simTime;
        public int count;
        public Vector3[] pos;
        public float[] progress;
        public int[] recycles;
        public bool[] finished;
        public int winner;
        public int laps;
    }

    /// <summary>A station's slice of the course, before overlap splitting.</summary>
    public struct ZoneSource
    {
        public string name;
        public Vector3 forward;
        public Vector3 focus;
        public float s0;
        public float s1;
    }

    /// <summary>A set-piece zone: a range of the course (lo..hi, may wrap) with the shot it blends in.</summary>
    public struct ZoneDef
    {
        public string name;
        public float lo;
        public float hi;
        public Vector3 forward;
        public Vector3 focus;
        public ZoneShot shot;
    }

    public struct CameraPose
    {
        public Vector3 position;
        public Vector3 look;
        public Vector3 aim;
        public Quaternion rotation;
    }

    /// <summary>Live values for logs and the offline report. Read only.</summary>
    public sealed class RaceCameraDebug
    {
        public float s;
        public float speed;
        public float extent;
        public int leader = -1;
        public string mode = "";
        public float[] zoneWeights = new float[0];
        public float occ;
        public float yaw;
        public float pitch;
        public float distance;
        public float vertical;
        /// <summary>Per-racer eligibility (0..1) used for the leader and the pack in the latest frame.</summary>
        public float[] eligibility = new float[0];
    }

    /// <summary>
    /// The mach-002 race camera as pure maths: no MonoBehaviour, no Physics, no Time. A drone rig that follows an arc-length
    /// focus along a smoothed course rail; every quantity it eventually hands to the lens is the output of a rate-limited
    /// critically damped spring, so nothing the racers do (rank swaps, recycles, staircase progress) can step the picture.
    /// A pure function of its inputs and dt, with substepping, so offline capture is deterministic. Design and numbers:
    /// plans/videos/mach-002-camera-rework.md.
    /// </summary>
    public sealed class RaceCameraCore
    {
        enum Mode { Opening, Follow, Winner, Outro }

        struct Target
        {
            public Vector3 lk;
            public float psi;
            public float theta;
            public float d;
        }

        sealed class ZoneState
        {
            public float u;
            public float activeTime;
            public float yawDelta;
            public bool yawValid;
            public float w;
        }

        readonly CourseTrack _track;
        readonly RaceCameraSettings _s;
        readonly ZoneDef[] _zones;
        readonly ZoneState[] _zs;
        readonly Vector3 _gridCentre;
        readonly Vector3 _openDir;
        readonly float _openDist;
        readonly float _tanV;
        readonly float _openPsi;
        readonly float _openTheta;
        readonly float _length;

        readonly RaceCameraDebug _debug = new RaceCameraDebug();
        public RaceCameraDebug Debug => _debug;

        // Per-racer scratch.
        float[] _S = new float[0];
        float[] _elig = new float[0];
        float[] _tRecycle = new float[0];
        int[] _lastRecycles = new int[0];
        int[] _order = new int[0];

        // Mode and crossfade.
        Mode _mode;
        Mode _fadeFrom;
        bool _fadeActive;
        float _fadeT;
        float _fadeDur = 1f;
        float _dx;
        bool _dxValid;
        float _goTime = 999f;
        float _finishedTime;
        float _countdownTime;
        bool _hasPose;
        bool _followInit;
        CameraPose _pose;
        float _aspect = 16f / 9f;

        // Follow signals (per frame).
        int _F = -1;
        float _SF;
        float _SFprev;
        bool _SFprevValid;
        int _holdCandidate = -1;
        float _hold;
        float _vff;
        float _eRaw;
        float _vRaw;
        Vector3 _oRaw;
        float _zTarget;
        float _swLocal;
        bool _finalLap;
        bool _winnerSignals;

        // Filter state.
        float _sFocus;
        float _vFocus;
        float _eF;
        float _veF;
        float _vF;
        float _vvF;
        Vector3 _oF;
        Vector3 _voF;
        Vector3 _lk;
        Vector3 _vLk;
        Vector3 _lkTPrev;
        float _psi;
        float _vPsi;
        float _theta;
        float _vTheta;
        float _d;
        float _vD;
        float _occ;

        public RaceCameraCore(CourseTrack track, RaceCameraSettings settings, ZoneDef[] zones, Vector3 gridCentre, Vector3 openDir, float openDistance, float fovDeg)
        {
            _track = track;
            _s = settings;
            _zones = zones ?? new ZoneDef[0];
            _zs = new ZoneState[_zones.Length];
            for (int i = 0; i < _zs.Length; i++)
            {
                _zs[i] = new ZoneState();
            }

            _gridCentre = gridCentre;
            _openDir = openDir.normalized;
            _openDist = openDistance;
            _tanV = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            _openPsi = Yaw(_openDir);
            _openTheta = Mathf.Asin(Mathf.Clamp(_openDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            _length = track.Length;
            _debug.zoneWeights = new float[_zones.Length];
        }

        // Setup helpers ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The opening shot over the start grid: looks from the gate's authored camera offset (or a three-quarter view from behind),
        /// never from below, pulled back far enough to hold every slot and its tag.
        /// </summary>
        public static void ComputeOpening(IList<Vector3> slots, Vector3 gateForward, Vector3 cameraOffset, bool hasOffset, float fovDeg, float aspect, float margin,
            out Vector3 centre, out Vector3 dir, out float distance)
        {
            Vector3 lo = slots[0];
            Vector3 hi = slots[0];
            for (int i = 1; i < slots.Count; i++)
            {
                lo = Vector3.Min(lo, slots[i]);
                hi = Vector3.Max(hi, slots[i]);
            }

            centre = (lo + hi) * 0.5f;
            Vector3 size = hi - lo;
            float preferred = 0f;
            if (hasOffset && cameraOffset.sqrMagnitude > 1e-3f)
            {
                dir = cameraOffset.normalized;
                preferred = cameraOffset.magnitude;
            }
            else
            {
                Vector3 forward = gateForward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                dir = (-forward * 0.75f + right * 0.45f + Vector3.up * 0.55f).normalized;
            }

            dir.y = Mathf.Max(dir.y, 0.35f);
            dir = dir.normalized;

            float width = Mathf.Max(size.x, size.z) + 1.6f;
            float height = size.y + 2.4f;
            float tanV = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            float asp = aspect > 0.1f ? aspect : 16f / 9f;
            float fit = Mathf.Max(width * 0.5f / (tanV * asp), height * 0.5f / tanV) * margin;
            distance = Mathf.Max(preferred, fit);
        }

        /// <summary>
        /// Builds the zone list: one per station whose name starts with a shot's match, active from preRoll before the station to
        /// postRoll after it. Neighbouring zones that overlap are cut at the midpoint between the earlier one's end and the later one's start.
        /// </summary>
        public static ZoneDef[] BuildZones(IList<ZoneSource> stations, RaceCameraSettings settings, float courseLength)
        {
            var list = new List<ZoneDef>();
            var s1s = new List<float>();
            var s0s = new List<float>();
            for (int i = 0; i < stations.Count; i++)
            {
                ZoneSource st = stations[i];
                if (string.IsNullOrEmpty(st.name))
                {
                    continue;
                }

                for (int k = 0; k < settings.zones.Length; k++)
                {
                    ZoneShot shot = settings.zones[k];
                    if (!string.IsNullOrEmpty(shot.match) && st.name.StartsWith(shot.match, StringComparison.OrdinalIgnoreCase))
                    {
                        Vector3 f = st.forward;
                        f.y = 0f;
                        f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
                        list.Add(new ZoneDef
                        {
                            name = st.name,
                            lo = st.s0 - shot.preRoll,
                            hi = st.s1 + shot.postRoll,
                            forward = f,
                            focus = st.focus,
                            shot = shot,
                        });
                        s0s.Add(st.s0);
                        s1s.Add(st.s1);
                        break;
                    }
                }
            }

            for (int i = 0; i + 1 < list.Count; i++)
            {
                ZoneDef a = list[i];
                ZoneDef b = list[i + 1];
                if (a.hi > b.lo)
                {
                    float mid = 0.5f * (s1s[i] + s0s[i + 1]);
                    a.hi = mid;
                    b.lo = mid;
                    list[i] = a;
                    list[i + 1] = b;
                }
            }

            return list.ToArray();
        }

        // Step ------------------------------------------------------------------------------------------------------

        public CameraPose Step(in RaceCameraInput inp, float dt, float aspect, bool blockedLastFrame)
        {
            dt = Mathf.Min(dt, _s.maxDt);
            if (dt <= 0f)
            {
                if (_hasPose)
                {
                    return _pose;
                }

                dt = 1f / 60f;
            }

            _aspect = aspect > 0.1f ? aspect : 16f / 9f;
            int count = inp.count;
            bool first = !_hasPose;
            if (_S.Length != count)
            {
                _S = new float[count];
                _elig = new float[count];
                _tRecycle = new float[count];
                _lastRecycles = new int[count];
                _order = new int[count];
                for (int i = 0; i < count; i++)
                {
                    _tRecycle[i] = -1e6f;
                    _lastRecycles[i] = inp.recycles[i];
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (inp.recycles[i] > _lastRecycles[i])
                {
                    _tRecycle[i] = inp.simTime;
                }

                _lastRecycles[i] = inp.recycles[i];
            }

            // Mode from phase.
            if (first)
            {
                _mode = ModeForPhase(inp);
                _goTime = 999f;
            }
            else
            {
                Mode old = _mode;
                if (_mode == Mode.Opening && inp.phase != RacePhase.Grid && inp.phase != RacePhase.Countdown)
                {
                    _mode = inp.phase == RacePhase.Finished && inp.winner >= 0 ? Mode.Winner : Mode.Follow;
                    _goTime = 0f;
                    StartFade(old, _mode == Mode.Follow ? _s.openToFollowFade : _s.followToWinnerFade);
                }
                else if (_mode == Mode.Follow && inp.phase == RacePhase.Finished && inp.winner >= 0)
                {
                    _mode = Mode.Winner;
                    StartFade(old, _s.followToWinnerFade);
                }
                else if (_mode == Mode.Winner && _finishedTime >= _s.winnerDwell)
                {
                    _mode = Mode.Outro;
                    StartFade(old, _s.winnerToOutroFade);
                }

            }

            if (inp.phase == RacePhase.Finished)
            {
                _finishedTime += dt;
            }

            if (inp.phase == RacePhase.Countdown)
            {
                _countdownTime += dt;
            }

            _goTime += dt;

            // Signals (Follow and Winner, and while the Winner shot is the source of the outro fade).
            bool winnerSignals = _mode == Mode.Winner || _mode == Mode.Outro;
            bool needSignals = _mode != Mode.Opening;
            if (needSignals)
            {
                ComputeSignals(inp, dt, winnerSignals);
                if (!_followInit)
                {
                    InitFollow();
                }
            }

            if (first)
            {
                Target snap = MakeTarget(inp, dt, true);
                _lk = snap.lk;
                _vLk = Vector3.zero;
                _lkTPrev = snap.lk;
                _psi = snap.psi;
                _vPsi = 0f;
                _theta = snap.theta;
                _vTheta = 0f;
                _d = snap.d;
                _vD = 0f;
                _hasPose = true;
                _pose = Compose();
                FillDebug();
                return _pose;
            }

            // Focus filters first, then the target from their end-of-frame state, then the pose springs toward it: the
            // target is valid at the end of the frame whatever the frame length, so frame jitter cannot shift it.
            int n = Mathf.Max(1, Mathf.CeilToInt(dt * _s.substepHz));
            float h = dt / n;
            if (_followInit)
            {
                for (int i = 0; i < n; i++)
                {
                    StepFocus(h);
                }
            }

            for (int i = 0; i < n; i++)
            {
                _occ = Mathf.Clamp01(_occ + (blockedLastFrame ? h / _s.obstructionRiseSeconds : -h / _s.obstructionFallSeconds));
            }

            Target tgt = MakeTarget(inp, dt, false);
            Vector3 ff = Vector3.ClampMagnitude((tgt.lk - _lkTPrev) / dt, _s.lookFeedForwardMax);
            StepPose(tgt, ff, n, h);

            _lkTPrev = tgt.lk;
            _pose = Compose();
            FillDebug();
            return _pose;
        }

        static Mode ModeForPhase(in RaceCameraInput inp)
        {
            switch (inp.phase)
            {
                case RacePhase.Grid:
                case RacePhase.Countdown:
                    return Mode.Opening;
                case RacePhase.Finished:
                    return inp.winner >= 0 ? Mode.Winner : Mode.Follow;
                default:
                    return Mode.Follow;
            }
        }

        void StartFade(Mode from, float seconds)
        {
            _fadeFrom = from;
            _fadeActive = true;
            _fadeT = 0f;
            _fadeDur = seconds;
            _dxValid = false;
        }

        // Signals ---------------------------------------------------------------------------------------------------

        void ComputeSignals(in RaceCameraInput inp, float dt, bool winnerMode)
        {
            int n = inp.count;
            float L = _length;
            for (int i = 0; i < n; i++)
            {
                _S[i] = inp.progress[i] * L;
            }

            _finalLap = inp.phase == RacePhase.FinalLap;
            _winnerSignals = winnerMode && inp.winner >= 0;
            bool leaderSwitched = false;

            if (_winnerSignals)
            {
                int w = inp.winner;
                if (_F != w)
                {
                    leaderSwitched = true;
                    _F = w;
                }

                float baseS = inp.laps * L;
                _swLocal = _track.ProjectNear(inp.pos[w], _swLocal, 10f, 25f);
                _SF = baseS + _swLocal;
                _eRaw = 0f;
                _vRaw = 0f;
                _zTarget = _SF;
                Vector3 off = inp.pos[w] - _track.Rail(TrackMod(_SF));
                _oRaw = ClampOffset(off);
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    float e = Smooth(_s.recycleBlockSeconds, _s.recycleRampEnd, inp.simTime - _tRecycle[i]);
                    float dist = (inp.pos[i] - _track.Raw(TrackMod(_S[i]))).magnitude;
                    e *= 1f - Smooth(_s.offCourseStart, _s.offCourseEnd, dist);
                    if (inp.finished[i])
                    {
                        e = 0f;
                    }

                    _elig[i] = e;
                }

                int c = -1;
                for (int i = 0; i < n; i++)
                {
                    if (_elig[i] >= 0.5f && (c < 0 || _S[i] > _S[c]))
                    {
                        c = i;
                    }
                }

                if (_F < 0 || _F >= n || _elig[_F] < 0.5f)
                {
                    if (c >= 0)
                    {
                        if (c != _F)
                        {
                            leaderSwitched = true;
                        }

                        _F = c;
                    }
                    else if (_F < 0)
                    {
                        _F = 0;
                        leaderSwitched = true;
                    }

                    _hold = 0f;
                    _holdCandidate = -1;
                }
                else if (c >= 0 && c != _F)
                {
                    float gap = _S[c] - _S[_F];
                    if (gap >= _s.leaderSwitchNowGap)
                    {
                        _F = c;
                        leaderSwitched = true;
                        _hold = 0f;
                        _holdCandidate = -1;
                    }
                    else if (gap >= _s.leaderSwitchHoldGap)
                    {
                        if (_holdCandidate != c)
                        {
                            _holdCandidate = c;
                            _hold = 0f;
                        }

                        _hold += dt;
                        if (_hold >= _s.leaderHoldSeconds)
                        {
                            _F = c;
                            leaderSwitched = true;
                            _hold = 0f;
                            _holdCandidate = -1;
                        }
                    }
                    else
                    {
                        _hold = 0f;
                        _holdCandidate = -1;
                    }
                }
                else
                {
                    _hold = 0f;
                    _holdCandidate = -1;
                }

                int F = _F;
                _SF = _S[F];
                int K = _finalLap ? _s.finalPackCount : _s.packCount;
                float G = _finalLap ? _s.finalPackLength : _s.packLength;

                // Followers sorted by progress, best first (insertion sort, at most 16 entries).
                int m = 0;
                for (int i = 0; i < n; i++)
                {
                    if (i == F || _elig[i] <= 0.05f)
                    {
                        continue;
                    }

                    int j = m++;
                    while (j > 0 && _S[_order[j - 1]] < _S[i])
                    {
                        _order[j] = _order[j - 1];
                        j--;
                    }

                    _order[j] = i;
                }

                Vector3 pF = inp.pos[F];
                float wsum = 1f;
                Vector3 rsum = pF;
                float ssum = _SF;
                float tail = 0f;
                float front = 0f;
                float yMin = pF.y;
                float yMax = pF.y;
                int take = Mathf.Min(m, K - 1);
                for (int k = 0; k < take; k++)
                {
                    int i = _order[k];
                    float g = _SF - _S[i];
                    float fade;
                    if (g < 0f)
                    {
                        front = Mathf.Max(front, -g * _elig[i]);
                        fade = 1f;
                    }
                    else
                    {
                        fade = 1f - Smooth(_s.packFadeStart * G, G, g);
                        tail = Mathf.Max(tail, g * fade * _elig[i]);
                    }

                    float wk = Mathf.Pow(_s.packWeightDecay, k + 1) * fade * _elig[i];
                    rsum += inp.pos[i] * wk;
                    ssum += _S[i] * wk;
                    wsum += wk;
                    if (wk >= 0.25f)
                    {
                        yMin = Mathf.Min(yMin, inp.pos[i].y);
                        yMax = Mathf.Max(yMax, inp.pos[i].y);
                    }
                }

                _eRaw = tail + front;
                Vector3 R = rsum / wsum;
                float zPack = ssum / wsum;
                _oRaw = ClampOffset(R - _track.Rail(TrackMod(zPack)));
                _vRaw = yMax - yMin;
                _zTarget = _SF - _s.focusBackFraction * _eF;
            }

            // Speed feed-forward from the leader's progress (not on a switch or a jump).
            if (_SFprevValid && !leaderSwitched && Mathf.Abs(_SF - _SFprev) < 2f)
            {
                float inst = (_SF - _SFprev) / dt;
                _vff += (inst - _vff) * (1f - Mathf.Exp(-dt / _s.speedFilterSeconds));
            }

            _vff = Mathf.Clamp(_vff, 0f, _s.speedFilterMax);
            _SFprev = _SF;
            _SFprevValid = true;
            if (_winnerSignals)
            {
                _zTarget = _SF;
            }
        }

        Vector3 ClampOffset(Vector3 o)
        {
            Vector3 h = new Vector3(o.x, 0f, o.z);
            h = Vector3.ClampMagnitude(h, _s.racerOffsetMax);
            return new Vector3(h.x, Mathf.Clamp(o.y, -_s.racerOffsetMaxY, _s.racerOffsetMaxY), h.z);
        }

        void InitFollow()
        {
            _followInit = true;
            _vFocus = 0f;
            _eF = _eRaw;
            _veF = 0f;
            _vF = _vRaw;
            _vvF = 0f;
            _voF = Vector3.zero;
            _vff = 0f;
            _zTarget = _winnerSignals ? _SF : _SF - _s.focusBackFraction * _eF;
            _sFocus = _zTarget;
            _oF = _oRaw;
            if (_hasPose)
            {
                // Entering the chase from the opening shot: start the offset where the camera is already looking, so the
                // follow target equals the current look point and nothing steps at GO.
                _oF = _lk - (_track.Rail(TrackMod(_sFocus)) + Vector3.up * _s.lookHeight);
            }
        }

        float TrackMod(float s) => CourseTrack.Mod(s, _length);

        static float Smooth(float a, float b, float x) => CameraSpring.SmoothStep(a, b, x);

        // Zones -----------------------------------------------------------------------------------------------------

        float FollowAzimuth()
        {
            float t = Mathf.Clamp01((_aspect - 0.5625f) / (1.7778f - 0.5625f));
            return Mathf.Lerp(_s.azimuthPortrait, _s.azimuthLandscape, t);
        }

        Vector3 DirH(Vector3 f, float phiDeg)
        {
            float phi = phiDeg * Mathf.Deg2Rad;
            return -f * Mathf.Cos(phi) + _track.Outside(f) * Mathf.Sin(phi);
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        void UpdateZoneYaw()
        {
            if (_zones.Length == 0 || !_followInit)
            {
                return;
            }

            float psiF = Yaw(DirH(_track.Heading(TrackMod(_sFocus)), FollowAzimuth()));
            for (int z = 0; z < _zones.Length; z++)
            {
                ZoneState st = _zs[z];
                if (st.u <= 0f && !ZoneOn(z))
                {
                    st.yawValid = false;
                    continue;
                }

                float psiZ = Yaw(DirH(_zones[z].forward, _zones[z].shot.azimuth));
                float dd = Mathf.DeltaAngle(psiF, psiZ);
                st.yawDelta = st.yawValid ? st.yawDelta + Mathf.DeltaAngle(st.yawDelta, dd) : dd;
                st.yawValid = true;
            }
        }

        bool ZoneOn(int z)
        {
            if (_mode != Mode.Follow)
            {
                return false;
            }

            ZoneDef zd = _zones[z];
            return CourseTrack.Mod(_sFocus - zd.lo, _length) < (zd.hi - zd.lo);
        }

        void ComputeZoneWeights()
        {
            float sum = 0f;
            for (int z = 0; z < _zs.Length; z++)
            {
                float u = _zs[z].u;
                _zs[z].w = u * u * (3f - 2f * u);
                sum += _zs[z].w;
            }

            if (sum > 1f)
            {
                for (int z = 0; z < _zs.Length; z++)
                {
                    _zs[z].w /= sum;
                }
            }
        }

        // Targets ---------------------------------------------------------------------------------------------------

        Target BuildTarget(Mode mode, in RaceCameraInput inp)
        {
            switch (mode)
            {
                case Mode.Opening:
                {
                    float d = _openDist;
                    if (inp.phase == RacePhase.Countdown)
                    {
                        float end = Mathf.Max(Mathf.Min(_s.minPushDistance, _openDist), _s.countdownPushIn * _openDist);
                        d = Mathf.Lerp(_openDist, end, CameraSpring.SmoothStep01(_countdownTime / Mathf.Max(0.1f, _s.countdownSeconds)));
                    }

                    return new Target { lk = _gridCentre + Vector3.up * 0.2f, psi = _openPsi, theta = _openTheta, d = d };
                }

                case Mode.Outro:
                    return new Target { lk = _gridCentre + Vector3.up * 0.2f, psi = _openPsi, theta = _openTheta, d = _s.outroDistanceFactor * _openDist };
                case Mode.Winner:
                    return FollowTarget(true);
                default:
                    return FollowTarget(false);
            }
        }

        Target FollowTarget(bool winnerStyle)
        {
            ComputeZoneWeights();
            if (winnerStyle)
            {
                // Zone targets are off in the winner shot; weights are ignored for it.
                for (int z = 0; z < _zs.Length; z++)
                {
                    _zs[z].w = 0f;
                }
            }

            float s = _sFocus;
            float lead = Mathf.Clamp(_vFocus * _s.lookAheadGain, 0f, _s.lookAheadMax);
            float sMod = TrackMod(s);
            Vector3 f = _track.Heading(sMod);

            float ow = 1f;
            float minD = winnerStyle ? _s.winnerMinDistance : (_finalLap ? _s.minDistanceFinalLap : _s.minDistance);
            float baseMin = minD;
            for (int z = 0; z < _zs.Length; z++)
            {
                float w = _zs[z].w;
                ow += w * (_zones[z].shot.offsetWeight - 1f);
                minD += w * (_zones[z].shot.minDistance - baseMin);
            }

            Vector3 baseP = _track.Rail(TrackMod(s + lead)) + _oF * ow + Vector3.up * _s.lookHeight;
            Vector3 lk = baseP;
            for (int z = 0; z < _zs.Length; z++)
            {
                float w = _zs[z].w;
                if (w > 0f)
                {
                    lk += w * _zones[z].shot.focusPull * (_zones[z].focus - baseP);
                }
            }

            float phi = winnerStyle ? _s.winnerAzimuth : FollowAzimuth();
            float pitchBase = winnerStyle ? _s.winnerPitch : _s.pitch;
            float psiT = Yaw(DirH(f, phi));
            float thetaT = pitchBase;
            for (int z = 0; z < _zs.Length; z++)
            {
                float w = _zs[z].w;
                if (w > 0f)
                {
                    psiT += w * _zs[z].yawDelta;
                    thetaT += w * (_zones[z].shot.elevation - pitchBase);
                }
            }

            // Distance fit: hold the pack (leader ahead of centre, tail behind) and its height in the frame.
            float tanH = _tanV * _aspect;
            float alpha = _s.focusBackFraction;
            // The leader's real distance from the look point counts too: when it speeds up or brakes faster than the focus can follow,
            // the camera widens (slowly) instead of losing it.
            float leaderDist = Mathf.Abs(_SF - (s + lead));
            float fracFront = Mathf.Max(Mathf.Abs(alpha * _eF - lead), leaderDist);
            float back = (1f - alpha) * _eF + lead;
            float psiRad = _psi * Mathf.Deg2Rad;
            Vector3 dirNow = new Vector3(Mathf.Sin(psiRad), 0f, Mathf.Cos(psiRad));
            float sinPhi = Mathf.Abs(Vector3.Dot(dirNow, _track.Outside(f)));
            float wh = Mathf.Max(fracFront, back) * Mathf.Max(0.5f, sinPhi) + _s.fitMarginWidth;
            float dH = wh / (tanH * _s.fitFill);
            float dV = (0.5f * _vF + _s.fitMarginHeight) / (_tanV * _s.fitFill);
            float dT = Mathf.Clamp(Mathf.Max(Mathf.Max(dH, dV), minD), baseMin, _s.maxDistance);

            return new Target { lk = lk, psi = psiT, theta = thetaT, d = dT };
        }

        Target Blend(Target a, Target b, float w)
        {
            float dd = Mathf.DeltaAngle(a.psi, b.psi);
            _dx = _dxValid ? _dx + Mathf.DeltaAngle(_dx, dd) : dd;
            _dxValid = true;
            return new Target
            {
                lk = Vector3.Lerp(a.lk, b.lk, w),
                psi = a.psi + w * _dx,
                theta = Mathf.Lerp(a.theta, b.theta, w),
                d = Mathf.Lerp(a.d, b.d, w),
            };
        }

        // Integration -----------------------------------------------------------------------------------------------

        Target MakeTarget(in RaceCameraInput inp, float dt, bool first)
        {
            UpdateZoneYaw();
            if (_fadeActive)
            {
                _fadeT += dt;
            }

            Target tgt = BuildTarget(_mode, inp);
            if (_fadeActive)
            {
                float w = CameraSpring.SmoothStep01(_fadeT / Mathf.Max(0.01f, _fadeDur));
                Target src = BuildTarget(_fadeFrom, inp);
                tgt = Blend(src, tgt, w);
                if (_fadeT >= _fadeDur)
                {
                    _fadeActive = false;
                }
            }

            // Ground guard and obstruction act on the targets only, so the pose springs rate-limit them.
            float lowPitch = first ? _s.minPitch : Mathf.Max(_s.minPitch, Mathf.Asin(Mathf.Clamp((_s.groundClearance - _lk.y) / Mathf.Max(1f, _d), -1f, 1f)) * Mathf.Rad2Deg);
            tgt.theta = Mathf.Clamp(tgt.theta + _s.obstructionPitch * _occ, lowPitch, _s.maxPitch);
            tgt.d *= 1f - _s.obstructionShrink * _occ;
            return tgt;
        }

        void StepFocus(float h)
        {
            CameraSpring.Step(ref _sFocus, ref _vFocus, _zTarget, _vff, _s.focusOmega, _s.focusSpeedMin, _s.focusSpeedMax, _s.focusAccelMax, h);
            CameraSpring.Step(ref _eF, ref _veF, _eRaw, 0f, _s.extentOmega, -_s.extentSpeedMax, _s.extentSpeedMax, _s.extentAccelMax, h);
            CameraSpring.Step(ref _vF, ref _vvF, _vRaw, 0f, _s.verticalOmega, -_s.verticalSpeedMax, _s.verticalSpeedMax, _s.verticalAccelMax, h);
            CameraSpring.Step(ref _oF, ref _voF, _oRaw, Vector3.zero, _s.offsetOmega, _s.offsetSpeedMax, _s.offsetAccelMax, h);
            StepZones(h);
        }

        void StepPose(Target tgt, Vector3 lkFeedForward, int n, float h)
        {
            bool outro = _mode == Mode.Outro;
            float pk = outro ? _s.outroOmegaScale : 1f;
            bool opening = _mode == Mode.Opening && !_fadeActive;
            float yawMax = _goTime < _s.yawAfterGoSeconds ? _s.yawSpeedMaxAfterGo : _s.yawSpeedMax;
            Vector3 lkStart = _lkTPrev;
            for (int i = 0; i < n; i++)
            {
                // The spring treats the target as moving from here at the feed-forward velocity.
                Vector3 lkT = Vector3.Lerp(lkStart, tgt.lk, (float)i / n);
                CameraSpring.Step(ref _lk, ref _vLk, lkT, lkFeedForward, _s.lookOmega * pk, _s.lookSpeedMax, _s.lookAccelMax, h);

                float psiT = _psi + Mathf.DeltaAngle(_psi, tgt.psi);
                CameraSpring.Step(ref _psi, ref _vPsi, psiT, 0f, _s.yawOmega * pk, -yawMax, yawMax, _s.yawAccelMax, h);
                CameraSpring.Step(ref _theta, ref _vTheta, tgt.theta, 0f, _s.pitchOmega * pk, -_s.pitchSpeedMax, _s.pitchSpeedMax, _s.pitchAccelMax, h);

                float dOmega = opening ? _s.openingDistanceOmega : (tgt.d > _d ? _s.distanceOmegaOut : _s.distanceOmegaIn) * pk;
                float dMax = outro ? _s.outroDistanceMaxSpeed : (opening ? _s.openingDistanceSpeedMax : _s.distanceSpeedMax);
                CameraSpring.Step(ref _d, ref _vD, tgt.d, 0f, dOmega, -dMax, dMax, _s.distanceAccelMax, h);
            }
        }

        void StepZones(float h)
        {
            for (int z = 0; z < _zs.Length; z++)
            {
                ZoneState st = _zs[z];
                bool on = ZoneOn(z);
                if (on)
                {
                    st.u = Mathf.Min(1f, st.u + h / _s.zoneBlendInSeconds);
                    st.activeTime += h;
                }
                else if (st.u > 0f)
                {
                    st.activeTime += h;
                    if (st.activeTime >= _s.zoneMinActiveSeconds)
                    {
                        st.u = Mathf.Max(0f, st.u - h / _s.zoneBlendOutSeconds);
                    }
                }

                if (st.u <= 0f && !on)
                {
                    st.activeTime = 0f;
                }
            }
        }

        CameraPose Compose()
        {
            float psi = _psi * Mathf.Deg2Rad;
            float th = _theta * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(psi) * Mathf.Cos(th), Mathf.Sin(th), Mathf.Cos(psi) * Mathf.Cos(th));
            Vector3 pos = _lk + dir * _d;
            Vector3 fwd = (_lk - pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            right = right.sqrMagnitude > 1e-8f ? right.normalized : Vector3.right;
            float compX = _aspect > 1f ? _s.aimOffsetLandscape : 0f;
            Vector3 aim = _lk + right * (compX * _d * _tanV * _aspect);
            return new CameraPose
            {
                position = pos,
                look = _lk,
                aim = aim,
                rotation = Quaternion.LookRotation(aim - pos, Vector3.up),
            };
        }

        void FillDebug()
        {
            _debug.s = _sFocus;
            _debug.speed = _vFocus;
            _debug.extent = _eF;
            _debug.vertical = _vF;
            _debug.leader = _F;
            _debug.mode = _mode == Mode.Opening ? "Opening" : _mode == Mode.Follow ? "Follow" : _mode == Mode.Winner ? "Winner" : "Outro";
            ComputeZoneWeights();
            for (int z = 0; z < _zs.Length; z++)
            {
                _debug.zoneWeights[z] = _zs[z].w;
            }

            if (_debug.eligibility.Length != _elig.Length)
            {
                _debug.eligibility = new float[_elig.Length];
            }

            Array.Copy(_elig, _debug.eligibility, _elig.Length);
            _debug.occ = _occ;
            _debug.yaw = _psi;
            _debug.pitch = _theta;
            _debug.distance = _d;
        }
    }
}
