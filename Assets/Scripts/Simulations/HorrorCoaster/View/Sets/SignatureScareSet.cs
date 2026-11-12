using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Indices into <see cref="SigEntity.p"/> shared by <c>SignatureScareBuilder</c> and the animator.</summary>
    public static class SigPart
    {
        // SprinterLeap: beast root, four leg pivots, jaw pivot
        public const int BeastRoot = 0, BeastLeg0 = 1, BeastJaw = 5;
        // SideLeap: ghoul root, arm pivots, jaw pivot, perch pillar (static)
        public const int GhoulRoot = 0, GhoulArmL = 1, GhoulArmR = 2, GhoulJaw = 3, GhoulPerch = 4;
        // SnakeStrike: head, hood, jaw, tongue, swallowed bulge, rider in the mouth, then the body segments
        public const int SnakeHead = 0, SnakeHood = 1, SnakeJaw = 2, SnakeTongue = 3, SnakeBulge = 4, SnakeRider = 5, SnakeSeg0 = 6, SnakeSegs = 48;
        // SpikeWall: left panel, right panel, then SpikeCount spikes per side
        public const int SpikeLeftPanel = 0, SpikeRightPanel = 1, SpikeLeft0 = 2, SpikeCount = 15, SpikeRight0 = 2 + SpikeCount;
        // BatFlood: BatCount bats, then the mouth glow
        public const int BatCount = 150, BatMouth = BatCount;
    }

    /// <summary>One pooled signature-scare entity: a hidden root at the origin, its own light and the animated parts (all posed in world space).</summary>
    [Serializable]
    public sealed class SigEntity
    {
        public ScareKind kind;
        public GameObject root;
        public Light light;
        public Transform[] p;
        [Tooltip("Seconds after the sting starts at which the entity is hidden again.")]
        public float duration = 2f;
        public float lightMax = 5f;
    }

    /// <summary>
    /// The five signature scares, each a different SHAPE of fear: a beast sprinting at the cart from far down the track, a ghoul dropping
    /// across the view from a high perch, a cobra eating a rider ahead of you, a wall firing spikes through the cars, and a flood of bats
    /// out of the pumpkin's mouth. Read-only on the simulation. Everything is a function of the rail, the event and PhaseTime, so a re-render
    /// is identical. The entities live in world space on the rail (so they are really there as the cart approaches) and only the final
    /// contact (a leap, a pass) is blended onto the camera. Lights ramp over at least 0.1 s; nothing flashes faster than 3 Hz.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class SignatureScareSet : ActSetBase
    {
        public override ActId Act => ActId.Station;

        public SigEntity[] entities;
        public float stingSeconds = 0.6f;

        int _active = -1;
        int _activeEvent = -1;
        bool _look;
        bool _shown;
        float _frozenS;
        float _rightFire;
        Vector3 _focus;

        static float Sm(float x) { return ActSetKit.Smooth01(x); }
        static float EO(float x) { x = Mathf.Clamp01(x); x = 1f - x; return 1f - x * x * x; }
        static float EI(float x) { x = Mathf.Clamp01(x); return x * x; }
        static float H(int a, int salt) { return ActSetKit.Hash01(a, salt); }

        public static bool IsSignature(ScareKind k) { return k >= ScareKind.SprinterLeap && k <= ScareKind.BatFlood; }

        protected override void OnBound()
        {
            HideAll();
            _active = -1;
            _activeEvent = -1;
            _shown = false;
            ClearLook();
        }

        void HideAll()
        {
            if (entities == null) return;
            foreach (SigEntity e in entities)
            {
                if (e != null) Hide(e);
            }
        }

        void Hide(SigEntity e)
        {
            if (e.root != null) e.root.SetActive(false);
            if (e.light != null) { e.light.intensity = 0f; e.light.enabled = false; }
        }

        void ClearLook()
        {
            if (_look)
            {
                CoasterLookTarget.Clear();
                _look = false;
            }
        }

        SigEntity Find(ScareKind kind)
        {
            if (entities == null) return null;
            foreach (SigEntity e in entities) if (e != null && e.kind == kind) return e;
            return null;
        }

        void LateUpdate()
        {
            if (State == null || entities == null) return;

            ScarePhase ph = State.Phase;
            SigEntity e = null;
            if (ph != ScarePhase.Idle && IsSignature(State.CurrentEvent.kind)) e = Find(State.CurrentEvent.kind);
            int idx = State.CurrentEventIndex;

            int na = e != null ? Array.IndexOf(entities, e) : -1;
            if (na != _active || (e != null && idx != _activeEvent))
            {
                if (_active >= 0 && entities[_active] != null) Hide(entities[_active]);
                _active = na;
                _activeEvent = e != null ? idx : -1;
                _shown = false;
                _rightFire = -1f;
                _frozenS = -1f;
            }

            if (e == null || e.p == null)
            {
                ClearLook();
                return;
            }

            bool pre = ph == ScarePhase.PreQuiet;
            float pt = State.PhaseTime;
            float tt = pre ? -1f : (ph == ScarePhase.Sting ? pt : stingSeconds + pt);
            if (!pre && tt > e.duration)
            {
                if (_shown) Hide(e);
                _shown = false;
                ClearLook();
                return;
            }

            float preLen = Mathf.Max(0.5f, State.CurrentEvent.preQuietSeconds);
            if (!_shown)
            {
                e.root.SetActive(true);
                _shown = true;
            }

            switch (e.kind)
            {
                case ScareKind.SprinterLeap: Sprinter(e, pre, pt, tt, preLen); break;
                case ScareKind.SideLeap: Leaper(e, pre, pt, tt, preLen); break;
                case ScareKind.SnakeStrike: Snake(e, pre, pt, tt); break;
                case ScareKind.SpikeWall: Spikes(e, pre, pt, tt); break;
                case ScareKind.BatFlood: Bats(e, pre, pt, tt, preLen, idx); break;
            }

            CoasterLookTarget.Set(_focus, pre ? 0.45f : 1f);
            _look = true;
        }

        // ---------------------------------------------------------------- helpers

        Vector3 RP(float s, float lat, float up) { return ScareView.RailPoint(State.Rail, s, lat, up); }

        void FrameAt(float s, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r)
        {
            float c = Mathf.Clamp(s, 0f, State.Rail.Length);
            SetUtil.Frame(State.Rail, c, out p, out f, out u, out r);
            p += f * (s - c);
        }

        static void Put(Transform t, Vector3 pos, Quaternion rot)
        {
            if (t != null) t.SetPositionAndRotation(pos, rot);
        }

        static Quaternion Look(Vector3 dir, Vector3 up)
        {
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            return Quaternion.LookRotation(dir.normalized, up);
        }

        static void Show(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        static void Light(SigEntity e, Vector3 pos, float intensity)
        {
            if (e.light == null) return;
            e.light.transform.position = pos;
            e.light.intensity = intensity;
            bool on = intensity > 0.01f;
            if (e.light.enabled != on) e.light.enabled = on;
        }

        static float Ramp(float tt, float on, float offStart, float offEnd, float max)
        {
            if (tt < 0f) return 0f;
            float up = Sm((tt - on) / 0.1f);
            float down = 1f - Sm((tt - offStart) / Mathf.Max(0.05f, offEnd - offStart));
            return max * up * down;
        }

        // ---------------------------------------------------------------- SprinterLeap
        // pre: a beast appears 90 m down the track and gallops at the cart, red eyes first. sting: it covers the last 6 m in one leap and
        // fills the lens (0.28 s), snaps and thrashes there, then is flung up and over the cart.

        const float BeastScale = 1.3f;
        static readonly Vector3 BeastHead = new Vector3(0f, 0.62f, 0.95f);

        void Sprinter(SigEntity e, bool pre, float pt, float tt, float preLen)
        {
            Transform[] p = e.p;
            float evD = State.CurrentEvent.distance;
            Vector3 pos;
            Quaternion rot;
            float run;
            float jaw;
            float gain;
            if (pre)
            {
                float u = Mathf.Clamp01(pt / preLen);
                float s = evD + 6f + 84f * Mathf.Pow(1f - u, 1.35f);
                FrameAt(s, out Vector3 bp, out Vector3 bf, out Vector3 bu, out Vector3 br);
                pos = bp + br * (Mathf.Sin(pt * 4.1f) * 0.3f * (1f - u)) + bu * 0.15f;
                rot = Look(-bf, bu);
                run = pt * (7f + 9f * u);
                jaw = 8f;
                gain = Sm(u * 1.4f);
                _focus = pos + bu * 0.8f;
            }
            else
            {
                FrameAt(evD + 6f, out Vector3 sp, out Vector3 sf, out Vector3 su, out Vector3 sr);
                Vector3 start = sp + su * 0.15f;
                ScareView.Eye(State, out Vector3 ep, out Quaternion er);
                Vector3 target = ep + er * Vector3.forward * 0.55f - er * Vector3.up * 0.15f;
                float k = EO(tt / 0.28f);
                Vector3 mid = Vector3.Lerp(start, target, k) + su * (0.9f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(tt / 0.28f)));
                rot = Look(ep - mid, Vector3.up) * Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(tt * 30f) * k);
                pos = mid - rot * (BeastHead * BeastScale) * k;
                float fling = EI((tt - 0.9f) / 0.4f);
                pos += (su * 2.2f + sf * 1.5f) * fling;
                rot = rot * Quaternion.Euler(-40f * fling, 0f, 0f);
                run = tt * 16f;
                jaw = tt < 0.28f ? 20f + 40f * (tt / 0.28f) : 50f + 12f * Mathf.Sin(tt * 40f);
                gain = 1f;
                _focus = target;
            }

            Put(p[SigPart.BeastRoot], pos, rot);
            p[SigPart.BeastRoot].localScale = Vector3.one * BeastScale;
            for (int i = 0; i < 4; i++)
            {
                float ph = i < 2 ? i * Mathf.PI : Mathf.PI * 0.5f + (i - 2) * Mathf.PI;
                float amp = pre ? 55f : 75f;
                p[SigPart.BeastLeg0 + i].localRotation = Quaternion.Euler(Mathf.Sin(run * 6.28f * 0.5f + ph) * amp, 0f, 0f);
            }

            p[SigPart.BeastJaw].localRotation = Quaternion.Euler(jaw, 0f, 0f);
            Light(e, pos + rot * new Vector3(0f, 1.2f, 1.6f), pre ? 1.4f * gain : Ramp(tt, 0.05f, 0.8f, 1.2f, e.lightMax));
        }

        // ---------------------------------------------------------------- SideLeap
        // pre: a ghoul crouches on a pillar 4 m to the right and 3 m up, eyes glowing, turning to watch the cart. sting: it throws itself across
        // the cart on a curve that passes a hand's breadth from the lens (right to left), arms wide and mouth open, and tumbles away behind.

        void Leaper(SigEntity e, bool pre, float pt, float tt, float preLen)
        {
            Transform[] p = e.p;
            float evD = State.CurrentEvent.distance;
            const float side = 1f;
            FrameAt(evD + 8f, out Vector3 pp, out Vector3 pf, out Vector3 pu, out Vector3 pr);
            Vector3 pillar = pp + pr * (side * 4.3f) + pu * 1.5f;
            Vector3 perch = pp + pr * (side * 4.3f) + pu * 3.05f;
            Put(p[SigPart.GhoulPerch], pillar, Quaternion.LookRotation(pf, pu));

            ScareView.Eye(State, out Vector3 ep, out Quaternion er);
            Vector3 pos;
            Quaternion rot;
            float armA;
            float jaw;
            if (pre)
            {
                float breathe = Mathf.Sin(pt * 3.4f) * 0.03f;
                pos = perch + pu * breathe;
                Vector3 to = ep - pos;
                to.y = 0f;
                rot = Look(to, Vector3.up);
                armA = -15f + 8f * Mathf.Sin(pt * 2.1f);
                jaw = 6f;
                p[SigPart.GhoulRoot].localScale = new Vector3(1f, 0.72f + breathe, 1f);
                _focus = perch + pu * 0.6f;
            }
            else
            {
                p[SigPart.GhoulRoot].localScale = Vector3.one;
                Vector3 near = ep + er * Vector3.forward * 0.95f - er * Vector3.up * 0.05f;
                Vector3 away = ep + er * (new Vector3(-side * 3.2f, -0.9f, -3.0f));
                const float tNear = 0.24f;
                const float tAway = 0.62f;
                if (tt < tNear)
                {
                    // high arc from the perch to the lens: drops fast at the end
                    float k = EI(tt / tNear) * 0.55f + 0.45f * (tt / tNear);
                    pos = Vector3.Lerp(perch, near, k);
                }
                else
                {
                    float k = Sm((tt - tNear) / (tAway - tNear));
                    pos = Vector3.Lerp(near, away, k);
                }

                Vector3 d = ep - pos;
                rot = Look(d, Vector3.up) * Quaternion.Euler(0f, 0f, -side * (20f + 160f * Sm((tt - tNear) / 0.4f)));
                armA = tt < tNear ? Mathf.Lerp(-15f, -105f, tt / tNear) : -105f + 25f * Mathf.Sin(tt * 25f);
                jaw = 55f;
                _focus = pos;
            }

            Put(p[SigPart.GhoulRoot], pos, rot);
            p[SigPart.GhoulArmL].localRotation = Quaternion.Euler(armA, 0f, -12f);
            p[SigPart.GhoulArmR].localRotation = Quaternion.Euler(armA, 0f, 12f);
            p[SigPart.GhoulJaw].localRotation = Quaternion.Euler(jaw, 0f, 0f);
            Light(e, pos + rot * new Vector3(0f, 1.4f, 1.0f), pre ? 0.9f : Ramp(tt, 0.05f, 0.5f, 0.8f, e.lightMax));
        }

        // ---------------------------------------------------------------- SnakeStrike
        // pre: a cobra rises out of the dark beside the rider's car, hood flaring, eyes on the viewer. sting: it strikes the rider in 0.14 s,
        // lifts them out of the seat, shakes them, then swallows (a bulge slides down its neck) and sinks away as the cart rolls past.

        const float StrikeEnd = 0.14f;

        void Snake(SigEntity e, bool pre, float pt, float tt)
        {
            Transform[] p = e.p;
            int seat = Mathf.Max(0, State.TakenSeat);
            float off = RiderRoster.SeatSpacing * (seat + 1);
            float evD = State.CurrentEvent.distance;
            float s;
            if (pre) s = evD + off;
            else if (tt < StrikeEnd) { s = State.Distance + off; _frozenS = s; }
            else s = _frozenS >= 0f ? _frozenS : evD + off;

            FrameAt(s, out Vector3 A, out Vector3 f, out Vector3 u, out Vector3 r);
            Vector3 B = A + r * -3.6f + u * -2.8f - f * 0.5f;
            Vector3 mouthRider = A + r * (RiderRoster.SeatX - 0.0f) + u * 1.05f;
            Vector3 cocked = A + r * -2.9f + u * 2.3f - f * 0.3f;
            ScareView.Eye(State, out Vector3 ep, out _);

            Vector3 head;
            Vector3 dir;
            float hood;
            float jaw;
            bool riderIn = false;
            float bulge = -1f;
            float tw = tt;
            if (pre)
            {
                float rise = Sm(pt / 1.2f);
                Vector3 hp = A + r * (-2.6f + Mathf.Sin(pt * 2.1f) * 0.25f) + u * Mathf.Lerp(-1.0f, 2.2f, rise) + f * Mathf.Lerp(-1f, 0.5f, rise);
                head = Vector3.Lerp(hp, cocked, Sm((pt - 1.2f) / 1.0f));
                dir = ep - head;
                hood = Sm((pt - 0.9f) / 0.6f);
                jaw = 18f + 10f * Mathf.Sin(pt * 5f);
                _focus = head;
            }
            else
            {
                if (tw < StrikeEnd)
                {
                    head = Vector3.Lerp(cocked, mouthRider, EI(tw / StrikeEnd));
                    dir = mouthRider - head;
                    jaw = 75f;
                }
                else
                {
                    Vector3 up1 = A + r * -1.2f + u * 4.6f;
                    float lift = Sm((tw - 0.35f) / 0.6f);
                    head = Vector3.Lerp(mouthRider, up1, lift);
                    head += (r * Mathf.Sin(tw * 38f) + u * Mathf.Cos(tw * 31f)) * (0.1f * (1f - lift));
                    float sink = Sm((tw - 1.9f) / 0.5f);
                    head = Vector3.Lerp(head, B + u * 0.5f, sink);
                    dir = Vector3.Lerp(mouthRider - head, u * 1f - r * 0.25f, Sm((tw - 0.3f) / 0.4f));
                    jaw = tw < 1.15f ? 6f : 30f * Mathf.Clamp01(1f - (tw - 1.15f) * 1.5f);
                    riderIn = tw >= StrikeEnd && tw < 1.15f;
                    if (tw > 1.1f && tw < 2.2f) bulge = Mathf.Clamp01((tw - 1.1f) / 0.9f);
                }

                hood = tw < 1.3f ? 1f : 1f - Sm((tw - 1.3f) / 0.4f);
                _focus = head;
            }

            dir = dir.sqrMagnitude < 1e-6f ? u : dir.normalized;
            Quaternion headRot = Look(dir, Vector3.up);
            Put(p[SigPart.SnakeHead], head, headRot);
            p[SigPart.SnakeJaw].localRotation = Quaternion.Euler(jaw, 0f, 0f);
            float tongue = Mathf.Max(0f, Mathf.Sin((pre ? pt : tw) * 2f * Mathf.PI * 2.2f));
            p[SigPart.SnakeTongue].localScale = new Vector3(1f, 1f, 0.2f + 1.0f * tongue);
            Put(p[SigPart.SnakeHood], head - dir * 0.32f, headRot);
            p[SigPart.SnakeHood].localScale = new Vector3(1.05f * hood, 1.25f * hood, 0.12f);
            Show(p[SigPart.SnakeHood], hood > 0.02f);

            // body: cubic curve from the base to the head, spheres tapering toward the head
            float dist = Vector3.Distance(B, head);
            Vector3 c1 = B + Vector3.up * Mathf.Max(1.2f, dist * 0.55f) + r * -0.4f;
            Vector3 c2 = head - dir * Mathf.Max(0.9f, dist * 0.42f);
            for (int i = 0; i < SigPart.SnakeSegs; i++)
            {
                float t = (i + 0.5f) / SigPart.SnakeSegs * 0.97f;
                Vector3 q = Bez(B, c1, c2, head, t);
                float rad = Mathf.Lerp(0.30f, 0.19f, t) * 2f;
                Transform sg = p[SigPart.SnakeSeg0 + i];
                sg.position = q;
                sg.localScale = Vector3.one * rad;
            }

            Transform bt = p[SigPart.SnakeBulge];
            Show(bt, bulge >= 0f);
            if (bulge >= 0f)
            {
                bt.position = Bez(B, c1, c2, head, Mathf.Lerp(0.93f, 0.5f, bulge));
                bt.localScale = Vector3.one * (0.46f * (1f - 0.4f * bulge));
            }

            Transform rf = p[SigPart.SnakeRider];
            Show(rf, riderIn);
            if (riderIn)
            {
                rf.SetPositionAndRotation(head + dir * 0.15f - Vector3.up * 0.35f, Look(dir, Vector3.up) * Quaternion.Euler(70f + 25f * Mathf.Sin(tw * 24f), 0f, 0f));
            }

            Light(e, head + u * 0.8f + r * 0.8f, pre ? 1.2f * Sm(pt / 1.0f) : Ramp(tw, 0f, 1.6f, 2.2f, e.lightMax));
        }

        static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float k = 1f - t;
            return k * k * k * a + 3f * k * k * t * b + 3f * k * t * t * c + t * t * t * d;
        }

        // ---------------------------------------------------------------- SpikeWall
        // pre: a rusted plate with warning lamps sits beside the car of the rider who is about to be taken. sting: spikes fire across the car
        // in 0.06 s (skewering the rider) and hold; a second plate beside the viewer fires as they reach it and stops 0.4 m from the face.

        static readonly float[] LeftRows = { 0.55f, 0.9f, 1.25f };
        static readonly float[] RightRows = { 1.0f, 1.3f, 1.6f };
        const float LeftLen = 2.0f, RightLen = 1.55f, WallX = 2.35f;
        /// <summary>Metres ahead of the event point where the viewer-side plate stands (the second volley fires 3.2 m before the viewer reaches it).</summary>
        public const float SpikeVolleyAhead = 9f;

        static float Ext(float x, float hold)
        {
            if (x < 0f) return 0f;
            if (x < 0.06f) return EO(x / 0.06f) * 1.06f;
            if (x < hold) return 1f + 0.06f * Mathf.Exp(-(x - 0.06f) * 18f) * Mathf.Sin((x - 0.06f) * 70f);
            return 1f - Sm((x - hold) / 0.25f);
        }

        void Spikes(SigEntity e, bool pre, float pt, float tt)
        {
            Transform[] p = e.p;
            int seat = Mathf.Max(0, State.TakenSeat);
            float off = RiderRoster.SeatSpacing * (seat + 1);
            float evD = State.CurrentEvent.distance;

            float sL;
            if (pre) sL = evD + off;
            else if (tt < 0.45f) { sL = State.Distance + off; _frozenS = sL; }
            else sL = _frozenS >= 0f ? _frozenS : evD + off;

            FrameAt(sL, out Vector3 lp, out Vector3 lf, out Vector3 lu, out Vector3 lr);
            Put(p[SigPart.SpikeLeftPanel], lp + lr * -WallX, Look(lr, lu));

            float sR = evD + SpikeVolleyAhead;
            FrameAt(sR, out Vector3 rp, out Vector3 rf, out Vector3 ru, out Vector3 rr);
            Put(p[SigPart.SpikeRightPanel], rp + rr * WallX, Look(-rr, ru));

            if (!pre && _rightFire < 0f && State.Distance >= sR - 3.2f) _rightFire = tt;
            float exL = pre ? 0.03f : Ext(tt, 1.3f);
            float exR = pre ? 0.03f : (_rightFire >= 0f ? Ext(tt - _rightFire, 0.9f) : 0.03f);
            exL = Mathf.Max(exL, 0.03f);
            exR = Mathf.Max(exR, 0.03f);
            for (int i = 0; i < SigPart.SpikeCount; i++)
            {
                p[SigPart.SpikeLeft0 + i].localScale = new Vector3(0.16f, 0.16f, exL * LeftLen);
                p[SigPart.SpikeRight0 + i].localScale = new Vector3(0.14f, 0.14f, exR * RightLen);
            }

            float lamp = 0.5f + 0.5f * Mathf.Sin(Mathf.Max(pt, 0f) * 12.5f);
            Vector3 lightPos = lp + lr * -1.3f + lu * 1.2f;
            float li;
            if (pre) li = 0.6f + 2.4f * lamp * Sm(pt / 0.6f);
            else li = Ramp(tt, 0f, 0.9f, 1.4f, e.lightMax);
            Light(e, lightPos, li);
            _focus = pre ? lp + lr * -1.2f + lu * 0.9f : (_rightFire >= 0f ? rp + rr * 1.0f + ru * 1.3f : lp + lr * -1.2f + lu * 0.9f);
        }

        // ---------------------------------------------------------------- BatFlood
        // The pumpkin's mouth glows and begins to leak bats a second before the cart reaches it; at the sting a flood pours out of the mouth,
        // streams past and over the cart, and a few bats hit the lens. Bats live on the rail, so they are there whatever the speed.

        void Bats(SigEntity e, bool pre, float pt, float tt, float preLen, int idx)
        {
            Transform[] p = e.p;
            float evD = State.CurrentEvent.distance;
            float now = pre ? pt - preLen : tt;
            const float mouthAhead = 10f;
            FrameAt(evD + mouthAhead, out Vector3 mp, out Vector3 mf, out Vector3 mu, out Vector3 mr);
            Vector3 mouth = mp + mu * 1.6f;
            ScareView.Eye(State, out Vector3 ep, out _);
            _focus = mouth;

            for (int i = 0; i < SigPart.BatCount; i++)
            {
                Transform bt = p[i];
                int sd = idx * 256 + i;
                float ts = -1.4f + 2.2f * Mathf.Pow(H(sd, 51), 0.8f);
                float dur = 0.9f + 0.8f * H(sd, 52);
                float w = (now - ts) / dur;
                bool on = w >= 0f && w <= 1f;
                Show(bt, on);
                if (!on) continue;

                float a = H(sd, 53) * 6.283f;
                float rs = Mathf.Sqrt(H(sd, 54)) * 2.4f;
                bool clip = (i % 11) == 4;
                float b = H(sd, 55) * 6.283f;
                float re = clip ? 0.10f + H(sd, 56) * 0.18f : 0.5f + H(sd, 56) * 2.3f;
                float ph = H(sd, 57) * 6.283f;
                Vector3 Pos(float ww)
                {
                    float s = Mathf.Lerp(evD + mouthAhead, evD - 14f, ww);
                    float lat = Mathf.Lerp(Mathf.Cos(a) * rs, 0.4f + Mathf.Cos(b) * re, Sm(ww)) + Mathf.Sin(ww * 13f + ph) * 0.3f * Mathf.Sin(Mathf.PI * ww);
                    float up = Mathf.Lerp(1.6f + Mathf.Sin(a) * rs * 0.8f, 1.3f + Mathf.Sin(b) * re * 0.8f, Sm(ww)) + Mathf.Cos(ww * 11f + ph) * 0.3f * Mathf.Sin(Mathf.PI * ww);
                    return RP(s, lat, up);
                }

                Vector3 pos = Pos(w);
                Vector3 vel = Pos(Mathf.Min(1f, w + 0.03f)) - Pos(Mathf.Max(0f, w - 0.03f));
                float flap = Mathf.Lerp(0.4f, 1f, 0.5f + 0.5f * Mathf.Sin(now * 32f + ph));
                float size = 0.38f + 0.14f * H(sd, 58) + (clip ? 0.12f : 0f);
                bt.SetPositionAndRotation(pos, Look(vel, Vector3.up) * Quaternion.Euler(0f, 0f, (H(sd, 59) - 0.5f) * 90f));
                bt.localScale = new Vector3(size * flap, size * (0.8f + 0.2f * flap), size);
            }

            Transform glow = p[SigPart.BatMouth];
            float g = pre ? Sm(pt / preLen) : 1f - Sm((tt - 1.2f) / 1.0f);
            glow.position = mouth;
            glow.rotation = Look(-mf, mu);
            glow.localScale = new Vector3(7f, 5.5f, 0.1f) * (0.4f + 0.6f * g);
            Show(glow, g > 0.02f);

            Light(e, mouth - mf * 3f, 6f * g);
        }
    }
}
