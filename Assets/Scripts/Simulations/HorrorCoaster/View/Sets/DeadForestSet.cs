using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Dead forest. Glowing eyes appear in the trunks as the cart nears and blink. The CrowBurst: in the quiet a handful of crows perch in
    /// the trees beside the rail ahead, hunched, eyes lit; at the sting all ~70 erupt from the trees at both sides, rush the camera and
    /// pour over and past it, a few clipping the lens. Paths are derived from a hash of the crow index (a re-render is identical) and end
    /// in the camera's frame, so the burst lands on the lens at any cart speed.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class DeadForestSet : CoasterSet
    {
        public override ActId Act => ActId.DeadForest;

        [Serializable]
        public sealed class Crow
        {
            public Transform root;
            public Transform wingLeft;
            public Transform wingRight;
            [NonSerialized] public Vector3 start;
            [NonSerialized] public Vector3 mid;        // eye-local (right, up, forward) pass point
            [NonSerialized] public Vector3 end;        // eye-local exit point
            [NonSerialized] public float duration;
            [NonSerialized] public float delay;
            [NonSerialized] public float phase;
            [NonSerialized] public bool clipper;
        }

        [Tooltip("Parents of an eye pair (two glowing spheres) on the trunks.")]
        public Transform[] eyePairs = new Transform[0];
        public float eyeRevealMeters = 45f;
        public Crow[] crows = new Crow[0];
        public Light flockLight;
        [Tooltip("Metres ahead of the burst event where the crows wait in the trees.")]
        public Vector2 perchRange = new Vector2(4f, 16f);
        public int perchedInQuiet = 8;

        int burstEvent = -1;
        readonly EventStage clock = new EventStage();
        readonly LookCue look = new LookCue();
        Vector3[] eyeScale;

        protected override void ResetSet()
        {
            burstEvent = -1;
            var list = SetUtil.EventsOf(State, ScareKind.CrowBurst);
            if (list.Count > 0)
            {
                burstEvent = list[0];
            }

            clock.Reset();
            look.Release();
            ScareView.SetLight(flockLight, 0f);
            eyeScale = new Vector3[eyePairs.Length];
            for (int i = 0; i < eyePairs.Length; i++)
            {
                if (eyePairs[i] != null)
                {
                    eyeScale[i] = eyePairs[i].localScale;
                }
            }

            float evDist = burstEvent >= 0 ? State.Events[burstEvent].distance : 0f;
            for (int i = 0; i < crows.Length; i++)
            {
                Crow c = crows[i];
                if (c == null || c.root == null)
                {
                    continue;
                }

                float a = SetUtil.Hash(i, 1);
                float side = SetUtil.Hash(i, 2) < 0.5f ? -1f : 1f;
                float lat = side * (5.5f + 3.5f * SetUtil.Hash(i, 14));
                float height = 1.5f + 4.5f * SetUtil.Hash(i, 15);
                c.start = ScareView.RailPoint(State.Rail, evDist + Mathf.Lerp(perchRange.x, perchRange.y, a), lat, height);
                c.clipper = i % 12 == 0;
                if (c.clipper)
                {
                    c.mid = new Vector3((SetUtil.Hash(i, 3) - 0.5f) * 0.5f, (SetUtil.Hash(i, 4) - 0.5f) * 0.3f, 0.5f + 0.2f * SetUtil.Hash(i, 5));
                }
                else
                {
                    c.mid = new Vector3((SetUtil.Hash(i, 3) - 0.5f) * 5f, 0.2f + 2.4f * SetUtil.Hash(i, 4), 1.5f + 3.5f * SetUtil.Hash(i, 5));
                }

                c.end = new Vector3((SetUtil.Hash(i, 6) - 0.5f) * 18f, 5f + 5f * SetUtil.Hash(i, 7), -8f - 8f * SetUtil.Hash(i, 8));
                c.duration = 1.4f + 0.8f * SetUtil.Hash(i, 9);
                c.delay = 0.25f * SetUtil.Hash(i, 10);
                c.phase = SetUtil.Hash(i, 11) * 6.28f;
                c.root.position = c.start;
                Show(c.root, false);
            }
        }

        protected override void Tick()
        {
            TickEyes();
        }

        void LateUpdate()
        {
            if (State == null || !Near)
            {
                return;
            }

            TickCrows();
        }

        void TickEyes()
        {
            Vector3 cart = State.CartPosition;
            float t = State.SimTime;
            for (int i = 0; i < eyePairs.Length; i++)
            {
                Transform e = eyePairs[i];
                if (e == null)
                {
                    continue;
                }

                float dist = Vector3.Distance(e.position, cart);
                float vis = 1f - SetUtil.Smooth((dist - 14f) / Mathf.Max(1f, eyeRevealMeters - 14f));
                float blinkPhase = Mathf.Repeat(t * 0.22f + SetUtil.Hash(i, 77), 1f);
                float open = blinkPhase < 0.04f ? 0.08f : 1f;
                bool on = vis > 0.02f;
                Show(e, on);
                if (on)
                {
                    Vector3 s = eyeScale[i];
                    e.localScale = new Vector3(s.x * vis, s.y * vis * open, s.z * vis);
                }
            }
        }

        void TickCrows()
        {
            clock.Update(State, burstEvent);
            if (clock.Stage == 0 || burstEvent < 0)
            {
                for (int i = 0; i < crows.Length; i++)
                {
                    if (crows[i] != null)
                    {
                        Show(crows[i].root, false);
                    }
                }

                ScareView.SetLight(flockLight, 0f);
                look.Release();
                return;
            }

            float t = State.SimTime;
            if (clock.Stage == 1)
            {
                // A few crows hunched in the trees, appearing with a stagger; the rest stay hidden so the quiet is not a giveaway.
                int shown = Mathf.Min(perchedInQuiet, crows.Length);
                for (int i = 0; i < crows.Length; i++)
                {
                    Crow c = crows[i];
                    if (c == null || c.root == null)
                    {
                        continue;
                    }

                    float appear = i < shown ? SetUtil.Smooth((clock.T - 0.2f - SetUtil.Hash(i, 12) * 0.7f) / 0.3f) : 0f;
                    Show(c.root, appear > 0.01f);
                    c.root.position = c.start;
                    c.root.rotation = Quaternion.Euler(0f, SetUtil.Hash(i, 13) * 360f, 0f);
                    c.root.localScale = Vector3.one * appear;
                    Fold(c, 0.15f + 0.1f * Mathf.Sin(t * 2f + c.phase));
                }

                ScareView.SetLight(flockLight, 0f);
                if (crows.Length > 0 && crows[0] != null && clock.T > 0.3f)
                {
                    look.Quiet(crows[0].start, 0.3f);
                }

                return;
            }

            float since = clock.SinceSting;
            ScareView.Eye(State, out Vector3 eye, out Quaternion eyeRot);
            bool any = false;
            Vector3 focus = eye + eyeRot * new Vector3(0f, 0.3f, 2.5f);
            for (int i = 0; i < crows.Length; i++)
            {
                Crow c = crows[i];
                if (c == null || c.root == null)
                {
                    continue;
                }

                float u = (since - c.delay) / c.duration;
                if (since < c.delay)
                {
                    Show(c.root, true);
                    c.root.position = c.start;
                    c.root.localScale = Vector3.one;
                    any = true;
                    continue;
                }

                if (u >= 1f)
                {
                    Show(c.root, false);
                    continue;
                }

                any = true;
                Show(c.root, true);
                Vector3 pos = Path(c, eye, eyeRot, u);
                Vector3 ahead = Path(c, eye, eyeRot, Mathf.Min(1f, u + 0.02f));
                Vector3 dir = ahead - pos;
                c.root.position = pos;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    c.root.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }

                c.root.localScale = Vector3.one * (1f - SetUtil.Smooth((u - 0.88f) / 0.12f));
                Fold(c, Mathf.Sin(t * 22f + c.phase) * 0.9f);
                if (i == 0)
                {
                    focus = pos;
                }
            }

            float fade = 1f - SetUtil.Smooth((since - 1.4f) / 0.8f);
            ScareView.SetLight(flockLight, any ? 4f * ScareView.EaseOutCubic(since / 0.1f) * fade : 0f);
            if (flockLight != null)
            {
                flockLight.transform.position = eye + eyeRot * new Vector3(0f, 0.6f, 1.2f);
            }

            if (since < 0.62f)
            {
                look.Sting(focus);
            }
            else
            {
                look.Release();
            }
        }

        /// <summary>Quadratic through the start (world), the pass point and the exit (both in the camera's frame), reached at u = 0.3 and 1.</summary>
        static Vector3 Path(Crow c, Vector3 eye, Quaternion eyeRot, float u)
        {
            const float m = 0.3f;
            Vector3 mid = eye + eyeRot * c.mid;
            Vector3 end = eye + eyeRot * c.end;
            float l0 = (u - m) * (u - 1f) / ((0f - m) * (0f - 1f));
            float l1 = u * (u - 1f) / (m * (m - 1f));
            float l2 = u * (u - m) / (1f * (1f - m));
            return c.start * l0 + mid * l1 + end * l2;
        }

        static void Fold(Crow c, float amount)
        {
            float deg = Mathf.Lerp(-70f, 60f, Mathf.Clamp01(amount * 0.5f + 0.5f));
            if (c.wingLeft != null)
            {
                c.wingLeft.localRotation = Quaternion.Euler(0f, 0f, deg);
            }

            if (c.wingRight != null)
            {
                c.wingRight.localRotation = Quaternion.Euler(0f, 0f, -deg);
            }
        }
    }
}
