using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Fills a <see cref="HorrorCoasterConfig"/> with the coast-001 storyboard: the rail layout, the act boundaries, the lift
    /// and cruise zones and the scare schedule. The rail is authored as a list of runs (length, turn, climb, bank); act
    /// boundaries and zones hang off control points, so they stay glued to the geometry. Three turn angles are solved
    /// numerically so the loop closes exactly on the station. Event distances are then placed by running the dynamics
    /// and asking where the cart is N seconds into each act, which is what makes the published event times land on the
    /// storyboard whatever the rail looks like.
    /// </summary>
    public static class HorrorCoasterDefaults
    {
        enum Ease { Linear, Smooth, In, Out }

        /// <summary>Rail builder: a turtle that emits control points, with bank and level weight per point.</summary>
        sealed class Builder
        {
            public readonly List<Vector3> pts = new List<Vector3>();
            public readonly List<float> bank = new List<float>();
            public readonly List<float> level = new List<float>();
            Vector3 _pos;
            float _yaw;        // radians; forward = (sin yaw, 0, cos yaw); increasing yaw turns right
            float _bank;
            float _level = 1f;
            float _levelNext = 1f;

            public int Last => pts.Count - 1;
            public Vector3 Pos => _pos;
            public float YawRad => _yaw;
            public float YawDeg => _yaw * Mathf.Rad2Deg;

            public Builder(Vector3 start, float yawDeg)
            {
                _pos = start;
                _yaw = yawDeg * Mathf.Deg2Rad;
                Emit(start, 0f, 1f);
            }

            void Emit(Vector3 p, float b, float l)
            {
                pts.Add(p);
                bank.Add(b);
                level.Add(l);
            }

            /// <summary>The level weight the NEXT run ramps toward (smoothstep over that run).</summary>
            public void LevelTo(float w)
            {
                _levelNext = w;
            }

            static float EaseF(Ease e, float u)
            {
                switch (e)
                {
                    case Ease.Smooth: return u * u * (3f - 2f * u);
                    case Ease.In: return u * u;
                    case Ease.Out: return 1f - (1f - u) * (1f - u);
                    default: return u;
                }
            }

            /// <summary>
            /// A run of <paramref name="len"/> metres of horizontal travel, turning <paramref name="turnDeg"/> (positive =
            /// right) at constant curvature, climbing <paramref name="dh"/> by <paramref name="ease"/>, and moving the bank
            /// to <paramref name="bankEnd"/> (positive = lean right) by the end.
            /// </summary>
            public void Run(float len, float turnDeg = 0f, float dh = 0f, float bankEnd = 0f, Ease ease = Ease.Linear, float step = 5f)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
                const int sub = 8;
                float turn = turnDeg * Mathf.Deg2Rad;
                float ds = len / (n * sub);
                float y0 = _pos.y, yaw0 = _yaw, b0 = _bank, l0 = _level;
                Vector3 p = _pos;
                for (int k = 1; k <= n; k++)
                {
                    for (int q = 0; q < sub; q++)
                    {
                        float uMid = ((k - 1) * sub + q + 0.5f) / (n * sub);
                        float yaw = yaw0 + turn * uMid;
                        p.x += Mathf.Sin(yaw) * ds;
                        p.z += Mathf.Cos(yaw) * ds;
                    }
                    float u = (float)k / n;
                    float sm = u * u * (3f - 2f * u);
                    p.y = y0 + dh * EaseF(ease, u);
                    Emit(p, Mathf.Lerp(b0, bankEnd, sm), Mathf.Lerp(l0, _levelNext, sm));
                }
                _pos = p;
                _yaw = yaw0 + turn;
                _bank = bankEnd;
                _level = _levelNext;
            }

            /// <summary>
            /// A winding stretch of <paramref name="total"/> metres in <paramref name="segs"/> (even) alternating runs whose
            /// turns add up to <paramref name="net"/> degrees, each swinging <paramref name="swing"/> degrees either side of the
            /// average. Banking follows the turn rate.
            /// </summary>
            public void Cruise(float total, float net, int segs, float swing)
            {
                float seg = total / segs;
                for (int i = 0; i < segs; i++)
                {
                    float turn = net / segs + ((i & 1) == 0 ? swing : -swing);
                    float bk = Mathf.Clamp(turn / seg * 6.5f, -26f, 26f);
                    Run(seg, turn, 0f, bk);
                }
            }

            /// <summary>
            /// A chain hill climbing <paramref name="rise"/> metres: eased in over <paramref name="a"/> m, straight incline over
            /// <paramref name="m"/> m, eased out over <paramref name="a"/> m, with the slopes matched so the profile has no kink.
            /// </summary>
            public void LiftHill(float rise, float a, float m, float turnDeg = 0f)
            {
                float s = rise / (a + m);
                float ra = s * a * 0.5f;
                float rm = s * m;
                float tot = 2f * a + m;
                Run(a, turnDeg * a / tot, ra, 0f, Ease.In);
                Run(m, turnDeg * m / tot, rm, 0f, Ease.Linear);
                Run(a, turnDeg * a / tot, ra, 0f, Ease.Out);
            }

            /// <summary>A free-running drop of <paramref name="drop"/> metres over <paramref name="len"/> m of horizontal travel.</summary>
            public void Drop(float len, float drop, float turnDeg, float bankEnd)
            {
                Run(len, turnDeg, -drop, bankEnd, Ease.Smooth);
            }

            /// <summary>A full vertical loop in the current heading plane, drifting sideways by <paramref name="shift"/> so it does not touch itself.</summary>
            public void Loop(float radius, float shift, int count = 40)
            {
                Vector3 f = new Vector3(Mathf.Sin(_yaw), 0f, Mathf.Cos(_yaw));
                Vector3 side = new Vector3(Mathf.Cos(_yaw), 0f, -Mathf.Sin(_yaw));
                Vector3 start = _pos;
                for (int k = 1; k <= count; k++)
                {
                    float x = (float)k / count;
                    float ang = 2f * Mathf.PI * x;
                    float lateral = shift * (x - Mathf.Sin(2f * Mathf.PI * x) / (2f * Mathf.PI));
                    Vector3 p = start + f * (radius * Mathf.Sin(ang)) + Vector3.up * (radius * (1f - Mathf.Cos(ang))) + side * lateral;
                    Emit(p, _bank, _level);
                }
                _pos = start + side * shift;
            }

            /// <summary>A corkscrew: the rail spirals around a straight axis, with the radius swelling in and out so it enters and leaves on the axis.</summary>
            public void Corkscrew(float radius, float turns, float length, int count = 48)
            {
                Vector3 f = new Vector3(Mathf.Sin(_yaw), 0f, Mathf.Cos(_yaw));
                Vector3 side = new Vector3(Mathf.Cos(_yaw), 0f, -Mathf.Sin(_yaw));
                Vector3 start = _pos;
                for (int k = 1; k <= count; k++)
                {
                    float x = (float)k / count;
                    float env = Mathf.Sin(Mathf.PI * x);
                    env *= env; // zero slope at both ends, so the rail leaves and rejoins the axis tangentially
                    float phi = 2f * Mathf.PI * turns * x;
                    Vector3 p = start + f * (length * x) + (side * Mathf.Sin(phi) + Vector3.up * Mathf.Cos(phi)) * (radius * env);
                    Emit(p, _bank, _level);
                }
                _pos = start + f * length;
            }
        }

        sealed class ZoneDef
        {
            public int a, b;
            public float da, db, speed;
        }

        /// <summary>Everything Apply needs from the layout pass.</summary>
        sealed class Layout
        {
            public Builder rail;
            public readonly Dictionary<ActId, int> actMarker = new Dictionary<ActId, int>();
            public readonly List<ZoneDef> lifts = new List<ZoneDef>();
            public readonly List<ZoneDef> brakes = new List<ZoneDef>();

            public void Lift(int a, float da, int b, float db) =>
                lifts.Add(new ZoneDef { a = a, da = da, b = b, db = db });

            public void Brake(int a, float da, int b, float db, float speed) =>
                brakes.Add(new ZoneDef { a = a, da = da, b = b, db = db, speed = speed });
        }

        /// <summary>The three turn angles (degrees) the closure solver adjusts so the ride ends on the station.</summary>
        struct Closure
        {
            public float street, mansion, tail;
        }

        // (act, seconds into the act, kind, takesRider, preQuiet, caption)
        struct EventSpec
        {
            public ActId act;
            public float seconds;
            public ScareKind kind;
            public bool takes;
            public float pre;
            public string caption;
        }

        static EventSpec E(ActId act, float seconds, ScareKind kind, bool takes, float pre, string caption = "")
        {
            return new EventSpec { act = act, seconds = seconds, kind = kind, takes = takes, pre = pre, caption = caption };
        }

        /// <summary>Short pre-quiet micro jump scare: never takes a rider, no caption.</summary>
        static EventSpec M(ActId act, float seconds, ScareKind kind, float pre = 0.6f)
        {
            return new EventSpec { act = act, seconds = seconds, kind = kind, takes = false, pre = pre, caption = "" };
        }

        static readonly EventSpec[] Specs =
        {
            E(ActId.ChainLift,          11f, ScareKind.FakeBat,        false, 1.2f),
            E(ActId.TrickOrTreatStreet,  9.5f, ScareKind.TreatDoor,     false, 0f,   "TREAT?"),
            E(ActId.TrickOrTreatStreet, 21.9f, ScareKind.TreatDoor,     false, 0f,   "TREAT?"),
            E(ActId.TrickOrTreatStreet, 34.5f, ScareKind.DoorGhoul,     true,  2.0f, "TRICK?"),
            // Graveyard: a cobra eats a rider, then something leaps off a crypt roof.
            E(ActId.Graveyard,          22f, ScareKind.SnakeStrike,   true,  2.6f),
            E(ActId.Graveyard,          35.0f, ScareKind.FakeLantern,   false, 1.2f),
            E(ActId.Graveyard,          50f, ScareKind.SideLeap,      false, 2.4f),
            E(ActId.DeadForest,         24f, ScareKind.CrowBurst,     true,  2.0f),
            // Tunnel: the wall itself fires spikes.
            E(ActId.TunnelOfFaces,      24f, ScareKind.SpikeWall,     true,  2.4f),
            E(ActId.Mansion,            26f, ScareKind.PaintingLunge, true,  2.0f),
            E(ActId.Asylum,             10f, ScareKind.FakeWheelchair, false, 1.2f),
            E(ActId.Asylum,           22.5f, ScareKind.FakeWheelchair, false, 1.2f),
            // Asylum corridor: a long straight, so the beast is seen coming from far off.
            E(ActId.Asylum,           35.5f, ScareKind.SprinterLeap,  false, 3.6f),
            E(ActId.FinalClimb,         14f, ScareKind.FakeEmptySeat, false, 1.2f),
            // Entering the giant pumpkin's mouth.
            E(ActId.FinalClimb,         19f, ScareKind.BatFlood,      false, 1.8f),
            E(ActId.FinalDrop,          22f, ScareKind.SkullFinale,   false, 2.0f),

            // ---- Micro jump scares (thinned: one or two of each kind, different shapes of fear are the signature scares above).
            M(ActId.TrickOrTreatStreet, 15.7f, ScareKind.WindowFace,  0.6f),
            M(ActId.TrickOrTreatStreet, 47.6f, ScareKind.ShadowRush,  0.7f),
            M(ActId.Graveyard,           9.0f, ScareKind.SpiderDrop,  0.8f),
            M(ActId.DeadForest,          8.0f, ScareKind.SkeletonPop, 0.6f),
            M(ActId.TunnelOfFaces,       7.5f, ScareKind.MirrorFace,  0.9f),
            M(ActId.TunnelOfFaces,      36.8f, ScareKind.ClawHand,    0.5f),
            M(ActId.Mansion,            11.0f, ScareKind.CoffinLid,   0.8f),
            M(ActId.Mansion,            38.8f, ScareKind.MirrorFace,  0.9f),
        };

        /// <summary>Reset <paramref name="cfg"/> to the coast-001 storyboard.</summary>
        public static void Apply(HorrorCoasterConfig cfg)
        {
            Closure closure = SolveClosure();
            Layout layout = BuildLayout(closure);
            Builder b = layout.rail;
            Vector3[] points = b.pts.ToArray();
            float[] bank = b.bank.ToArray();
            float[] level = b.level.ToArray();
            var rail = new CoasterRail(points, bank, level);

            // Physics and run settings.
            cfg.fixedTimestep = 1f / 60f;
            cfg.maxDurationSeconds = 600f;
            cfg.ridersAtStart = 6;
            cfg.liftSpeed = 6f;
            cfg.maxSpeed = 30f;
            cfg.gravity = 9.81f;
            cfg.drag = 0.0008f;
            cfg.rollingFriction = 0.006f;
            cfg.minCrawlSpeed = 1.5f;
            cfg.brakeDecel = 5f;
            cfg.driveAccel = 4f;
            cfg.liftAccel = 3f;
            cfg.stationDwellSeconds = 6f;
            cfg.endHoldSeconds = 8f;
            cfg.stingSeconds = 0.6f;
            cfg.aftermathSeconds = 2f;
            cfg.fearRiseSeconds = 12f;
            cfg.microFearRiseSeconds = 3f;
            cfg.easeSeconds = 1.5f;
            cfg.easeMinSpeed = 5f;
            cfg.easeFraction = 0.6f;
            cfg.wobbleAmplitude = 0.04f;
            cfg.totalTargetSeconds = 420f;
            cfg.randomizeSeedOnStart = true;

            cfg.railPoints = points;
            cfg.railBankDeg = bank;
            cfg.railLevelWeight = level;

            // Acts, in ride order.
            ActId[] order =
            {
                ActId.Station, ActId.ChainLift, ActId.TrickOrTreatStreet, ActId.Graveyard, ActId.DeadForest,
                ActId.TunnelOfFaces, ActId.Trip, ActId.Mansion, ActId.Asylum, ActId.FinalClimb, ActId.FinalDrop,
                ActId.StationReturn
            };
            var acts = new ActSpan[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                acts[i] = new ActSpan { act = order[i], startDistance = rail.ControlDistance(layout.actMarker[order[i]]) };
            }
            cfg.acts = acts;

            // Zones.
            cfg.liftStarts = new float[layout.lifts.Count];
            cfg.liftEnds = new float[layout.lifts.Count];
            for (int i = 0; i < layout.lifts.Count; i++)
            {
                ZoneDef z = layout.lifts[i];
                cfg.liftStarts[i] = rail.ControlDistance(z.a) + z.da;
                cfg.liftEnds[i] = rail.ControlDistance(z.b) + z.db;
            }
            cfg.brakeStarts = new float[layout.brakes.Count];
            cfg.brakeEnds = new float[layout.brakes.Count];
            cfg.brakeSpeeds = new float[layout.brakes.Count];
            for (int i = 0; i < layout.brakes.Count; i++)
            {
                ZoneDef z = layout.brakes[i];
                cfg.brakeStarts[i] = rail.ControlDistance(z.a) + z.da;
                cfg.brakeEnds[i] = Mathf.Min(rail.ControlDistance(z.b) + z.db, rail.Length + 1f);
                cfg.brakeSpeeds[i] = z.speed;
            }

            // Place the events. The dynamics slow down before every big scare (ease), which moves the clock a little, so
            // the placement is repeated with the previous pass's events until the distances stop changing.
            cfg.events = new ScareEvent[0];
            float dt = cfg.fixedTimestep;
            for (int pass = 0; pass < 6; pass++)
            {
                var distanceAtTick = new List<float>(40000) { 0f };
                var dyn = new CoasterDynamics(rail, cfg);
                for (int i = 0; i < 120000 && !dyn.AtEnd; i++)
                {
                    dyn.Step(dt);
                    distanceAtTick.Add(dyn.Distance);
                }

                var placed = new List<ScareEvent>(Specs.Length);
                for (int i = 0; i < Specs.Length; i++)
                {
                    EventSpec s = Specs[i];
                    float actTime = TimeAtDistance(distanceAtTick, dt, cfg.ActStart(s.act));
                    float d = DistanceAtTime(distanceAtTick, dt, actTime + s.seconds);
                    placed.Add(new ScareEvent
                    {
                        distance = d,
                        kind = s.kind,
                        takesRider = s.takes,
                        preQuietSeconds = s.pre,
                        cueCaption = s.caption
                    });
                }
                // Stable by authored order: ties and out-of-order specs both end up ascending.
                for (int i = 1; i < placed.Count; i++)
                {
                    ScareEvent x = placed[i];
                    int j = i - 1;
                    while (j >= 0 && placed[j].distance > x.distance)
                    {
                        placed[j + 1] = placed[j];
                        j--;
                    }
                    placed[j + 1] = x;
                }
                cfg.events = placed.ToArray();
            }

            cfg.intent = "coast-001 Haunted Coaster: six riders, every real scare takes one, ride length " +
                         cfg.totalTargetSeconds.ToString("0") + " s (storyboard defaults).";
        }

        static float TimeAtDistance(List<float> dist, float dt, float d)
        {
            for (int i = 0; i < dist.Count; i++)
            {
                if (dist[i] >= d) return i * dt;
            }
            return (dist.Count - 1) * dt;
        }

        static float DistanceAtTime(List<float> dist, float dt, float t)
        {
            float x = t / dt;
            int i = Mathf.Clamp((int)x, 0, dist.Count - 1);
            if (i >= dist.Count - 1) return dist[dist.Count - 1];
            return Mathf.Lerp(dist[i], dist[i + 1], x - i);
        }

        // ------------------------------------------------------------------------------------------------
        // Closure: the loop must end exactly on the station, heading the way it started. Newton on three turn angles.
        // ------------------------------------------------------------------------------------------------
        static readonly Closure ClosureGuess = new Closure { street = 80f, mansion = 90f, tail = 20f };

        static void ClosureError(Closure c, out float ex, out float ez, out float eyaw)
        {
            Builder b = BuildLayout(c).rail;
            ex = b.Pos.x;
            ez = b.Pos.z;
            float yaw = b.YawDeg;
            eyaw = yaw - 360f * Mathf.Round(yaw / 360f);
        }

        static Closure With(Closure c, int i, float v)
        {
            if (i == 0) c.street = v;
            else if (i == 1) c.mansion = v;
            else c.tail = v;
            return c;
        }

        static float Get(Closure c, int i)
        {
            return i == 0 ? c.street : (i == 1 ? c.mansion : c.tail);
        }

        static Closure SolveClosure()
        {
            Closure c = ClosureGuess;
            const float yawScale = 5f; // 1 degree of heading error counts like 5 m of position error
            for (int it = 0; it < 14; it++)
            {
                ClosureError(c, out float ex, out float ez, out float ey);
                if (Mathf.Abs(ex) < 0.02f && Mathf.Abs(ez) < 0.02f && Mathf.Abs(ey) < 0.002f) break;
                var j = new float[3, 3];
                for (int k = 0; k < 3; k++)
                {
                    const float h = 0.5f;
                    ClosureError(With(c, k, Get(c, k) + h), out float fx, out float fz, out float fy);
                    j[0, k] = (fx - ex) / h;
                    j[1, k] = (fz - ez) / h;
                    j[2, k] = (fy - ey) * yawScale / h;
                }
                float[] rhs = { -ex, -ez, -ey * yawScale };
                float[] step = Solve3(j, rhs);
                if (step == null) break;
                float damp = 1f;
                float mx = Mathf.Max(Mathf.Abs(step[0]), Mathf.Max(Mathf.Abs(step[1]), Mathf.Abs(step[2])));
                if (mx > 25f) damp = 25f / mx;
                for (int k = 0; k < 3; k++) c = With(c, k, Get(c, k) + step[k] * damp);
            }
            return c;
        }

        /// <summary>3x3 solve by Cramer's rule; null when singular.</summary>
        static float[] Solve3(float[,] m, float[] r)
        {
            float det = Det(m);
            if (Mathf.Abs(det) < 1e-9f) return null;
            var res = new float[3];
            for (int k = 0; k < 3; k++)
            {
                var t = (float[,])m.Clone();
                for (int i = 0; i < 3; i++) t[i, k] = r[i];
                res[k] = Det(t) / det;
            }
            return res;
        }

        static float Det(float[,] m)
        {
            return m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
                 - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
                 + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
        }

        // ------------------------------------------------------------------------------------------------
        // The layout. Station at y 2.5 over ground at y 1.5; every number below is metres or degrees.
        // Three chain hills (48 m, 44 m, 57 m) are the highest points before the drops they feed; the Trip's
        // spiral lifts the cart to 41.5 m for the mansion drop. Everything else is a cruise zone at 9-15 m/s.
        // ------------------------------------------------------------------------------------------------
        static Layout BuildLayout(Closure c)
        {
            var L = new Layout();
            var b = new Builder(new Vector3(0f, 2.5f, 0f), 0f);
            L.rail = b;

            // Station: the cart sits for stationDwellSeconds, then pulls out to chain speed.
            L.actMarker[ActId.Station] = 0;
            b.Run(12f);
            int mStationEnd = b.Last;
            L.Brake(0, 0f, mStationEnd, 0f, 6f);

            // Main chain lift: 48 m up, 6 m/s.
            L.actMarker[ActId.ChainLift] = b.Last;
            b.LiftHill(48f, 18f, 70f);
            L.Lift(L.actMarker[ActId.ChainLift], 0f, b.Last, 0f);

            // Trick or Treat Street: 49 m drop off the crest to ground level, cruise through the town, a second chain hill.
            L.actMarker[ActId.TrickOrTreatStreet] = b.Last;
            b.Drop(105f, 49f, 8f, 6f);
            int mDropEnd = b.Last;
            b.Cruise(310f, c.street, 4, 28f);
            b.Run(10f, 0f, 0f, 0f);
            int mHillStart = b.Last;
            b.LiftHill(44f, 14f, 50f);
            L.Brake(mDropEnd, -25f, mHillStart, 0f, 11f);
            L.Lift(mHillStart, 0f, b.Last, 0f);

            // Graveyard: 40 m drop into the mist, then a creep through the stones.
            L.actMarker[ActId.Graveyard] = b.Last;
            b.Drop(98f, 44f, 4f, 4f);
            int mGraveDropEnd = b.Last;
            b.Cruise(490f, 70f, 6, 35f);
            int mForest = b.Last;
            L.Brake(mGraveDropEnd, -25f, mForest, 0f, 10f);

            // Dead forest: banked S-curves, a little faster.
            L.actMarker[ActId.DeadForest] = b.Last;
            b.Cruise(490f, 85f, 6, 62f);
            int mTunnel = b.Last;
            L.Brake(mForest, 0f, mTunnel, 0f, 12f);

            // Tunnel of faces: a long dark straight.
            L.actMarker[ActId.TunnelOfFaces] = b.Last;
            b.Run(550f);
            int mTripStart = b.Last;
            L.Brake(mTunnel, 0f, mTripStart, 0f, 14f);

            // The Trip: spiral up, a full vertical loop, a corkscrew, then upright again. Level weight is 0 inside so the
            // rail is a pure parallel-transported curve; it ramps back to 1 on the exit run. The whole act is a drive zone
            // (a tyre-driven "dream"), so its 40 m of climb needs no chain.
            L.actMarker[ActId.Trip] = b.Last;
            b.LevelTo(0f);
            b.Run(30f);
            b.Run(190f, -360f, 20f, -20f, Ease.Smooth);  // left-hand spirals: their twist cancels most of the corkscrew's,
            b.Run(190f, -360f, 20f, 0f, Ease.Smooth);    // so the cart leaves the Trip nearly upright
            b.Run(30f);
            b.Loop(14f, 8f);
            b.Run(20f);
            b.Corkscrew(6.5f, 2f, 80f);
            b.LevelTo(1f);
            b.Run(90f);
            int mTripEnd = b.Last;
            L.Brake(mTripStart, 0f, mTripEnd, 0f, 15f);

            // Mansion: 40 m drop through the front doors, then the portrait halls.
            L.actMarker[ActId.Mansion] = b.Last;
            b.Drop(95f, 40f, 4f, 0f);
            int mMansionDropEnd = b.Last;
            b.Cruise(480f, c.mansion, 4, 35f);
            int mAsylum = b.Last;
            L.Brake(mMansionDropEnd, -25f, mAsylum, 0f, 11f);

            // Asylum: a long straight corridor.
            L.actMarker[ActId.Asylum] = b.Last;
            b.Run(546f);
            int mClimb = b.Last;
            L.Brake(mAsylum, 0f, mClimb, 0f, 13f);

            // Final climb: 57 m lift to a crest higher than the first, turning north as it goes. No crawl at the crest.
            L.actMarker[ActId.FinalClimb] = b.Last;
            b.LiftHill(57f, 16f, 80f, 60f);
            int mCrest = b.Last;
            L.Lift(mClimb, 0f, mCrest, 0f);

            // Final drop: 57 m straight down through the pumpkin, then roll out.
            L.actMarker[ActId.FinalDrop] = b.Last;
            b.Drop(115f, 57f, 0f, 0f);
            int mFinalDropEnd = b.Last;
            b.Cruise(260f, c.tail, 2, 8f);
            int mReturn = b.Last;
            L.Brake(mFinalDropEnd, -25f, mReturn, 0f, 12f);

            // Station return: cruise in, the last 30 m is the brake run.
            L.actMarker[ActId.StationReturn] = b.Last;
            b.Run(70f, 0f, 1f, 0f, Ease.Smooth);
            int mEnd = b.Last;
            L.Brake(mReturn, 0f, mEnd, -30f, 9f);
            L.Brake(mEnd, -30f, mEnd, -8f, 3.5f);
            L.Brake(mEnd, -8f, mEnd, 20f, 1.5f);

            return L;
        }
    }
}
