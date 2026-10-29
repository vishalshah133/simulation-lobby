using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimulationLobby.Presentation;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    public enum DtProfile { Fixed60, Fixed30, Jitter }

    /// <summary>Everything measured about one offline camera run.</summary>
    public sealed class CameraMetrics
    {
        public string label;
        public float aspect;
        public int frames;
        public int racingFrames;
        public int pops;
        public double maxAngularVelocity, p95AngularVelocity;
        public double maxAngularAcceleration;
        public double maxAcceleration, p95Acceleration;
        public double jerkP99;
        public double maxSpeed;
        public double maxYawRate;
        public double leaderSafe, top3Safe, top6Safe;
        public double minDistance, minDistanceAll, minY;
        public double diameterP5Px;
        public double deviationPosP99 = double.NaN, deviationRotP99 = double.NaN;
        public int framingFrames;
        /// <summary>Times (s) of framing samples where the leading racer was outside the safe area.</summary>
        public readonly List<float> leaderMissTimes = new List<float>();

        // Trajectory for cross-profile comparison.
        public readonly List<float> times = new List<float>();
        public readonly List<Vector3> positions = new List<Vector3>();
        public readonly List<Quaternion> rotations = new List<Quaternion>();
        public readonly List<bool> racing = new List<bool>();
    }

    /// <summary>
    /// Offline smoothness test for the race camera: replays a trace (synthetic, or a recorded one) through
    /// <see cref="RaceCameraCore"/> at several frame-time profiles and measures acceleration, jerk, angular speed, pops,
    /// framing and cross-profile consistency against fixed thresholds. No scene, no physics, no play mode.
    /// </summary>
    public static class RaceCameraHarness
    {
        const float Fov = 38f;
        const float TanV = 0.34432761f; // tan(19 deg)

        // Synthetic trace ------------------------------------------------------------------------------------------

        /// <summary>
        /// Course header for runs without a scene: a stadium loop (two 16 m radius turns) through the sixteen segments of the
        /// current scene (station table of plans/videos/mach-002-camera-rework.md), with plausible heights.
        /// </summary>
        public static RaceTrace FallbackHeader()
        {
            string[] names = { "Start Gate", "Helix Slide", "Screw Lift 1", "Peg Wall", "Bucket Wheel", "Ski Jump", "Vortex Funnel", "Screw Lift 2" };
            // Entry/exit cumulative distances of the scene (stations then connectors).
            float[] len = { 5.4f, 4.6f, 10f, 3f, 27.8f, 6.1f, 5.8f, 3.1f, 10.8f, 8.4f, 10.6f, 10.9f, 7.5f, 23.3f, 27.8f, 8.0f };
            float[] yv = { 10f, 10f, 9.5f, 4.5f, 4f, 16f, 15f, 11f, 10.5f, 8f, 7.5f, 6f, 5f, 2f, 1.5f, 10f };
            float total = 0f;
            for (int i = 0; i < len.Length; i++)
            {
                total += len[i];
            }

            const float R = 16f;
            float A = (total - 2f * Mathf.PI * R) * 0.5f;
            var verts = new Vector3[16];
            float cum = 0f;
            for (int v = 0; v < 16; v++)
            {
                Vector3 p = StadiumPoint(cum, A, R);
                p.y = yv[v];
                verts[v] = p;
                cum += len[v];
            }

            var trace = new RaceTrace { vertices = verts, laps = 2, racerCount = 16, fixedDt = 0.01f };
            var track = new CourseTrack(verts);
            trace.length = track.Length;
            trace.stations = new RaceTrace.StationInfo[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 a = verts[2 * i];
                Vector3 b = verts[2 * i + 1];
                Vector3 f = b - a;
                f.y = 0f;
                if (f.sqrMagnitude < 1e-4f)
                {
                    f = track.Heading(track.VertexDistance(2 * i));
                }

                trace.stations[i] = new RaceTrace.StationInfo
                {
                    name = names[i],
                    forward = f.normalized,
                    focus = (a + b) * 0.5f + Vector3.up * 1.5f,
                    cameraOffset = i == 0 ? new Vector3(-3f, 4f, -6f) : new Vector3(0f, 3f, -6f),
                    hasFocus = true,
                    s0 = track.VertexDistance(2 * i),
                    s1 = track.VertexDistance(2 * i + 1),
                };
            }

            trace.gridSlots = new Vector3[16];
            Vector3 h0 = track.Heading(1f);
            Vector3 side = Vector3.Cross(Vector3.up, h0);
            Vector3 origin = track.Raw(1.5f) + Vector3.up * 0.3f;
            for (int slot = 0; slot < 16; slot++)
            {
                int row = slot / 4;
                int col = slot % 4;
                trace.gridSlots[slot] = origin - h0 * (row * 0.7f) + side * ((col - 1.5f) * 0.7f);
            }

            return trace;
        }

        static Vector3 StadiumPoint(float s, float A, float R)
        {
            float arc = Mathf.PI * R;
            float P = 2f * A + 2f * arc;
            s = CourseTrack.Mod(s, P);
            if (s < A)
            {
                return new Vector3(-R, 0f, s);
            }

            s -= A;
            if (s < arc)
            {
                float th = Mathf.PI - s / R;
                return new Vector3(R * Mathf.Cos(th), 0f, A + R * Mathf.Sin(th));
            }

            s -= arc;
            if (s < A)
            {
                return new Vector3(R, 0f, A - s);
            }

            s -= A;
            float th2 = -s / R;
            return new Vector3(R * Mathf.Cos(th2), 0f, R * Mathf.Sin(th2));
        }

        sealed class Racer
        {
            public float factor, phi;
            public float s;
            public float progress;
            public int recycles;
            public bool finished;
            public int state; // 0 normal, 1 hold to cannon, 2 arc, 3 resume
            public float stateStart;
            public Vector3 holdFrom;
            public float holdS;
            public float baseS;
            public int doomedLap; // lap index (0, 1) on which this racer misses the ski jump, -1 none
            public float s0;
        }

        static float Gauss(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        /// <summary>
        /// A seeded stand-in for a real run (100 Hz ticks): sixteen racers with realistic per-station speeds, weave, jitter, bounce,
        /// a helix coil and a vortex spiral, plus two ski-jump misses that go through the recycle cannon (the camera's hard cases).
        /// </summary>
        public static RaceTrace Synthetic(int seed, RaceTrace header, int laps = 2)
        {
            if (header == null)
            {
                header = FallbackHeader();
            }

            var rng = new System.Random(seed);
            var track = new CourseTrack(header.vertices);
            float L = track.Length;
            int n = header.racerCount;
            float dt = header.fixedDt;

            var trace = new RaceTrace
            {
                length = L,
                vertices = header.vertices,
                stations = header.stations,
                gridSlots = header.gridSlots,
                laps = laps,
                racerCount = n,
                fixedDt = dt,
            };

            // Station ranges.
            float helixLo = 0f, helixHi = 0f, vortexLo = 0f, vortexHi = 0f, skiExit = 0f;
            foreach (RaceTrace.StationInfo st in header.stations)
            {
                string nm = (st.name ?? "").ToLowerInvariant();
                if (nm.StartsWith("helix")) { helixLo = st.s0; helixHi = st.s1; }
                if (nm.StartsWith("vortex")) { vortexLo = st.s0; vortexHi = st.s1; }
                if (nm.StartsWith("ski")) { skiExit = st.s1; }
            }

            // Speed profile along the lap, box-smoothed so speed changes over a few metres like a ball does.
            const float tableDs = 0.25f;
            int tn = Mathf.CeilToInt(L / tableDs);
            var raw = new float[tn];
            for (int i = 0; i < tn; i++)
            {
                raw[i] = StationSpeed(header.stations, i * tableDs);
            }

            var speed = new float[tn];
            int box = Mathf.RoundToInt(2.5f / tableDs);
            for (int i = 0; i < tn; i++)
            {
                float sum = 0f;
                for (int k = -box; k <= box; k++)
                {
                    sum += raw[((i + k) % tn + tn) % tn];
                }

                speed[i] = sum / (2 * box + 1);
            }

            Func<float, float> speedAt = s =>
            {
                float f = CourseTrack.Mod(s, L) / tableDs;
                int i0 = Mathf.FloorToInt(f) % tn;
                return Mathf.Lerp(speed[i0], speed[(i0 + 1) % tn], f - Mathf.Floor(f));
            };

            // Racers.
            var perm = new int[n];
            for (int i = 0; i < n; i++) perm[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = perm[i]; perm[i] = perm[j]; perm[j] = tmp;
            }

            var racers = new Racer[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 slot = header.gridSlots.Length > 0 ? header.gridSlots[i % header.gridSlots.Length] : track.Raw(1f);
                float s0 = track.ProjectNear(slot, 0f, 12f, 12f);
                racers[i] = new Racer
                {
                    factor = 0.92f + 0.012f * perm[i],
                    phi = (float)(rng.NextDouble() * 2.0 * Math.PI),
                    s = s0,
                    s0 = s0,
                    progress = Mathf.Max(0f, s0) / L,
                    doomedLap = -1,
                };
            }

            const float goTime = 9f;
            Func<Racer, float, float> factorAt = (r, t) => r.factor * (1f + 0.08f * Mathf.Sin(0.3f * t + r.phi));

            // Pre-pass without events: who reaches the ski-jump exit in which order, per lap.
            var cross = new float[n, 2];
            {
                var sim = new float[n];
                for (int i = 0; i < n; i++) { sim[i] = racers[i].s0; cross[i, 0] = cross[i, 1] = float.MaxValue; }
                for (float t = goTime; t < goTime + 400f; t += dt)
                {
                    for (int i = 0; i < n; i++)
                    {
                        float before = sim[i];
                        sim[i] += speedAt(sim[i]) * factorAt(racers[i], t) * dt;
                        for (int lap = 0; lap < 2; lap++)
                        {
                            float mark = lap * L + skiExit;
                            if (before < mark && sim[i] >= mark && cross[i, lap] == float.MaxValue) cross[i, lap] = t;
                        }
                    }
                }
            }

            int doomed1 = 0;
            for (int i = 1; i < n; i++) if (cross[i, 0] < cross[doomed1, 0]) doomed1 = i;
            var order2 = new List<int>();
            for (int i = 0; i < n; i++) if (i != doomed1) order2.Add(i);
            order2.Sort((a, b) => cross[a, 1].CompareTo(cross[b, 1]));
            int doomed2 = order2[2];
            racers[doomed1].doomedLap = 0;
            racers[doomed2].doomedLap = 1;

            Vector3 gridCentre = trace.GridCentre() + Vector3.up * 0.3f;
            Vector3 cannonPos = track.Raw(skiExit) + track.Outside(track.Heading(skiExit)) * 12f + Vector3.down;

            Func<int, float, float, Vector3> model = (i, s, t) =>
            {
                Racer r = racers[i];
                Vector3 f = track.Heading(s);
                Vector3 p = track.Raw(s)
                            + track.Outside(f) * (0.5f * Mathf.Sin(2f * Mathf.PI * 0.7f * t + r.phi))
                            + Vector3.up * (0.08f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 3f * t)));
                float sm = CourseTrack.Mod(s, L);
                if (helixHi > helixLo && sm >= helixLo && sm <= helixHi)
                {
                    float u = (sm - helixLo) / (helixHi - helixLo);
                    float env = CameraSpring.SmoothStep(0f, 0.1f, u) * (1f - CameraSpring.SmoothStep(0.9f, 1f, u));
                    float ang = 2f * Mathf.PI * 2f * u + r.phi * 0.2f;
                    p += new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (2.6f * env);
                }

                if (vortexHi > vortexLo && sm >= vortexLo && sm <= vortexHi)
                {
                    float u = (sm - vortexLo) / (vortexHi - vortexLo);
                    float env = CameraSpring.SmoothStep(0f, 0.1f, u) * (1f - CameraSpring.SmoothStep(0.9f, 1f, u));
                    float ang = 2f * Mathf.PI * 3f * u + r.phi;
                    p += new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (Mathf.Lerp(1.5f, 0.3f, u) * env);
                }

                return p;
            };

            var ph = new List<byte>();
            var leaderL = new List<short>();
            var winnerL = new List<short>();
            var simT = new List<float>();
            var posL = new List<Vector3>();
            var progL = new List<float>();
            var rankL = new List<byte>();
            var recL = new List<byte>();
            var finL = new List<bool>();

            int winner = -1;
            float finishAt = -1f;
            var ranks = new int[n];
            for (int k = 0; ; k++)
            {
                float t = k * dt;
                if (finishAt >= 0f && t > finishAt + 15f)
                {
                    break;
                }

                if (k > 400000)
                {
                    break;
                }

                // Advance racers.
                if (t >= goTime)
                {
                    for (int i = 0; i < n; i++)
                    {
                        Racer r = racers[i];
                        switch (r.state)
                        {
                            case 0:
                            {
                                float before = r.s;
                                r.s += speedAt(r.s) * factorAt(r, t) * dt;
                                if (!r.finished)
                                {
                                    r.progress = Mathf.Max(r.progress, Mathf.Max(0f, r.s) / L);
                                }

                                if (!r.finished && r.s >= laps * L)
                                {
                                    r.finished = true;
                                    r.progress = laps;
                                    if (winner < 0)
                                    {
                                        winner = i;
                                        finishAt = t;
                                    }
                                }

                                int lap = Mathf.FloorToInt(before / L);
                                if (r.doomedLap == lap && !r.finished && before < lap * L + skiExit && r.s >= lap * L + skiExit)
                                {
                                    r.state = 1;
                                    r.stateStart = t;
                                    r.holdFrom = model(i, r.s, t);
                                    r.holdS = r.s;
                                    r.baseS = lap * L;
                                }

                                break;
                            }

                            case 1:
                                if (t - r.stateStart >= 4f)
                                {
                                    r.state = 2;
                                    r.stateStart = t;
                                    r.recycles++;
                                    r.progress = r.baseS / L;
                                    r.s = r.baseS;
                                }

                                break;
                            case 2:
                                if (t - r.stateStart >= 3f)
                                {
                                    r.state = 3;
                                    r.stateStart = t;
                                    r.s = r.baseS + 0.5f;
                                }

                                break;
                            default:
                            {
                                r.s += speedAt(r.s) * factorAt(r, t) * dt;
                                r.progress = Mathf.Max(r.progress, Mathf.Max(0f, r.s) / L);
                                if (t - r.stateStart >= 1f)
                                {
                                    r.state = 0;
                                }

                                if (r.s >= laps * L && !r.finished)
                                {
                                    r.finished = true;
                                    r.progress = laps;
                                    if (winner < 0) { winner = i; finishAt = t; }
                                }

                                break;
                            }
                        }
                    }
                }

                // Record the tick.
                int leader = 0;
                for (int i = 1; i < n; i++)
                {
                    if (racers[i].progress > racers[leader].progress) leader = i;
                }

                RacePhase phase = t < 6f ? RacePhase.Grid : (t < goTime ? RacePhase.Countdown : RacePhase.Racing);
                if (phase == RacePhase.Racing && racers[leader].progress >= 1f) phase = RacePhase.FinalLap;
                if (winner >= 0) phase = RacePhase.Finished;
                ph.Add((byte)phase);
                leaderL.Add((short)leader);
                winnerL.Add((short)winner);
                simT.Add(t);

                for (int i = 0; i < n; i++)
                {
                    int rk = 1;
                    for (int j = 0; j < n; j++)
                    {
                        if (j != i && (racers[j].progress > racers[i].progress + 0.002f || (Mathf.Abs(racers[j].progress - racers[i].progress) <= 0.002f && j < i)))
                        {
                            rk++;
                        }
                    }

                    ranks[i] = rk;
                }

                for (int i = 0; i < n; i++)
                {
                    Racer r = racers[i];
                    Vector3 slot = header.gridSlots.Length > 0 ? header.gridSlots[i % header.gridSlots.Length] : Vector3.zero;
                    Vector3 p;
                    if (t < goTime)
                    {
                        p = slot;
                    }
                    else
                    {
                        float lift = CameraSpring.SmoothStep01((t - goTime) / 1.5f);
                        switch (r.state)
                        {
                            case 1:
                                p = Vector3.Lerp(r.holdFrom, cannonPos, CameraSpring.SmoothStep01((t - r.stateStart) / 4f));
                                break;
                            case 2:
                            {
                                float u = Mathf.Clamp01((t - r.stateStart) / 3f);
                                p = Vector3.Lerp(cannonPos, gridCentre, u) + Vector3.up * (4f * 15f * u * (1f - u));
                                break;
                            }

                            case 3:
                                p = Vector3.Lerp(gridCentre, model(i, r.s, t), CameraSpring.SmoothStep01((t - r.stateStart) / 1f));
                                break;
                            default:
                                p = Vector3.Lerp(slot, model(i, r.s, t), lift);
                                break;
                        }
                    }

                    p += new Vector3(Gauss(rng), Gauss(rng), Gauss(rng)) * 0.05f;
                    posL.Add(p);
                    progL.Add(r.finished ? laps : r.progress);
                    rankL.Add((byte)ranks[i]);
                    recL.Add((byte)r.recycles);
                    finL.Add(r.finished);
                }
            }

            trace.AllocateTicks(simT.Count);
            simT.CopyTo(trace.simTime);
            ph.CopyTo(trace.phase);
            leaderL.CopyTo(trace.leader);
            winnerL.CopyTo(trace.winner);
            posL.CopyTo(trace.pos);
            progL.CopyTo(trace.progress);
            rankL.CopyTo(trace.rank);
            recL.CopyTo(trace.recycles);
            finL.CopyTo(trace.finished);
            return trace;
        }

        static float StationSpeed(RaceTrace.StationInfo[] stations, float s)
        {
            foreach (RaceTrace.StationInfo st in stations)
            {
                if (s < st.s0 || s > st.s1)
                {
                    continue;
                }

                string nm = (st.name ?? "").ToLowerInvariant();
                if (nm.StartsWith("screw")) return 0.9f;
                if (nm.StartsWith("bucket")) return 2.5f;
                if (nm.StartsWith("helix") || nm.StartsWith("vortex")) return 3f;
                if (nm.StartsWith("ski")) return 8f;
                if (nm.StartsWith("start")) return 1.5f;
            }

            return 6f;
        }

        // Evaluation ------------------------------------------------------------------------------------------------

        sealed class DtSource
        {
            readonly DtProfile _profile;
            readonly System.Random _rng;
            float _wall;
            float _nextHitch;

            public DtSource(DtProfile profile, int seed)
            {
                _profile = profile;
                _rng = new System.Random(seed);
                _nextHitch = 4f + (float)_rng.NextDouble() * 2f;
            }

            public float Next()
            {
                float dt;
                switch (_profile)
                {
                    case DtProfile.Fixed60:
                        dt = 1f / 60f;
                        break;
                    case DtProfile.Fixed30:
                        dt = 1f / 30f;
                        break;
                    default:
                        dt = Mathf.Lerp(1f / 90f, 1f / 30f, (float)_rng.NextDouble());
                        if (_wall + dt >= _nextHitch)
                        {
                            dt = 0.1f;
                            _nextHitch = _wall + dt + 4f + (float)_rng.NextDouble() * 2f;
                        }

                        break;
                }

                _wall += dt;
                return dt;
            }
        }

        public static RaceCameraCore NewCore(RaceTrace trace, RaceCameraSettings settings, float aspect)
        {
            var track = new CourseTrack(trace.vertices);
            trace.ComputeOpening(Fov, aspect, settings.openingMargin, out Vector3 centre, out Vector3 dir, out float dist);
            ZoneDef[] zones = RaceCameraCore.BuildZones(trace.ZoneSources(), settings, track.Length);
            return new RaceCameraCore(track, settings, zones, centre, dir, dist, Fov);
        }

        /// <summary>Optional per-frame callback (time, dt, pose, debug) for diagnostics.</summary>
        public static Action<float, float, CameraPose, RaceCameraDebug> FrameHook;

        public static CameraMetrics Evaluate(RaceTrace trace, RaceCameraSettings settings, DtProfile profile, int dtSeed, float aspect)
        {
            RaceCameraCore core = NewCore(trace, settings, aspect);
            RaceCameraInput input = trace.NewInput();
            var dts = new DtSource(profile, dtSeed);
            var m = new CameraMetrics { label = profile.ToString(), aspect = aspect };
            float tanH = TanV * aspect;

            bool landscape = aspect > 1f;
            float xMin = landscape ? -0.92f : -0.9f;
            float xMax = landscape ? 0.60f : 0.9f;
            const float yMin = -0.85f, yMax = 0.70f;

            var rt = new List<float>();
            var rdt = new List<float>();
            var rpos = new List<Vector3>();
            var rfwd = new List<Vector3>();
            var ryaw = new List<float>();

            int safeLeader = 0, safeTop3 = 0, framingFrames = 0;
            double top6Sum = 0;
            var diameters = new List<double>();
            double minDist = double.MaxValue, minDistAll = double.MaxValue, minY = double.MaxValue;
            float goT = -1f;
            int lastLeader = -1;
            float lastSwitch = -100f;

            float t = 0f;
            var order = new int[trace.racerCount];
            while (t < trace.End)
            {
                float dt = dts.Next();
                t += dt;
                trace.Sample(t, ref input);
                CameraPose pose = core.Step(input, dt, aspect, false);
                RaceCameraDebug dbg = core.Debug;
                FrameHook?.Invoke(t, dt, pose, dbg);
                bool racing = input.phase == RacePhase.Racing || input.phase == RacePhase.FinalLap;

                m.times.Add(t);
                m.positions.Add(pose.position);
                m.rotations.Add(pose.rotation);
                m.racing.Add(racing);
                m.frames++;

                if (dbg.leader != lastLeader)
                {
                    lastLeader = dbg.leader;
                    lastSwitch = t;
                }

                if (!racing)
                {
                    continue;
                }

                m.racingFrames++;
                rt.Add(t);
                rdt.Add(dt);
                rpos.Add(pose.position);
                rfwd.Add(pose.rotation * Vector3.forward);
                ryaw.Add(dbg.yaw);
                minY = Math.Min(minY, pose.position.y);
                double dLook = (pose.position - pose.look).magnitude;
                minDistAll = Math.Min(minDistAll, dLook);
                if (goT < 0f) goT = t;
                if (t - goT >= 6f)
                {
                    minDist = Math.Min(minDist, dLook);
                }

                if (t - lastSwitch < 2f || dbg.eligibility.Length != trace.racerCount)
                {
                    continue;
                }

                // Top racers by progress among eligible ones.
                int m6 = 0;
                for (int i = 0; i < trace.racerCount; i++)
                {
                    if (dbg.eligibility[i] < 0.5f || input.finished[i])
                    {
                        continue;
                    }

                    int j = m6++;
                    while (j > 0 && input.progress[order[j - 1]] < input.progress[i])
                    {
                        order[j] = order[j - 1];
                        j--;
                    }

                    order[j] = i;
                }

                if (m6 == 0)
                {
                    continue;
                }

                framingFrames++;
                Quaternion inv = Quaternion.Inverse(pose.rotation);
                int in6 = 0;
                bool all3 = true;
                int top = Math.Min(6, m6);
                for (int k = 0; k < top; k++)
                {
                    Vector3 v = inv * (input.pos[order[k]] - pose.position);
                    bool inside = v.z > 0.5f;
                    if (inside)
                    {
                        float nx = v.x / (v.z * tanH);
                        float ny = v.y / (v.z * TanV);
                        inside = nx >= xMin && nx <= xMax && ny >= yMin && ny <= yMax;
                        if (k == 0)
                        {
                            float diameterPx = 0.4f / (v.z * 2f * TanV) * (landscape ? 1080f : 1920f);
                            diameters.Add(diameterPx);
                        }
                    }

                    if (inside)
                    {
                        in6++;
                        if (k == 0)
                        {
                            safeLeader++;
                        }
                    }
                    else if (k == 0)
                    {
                        m.leaderMissTimes.Add(t);
                    }
                    else if (k < 3)
                    {
                        all3 = false;
                    }
                }

                if (all3 && m6 >= 1)
                {
                    safeTop3++;
                }

                top6Sum += (double)in6 / top;
            }

            m.framingFrames = framingFrames;
            m.leaderSafe = framingFrames > 0 ? 100.0 * safeLeader / framingFrames : 0;
            m.top3Safe = framingFrames > 0 ? 100.0 * safeTop3 / framingFrames : 0;
            m.top6Safe = framingFrames > 0 ? 100.0 * top6Sum / framingFrames : 0;
            m.minDistance = minDist;
            m.minDistanceAll = minDistAll;
            m.minY = minY;
            diameters.Sort();
            m.diameterP5Px = diameters.Count > 0 ? diameters[(int)(diameters.Count * 0.05)] : 0;

            // Kinematics over the racing window.
            int count = rt.Count;
            var speeds = new List<double>();
            var accs = new List<double>();
            var jerks = new List<double>();
            var angV = new List<double>();
            var angA = new List<double>();
            Vector3 vPrev = Vector3.zero;
            double aPrev = 0;
            Vector3 accPrev = Vector3.zero;
            double wPrev = 0;
            float dtPrev = 0f;
            Vector3 jDummy = Vector3.zero;
            for (int k = 1; k < count; k++)
            {
                float dtk = rdt[k];
                Vector3 dp = rpos[k] - rpos[k - 1];
                Vector3 vel = dp / dtk;
                double speed = vel.magnitude;
                speeds.Add(speed);
                double w = StableAngle(rfwd[k], rfwd[k - 1]) / dtk;
                angV.Add(w);
                double yawRate = Math.Abs(Mathf.DeltaAngle(ryaw[k - 1], ryaw[k])) / dtk;
                m.maxYawRate = Math.Max(m.maxYawRate, yawRate);
                // A position step beyond what the previous velocity explains (a hitch frame legitimately moves far).
                bool pop = k >= 2 && (dp - vPrev * dtk).magnitude > 0.6f;

                if (k >= 2)
                {
                    float mean = 0.5f * (dtk + dtPrev);
                    Vector3 acc = (vel - vPrev) / mean;
                    double am = acc.magnitude;
                    accs.Add(am);
                    double wa = Math.Abs(w - wPrev) / mean;
                    angA.Add(wa);
                    if (am > 20.0 || wa > 200.0)
                    {
                        pop = true;
                    }

                    if (k >= 3)
                    {
                        jerks.Add((acc - accPrev).magnitude / mean);
                    }

                    accPrev = acc;
                    aPrev = am;
                }

                if (pop)
                {
                    m.pops++;
                }

                vPrev = vel;
                wPrev = w;
                dtPrev = dtk;
            }

            m.maxSpeed = Max(speeds);
            m.maxAngularVelocity = Max(angV);
            m.p95AngularVelocity = Percentile(angV, 0.95);
            m.maxAngularAcceleration = Max(angA);
            m.maxAcceleration = Max(accs);
            m.p95Acceleration = Percentile(accs, 0.95);
            m.jerkP99 = Percentile(jerks, 0.99);
            _ = aPrev;
            _ = jDummy;
            return m;
        }

        /// <summary>Angle between two vectors in degrees, stable for tiny angles (Vector3.Angle quantises near zero, which reads as angular jerk).</summary>
        static double StableAngle(Vector3 a, Vector3 b)
        {
            Vector3 c = Vector3.Cross(a, b);
            return Math.Atan2(c.magnitude, Vector3.Dot(a, b)) * 180.0 / Math.PI;
        }

        static double Max(List<double> v)
        {
            double m = 0;
            foreach (double d in v) if (d > m) m = d;
            return m;
        }

        static double Percentile(List<double> v, double p)
        {
            if (v.Count == 0) return 0;
            var s = new List<double>(v);
            s.Sort();
            return s[Math.Min(s.Count - 1, (int)(p * s.Count))];
        }

        /// <summary>Compares a run with the Fixed60 reference at the reference's racing timestamps.</summary>
        public static void CompareToReference(CameraMetrics run, CameraMetrics reference)
        {
            var pd = new List<double>();
            var rd = new List<double>();
            int j = 0;
            for (int k = 0; k < reference.times.Count; k++)
            {
                if (!reference.racing[k])
                {
                    continue;
                }

                float t = reference.times[k];
                while (j + 1 < run.times.Count && run.times[j + 1] < t)
                {
                    j++;
                }

                if (j + 1 >= run.times.Count || t < run.times[0])
                {
                    continue;
                }

                float u = Mathf.Clamp01((t - run.times[j]) / Mathf.Max(1e-6f, run.times[j + 1] - run.times[j]));
                Vector3 p = Vector3.Lerp(run.positions[j], run.positions[j + 1], u);
                Quaternion q = Quaternion.Slerp(run.rotations[j], run.rotations[j + 1], u);
                pd.Add((p - reference.positions[k]).magnitude);
                rd.Add(Quaternion.Angle(q, reference.rotations[k]));
            }

            run.deviationPosP99 = Percentile(pd, 0.99);
            run.deviationRotP99 = Percentile(rd, 0.99);
        }

        // Report ----------------------------------------------------------------------------------------------------

        static void Row(StringBuilder sb, string name, double value, string unit, string limit, bool pass)
        {
            sb.AppendLine(string.Format("  {0,-34} {1,10:0.00} {2,-5} limit {3,-10} {4}", name, value, unit, limit, pass ? "PASS" : "FAIL"));
        }

        /// <summary>Appends the metric rows with the section D thresholds; <paramref name="relax"/> scales the upper limits (1.25 for Fixed30 and Jitter).</summary>
        public static bool Report(StringBuilder sb, CameraMetrics m, float relax, bool judge)
        {
            bool all = true;
            Action<string, double, string, double, bool> up = (name, v, unit, lim, strict) =>
            {
                double l = strict ? lim : lim * relax;
                bool ok = v <= l;
                all &= ok;
                Row(sb, name, v, unit, "<= " + l.ToString("0.##"), ok);
            };

            sb.AppendLine(string.Format("{0} @ aspect {1:0.000}: {2} frames, {3} racing, {4} framing samples", m.label, m.aspect, m.frames, m.racingFrames, m.framingFrames));
            up("Pops", m.pops, "", 0, true);
            up("Max angular velocity", m.maxAngularVelocity, "deg/s", 25, false);
            up("P95 angular velocity", m.p95AngularVelocity, "deg/s", 15, false);
            up("Max acceleration", m.maxAcceleration, "m/s2", 10, false);
            up("P95 acceleration", m.p95Acceleration, "m/s2", 4, false);
            up("Jerk p99", m.jerkP99, "m/s3", 40, false);
            up("Max camera speed", m.maxSpeed, "m/s", 18, false);
            sb.AppendLine(string.Format("  {0,-34} {1,10:0.00} deg/s   (info) max angular accel {2:0.0} deg/s2, min distance incl. opening push-in {3:0.0} m", "Max yaw rate (unwrapped)", m.maxYawRate, m.maxAngularAcceleration, m.minDistanceAll));
            Action<string, double, double, bool> low = (name, v, lim, strict) =>
            {
                bool ok = v >= lim;
                all &= ok;
                Row(sb, name, v, name.Contains("distance") || name.Contains("Camera y") ? "m" : (name.Contains("diameter") ? "px" : "%"), ">= " + lim.ToString("0.##"), ok);
            };

            if (m.aspect > 1f)
            {
                low("Leader in safe area", m.leaderSafe, 97, true);
                low("All of top-3 in frame", m.top3Safe, 85, true);
                low("Mean top-6 in frame", m.top6Safe, 75, true);
            }
            else
            {
                sb.AppendLine(string.Format("  {0,-34} {1,10:0.0} / top-3 {2:0.0} / top-6 {3:0.0} %  (report only)", "Leader / framing", m.leaderSafe, m.top3Safe, m.top6Safe));
            }

            low("Min distance (from GO+6 s)", m.minDistance, 7.5, true);
            low("Camera y", m.minY, 1.0, true);
            low("Leader diameter p5 (14 px)", m.diameterP5Px, 14, true);
            if (!double.IsNaN(m.deviationPosP99))
            {
                up("Deviation from Fixed60 (pos p99)", m.deviationPosP99, "m", 0.5, true);
                up("Deviation from Fixed60 (rot p99)", m.deviationRotP99, "deg", 1.5, true);
            }

            if (!judge)
            {
                sb.AppendLine("  (report only, not judged)");
                return true;
            }

            sb.AppendLine("  => " + (all ? "ALL PASS" : "SOME FAIL"));
            return all;
        }

        public static string Sanity(RaceTrace trace, RaceCameraSettings settings)
        {
            var sb = new StringBuilder();
            var track = new CourseTrack(trace.vertices);
            sb.AppendLine(string.Format("Sanity: CourseTrack.Length {0:0.0000} vs header length {1:0.0000} (diff {2:0.00000}) {3}",
                track.Length, trace.length, Mathf.Abs(track.Length - trace.length), Mathf.Abs(track.Length - trace.length) < 1e-3f ? "OK" : "MISMATCH"));
            sb.AppendLine("Sanity: InteriorOnRight = " + track.InteriorOnRight);

            // Spring stability at a long frame: 1/15 s with the substep rule.
            float x = 0f, v = 0f;
            float dt = 1f / 15f;
            int n = Mathf.CeilToInt(dt * settings.substepHz);
            float peak = 0f;
            for (int frame = 0; frame < 150; frame++)
            {
                for (int i = 0; i < n; i++)
                {
                    CameraSpring.Step(ref x, ref v, 10f, 0f, 3f, -100f, 100f, 1000f, dt / n);
                }

                peak = Mathf.Max(peak, x);
            }

            sb.AppendLine(string.Format("Sanity: spring at dt 1/15 (omega 3, {0} substeps): peak {1:0.000} for target 10, settled {2:0.0000} {3}", n, peak, x, peak <= 10.05f && Mathf.Abs(x - 10f) < 0.01f ? "OK" : "UNSTABLE"));

            ZoneDef[] zones = RaceCameraCore.BuildZones(trace.ZoneSources(), settings, track.Length);
            bool ok = true;
            for (int i = 0; i < zones.Length; i++)
            {
                if (i + 1 < zones.Length && zones[i].hi > zones[i + 1].lo + 1e-4f)
                {
                    ok = false;
                }

                sb.AppendLine(string.Format("  zone {0,-14} {1,7:0.0} .. {2,7:0.0}", zones[i].name, zones[i].lo, zones[i].hi));
            }

            sb.AppendLine("Sanity: zones do not overlap: " + (ok ? "OK" : "OVERLAP"));
            return sb.ToString();
        }

        /// <summary>
        /// Runs the whole suite. With a null path it builds a synthetic trace from the open scene (or the built-in fallback course
        /// when no scene race is findable), otherwise it replays a recorded trace. Returns the report and writes it to
        /// Temp/RaceCamTraces/report.txt.
        /// </summary>
        public static string RunSuite(string tracePathOrNull)
        {
            return RunSuite(tracePathOrNull, null, 12345);
        }

        public static string RunSuite(string tracePathOrNull, RaceCameraSettings settingsOrNull, int seed)
        {
            RaceCameraSettings settings = settingsOrNull ?? new RaceCameraSettings();
            var sb = new StringBuilder();
            RaceTrace trace;
            if (!string.IsNullOrEmpty(tracePathOrNull))
            {
                trace = RaceTrace.Load(tracePathOrNull);
                sb.AppendLine("Trace: recorded " + tracePathOrNull);
            }
            else
            {
                RaceTrace header = null;
                string source = "built-in fallback course (stadium loop through the plan's station table)";
                MachineRaceSimulation sim = UnityEngine.Object.FindAnyObjectByType<MachineRaceSimulation>();
                if (sim != null && sim.stations != null && sim.stations.Count >= 2)
                {
                    try
                    {
                        header = RaceTrace.HeaderFromScene(sim);
                        source = "scene '" + sim.gameObject.scene.name + "' course";
                    }
                    catch (Exception e)
                    {
                        header = null;
                        sb.AppendLine("Scene header failed (" + e.Message + "), using the fallback course.");
                    }
                }

                trace = Synthetic(seed, header);
                sb.AppendLine("Trace: synthetic seed " + seed + " on " + source + ", " + trace.racerCount + " racers, " + trace.laps + " laps, " + trace.tickCount + " ticks (" + trace.End.ToString("0.0") + " s)");
                trace.Save(Path.Combine(RaceTrace.TraceDirectory, "synthetic.trace"));
            }

            sb.AppendLine(Sanity(trace, settings));

            float wide = 16f / 9f;
            CameraMetrics f60 = Evaluate(trace, settings, DtProfile.Fixed60, 1, wide);
            CameraMetrics f30 = Evaluate(trace, settings, DtProfile.Fixed30, 1, wide);
            CameraMetrics jit = Evaluate(trace, settings, DtProfile.Jitter, 7, wide);
            CompareToReference(f30, f60);
            CompareToReference(jit, f60);
            CameraMetrics tall = Evaluate(trace, settings, DtProfile.Fixed60, 1, 9f / 16f);

            bool pass = true;
            pass &= Report(sb, f60, 1f, true);
            pass &= Report(sb, f30, 1.25f, true);
            pass &= Report(sb, jit, 1.25f, true);
            Report(sb, tall, 1f, false);
            sb.AppendLine("OVERALL: " + (pass ? "PASS" : "FAIL"));

            string report = sb.ToString();
            try
            {
                Directory.CreateDirectory(RaceTrace.TraceDirectory);
                File.WriteAllText(Path.Combine(RaceTrace.TraceDirectory, "report.txt"), report);
            }
            catch (Exception e)
            {
                report += "\n(report not saved: " + e.Message + ")";
            }

            UnityEngine.Debug.Log("[RaceCamHarness]\n" + report);
            return report;
        }

        [MenuItem("Simulation Lobby/Tools/Race Camera Smoothness Test (mach-002)", false, 950)]
        static void RunFromMenu()
        {
            RunSuite(null);
        }
    }
}
