using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Indices into <see cref="MicroEntity.p"/> shared by the builder and the animator.</summary>
    public static class MicroPart
    {
        // SpiderDrop: body, thread, then hip/knee pivots per leg (8 legs: 2 + 2*i, 3 + 2*i)
        public const int SpiderBody = 0, SpiderThread = 1, SpiderLeg0 = 2, SpiderLegs = 8;
        // WindowFace
        public const int WinFace = 0, WinHandL = 1, WinHandR = 2;
        // SkeletonPop
        public const int SkelRoot = 0, SkelHead = 1, SkelJaw = 2, SkelArm = 3, SkelHole = 4;
        // ClawHand: hand A, hand B, then 5 finger pivots each
        public const int ClawA = 0, ClawB = 1, ClawFingerA = 2, ClawFingerB = 7;
        // BatSwarm: p[0..BatCount-1]
        public const int BatCount = 48;
        // CoffinLid
        public const int CofCoffin = 0, CofLid = 1, CofCorpse = 2, CofHead = 3, CofJaw = 4, CofArmL = 5, CofArmR = 6, CofGlow = 7;
        // ShadowRush
        public const int ShRoot = 0, ShArmL = 1, ShArmR = 2, ShLegL = 3, ShLegR = 4;
        // MirrorFace
        public const int MirMirror = 0, MirPane = 1, MirFace = 2, MirCrack = 3, MirShard0 = 4, MirShards = 24;
    }

    /// <summary>One pooled micro-scare entity: a hidden root, its own light and the animated parts.</summary>
    [Serializable]
    public sealed class MicroEntity
    {
        public ScareKind kind;
        public GameObject root;
        public Light light;
        public Transform[] p;
        [Tooltip("Seconds after the sting starts at which the entity is hidden again.")]
        public float duration = 0.9f;
        [Tooltip("Peak intensity of the entity's own point light.")]
        public float lightMax = 4.5f;
    }

    /// <summary>
    /// The micro jump scares (spider drop, window face, skeleton pop, claw hand, bat swarm, coffin lid, shadow rush, mirror face).
    /// One pooled entity per kind, prebuilt hidden by <c>MicroScareBuilder</c>. Read-only on the simulation: it shows an entity only
    /// while the current event is a micro kind, anchors it to the rail at the event distance during PreQuiet, and to the cart during
    /// the Sting (so a lunge stays in front of the lens at any speed). Everything is a function of PhaseTime and the event index,
    /// so a re-render is identical. Lights ramp over at least 0.1 s and nothing flashes faster than 3 Hz.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class MicroScareSet : ActSetBase
    {
        public override ActId Act => ActId.Station;

        public MicroEntity[] entities;
        [Tooltip("Length of the sim's Sting phase; the Aftermath clock continues the same timeline.")]
        public float stingSeconds = 0.6f;

        static readonly Vector3 NominalEye = new Vector3(0.4f, 1.3f, -0.3f);

        int _active = -1;
        int _activeEvent = -1;
        bool _look;
        bool _shown;

        // lens frame in entity-root space
        Vector3 _lens;
        Vector3 _fwdL = Vector3.forward;
        Vector3 _upL = Vector3.up;
        Vector3 _rightL = Vector3.right;
        Vector3 _focus;

        protected override void OnBound()
        {
            HideAll();
            _active = -1;
            _activeEvent = -1;
            _shown = false;
            if (_look)
            {
                CoasterLookTarget.Clear();
                _look = false;
            }
        }

        void HideAll()
        {
            if (entities == null) return;
            foreach (MicroEntity e in entities)
            {
                if (e == null) continue;
                if (e.root != null) e.root.SetActive(false);
                if (e.light != null) { e.light.intensity = 0f; e.light.enabled = false; }
            }
        }

        MicroEntity Find(ScareKind kind)
        {
            if (entities == null) return null;
            foreach (MicroEntity e in entities) if (e != null && e.kind == kind) return e;
            return null;
        }

        void LateUpdate()
        {
            if (State == null || entities == null) return;

            ScarePhase ph = State.Phase;
            MicroEntity e = null;
            if (ph != ScarePhase.Idle && State.CurrentEvent.kind.IsMicro()) e = Find(State.CurrentEvent.kind);
            int idx = State.CurrentEventIndex;

            int na = e != null ? Array.IndexOf(entities, e) : -1;
            if (na != _active || (e != null && idx != _activeEvent))
            {
                if (_active >= 0 && entities[_active] != null) Hide(entities[_active]);
                _active = na;
                _activeEvent = e != null ? idx : -1;
                _shown = false;
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

            float preLen = Mathf.Max(0.2f, State.CurrentEvent.preQuietSeconds);
            Place(e, pre, pt, preLen);
            if (!_shown)
            {
                e.root.SetActive(true);
                _shown = true;
            }

            ComputeLens(e, pre);
            Animate(e, pre, pt, tt, preLen, idx);

            CoasterLookTarget.Set(e.root.transform.TransformPoint(_focus), pre ? 0.3f : 1f);
            _look = true;
        }

        void Hide(MicroEntity e)
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

        // ---------------------------------------------------------------- frames

        void Place(MicroEntity e, bool pre, float pt, float preLen)
        {
            Vector3 pos, f, u;
            if (pre)
            {
                float d = State.CurrentEvent.distance;
                if (e.kind == ScareKind.ShadowRush) d += ShadowAhead(pt, preLen);
                State.Rail.Sample(Mathf.Clamp(d, 0f, State.Rail.Length), out pos, out f, out u);
            }
            else
            {
                pos = State.CartPosition; f = State.CartForward; u = State.CartUp;
            }

            if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
            if (u.sqrMagnitude < 1e-6f) u = Vector3.up;
            e.root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(f, u));
        }

        // The shadow stands 10 m ahead of the event point and sprints along the rail in the last run seconds of the quiet.
        const float ShadowStand = 10f, ShadowLens = 0.7f;
        float ShadowAhead(float pt, float preLen)
        {
            float run = Mathf.Min(0.6f, preLen * 0.85f);
            float u = Mathf.Clamp01((pt - (preLen - run)) / run);
            return Mathf.Lerp(ShadowStand, ShadowLens, u * u);
        }

        void ComputeLens(MicroEntity cur, bool pre)
        {
            Transform rt = cur.root.transform;
            Camera cam = Camera.main;
            if (pre || cam == null || rt == null)
            {
                _lens = NominalEye;
                _fwdL = Vector3.forward;
            }
            else
            {
                _lens = rt.InverseTransformPoint(cam.transform.position);
                Vector3 cf = rt.InverseTransformDirection(cam.transform.forward);
                _fwdL = Vector3.Slerp(Vector3.forward, cf, 0.65f).normalized;
            }

            _rightL = Vector3.Cross(Vector3.up, _fwdL);
            if (_rightL.sqrMagnitude < 1e-4f) _rightL = Vector3.right;
            _rightL.Normalize();
            _upL = Vector3.Cross(_fwdL, _rightL).normalized;
        }

        // ---------------------------------------------------------------- helpers

        static float Sm(float x) { return ActSetKit.Smooth01(x); }
        static float EO(float x) { x = Mathf.Clamp01(x); x = 1f - x; return 1f - x * x * x; }
        static float EI(float x) { x = Mathf.Clamp01(x); return x * x; }
        static float H(int a, int salt) { return ActSetKit.Hash01(a, salt); }

        /// <summary>Peak light: ramps in over 0.1 s from <paramref name="on"/>, fades out between the two off times.</summary>
        static float Ramp(float tt, float on, float offStart, float offEnd, float max)
        {
            if (tt < 0f) return 0f;
            float up = Sm((tt - on) / 0.1f);
            float down = 1f - Sm((tt - offStart) / Mathf.Max(0.05f, offEnd - offStart));
            return max * up * down;
        }

        static void SetLight(MicroEntity e, Vector3 localPos, float intensity)
        {
            if (e.light == null) return;
            e.light.transform.localPosition = localPos;
            e.light.intensity = intensity;
            bool on = intensity > 0.01f;
            if (e.light.enabled != on) e.light.enabled = on;
        }

        static void ShowT(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        /// <summary>Nth event of its kind (0-based): used to alternate left / right deterministically.</summary>
        int Ordinal(ScareKind kind, int idx)
        {
            int n = 0;
            for (int i = 0; i < idx && i < State.Events.Count; i++) if (State.Events[i].kind == kind) n++;
            return n;
        }

        float Side(ScareKind kind, int idx)
        {
            return (Ordinal(kind, idx) & 1) == 0 ? -1f : 1f;
        }

        Quaternion Face(Vector3 from, Vector3 to, float roll)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
            return Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        }

        void Animate(MicroEntity e, bool pre, float pt, float tt, float preLen, int idx)
        {
            switch (e.kind)
            {
                case ScareKind.SpiderDrop: Spider(e, pre, pt, tt, idx); break;
                case ScareKind.WindowFace: Window(e, pre, pt, tt, idx); break;
                case ScareKind.SkeletonPop: Skeleton(e, pre, pt, tt, idx); break;
                case ScareKind.ClawHand: Claw(e, pre, pt, tt, idx); break;
                case ScareKind.BatSwarm: Bats(e, pre, pt, tt, idx); break;
                case ScareKind.CoffinLid: Coffin(e, pre, pt, tt, idx); break;
                case ScareKind.ShadowRush: Shadow(e, pre, pt, tt, preLen, idx); break;
                case ScareKind.MirrorFace: Mirror(e, pre, pt, tt, idx); break;
            }
        }

        // ---------------------------------------------------------------- SpiderDrop
        // sting: drop 0..0.12 (ease-in, jerk stop on the thread), splayed + lashing to 0.55, retract up 0.55..0.85

        void Spider(MicroEntity e, bool pre, float pt, float tt, int idx)
        {
            Transform[] p = e.p;
            float side = Side(e.kind, idx);
            float ph0 = H(idx, 3) * 6.283f;
            Vector3 hang = new Vector3(0.4f + side * 0.25f, 3.3f, 1.7f);
            Vector3 target = _lens + _fwdL * 0.95f - _upL * 0.05f;
            Vector3 body;
            float scale = 1f;
            float splay;
            if (pre)
            {
                scale = Sm(pt / 0.25f);
                body = hang + new Vector3(Mathf.Sin(pt * 3f + ph0) * 0.06f, Mathf.Sin(pt * 2f) * 0.05f, 0f);
                splay = 0f;
            }
            else
            {
                float u = Mathf.Clamp01(tt / 0.12f);
                if (tt < 0.55f)
                {
                    body = Vector3.Lerp(hang, target, EI(u));
                    if (tt > 0.12f) body += _upL * (Mathf.Exp(-(tt - 0.12f) * 14f) * Mathf.Sin((tt - 0.12f) * 60f) * 0.07f);
                }
                else
                {
                    body = Vector3.Lerp(target, target + _upL * 3.8f, EI((tt - 0.55f) / 0.3f));
                }

                splay = Sm(u);
            }

            Transform b = p[MicroPart.SpiderBody];
            b.localPosition = body;
            b.localRotation = Face(body, _lens, side * 8f);
            b.localScale = Vector3.one * Mathf.Max(0.001f, scale);

            for (int i = 0; i < MicroPart.SpiderLegs; i++)
            {
                bool right = i < 4;
                int k = i & 3;
                float yaw = (k - 1.5f) * 24f;
                float wig = pre ? Mathf.Sin(pt * 6f + i * 1.7f + ph0) * 6f : Mathf.Sin(tt * 38f + i * 1.9f + ph0) * 9f * splay;
                float femur = Mathf.Lerp(75f, 38f, splay) + wig;
                float knee = Mathf.Lerp(150f, 95f, splay) - wig * 0.6f;
                p[MicroPart.SpiderLeg0 + 2 * i].localRotation = Quaternion.Euler(0f, right ? yaw : 180f - yaw, femur);
                p[MicroPart.SpiderLeg0 + 2 * i + 1].localRotation = Quaternion.Euler(0f, 0f, -knee);
            }

            Vector3 top = new Vector3(hang.x, hang.y + 6f, hang.z);
            Vector3 from = body + Vector3.up * 0.25f;
            Vector3 dir = top - from;
            float len = dir.magnitude;
            Transform th = p[MicroPart.SpiderThread];
            ShowT(th, scale > 0.02f);
            th.localPosition = (from + top) * 0.5f;
            th.localRotation = Quaternion.FromToRotation(Vector3.up, dir / Mathf.Max(0.01f, len));
            th.localScale = new Vector3(0.014f, len * 0.5f, 0.014f);

            SetLight(e, _lens + _fwdL * 0.35f + _upL * 0.3f, pre ? 0f : Ramp(tt, 0f, 0.55f, 0.85f, e.lightMax));
            _focus = body;
        }

        // ---------------------------------------------------------------- WindowFace
        // sting: slam in 0..0.1, hands 0.03..0.13, hold with a shudder, pull back into the dark 0.55..0.85

        void Window(MicroEntity e, bool pre, float pt, float tt, int idx)
        {
            Transform[] p = e.p;
            float s = Side(e.kind, idx);
            Vector3 far = _lens + _rightL * (s * 3.0f) + _fwdL * 0.5f + _upL * 0.05f;
            Vector3 slam = _lens + _fwdL * 0.75f + _rightL * (s * 0.38f);
            Vector3 pos;
            float sc;
            if (pre)
            {
                pos = far + _upL * (Mathf.Sin(pt * 2.5f) * 0.04f);
                sc = 0.8f * Sm(pt / 0.25f);
            }
            else
            {
                float u = EO(tt / 0.10f);
                pos = Vector3.Lerp(far, slam, u);
                sc = Mathf.Lerp(0.8f, 1.25f, u);
                if (tt > 0.1f && tt < 0.55f) pos += _rightL * (Mathf.Sin(tt * 55f) * 0.012f) + _upL * (Mathf.Cos(tt * 47f) * 0.01f);
                if (tt > 0.55f)
                {
                    float r = EI((tt - 0.55f) / 0.3f);
                    pos = Vector3.Lerp(slam, far, r);
                    sc *= 1f - r * 0.4f;
                }
            }

            Transform f = p[MicroPart.WinFace];
            f.localPosition = pos;
            f.localRotation = Face(pos, _lens, s * 10f);
            f.localScale = Vector3.one * Mathf.Max(0.001f, sc);

            for (int h = 0; h < 2; h++)
            {
                Transform hand = p[h == 0 ? MicroPart.WinHandL : MicroPart.WinHandR];
                ShowT(hand, !pre);
                if (pre) continue;
                float hs = h == 0 ? -1f : 1f;
                Vector3 slamH = slam + _rightL * (hs * 0.46f * sc) - _upL * 0.1f;
                Vector3 farH = slamH + _rightL * (hs * 0.9f) - _upL * 0.15f;
                float uh = EO((tt - 0.03f) / 0.10f);
                Vector3 hp = Vector3.Lerp(farH, slamH, uh);
                if (tt > 0.55f) hp = Vector3.Lerp(slamH, farH, EI((tt - 0.55f) / 0.3f));
                hand.localPosition = hp;
                hand.localRotation = Face(hp, _lens, hs * 14f);
                hand.localScale = Vector3.one * 1.5f;
            }

            SetLight(e, _lens + _fwdL * 0.3f + _upL * 0.15f, pre ? 0f : Ramp(tt, 0f, 0.55f, 0.85f, e.lightMax));
            _focus = pos;
        }

        // ---------------------------------------------------------------- SkeletonPop
        // pre: green hatch glows beside the rail. sting: springs up 0..0.14, arm swipe 0.12..0.35, jaw clacks, drops back 0.55..0.85

        void Skeleton(MicroEntity e, bool pre, float pt, float tt, int idx)
        {
            Transform[] p = e.p;
            float s = Side(e.kind, idx);
            Vector3 hole = new Vector3(s * 1.4f, -0.9f, 1.5f);
            Transform holeT = p[MicroPart.SkelHole];
            holeT.localPosition = hole;
            holeT.localScale = Vector3.one * Mathf.Max(0.001f, Sm((pre ? pt : 1f) / 0.25f));

            Transform root = p[MicroPart.SkelRoot];
            const float headH = 1.5f;
            Vector3 headTarget = _lens + _fwdL * 0.9f;
            if (pre)
            {
                ShowT(root, false);
                SetLight(e, hole + Vector3.up * 0.4f, 1.2f * Sm(pt / 0.3f));
                _focus = hole;
                return;
            }

            ShowT(root, true);
            float u = EO(tt / 0.14f);
            float lean = Mathf.Lerp(8f, 50f, u);
            Quaternion r = Quaternion.LookRotation(-_fwdL, Vector3.up) * Quaternion.Euler(lean, 0f, 0f);
            Vector3 rootEnd = headTarget - r * new Vector3(0f, headH, 0f);
            Vector3 rootStart = hole + new Vector3(0f, -headH - 0.5f, 0f);
            Vector3 pos = Vector3.Lerp(rootStart, rootEnd, u);
            if (tt > 0.55f) pos = Vector3.Lerp(rootEnd, rootStart, EI((tt - 0.55f) / 0.3f));
            root.localPosition = pos;
            root.localRotation = r;

            float clack = 0.5f + 0.5f * Mathf.Sin(tt * 50f);
            p[MicroPart.SkelJaw].localRotation = Quaternion.Euler(8f + 24f * clack, 0f, 0f);
            p[MicroPart.SkelHead].localRotation = Quaternion.Euler(Mathf.Sin(tt * 30f) * 5f, Mathf.Sin(tt * 22f) * 9f, 0f);
            float sw = Sm((tt - 0.12f) / 0.23f);
            p[MicroPart.SkelArm].localRotation = Quaternion.Euler(-100f, Mathf.Lerp(55f, -75f, sw), 0f);

            SetLight(e, _lens + _fwdL * 0.35f + _upL * 0.2f, Ramp(tt, 0f, 0.55f, 0.85f, e.lightMax));
            _focus = headTarget;
        }

        // ---------------------------------------------------------------- ClawHand
        // pre: fingertips creep up at the bottom edge. sting: hand A rises 0..0.14, hand B 0.1..0.24, claw until 0.55, sink 0.55..0.83

        void Claw(MicroEntity e, bool pre, float pt, float tt, int idx)
        {
            Transform[] p = e.p;
            Vector3 tA = _lens + _fwdL * 0.7f - _rightL * 0.22f - _upL * 0.30f;
            Vector3 tB = _lens + _fwdL * 0.62f + _rightL * 0.20f - _upL * 0.38f;
            Quaternion baseRot = Quaternion.LookRotation(-_fwdL, Vector3.up);
            for (int h = 0; h < 2; h++)
            {
                Transform hand = p[h == 0 ? MicroPart.ClawA : MicroPart.ClawB];
                Vector3 target = h == 0 ? tA : tB;
                Vector3 start = target - _upL * 1.1f;
                float t = tt - h * 0.10f;
                Vector3 pos;
                if (pre)
                {
                    ShowT(hand, h == 0);
                    pos = start + _upL * (0.22f * Sm(pt / 0.35f) + Mathf.Sin(pt * 9f) * 0.015f);
                }
                else
                {
                    ShowT(hand, true);
                    float u = EO(t / 0.14f);
                    pos = Vector3.Lerp(start, target, u);
                    if (t > 0.14f) pos += _fwdL * (0.12f * Sm((t - 0.14f) / 0.3f));
                    if (tt > 0.55f) pos = Vector3.Lerp(pos, start, EI((tt - 0.55f) / 0.28f));
                }

                hand.localPosition = pos;
                hand.localRotation = baseRot * Quaternion.Euler(25f, 0f, h == 0 ? 8f : -8f);
                int f0 = h == 0 ? MicroPart.ClawFingerA : MicroPart.ClawFingerB;
                for (int i = 0; i < 5; i++)
                {
                    float ph = H(idx, 20 + i + 5 * h) * 6.283f;
                    float curl = pre ? 25f + Mathf.Sin(pt * 10f + ph) * 12f : 38f + 30f * (0.5f + 0.5f * Mathf.Sin(tt * 44f + ph));
                    p[f0 + i].localRotation = Quaternion.Euler(curl, 0f, 0f);
                }
            }

            SetLight(e, _lens + _fwdL * 0.3f + _upL * 0.1f, pre ? 0f : Ramp(tt, 0f, 0.55f, 0.85f, e.lightMax));
            _focus = tA;
        }

        // ---------------------------------------------------------------- BatSwarm
        // pre: the cloud hovers far ahead. sting: each bat flies through the lens axis over 0.5 s (staggered up to 0.18 s), 3 clip the lens

        void Bats(MicroEntity e, bool pre, float pt, float tt, int idx)
        {
            Transform[] p = e.p;
            int n = Mathf.Min(MicroPart.BatCount, 40 + Mathf.FloorToInt(H(idx, 5) * 9f));
            Vector3 centre = _lens + _fwdL * 16f;
            for (int i = 0; i < MicroPart.BatCount; i++)
            {
                Transform bt = p[i];
                bool on = i < n;
                ShowT(bt, on);
                if (!on) continue;

                float a = H(idx * 64 + i, 31) * 6.283f;
                float rs = 0.4f + Mathf.Sqrt(H(idx * 64 + i, 32)) * 3.2f;
                Vector3 start = _lens + _fwdL * (14f + H(idx * 64 + i, 33) * 5f) + _rightL * (Mathf.Cos(a) * rs) + _upL * (Mathf.Sin(a) * rs * 0.7f);
                float clip = (i % 17 == 3) ? 1f : 0f;
                float re = clip > 0f ? 0.10f + H(idx * 64 + i, 34) * 0.12f : 0.55f + H(idx * 64 + i, 34) * 1.6f;
                float b = H(idx * 64 + i, 35) * 6.283f;
                Vector3 end = _lens + _fwdL * -3.2f + _rightL * (Mathf.Cos(b) * re) + _upL * (Mathf.Sin(b) * re * 0.8f);
                float delay = H(idx * 64 + i, 36) * 0.18f;
                float ph = H(idx * 64 + i, 37) * 6.283f;

                Vector3 pos;
                float time = pre ? pt : tt;
                if (pre)
                {
                    pos = start + new Vector3(Mathf.Sin(pt * 3f + ph), Mathf.Cos(pt * 2.3f + ph), 0f) * 0.25f;
                }
                else
                {
                    float u = Mathf.Clamp01((tt - delay) / 0.5f);
                    pos = Vector3.Lerp(start, end, u);
                    pos += _rightL * (Mathf.Sin(tt * 9f + ph) * 0.12f * (1f - u)) + _upL * (Mathf.Cos(tt * 7f + ph) * 0.1f * (1f - u));
                }

                float flap = Mathf.Lerp(0.45f, 1f, 0.5f + 0.5f * Mathf.Sin(time * 30f + ph));
                float size = 0.32f + 0.12f * H(idx * 64 + i, 38) + clip * 0.1f;
                bt.localPosition = pos;
                bt.localRotation = Face(pos, _lens, (H(idx * 64 + i, 39) - 0.5f) * 80f);
                bt.localScale = new Vector3(size * flap, size * (0.8f + 0.2f * flap), size);
            }

            SetLight(e, _lens + _fwdL * 1.6f + _upL * 0.3f, pre ? 0f : Ramp(tt, 0.1f, 0.45f, 0.7f, e.lightMax));
            _focus = _lens + _fwdL * (pre ? 16f : Mathf.Lerp(12f, 1.5f, Mathf.Clamp01(tt / 0.4f)));
        }

        // ---------------------------------------------------------------- CoffinLid
        // pre: coffin pops in, lid ajar, green light seeps out. sting: lid explodes 0..0.12, corpse lunges 0..0.15, holds, falls back 0.55..0.85

        void Coffin(MicroEntity e, bool pre, float pt, float tt, int idx)
        {
            Transform[] p = e.p;
            float s = Side(e.kind, idx);
            Vector3 cpos = new Vector3(s * 2.1f, -0.2f, 2.0f);
            Quaternion crot = Quaternion.LookRotation(new Vector3(-s * 0.5f, 0f, -0.85f), Vector3.up);
            Transform cof = p[MicroPart.CofCoffin];
            cof.localPosition = cpos;
            cof.localRotation = crot;
            float appear = pre ? Sm(pt / 0.2f) : 1f;
            cof.localScale = Vector3.one * Mathf.Max(0.001f, appear);

            Transform lid = p[MicroPart.CofLid];
            Transform corpse = p[MicroPart.CofCorpse];
            Vector3 inside = cpos + crot * new Vector3(0f, 1.2f, -0.1f);

            if (pre)
            {
                ShowT(corpse, false);
                ShowT(p[MicroPart.CofGlow], true);
                ShowT(lid, true);
                lid.localRotation = Quaternion.Euler(0f, 9f * Sm(pt / 0.3f) + (pt > 0.3f ? Mathf.Sin(pt * 22f) * 1.5f : 0f), 0f);
                lid.localPosition = new Vector3(0.42f, 1.0f, 0f);
                SetLight(e, inside, 1.4f * Sm(pt / 0.3f));
                _focus = cpos + Vector3.up * 1.0f;
                return;
            }

            // lid: pivot is the hinge, in coffin-local space; flies out and away from the rail (cart-frame velocity)
            Vector3 vCart = new Vector3(s * 3.0f, 2.5f, 3.0f);
            Vector3 vLocal = Quaternion.Inverse(crot) * vCart;
            ShowT(lid, tt < 0.5f);
            float open = 9f + 130f * EO(tt / 0.12f);
            lid.localRotation = Quaternion.Euler(0f, open + Mathf.Max(0f, tt - 0.12f) * 400f, Mathf.Max(0f, tt - 0.12f) * 300f);
            lid.localPosition = new Vector3(0.42f, 1.0f, 0f) + vLocal * Mathf.Max(0f, tt - 0.1f);
            ShowT(p[MicroPart.CofGlow], tt < 0.05f);

            ShowT(corpse, true);
            float u = EO(tt / 0.15f);
            Vector3 headTarget = _lens + _fwdL * 0.85f;
            Quaternion rEnd = Quaternion.LookRotation(-_fwdL, Vector3.up) * Quaternion.Euler(35f, 0f, 0f);
            Vector3 rootEnd = headTarget - rEnd * new Vector3(0f, 1.5f, 0f);
            Vector3 rootStart = cpos + crot * new Vector3(0f, 0.05f, -0.15f);
            Vector3 pos = Vector3.Lerp(rootStart, rootEnd, u);
            Quaternion rot = Quaternion.Slerp(crot, rEnd, u);
            if (tt > 0.55f)
            {
                float r = EI((tt - 0.55f) / 0.3f);
                pos = Vector3.Lerp(rootEnd, rootStart, r);
                rot = Quaternion.Slerp(rEnd, crot, r);
            }

            corpse.localPosition = pos;
            corpse.localRotation = rot;
            p[MicroPart.CofHead].localRotation = Quaternion.Euler(Mathf.Sin(tt * 28f) * 6f, Mathf.Sin(tt * 19f) * 10f, 0f);
            p[MicroPart.CofJaw].localRotation = Quaternion.Euler(20f + 12f * Mathf.Sin(tt * 30f), 0f, 0f);
            float arm = Mathf.Lerp(0f, -80f, u);
            p[MicroPart.CofArmL].localRotation = Quaternion.Euler(arm, 0f, -12f);
            p[MicroPart.CofArmR].localRotation = Quaternion.Euler(arm, 0f, 12f);

            float k = Sm(tt / 0.1f);
            float fade = 1f - Sm((tt - 0.55f) / 0.3f);
            SetLight(e, Vector3.Lerp(inside, _lens + _fwdL * 0.35f + _upL * 0.1f, k), Mathf.Lerp(1.4f, e.lightMax, k) * fade);
            _focus = headTarget;
        }

        // ---------------------------------------------------------------- ShadowRush
        // pre: eyes in the far dark, then the sprint runs along the rail (see ShadowAhead). sting: passes through the lens and the cart 0..0.45

        void Shadow(MicroEntity e, bool pre, float pt, float tt, float preLen, int idx)
        {
            Transform[] p = e.p;
            Transform root = p[MicroPart.ShRoot];
            float run = Mathf.Min(0.6f, preLen * 0.85f);
            float stride;
            Vector3 pos;
            float x = NominalEye.x;
            if (pre)
            {
                pos = new Vector3(x, 0f, 0f);
                float runT = Mathf.Max(0f, pt - (preLen - run));
                stride = runT * 24f;
            }
            else
            {
                float u = Mathf.Clamp01(tt / 0.45f);
                pos = new Vector3(x, 0f, Mathf.Lerp(ShadowLens, -2.6f, u));
                stride = (run + tt) * 24f;
            }

            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(14f, 180f, 0f);
            float sw = 55f;
            p[MicroPart.ShArmL].localRotation = Quaternion.Euler(Mathf.Sin(stride) * sw, 0f, 0f);
            p[MicroPart.ShArmR].localRotation = Quaternion.Euler(-Mathf.Sin(stride) * sw, 0f, 0f);
            p[MicroPart.ShLegL].localRotation = Quaternion.Euler(-Mathf.Sin(stride) * 50f, 0f, 0f);
            p[MicroPart.ShLegR].localRotation = Quaternion.Euler(Mathf.Sin(stride) * 50f, 0f, 0f);

            SetLight(e, _lens + _fwdL * 0.3f + _upL * 0.1f, pre ? 0f : Ramp(tt, 0f, 0.3f, 0.5f, e.lightMax));
            _focus = pos + Vector3.up * 1.5f;
        }

        // ---------------------------------------------------------------- MirrorFace
        // pre: cracked dark glass dead ahead. sting: face appears in it 0..0.2, smashes 0.2 (shards fly, face lunges 0.2..0.34), hold, back 0.7..0.95

        void Mirror(MicroEntity e, bool pre, float pt, float tt, int idx)
        {
            Transform[] p = e.p;
            Vector3 mpos = new Vector3(NominalEye.x, 1.45f, 3.3f);
            Quaternion mrot = Quaternion.Euler(0f, 180f, 0f);
            Transform mir = p[MicroPart.MirMirror];
            mir.localPosition = mpos;
            mir.localRotation = mrot;
            mir.localScale = Vector3.one * Mathf.Max(0.001f, pre ? Sm(pt / 0.25f) : 1f);

            Transform face = p[MicroPart.MirFace];
            Vector3 inGlass = mpos + new Vector3(0f, 0f, 0.15f);
            bool smashed = !pre && tt >= 0.2f;
            ShowT(p[MicroPart.MirPane], !smashed);
            ShowT(p[MicroPart.MirCrack], !smashed);

            if (pre)
            {
                ShowT(face, false);
                for (int i = 0; i < MicroPart.MirShards; i++) ShowT(p[MicroPart.MirShard0 + i], false);
                SetLight(e, mpos, 0f);
                _focus = mpos;
                return;
            }

            ShowT(face, true);
            Vector3 fpos;
            float fsc;
            Vector3 faceTarget = _lens + _fwdL * 0.9f;
            if (!smashed)
            {
                fpos = inGlass;
                fsc = Mathf.Lerp(0.3f, 0.8f, EO(tt / 0.18f));
            }
            else
            {
                float u = EO((tt - 0.2f) / 0.14f);
                fpos = Vector3.Lerp(inGlass, faceTarget, u);
                fsc = Mathf.Lerp(0.8f, 1.3f, u);
                if (tt > 0.7f)
                {
                    float r = EI((tt - 0.7f) / 0.25f);
                    fpos = Vector3.Lerp(faceTarget, inGlass, r);
                    fsc *= 1f - r * 0.6f;
                }
            }

            face.localPosition = fpos;
            face.localRotation = Face(fpos, _lens, 0f);
            face.localScale = Vector3.one * Mathf.Max(0.001f, fsc);

            float ts = tt - 0.2f;
            for (int i = 0; i < MicroPart.MirShards; i++)
            {
                Transform sh = p[MicroPart.MirShard0 + i];
                bool on = ts >= 0f && tt < 0.8f;
                ShowT(sh, on);
                if (!on) continue;
                Vector3 o = mpos + new Vector3((H(idx * 32 + i, 51) - 0.5f) * 0.9f, (H(idx * 32 + i, 52) - 0.5f) * 1.6f, 0.05f);
                float a = H(idx * 32 + i, 53) * 6.283f;
                float rr = (i % 6 == 1) ? 0.15f : 0.2f + H(idx * 32 + i, 54) * 0.9f;
                Vector3 aim = _lens + _rightL * (Mathf.Cos(a) * rr) + _upL * (Mathf.Sin(a) * rr);
                Vector3 dir = (aim - o).normalized;
                float spd = 9f + H(idx * 32 + i, 55) * 7f;
                Vector3 pos = o + dir * (spd * ts) - Vector3.up * (2.5f * ts * ts);
                sh.localPosition = pos;
                sh.localRotation = Quaternion.Euler(ts * (300f + H(idx * 32 + i, 56) * 500f), ts * 420f, H(idx * 32 + i, 57) * 360f);
                sh.localScale = Vector3.one * (0.6f + H(idx * 32 + i, 58) * 0.7f);
            }

            Vector3 lightFront = _lens + _fwdL * 0.35f + _upL * 0.1f;
            float lk = Sm((tt - 0.2f) / 0.15f);
            float inten = Mathf.Lerp(1.5f * Sm(tt / 0.1f), e.lightMax, lk) * (1f - Sm((tt - 0.7f) / 0.25f));
            SetLight(e, Vector3.Lerp(mpos + new Vector3(0f, 0f, -0.3f), lightFront, lk), inten);
            _focus = smashed ? fpos : mpos;
        }
    }
}
