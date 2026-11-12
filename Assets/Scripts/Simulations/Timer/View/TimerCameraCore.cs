using System;
using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Shared.Tracks;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer
{
    /// <summary>One critically damped spring's tuning: natural frequency and hard limits.</summary>
    [Serializable]
    public struct TimerSpringLimits
    {
        public float omega;
        public float maxVelocity;
        public float maxAcceleration;

        public TimerSpringLimits(float omega, float maxVelocity, float maxAcceleration)
        {
            this.omega = omega;
            this.maxVelocity = maxVelocity;
            this.maxAcceleration = maxAcceleration;
        }
    }

    /// <summary>The shots: the hopper hold, a follow of one marble (high chase or near-overhead), the collector.</summary>
    public enum TimerShotKind { Setup = 0, Chase = 1, Overhead = 2, Collector = 3 }

    /// <summary>How a follow shot sits relative to the course tangent at the subject, and how it frames.</summary>
    [Serializable]
    public struct TimerShot
    {
        [Tooltip("Degrees off the course tangent. 0 = straight behind the subject. The side is picked per shot.")]
        public float yawOffsetDeg;
        public float pitchDeg;
        [Tooltip("Metres from the aim point to the camera (before the aspect boost).")]
        public float distance;
        [Tooltip("Metres along the course from the subject to the aim point. Negative aims behind it, which puts the subject in the leading part of the frame and the marbles behind it in the rest.")]
        public float alongOffset;
        [Tooltip("Metres above the track the camera looks.")]
        public float lift;

        public TimerShot(float yawOffsetDeg, float pitchDeg, float distance, float alongOffset = 0f, float lift = 0.5f)
        {
            this.yawOffsetDeg = yawOffsetDeg;
            this.pitchDeg = pitchDeg;
            this.distance = distance;
            this.alongOffset = alongOffset;
            this.lift = lift;
        }
    }

    /// <summary>Every tunable of the timer camera. Pure data; the director owns an instance, the core reads it.</summary>
    [Serializable]
    public sealed class TimerCameraSettings
    {
        [Header("Follow shots (a rider marble, or a visited marble)")]
        [Tooltip("High chase from behind: pitch ~50, so the side walls never hide the marbles. Field renamed (chaseShot) so scenes pick up the new defaults.")]
        public TimerShot chaseShot = new TimerShot(10f, 50f, 11f, -1.8f, 0.4f);
        [Tooltip("Near-overhead variant, offset to the outside of bends when there is one.")]
        public TimerShot overheadShot = new TimerShot(20f, 68f, 10f, -1.4f, 0.4f);
        [Tooltip("Setup push-in start and end distance, pitch, yaw offset and the lift above the hopper.")]
        public float setupDistanceStart = 20f;
        public float setupDistanceEnd = 11f;
        public float setupPitchDeg = 55f;
        public float setupYawOffset = 35f;
        public float setupLift = 1.2f;
        [Tooltip("Collector shot (the last seconds, and the occasional visit). Distance is before the aspect boost.")]
        public TimerShot collectorShot = new TimerShot(35f, 45f, 13f, 0f, 1.5f);
        [Tooltip("Metres beyond the finish line the collector shot looks.")]
        public float collectorAhead = 1.5f;
        [Tooltip("Slow yaw drift on the collector shot, degrees per second.")]
        public float collectorDriftDeg = 2f;

        [Header("Schedule (all seconds are simulated seconds)")]
        [Tooltip("A follow shot of the same rider is re-cut with the other variant after this long.")]
        [Min(1f)] public float shotMin = 9f;
        [Min(1f)] public float shotMax = 14f;
        [Tooltip("Seconds of follow between visits to somewhere else on the course.")]
        [Min(1f)] public float visitDelayMin = 18f;
        [Min(1f)] public float visitDelayMax = 30f;
        [Min(1f)] public float visitMin = 5f;
        [Min(1f)] public float visitMax = 8f;
        [Tooltip("No voluntary cut sooner than this after the previous one. (The rider or visited marble being collected, and the last seconds, cut regardless.)")]
        [Min(1f)] public float minCutGap = 8f;
        [Tooltip("Seconds before 0:00 the camera cuts to the collector and stays there.")]
        public float finalCollectorSeconds = 8f;
        [Tooltip("Seconds the camera keeps following a marble after it is collected, before cutting away.")]
        public float collectGrace = 1f;
        [Tooltip("A new rider is picked from marbles in this first fraction of the course.")]
        [Range(0.05f, 1f)] public float riderPickFraction = 0.3f;
        [Tooltip("Metres: marbles within this radius behind a candidate rider count as 'a pack behind it'.")]
        public float behindRadius = 6f;
        [Tooltip("A visited marble is at least this many metres along the course from the rider.")]
        public float visitMinSeparation = 60f;
        [Tooltip("A zone visit (hopper, peg field, helix) takes the marble nearest the zone only if within this many metres of it.")]
        public float visitZoneRadius = 45f;
        [Tooltip("Mixed with the runner seed. Every 'random' choice is a hash of both, so the same seed films the same.")]
        public int tourSeed = 17;

        [Header("Rail (pre-smoothed course)")]
        [Min(0.1f)] public float railSpacing = 0.5f;
        [Min(0f)] public float railSmoothRadius = 3f;
        [Min(0)] public int railSmoothPasses = 2;
        [Tooltip("Heading is the rail tangent over +/- this many metres: wide enough that a switchback reads as one turn.")]
        [Min(0.5f)] public float tangentWindow = 11f;
        [Tooltip("Samples either side of the last projection searched when projecting the subject onto the rail.")]
        [Min(4)] public int projectionWindow = 60;

        [Header("Subject filters")]
        [Tooltip("Seconds of the low-pass on the subject's observed speed along the rail (feed-forward for the arc-length spring).")]
        public float speedTau = 0.6f;
        [Tooltip("Seconds of the low-pass on the lateral offset toward the real body.")]
        public float lateralTau = 1f;
        [Tooltip("Metres: the most the aim point may leave the rail toward the real body.")]
        public float lateralClamp = 1.5f;

        [Header("Pose springs")]
        [Tooltip("Subject arc length along the rail (metres). Velocity cap is the fastest the camera chases.")]
        public TimerSpringLimits riderSpring = new TimerSpringLimits(1.2f, 14f, 6f);
        public TimerSpringLimits focusSpring = new TimerSpringLimits(1.6f, 30f, 14f);
        [Tooltip("Degrees.")]
        public TimerSpringLimits yawSpring = new TimerSpringLimits(1.5f, 35f, 40f);
        [Tooltip("Degrees.")]
        public TimerSpringLimits pitchSpring = new TimerSpringLimits(1.3f, 14f, 20f);
        public TimerSpringLimits distanceSpring = new TimerSpringLimits(1.1f, 14f, 8f);
        [Tooltip("Hard floor on the pitch spring for follow shots and visits: the camera is never level with the walls.")]
        [Range(20f, 85f)] public float followMinPitch = 45f;
        [Tooltip("Hard floor for the collector shot, so you look down into the glass.")]
        [Range(20f, 85f)] public float collectorMinPitch = 40f;
        [Tooltip("Hard floor for the setup shot.")]
        [Range(20f, 85f)] public float setupMinPitch = 40f;

        [Header("Framing")]
        [Min(1f)] public float minDistance = 6f;
        [Min(1f)] public float maxDistance = 24f;
        [Tooltip("The subject-in-view guard may push the distance past maxDistance up to this.")]
        public float widenMaxDistance = 32f;
        [Tooltip("Portrait frames get up to this multiple of the shot distance.")]
        [Range(1f, 2f)] public float maxAspectBoost = 1.4f;
        [Tooltip("The subject's real position must stay inside this fraction of the frame (inner 80% by default).")]
        [Range(0.3f, 1f)] public float safeHeight = 0.8f;
        [Range(0.3f, 1f)] public float safeWidth = 0.8f;
        [Tooltip("Metres added to the distance the guard asks for.")]
        public float widenMargin = 0.4f;
        public float widenRiseTau = 0.15f;
        public float widenFallTau = 1.5f;

        [Header("Robustness")]
        [Min(0.001f)] public float maxFrameDt = 0.1f;
        [Min(30f)] public float substepHz = 120f;

        public TimerShot Variant(int variant)
        {
            return variant == 0 ? chaseShot : overheadShot;
        }
    }

    /// <summary>A stretch of the course worth an orbit shot (a peg field, a helix), in rail distance.</summary>
    public struct TimerFeature
    {
        public string label;
        public float start;
        public float end;

        public float Mid => (start + end) * 0.5f;

        public float Length => end - start;
    }

    public static class TimerCourseFeatures
    {
        /// <summary>
        /// Finds peg fields and helices from the layout's sections. Section lengths are estimated (arc length over the slope for
        /// curves) and rescaled so they add up to the real path length, which is close enough to aim an orbit at.
        /// </summary>
        public static List<TimerFeature> Find(TrackLayout layout, float totalLength)
        {
            var features = new List<TimerFeature>();
            if (layout == null || layout.sections == null || layout.sections.Length == 0 || totalLength <= 0f)
            {
                return features;
            }

            TrackSection[] sections = layout.sections;
            int n = sections.Length;
            var length = new float[n];
            var start = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                TrackSection s = sections[i];
                if (s.kind == TrackSectionKind.Curve)
                {
                    float arc = Mathf.Abs(s.radius * s.arcDegrees * Mathf.Deg2Rad);
                    length[i] = arc / Mathf.Max(0.3f, Mathf.Cos(s.slopeDegrees * Mathf.Deg2Rad));
                }
                else
                {
                    length[i] = Mathf.Max(0f, s.length);
                }

                start[i] = sum;
                sum += length[i];
            }

            float scale = sum > 1e-3f ? totalLength / sum : 1f;
            var used = new bool[n];

            if (layout.obstacles != null)
            {
                foreach (TrackObstacle o in layout.obstacles)
                {
                    if ((o.kind != ObstacleKind.PegGrid && o.kind != ObstacleKind.PegPyramid) || o.section < 0 || o.section >= n)
                    {
                        continue;
                    }

                    float sectionStart = start[o.section] * scale;
                    float sectionEnd = sectionStart + length[o.section] * scale;
                    float a = Mathf.Clamp(sectionStart + o.offset * scale, sectionStart, sectionEnd);
                    float b = Mathf.Min(sectionEnd, a + Mathf.Max(8f, o.count * o.spacing * scale));
                    features.Add(new TimerFeature { label = string.IsNullOrEmpty(o.label) ? "Pegs" : o.label, start = a, end = b });
                    used[o.section] = true;
                }
            }

            for (int i = 0; i < n; i++)
            {
                TrackSection s = sections[i];
                if (!used[i] && s.kind == TrackSectionKind.Curve && Mathf.Abs(s.arcDegrees) >= 360f)
                {
                    features.Add(new TimerFeature { label = s.label, start = start[i] * scale, end = (start[i] + length[i]) * scale });
                }
            }

            features.Sort((x, y) => x.start.CompareTo(y.start));
            return features;
        }
    }

    /// <summary>
    /// The course as a pre-smoothed polyline sampled at a fixed arc-length step. Parametrised by sample index, so a
    /// distance <c>d</c> here is the same distance along the raw course (smoothing never remaps it).
    /// </summary>
    public sealed class CourseRail
    {
        readonly Vector3[] _p;
        readonly float _spacing;

        CourseRail(Vector3[] points, float spacing)
        {
            _p = points;
            _spacing = spacing;
            Vector3 min = points[0];
            Vector3 max = points[0];
            for (int i = 1; i < points.Length; i++)
            {
                min = Vector3.Min(min, points[i]);
                max = Vector3.Max(max, points[i]);
            }

            BoundsCenter = (min + max) * 0.5f;
            BoundsRadius = (max - min).magnitude * 0.5f;
            Vector3 overall = points[points.Length - 1] - points[0];
            overall.y = 0f;
            OverallYaw = overall.sqrMagnitude > 1e-6f ? Mathf.Atan2(overall.x, overall.z) * Mathf.Rad2Deg : 0f;
        }

        public int Count => _p.Length;

        public float Length => (_p.Length - 1) * _spacing;

        public Vector3 BoundsCenter { get; }

        public float BoundsRadius { get; }

        public float OverallYaw { get; }

        /// <summary>Raw samples every <paramref name="spacing"/> metres of course distance, smoothed here.</summary>
        public static CourseRail FromSamples(Vector3[] raw, float spacing, float smoothRadius, int passes)
        {
            if (raw.Length < 2)
            {
                throw new ArgumentException("A rail needs at least two samples.");
            }

            var current = (Vector3[])raw.Clone();
            int radius = Mathf.RoundToInt(smoothRadius / spacing);
            var next = new Vector3[current.Length];
            for (int pass = 0; pass < passes && radius > 0; pass++)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    Vector3 sum = Vector3.zero;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        sum += current[Mathf.Clamp(i + k, 0, current.Length - 1)];
                        count++;
                    }

                    next[i] = sum / count;
                }

                next[0] = raw[0];
                next[next.Length - 1] = raw[raw.Length - 1];
                Vector3[] swap = current;
                current = next;
                next = swap;
            }

            return new CourseRail(current, spacing);
        }

        public Vector3 PositionAt(float d)
        {
            float f = Mathf.Clamp(d / _spacing, 0f, _p.Length - 1);
            int a = Mathf.Min((int)f, _p.Length - 2);
            return Vector3.Lerp(_p[a], _p[a + 1], f - a);
        }

        public Vector3 TangentAt(float d, float window)
        {
            Vector3 t = PositionAt(d + window) - PositionAt(d - window);
            return t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.forward;
        }

        /// <summary>Heading of the horizontal part of the tangent, so a steep drop never turns into a spin.</summary>
        public float TangentYawDeg(float d, float window)
        {
            Vector3 t = TangentAt(d, window);
            t.y = 0f;
            return t.sqrMagnitude > 1e-8f ? Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg : OverallYaw;
        }

        /// <summary>Course distance of the rail point nearest <paramref name="position"/>, searched near <paramref name="hint"/> (a sample index).</summary>
        public float Project(Vector3 position, ref int hint, int window)
        {
            int n = _p.Length;
            hint = Mathf.Clamp(hint, 0, n - 1);
            int start = Mathf.Max(0, hint - window);
            int end = Mathf.Min(n - 1, hint + window);
            int best = hint;
            float bestSqr = float.MaxValue;
            for (int i = start; i <= end; i++)
            {
                float sqr = (_p[i] - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            hint = best;
            int a = Mathf.Min(best, n - 2);
            Vector3 segment = _p[a + 1] - _p[a];
            float sqrLength = segment.sqrMagnitude;
            float t = sqrLength > 1e-10f ? Vector3.Dot(position - _p[a], segment) / sqrLength : 0f;
            return (a + Mathf.Clamp(t, -1f, 1f)) * _spacing;
        }
    }


    /// <summary>The marbles as the camera core sees them. Read-only.</summary>
    public interface ITimerMarbles
    {
        int Count { get; }

        bool OnTrack(int i);

        Vector3 Position(int i);
    }

    /// <summary>End-of-frame view of the run that the camera core consumes.</summary>
    public struct TimerCameraObservation
    {
        public float dt;
        public float elapsed;
        public TimerPhase phase;
        public bool released;
        public float remaining;
        public float setupRemaining;
        public ITimerMarbles marbles;
        public Vector3 hopper;
        public Vector3 collector;
        public float aspect;
        public float verticalFovDeg;
    }

    /// <summary>
    /// Pure camera maths for the <c>timer</c> course: no MonoBehaviour, Physics, Time or UnityEngine.Random, so it can run over a
    /// synthetic trace. The camera rides with one RIDER marble down the whole course (framed as the front runner with the
    /// marbles behind it in view), occasionally cutting to a VISITED marble elsewhere, and cutting to the collector for the last
    /// seconds. Inside a shot everything is continuous: the subject's arc length along a pre-smoothed rail is filtered by a
    /// critically damped spring (never the raw body), heading comes from the rail tangent (never velocity), a heavily filtered
    /// clamped offset leans toward the real body, one pose of springs with velocity and acceleration caps, fixed substeps. A
    /// guard widens the distance (through its spring) whenever the subject's real position leaves the inner part of the frame.
    /// Between shots the camera cuts cleanly. Every random choice hashes the seed, so the same seed films the same.
    /// </summary>
    public sealed class TimerCameraCore
    {
        enum Mode { Setup, Subject, Collector }

        readonly TimerCameraSettings _s;
        readonly CourseRail _rail;
        readonly IReadOnlyList<TimerFeature> _features;
        readonly float _startD;
        readonly float _finishD;

        int _seed;
        bool _init;
        float _tanV;
        float _tanH;
        float _boost = 1f;
        Vector3 _collector;
        Vector3 _hopper;
        ITimerMarbles _marbles;
        float[] _dBuf = Array.Empty<float>();

        // Shot / schedule state (frame level, driven by simulated time).
        Mode _mode;
        bool _finaleShot;
        bool _isVisit;
        int _subject = -1;
        int _rider = -1;
        bool _firstRiderDone;
        bool _subValid;
        bool _pendingSide;
        int _shotIndex;
        int _variant;
        float _sign = 1f;
        float _now;
        float _cutAt;
        float _lostAt = -1f;
        float _shotLen;
        float _visitLen;
        float _nextVisitAt = -1f;
        int _pickCounter;
        float _setupTotal;
        float _setupProgress;

        // Subject state.
        int _projHint;
        float _subRaw;
        float _prevSubRaw;
        float _subSpeed;
        Vector3 _subPos;
        Vector3 _offset;

        // Substep state.
        float _sd;
        float _sdV;
        float _drift;
        float _needF;
        Vector3 _focus;
        Vector3 _focusV;
        Vector3 _prevFocusTarget;
        float _yaw;
        float _yawV;
        float _yawTarget;
        float _prevYawTarget;
        float _pitch;
        float _pitchV;
        float _prevPitchTarget;
        float _dist;
        float _distV;
        float _prevDistTarget;

        public TimerCameraCore(TimerCameraSettings settings, CourseRail rail, float startDistance, float finishDistance,
            IReadOnlyList<TimerFeature> features, int seed = 0)
        {
            _s = settings;
            _rail = rail;
            _features = features ?? Array.Empty<TimerFeature>();
            _finishD = Mathf.Min(finishDistance, rail.Length);
            _startD = Mathf.Clamp(startDistance, 0f, _finishD);
            _seed = seed;
            Reset();
        }

        public Vector3 Focus => _focus;

        public float Distance => _dist;

        public float YawDeg => _yaw;

        public float PitchDeg => _pitch;

        public Quaternion Rotation => Quaternion.Euler(_pitch, _yaw, 0f);

        public Vector3 Position => _focus - Rotation * Vector3.forward * _dist;

        public int CutCount { get; private set; }

        public int VisitCount { get; private set; }

        public int RiderCount { get; private set; }

        public bool HasSubject => _mode == Mode.Subject && _subValid;

        public bool IsVisit => _mode == Mode.Subject && _isVisit;

        public Vector3 SubjectPosition => _subPos;

        public int Subject => _mode == Mode.Subject ? _subject : -1;

        public float SecondsSinceCut => _now - _cutAt;

        public TimerShotKind Kind => _mode == Mode.Setup ? TimerShotKind.Setup
            : _mode == Mode.Collector ? TimerShotKind.Collector
            : _variant == 0 ? TimerShotKind.Chase : TimerShotKind.Overhead;

        /// <summary>Set before the run starts; every random choice is a hash of this and <see cref="TimerCameraSettings.tourSeed"/>.</summary>
        public void SetSeed(int seed)
        {
            if (seed != _seed)
            {
                _seed = seed;
                _pickCounter = 0;
            }
        }

        public void Reset()
        {
            _init = false;
            _mode = Mode.Setup;
            _finaleShot = false;
            _isVisit = false;
            _subject = -1;
            _rider = -1;
            _firstRiderDone = false;
            _subValid = false;
            _pendingSide = false;
            _shotIndex = 0;
            _variant = 0;
            _sign = 1f;
            _now = 0f;
            _cutAt = 0f;
            _lostAt = -1f;
            _nextVisitAt = -1f;
            _pickCounter = 0;
            _setupTotal = 0f;
            _setupProgress = 0f;
            _subSpeed = 7f;
            _offset = Vector3.zero;
            _drift = 0f;
            _needF = 0f;
            CutCount = 0;
            VisitCount = 0;
            RiderCount = 0;
        }

        public void Step(in TimerCameraObservation o)
        {
            float dt = Mathf.Clamp(o.dt, 0f, _s.maxFrameDt);
            float tanHalf = Mathf.Tan(o.verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            _tanV = tanHalf * _s.safeHeight;
            _tanH = tanHalf * o.aspect * _s.safeWidth;
            _boost = Mathf.Clamp(16f / 9f / Mathf.Max(0.2f, o.aspect), 1f, _s.maxAspectBoost);
            _collector = o.collector + _rail.TangentAt(_finishD, _s.tangentWindow) * _s.collectorAhead;
            _hopper = o.hopper;
            _marbles = o.marbles;
            _now = o.elapsed;

            bool first = !_init;
            bool cut = Schedule(o) || first;
            if (cut)
            {
                CutCount++;
            }

            ObserveSubject(dt, cut);

            if (cut)
            {
                Snap();
                _init = true;
                return;
            }

            if (dt <= 0f)
            {
                return;
            }

            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * _s.substepHz));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Substep(h, (i + 1f) / steps);
            }

            _prevSubRaw = _subRaw;
        }

        // ---- Schedule (frame level; returns true when the camera must cut) --------------------------------

        bool Schedule(in TimerCameraObservation o)
        {
            if (o.phase == TimerPhase.Finale || o.phase == TimerPhase.Done || (o.released && o.remaining <= _s.finalCollectorSeconds))
            {
                if (_mode == Mode.Collector)
                {
                    _finaleShot = true;
                    return false;
                }

                return BeginCollector(true);
            }

            if (!o.released)
            {
                _setupTotal = Mathf.Max(_setupTotal, o.setupRemaining);
                _setupProgress = _setupTotal > 0.01f ? Mathf.Clamp01(1f - o.setupRemaining / _setupTotal) : 1f;
                if (_mode != Mode.Setup || !_init)
                {
                    _mode = Mode.Setup;
                    _subject = -1;
                    _sign = ChooseSide(_hopper, _rail.TangentYawDeg(_startD, _s.tangentWindow), _startD,
                        new TimerShot(_s.setupYawOffset, _s.setupPitchDeg, _s.setupDistanceEnd), _s.setupDistanceEnd);
                    _cutAt = _now;
                    return true;
                }

                return false;
            }

            if (_mode == Mode.Setup)
            {
                // Released: ride the first marble (the true leader), then whoever the seeded pick chooses. Glide, no cut.
                _setupProgress = 1f;
                int idx = !_firstRiderDone && _marbles.Count > 0 && _marbles.OnTrack(0) ? 0 : PickRider();
                if (idx >= 0)
                {
                    _firstRiderDone = true;
                    BeginSubject(idx, false);
                }

                return false;
            }

            if (_mode == Mode.Collector)
            {
                if (_now - _cutAt >= _visitLen)
                {
                    return ReturnToRider();
                }

                return false;
            }

            bool lostExpired = false;
            if (!_marbles.OnTrack(_subject))
            {
                if (_lostAt < 0f)
                {
                    _lostAt = _now;
                }

                lostExpired = _now - _lostAt >= _s.collectGrace;
            }

            float since = _now - _cutAt;
            if (_isVisit)
            {
                return lostExpired || since >= _visitLen ? ReturnToRider() : false;
            }

            if (lostExpired)
            {
                return NewRider();
            }

            if (_nextVisitAt >= 0f && _now >= _nextVisitAt && since >= _s.minCutGap
                && o.remaining > _s.finalCollectorSeconds + _s.visitMax + 2f)
            {
                if (TryStartVisit())
                {
                    return true;
                }

                _nextVisitAt = _now + 4f;
            }

            if (since >= _shotLen && since >= _s.minCutGap && o.remaining > _s.finalCollectorSeconds + 1f)
            {
                BeginSubject(_subject, false);
                return true;
            }

            return false;
        }

        bool ReturnToRider()
        {
            _nextVisitAt = _now + NextVisitDelay();
            if (_rider >= 0 && _marbles.OnTrack(_rider))
            {
                int hint = 0;
                float d = _rail.Project(_marbles.Position(_rider), ref hint, _rail.Count);
                if (d < _startD + 0.85f * (_finishD - _startD))
                {
                    BeginSubject(_rider, false);
                    return true;
                }
            }

            return NewRider();
        }

        bool NewRider()
        {
            int idx = PickRider();
            if (idx < 0)
            {
                _mode = Mode.Setup;
                _setupProgress = 1f;
                _subject = -1;
                _cutAt = _now;
                return true;
            }

            RiderCount++;
            BeginSubject(idx, false);
            return true;
        }

        void BeginSubject(int idx, bool visit)
        {
            _mode = Mode.Subject;
            _finaleShot = false;
            _isVisit = visit;
            _subject = idx;
            _subValid = false;
            _pendingSide = true;
            _lostAt = -1f;
            _cutAt = _now;
            _variant = _shotIndex++ & 1;
            if (!visit)
            {
                _rider = idx;
                _shotLen = Mathf.Lerp(_s.shotMin, _s.shotMax, Hash01(++_pickCounter));
                if (_nextVisitAt < 0f)
                {
                    _nextVisitAt = _now + NextVisitDelay();
                }
            }
            else
            {
                _visitLen = Mathf.Lerp(_s.visitMin, _s.visitMax, Hash01(++_pickCounter));
            }
        }

        bool BeginCollector(bool finale)
        {
            _mode = Mode.Collector;
            _finaleShot = finale;
            _subject = -1;
            _cutAt = _now;
            _drift = 0f;
            _sign = ChooseSide(_collector + Vector3.up * _s.collectorShot.lift, _rail.TangentYawDeg(_finishD, _s.tangentWindow), _finishD,
                _s.collectorShot, _s.collectorShot.distance);
            if (!finale)
            {
                _visitLen = Mathf.Lerp(_s.visitMin, _s.visitMax, Hash01(++_pickCounter));
            }

            return true;
        }

        float NextVisitDelay()
        {
            return Mathf.Lerp(_s.visitDelayMin, _s.visitDelayMax, Hash01(++_pickCounter));
        }

        // Where to look: the collector filling, the pack near the hopper, a peg field or helix, or any marble far from the rider.
        bool TryStartVisit()
        {
            int n = _marbles.Count;
            EvaluateProgress(n);
            float riderD = _subRaw;

            var zones = new List<float>(4 + _features.Count);
            zones.Add(float.NaN);
            if (Mathf.Abs(_finishD - riderD) >= _s.visitMinSeparation)
            {
                zones.Add(_finishD);
            }

            zones.Add(_startD + 20f);
            for (int i = 0; i < _features.Count; i++)
            {
                zones.Add(_features[i].Mid);
            }

            float zone = zones[Mathf.Min(zones.Count - 1, (int)(Hash01(++_pickCounter) * zones.Count))];
            if (Mathf.Approximately(zone, _finishD) && !float.IsNaN(zone))
            {
                VisitCount++;
                return BeginCollector(false);
            }

            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = _dBuf[i];
                if (float.IsNaN(d) || i == _rider || Mathf.Abs(d - riderD) < _s.visitMinSeparation)
                {
                    continue;
                }

                float score = float.IsNaN(zone) ? Hash01(++_pickCounter) : Mathf.Abs(d - zone);
                if (!float.IsNaN(zone) && score > _s.visitZoneRadius)
                {
                    continue;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            if (best < 0 && !float.IsNaN(zone))
            {
                for (int i = 0; i < n; i++)
                {
                    float d = _dBuf[i];
                    if (float.IsNaN(d) || i == _rider || Mathf.Abs(d - riderD) < _s.visitMinSeparation)
                    {
                        continue;
                    }

                    float score = Hash01(++_pickCounter);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
            }

            if (best < 0)
            {
                return false;
            }

            VisitCount++;
            BeginSubject(best, true);
            return true;
        }

        // A marble in the first part of the course, preferring one with marbles close behind it, picked with the seeded hash.
        int PickRider()
        {
            int n = _marbles.Count;
            EvaluateProgress(n);
            float lo = _startD - 5f;
            float hi = _startD + _s.riderPickFraction * (_finishD - _startD);
            float behindSqr = _s.behindRadius * _s.behindRadius;
            int best = -1;
            float bestScore = float.MinValue;
            int fallback = -1;
            float fallbackD = float.MaxValue;
            int pick = ++_pickCounter;
            for (int i = 0; i < n; i++)
            {
                float d = _dBuf[i];
                if (float.IsNaN(d))
                {
                    continue;
                }

                if (d < fallbackD)
                {
                    fallbackD = d;
                    fallback = i;
                }

                if (d < lo || d > hi)
                {
                    continue;
                }

                Vector3 p = _marbles.Position(i);
                int behind = 0;
                for (int j = 0; j < n; j++)
                {
                    float dj = _dBuf[j];
                    if (j != i && !float.IsNaN(dj) && dj < d && (_marbles.Position(j) - p).sqrMagnitude < behindSqr)
                    {
                        behind++;
                    }
                }

                float score = Mathf.Min(behind, 6) + Hash01(pick * 7919 + i) * 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best >= 0 ? best : fallback;
        }

        void EvaluateProgress(int n)
        {
            if (_dBuf.Length < n)
            {
                _dBuf = new float[n];
            }

            for (int i = 0; i < n; i++)
            {
                if (_marbles.OnTrack(i))
                {
                    int hint = 0;
                    _dBuf[i] = _rail.Project(_marbles.Position(i), ref hint, _rail.Count);
                }
                else
                {
                    _dBuf[i] = float.NaN;
                }
            }
        }

        float Hash01(int i)
        {
            unchecked
            {
                uint h = (uint)(i + 1) * 2654435761u ^ (uint)_s.tourSeed * 40503u ^ (uint)_seed * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // Of the two sides of the course, the one whose camera point stays further from the rest of the course, so the camera
        // is not boxed in by a switchback; near ties go to the side further from the bounds centre.
        float ChooseSide(Vector3 focus, float tangentYawDeg, float centreD, TimerShot shot, float distance)
        {
            var score = new float[2];
            var away = new float[2];
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                Quaternion rot = Quaternion.Euler(shot.pitchDeg, tangentYawDeg + sign * shot.yawOffsetDeg, 0f);
                Vector3 cam = focus - rot * Vector3.forward * distance;
                float s = float.MaxValue;
                for (float d = centreD - 60f; d <= centreD + 60f; d += 1.5f)
                {
                    if (Mathf.Abs(d - centreD) < 12f || d < 0f || d > _rail.Length)
                    {
                        continue;
                    }

                    s = Mathf.Min(s, (_rail.PositionAt(d) - cam).sqrMagnitude);
                }

                score[side] = s;
                Vector3 c = cam - _rail.BoundsCenter;
                c.y = 0f;
                away[side] = c.sqrMagnitude;
            }

            float hi = Mathf.Max(score[0], score[1]);
            if (Mathf.Abs(score[0] - score[1]) < 0.1f * hi)
            {
                return away[0] >= away[1] ? 1f : -1f;
            }

            return score[0] > score[1] ? 1f : -1f;
        }

        // ---- Subject (frame level) ------------------------------------------------------------------------

        void ObserveSubject(float dt, bool cut)
        {
            if (_mode != Mode.Subject)
            {
                return;
            }

            Vector3 pos = _marbles.Position(_subject);
            _subPos = pos;
            if (!_subValid)
            {
                int hint = 0;
                float d = _rail.Project(pos, ref hint, _rail.Count);
                _projHint = hint;
                _subRaw = d;
                _prevSubRaw = d;
                _sd = d;
                _sdV = 0f;
                _subSpeed = 7f;
                _offset = Vector3.ClampMagnitude(pos - _rail.PositionAt(d), _s.lateralClamp);
                _subValid = true;
            }
            else
            {
                float proj = _rail.Project(pos, ref _projHint, _s.projectionWindow);
                float instant = 0f;
                if (proj > _subRaw)
                {
                    instant = dt > 1e-4f ? Mathf.Min((proj - _subRaw) / dt, 20f) : _subSpeed;
                    _subRaw = proj;
                }

                if (dt > 1e-4f)
                {
                    _subSpeed += (instant - _subSpeed) * (1f - Mathf.Exp(-dt / Mathf.Max(0.05f, _s.speedTau)));
                }

                Vector3 off = Vector3.ClampMagnitude(pos - _rail.PositionAt(proj), _s.lateralClamp);
                _offset = Vector3.Lerp(_offset, off, 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, _s.lateralTau)));
            }

            if (_pendingSide)
            {
                _pendingSide = false;
                TimerShot shot = _s.Variant(_variant);
                Vector3 aim = _rail.PositionAt(_sd + shot.alongOffset) + Vector3.up * shot.lift;
                _sign = ChooseSide(aim, _rail.TangentYawDeg(_sd, _s.tangentWindow), _sd, shot, ClampDistance(shot.distance));
                if (_variant == 1)
                {
                    // Overhead sits on the outside of a bend (positive yaw offset = camera on the left = outside of a right turn).
                    float turn = Mathf.DeltaAngle(_rail.TangentYawDeg(_sd - 8f, _s.tangentWindow), _rail.TangentYawDeg(_sd + 8f, _s.tangentWindow));
                    if (Mathf.Abs(turn) > 6f)
                    {
                        _sign = turn > 0f ? 1f : -1f;
                    }
                }
            }
        }

        // ---- Pose -----------------------------------------------------------------------------------------

        void Snap()
        {
            _yawTarget = _init ? _yaw : _rail.OverallYaw;
            ComputeTarget(out Vector3 focus, out float yaw, out float pitch, out float distBase);
            _focus = focus;
            _focusV = Vector3.zero;
            _yaw = yaw;
            _yawV = 0f;
            _pitch = pitch;
            _pitchV = 0f;
            _needF = _mode == Mode.Subject ? RequiredDistance() : 0f;
            float dist = TargetDistance(distBase);
            _dist = dist;
            _distV = 0f;
            _prevFocusTarget = focus;
            _prevYawTarget = yaw;
            _prevPitchTarget = pitch;
            _prevDistTarget = dist;
            _sdV = 0f;
        }

        void Substep(float h, float frameFraction)
        {
            if (_mode == Mode.Subject && _subValid)
            {
                TimerSpringLimits r = _s.riderSpring;
                float target = Mathf.Lerp(_prevSubRaw, _subRaw, frameFraction);
                float speed = Mathf.Clamp(_subSpeed, 0f, r.maxVelocity);
                CameraSpring.Step(ref _sd, ref _sdV, target, speed, r.omega, -r.maxVelocity, r.maxVelocity, r.maxAcceleration, h);
            }

            if (_mode == Mode.Collector)
            {
                _drift += _s.collectorDriftDeg * h;
            }

            // Target from the filter state as it stands at the end of this substep.
            ComputeTarget(out Vector3 focusT, out float yawT, out float pitchT, out float distBase);
            float distT = distBase;
            if (_mode == Mode.Subject)
            {
                float need = RequiredDistance();
                float tau = need > _needF ? _s.widenRiseTau : _s.widenFallTau;
                _needF += (need - _needF) * (1f - Mathf.Exp(-h / Mathf.Max(0.01f, tau)));
                distT = TargetDistance(distBase);
            }

            float inv = 1f / h;
            CameraSpring.Step(ref _focus, ref _focusV, focusT, (focusT - _prevFocusTarget) * inv,
                _s.focusSpring.omega, _s.focusSpring.maxVelocity, _s.focusSpring.maxAcceleration, h);
            CameraSpring.Step(ref _yaw, ref _yawV, yawT, (yawT - _prevYawTarget) * inv,
                _s.yawSpring.omega, -_s.yawSpring.maxVelocity, _s.yawSpring.maxVelocity, _s.yawSpring.maxAcceleration, h);
            CameraSpring.Step(ref _pitch, ref _pitchV, pitchT, (pitchT - _prevPitchTarget) * inv,
                _s.pitchSpring.omega, -_s.pitchSpring.maxVelocity, _s.pitchSpring.maxVelocity, _s.pitchSpring.maxAcceleration, h);
            CameraSpring.Step(ref _dist, ref _distV, distT, (distT - _prevDistTarget) * inv,
                _s.distanceSpring.omega, -_s.distanceSpring.maxVelocity, _s.distanceSpring.maxVelocity, _s.distanceSpring.maxAcceleration, h);

            // Hard floor on pose pitch (not a position clamp): whatever the spring did, never lower than the floor.
            float floor = PitchFloor();
            if (_pitch < floor)
            {
                _pitch = floor;
                _pitchV = Mathf.Max(0f, _pitchV);
            }
            else if (_pitch > 85f)
            {
                _pitch = 85f;
                _pitchV = Mathf.Min(0f, _pitchV);
            }

            _prevFocusTarget = focusT;
            _prevYawTarget = yawT;
            _prevPitchTarget = pitchT;
            _prevDistTarget = distT;
        }

        // The distance at which the subject's real position sits inside the safe area, for the pose as it stands. Independent of
        // the distance itself (the aim point stays put), so it cannot feed back on itself.
        float RequiredDistance()
        {
            Vector3 rel = Quaternion.Inverse(Quaternion.Euler(_pitch, _yaw, 0f)) * (_subPos - _focus);
            return Mathf.Max(Mathf.Abs(rel.x) / Mathf.Max(0.01f, _tanH), Mathf.Abs(rel.y) / Mathf.Max(0.01f, _tanV)) - rel.z;
        }

        float TargetDistance(float baseDistance)
        {
            float widened = Mathf.Max(baseDistance, _needF + _s.widenMargin);
            return Mathf.Clamp(widened, _s.minDistance, Mathf.Max(_s.maxDistance, _s.widenMaxDistance));
        }

        void ComputeTarget(out Vector3 focus, out float yaw, out float pitch, out float dist)
        {
            float y;
            switch (_mode)
            {
                case Mode.Collector:
                {
                    TimerShot shot = _s.collectorShot;
                    focus = _collector + Vector3.up * shot.lift;
                    y = _rail.TangentYawDeg(_finishD, _s.tangentWindow) + _sign * shot.yawOffsetDeg + _sign * _drift;
                    pitch = shot.pitchDeg;
                    dist = ClampDistance(shot.distance);
                    break;
                }
                case Mode.Subject:
                {
                    TimerShot shot = _s.Variant(_variant);
                    focus = _rail.PositionAt(_sd + shot.alongOffset) + _offset + Vector3.up * shot.lift;
                    y = _rail.TangentYawDeg(_sd, _s.tangentWindow) + _sign * shot.yawOffsetDeg;
                    pitch = shot.pitchDeg;
                    dist = ClampDistance(shot.distance);
                    break;
                }
                default:
                {
                    focus = _hopper + Vector3.up * _s.setupLift;
                    y = _rail.TangentYawDeg(_startD, _s.tangentWindow) + _sign * _s.setupYawOffset;
                    pitch = _s.setupPitchDeg;
                    float push = CameraSpring.SmoothStep01(_setupProgress);
                    dist = Mathf.Max(_s.minDistance, Mathf.Lerp(_s.setupDistanceStart, _s.setupDistanceEnd, push) * _boost);
                    break;
                }
            }

            // Continuous unwrapped yaw so a 360 wrap never reaches the spring.
            yaw = _yawTarget + Mathf.DeltaAngle(_yawTarget, y);
            _yawTarget = yaw;
            pitch = Mathf.Clamp(pitch, PitchFloor(), 85f);
        }

        float PitchFloor()
        {
            return _mode == Mode.Collector ? _s.collectorMinPitch : _mode == Mode.Setup ? _s.setupMinPitch : _s.followMinPitch;
        }

        float ClampDistance(float distance)
        {
            return Mathf.Clamp(distance * _boost, _s.minDistance, _s.maxDistance);
        }

        // ---- Numeric check --------------------------------------------------------------------------------

        public struct CheckResult
        {
            public int frames;
            public int pops;
            public int cuts;
            public int visits;
            public int riders;
            public float maxSpeed;
            public float maxAcceleration;
            public float maxTurnRateDegPerSec;
            public float minDistance;
            public float minPitchDeg;
            public float maxDistance;
            public float subjectInSafePercent;
            public float subjectInFramePercent;

            public override string ToString()
            {
                return $"frames {frames}, cuts {cuts} (visits {visits}, riders {riders}), pops {pops}, max speed {maxSpeed:0.0} m/s, " +
                       $"max accel {maxAcceleration:0.0} m/s2, max turn {maxTurnRateDegPerSec:0.0} deg/s, distance {minDistance:0.0}-{maxDistance:0.0} m, min pitch {minPitchDeg:0.0} deg, " +
                       $"rider in safe area {subjectInSafePercent:0.0}% (in frame {subjectInFramePercent:0.0}%)";
            }
        }

        // A deterministic stand-in for the stream: a marble every `interval` seconds, each running the rail at a wobbling speed.
        sealed class SyntheticMarbles : ITimerMarbles
        {
            readonly CourseRail _rail;
            readonly float _start;
            readonly float _finish;
            readonly float _interval;
            readonly float[] _factor;
            readonly float[] _phase;
            public float Time;

            public SyntheticMarbles(CourseRail rail, float start, float finish, float speed, float interval, int count, int seed)
            {
                _rail = rail;
                _start = start;
                _finish = finish;
                _interval = interval;
                var rng = new System.Random(seed);
                _factor = new float[count];
                _phase = new float[count];
                for (int i = 0; i < count; i++)
                {
                    _factor[i] = speed * (0.9f + 0.2f * (float)rng.NextDouble());
                    _phase[i] = (float)rng.NextDouble() * 6.28f;
                }
            }

            public int Count => _factor.Length;

            float Dist(int i)
            {
                float rel = Time - i * _interval;
                return _start + Mathf.Max(0f, rel) * _factor[i] * (1f + 0.18f * Mathf.Sin(rel * 0.9f + _phase[i]));
            }

            public bool OnTrack(int i)
            {
                return Time >= i * _interval && Dist(i) < _finish;
            }

            public Vector3 Position(int i)
            {
                float rel = Mathf.Max(0f, Time - i * _interval);
                float d = Mathf.Min(Dist(i), _rail.Length);
                Vector3 t = _rail.TangentAt(d, 2f);
                Vector3 side = Vector3.Cross(Vector3.up, t).normalized;
                return _rail.PositionAt(d) + side * 0.6f * Mathf.Sin(rel * 1.7f + _phase[i]) +
                       Vector3.up * (0.25f + 0.2f * Mathf.Abs(Mathf.Sin(rel * 6f + _phase[i])));
            }
        }

        /// <summary>
        /// Runs the core over a synthetic stream and reports smoothness and framing numbers: pops, max angular rate and
        /// acceleration (frames on a cut are skipped, a cut is a cut) and how often the rider's real position sits inside the
        /// safe area (the first 0.5 s after a cut excluded). <paramref name="jitter"/> 0 gives a constant frame time, 0.5
        /// varies each frame by up to +/-50%. A harness can call this at 60 fps, 30 fps and jittery frame times, and with a
        /// portrait aspect of 9/16.
        /// </summary>
        public static CheckResult RunSyntheticCheck(TimerCameraSettings settings, CourseRail rail, float startDistance,
            float finishDistance, IReadOnlyList<TimerFeature> features, float frameDt, float jitter, int seed,
            float duration = 180f, float hold = 5f, float streamSpeed = 7.5f, float aspect = 16f / 9f, float fovDeg = 40f)
        {
            var core = new TimerCameraCore(settings, rail, startDistance, finishDistance, features, seed);
            var marbles = new SyntheticMarbles(rail, startDistance, finishDistance, streamSpeed, 0.64f, 400, seed);
            var rng = new System.Random(seed);
            var result = new CheckResult { minDistance = float.MaxValue, minPitchDeg = float.MaxValue };
            float total = hold + duration + 6f;
            float t = 0f;
            Vector3 lastPos = default;
            Vector3 lastVel = default;
            Quaternion lastRot = Quaternion.identity;
            int lastCuts = 0;
            bool lastWasCut = true;
            int frame = 0;
            int measured = 0;
            int safe = 0;
            int inFrame = 0;
            Vector3 collector = rail.PositionAt(finishDistance);
            Vector3 hopper = rail.PositionAt(startDistance);
            float tanHalf = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);

            while (t < total)
            {
                float dt = frameDt * (1f + jitter * (float)(rng.NextDouble() * 2.0 - 1.0));
                t += dt;
                float run = Mathf.Max(0f, t - hold);
                bool released = t >= hold;
                marbles.Time = run;
                float remaining = Mathf.Max(0f, duration - run);

                var obs = new TimerCameraObservation
                {
                    dt = dt,
                    elapsed = Mathf.Min(run, duration),
                    phase = !released ? TimerPhase.Setup : run < duration ? TimerPhase.Running : TimerPhase.Finale,
                    released = released,
                    remaining = remaining,
                    setupRemaining = released ? 0f : hold - t,
                    marbles = marbles,
                    hopper = hopper,
                    collector = collector,
                    aspect = aspect,
                    verticalFovDeg = fovDeg
                };

                core.Step(obs);
                Vector3 pos = core.Position;
                Quaternion rot = core.Rotation;
                bool cutNow = core.CutCount != lastCuts;
                lastCuts = core.CutCount;
                if (frame > 5 && !cutNow)
                {
                    Vector3 vel = (pos - lastPos) / dt;
                    float speed = vel.magnitude;
                    result.maxSpeed = Mathf.Max(result.maxSpeed, speed);
                    if (speed > 45f)
                    {
                        result.pops++;
                    }

                    if (!lastWasCut)
                    {
                        result.maxAcceleration = Mathf.Max(result.maxAcceleration, (vel - lastVel).magnitude / dt);
                    }

                    result.maxTurnRateDegPerSec = Mathf.Max(result.maxTurnRateDegPerSec, Quaternion.Angle(lastRot, rot) / dt);
                    lastVel = vel;
                }

                if (core.HasSubject && core.SecondsSinceCut >= 0.5f)
                {
                    Vector3 rel = Quaternion.Inverse(rot) * (core.SubjectPosition - pos);
                    measured++;
                    if (rel.z > 0.1f)
                    {
                        float x = Mathf.Abs(rel.x) / rel.z;
                        float y = Mathf.Abs(rel.y) / rel.z;
                        if (x <= tanHalf * aspect && y <= tanHalf)
                        {
                            inFrame++;
                        }

                        if (x <= tanHalf * aspect * 0.8f && y <= tanHalf * 0.8f)
                        {
                            safe++;
                        }
                    }
                }

                if (core.HasSubject || core.Kind == TimerShotKind.Collector)
                {
                    result.minPitchDeg = Mathf.Min(result.minPitchDeg, core.PitchDeg);
                }

                result.minDistance = Mathf.Min(result.minDistance, core.Distance);
                result.maxDistance = Mathf.Max(result.maxDistance, core.Distance);
                lastWasCut = cutNow;
                lastPos = pos;
                lastRot = rot;
                frame++;
            }

            result.frames = frame;
            result.cuts = core.CutCount;
            result.visits = core.VisitCount;
            result.riders = core.RiderCount;
            result.subjectInSafePercent = measured > 0 ? 100f * safe / measured : 0f;
            result.subjectInFramePercent = measured > 0 ? 100f * inFrame / measured : 0f;
            return result;
        }
    }
}
