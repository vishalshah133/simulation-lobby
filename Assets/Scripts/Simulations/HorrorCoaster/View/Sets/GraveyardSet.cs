using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Graveyard. Three scheduled entities:
    /// GraveHand: a bone hand erupts INSIDE the viewer's own cart, from the empty left seat. Quiet: fingertips twitch up through the floor at
    /// the lower left. Sting: it lunges to the lap bar at ~0.5 m in under 0.2 s, grabs, and sinks again (the rider ahead is snatched by the roster).
    /// FakeLantern: flickers harder in the quiet and goes out; nothing else happens.
    /// GraveyardGhost: quiet: a cold wisp bobs among the stones off to the left. Sting: the ghost is out of frame on frame 0 and lunges to ~1 m
    /// from the lens (mouth open, own light), then is left behind.
    /// Everything starts hidden / unlit and resets in ResetSet. Scare entities are placed in LateUpdate, after the camera.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class GraveyardSet : CoasterSet
    {
        public override ActId Act => ActId.Graveyard;

        [Header("Grave hand (rides in the viewer's cart)")]
        public Transform graveRoot;
        public Transform handPivot;
        public Transform[] fingers = new Transform[0];
        public Light graveGlow;
        [Tooltip("Cart-local (right, up, forward) of the spot the fingertips poke through in the quiet.")]
        public Vector3 cueSpot = new Vector3(-0.6f, 0.1f, 0.9f);
        public float buriedY = -1.35f;

        [Header("Fake lantern")]
        public Renderer lanternBulb;
        public Material lanternOn;
        public Material lanternOff;
        public Light lanternLight;
        public FlickerLight lanternFlicker;

        [Header("Ghost")]
        public Transform ghost;
        public Transform ghostMouth;
        public Light ghostLight;
        public Transform wisp;
        public Light wispLight;
        [Tooltip("Metres ahead of the event point and to the left where the ghost waits, and its height.")]
        public float ghostAhead = 6f;
        public float ghostSide = -7f;
        public float ghostHeight = 1.6f;

        int handEvent = -1;
        int lanternEvent = -1;
        int ghostEvent = -1;
        readonly EventStage handClock = new EventStage();
        readonly EventStage lanternClock = new EventStage();
        readonly EventStage ghostClock = new EventStage();
        readonly LookCue handLook = new LookCue();
        readonly LookCue ghostLookCue = new LookCue();
        readonly LungeTrack ghostTrack = new LungeTrack();
        Vector3 mouthScale = Vector3.one;
        bool mouthKnown;
        static readonly Vector3 GhostFaceLocal = new Vector3(0f, 1.6f, 0.34f);

        protected override void ResetSet()
        {
            handEvent = First(ScareKind.GraveHand);
            lanternEvent = First(ScareKind.FakeLantern);
            ghostEvent = First(ScareKind.GraveyardGhost);
            handClock.Reset();
            lanternClock.Reset();
            ghostClock.Reset();
            ghostTrack.Reset();
            handLook.Release();
            ghostLookCue.Release();
            Show(graveRoot, false);
            Show(ghost, false);
            Show(wisp, false);
            ScareView.SetLight(graveGlow, 0f);
            ScareView.SetLight(ghostLight, 0f);
            ScareView.SetLight(wispLight, 0f);

            if (ghostMouth != null && !mouthKnown)
            {
                mouthScale = ghostMouth.localScale;
                mouthKnown = true;
            }

            SetLantern(true, 1f);
        }

        int First(ScareKind kind)
        {
            var list = SetUtil.EventsOf(State, kind);
            return list.Count > 0 ? list[0] : -1;
        }

        protected override void Tick()
        {
            TickLantern();
        }

        void LateUpdate()
        {
            if (State == null || !Near)
            {
                return;
            }

            TickHand();
            TickGhost();
        }

        // ------------------------------------------------------------ grave hand

        void CartFrame(out Vector3 pos, out Vector3 f, out Vector3 u, out Vector3 r)
        {
            pos = State.CartPosition;
            f = State.CartForward;
            u = State.CartUp;
            r = Vector3.Cross(u, f);
        }

        void TickHand()
        {
            if (graveRoot == null || handPivot == null)
            {
                return;
            }

            handClock.Update(State, handEvent);
            if (handClock.Stage == 0)
            {
                Show(graveRoot, false);
                ScareView.SetLight(graveGlow, 0f);
                handLook.Release();
                return;
            }

            CartFrame(out Vector3 cart, out Vector3 f, out Vector3 u, out Vector3 r);
            Vector3 homePos = cart + r * cueSpot.x + u * cueSpot.y + f * cueSpot.z;
            Quaternion homeRot = SetUtil.Look(f, u);   // hand local +Y is the cart's up: fingers point up
            float t = State.SimTime;
            float pivotY;
            Vector3 pos;
            Quaternion rot;

            if (handClock.Stage == 1)
            {
                float appear = SetUtil.Smooth((handClock.T - 0.4f) / 0.6f);
                bool on = handClock.T > 0.4f;
                Show(graveRoot, on);
                pivotY = buriedY + 0.45f * appear + 0.04f * Mathf.Sin(t * 11f);
                pos = homePos;
                rot = homeRot;
                ScareView.SetLight(graveGlow, 0f);
                if (on)
                {
                    handLook.Quiet(homePos + u * 0.3f, 0.3f);
                }
            }
            else
            {
                float since = handClock.SinceSting;
                if (since > 1.4f)
                {
                    Show(graveRoot, false);
                    ScareView.SetLight(graveGlow, 0f);
                    handLook.Release();
                    return;
                }

                Show(graveRoot, true);
                ScareView.Eye(State, out Vector3 eye, out Quaternion eyeRot);
                Vector3 endPos = eye + eyeRot * new Vector3(-0.4f, -0.85f, 0.2f);
                Quaternion endRot = eyeRot * Quaternion.Euler(55f, 0f, 12f);
                float k = ScareView.EaseOutCubic(since / 0.16f);
                float sink = SetUtil.Smooth((since - 0.55f) / 0.5f);
                pos = Vector3.Lerp(homePos, endPos, k) + eyeRot * new Vector3(0f, -1.3f * sink, -0.2f * sink);
                rot = Quaternion.Slerp(homeRot, endRot, k);
                pivotY = Mathf.Lerp(buriedY + 0.45f, 0f, k);
                ScareView.SetLight(graveGlow, 4f * ScareView.EaseOutCubic(since / 0.08f) * (1f - sink));
                if (since < 0.6f)
                {
                    handLook.Sting(pos + rot * new Vector3(0f, 0.9f, 0f));
                }
                else
                {
                    handLook.Release();
                }
            }

            graveRoot.SetPositionAndRotation(pos, rot);
            handPivot.localPosition = new Vector3(0f, pivotY, 0f);
            float curl = handClock.Stage >= 2 ? 30f : 0f;
            for (int i = 0; i < fingers.Length; i++)
            {
                if (fingers[i] != null)
                {
                    fingers[i].localRotation = Quaternion.Euler(curl + 16f * Mathf.Sin(t * 13f + i * 1.3f), 0f, -(i - 1.5f) * 9f);
                }
            }
        }

        // ------------------------------------------------------------ lantern

        void TickLantern()
        {
            if (lanternEvent < 0)
            {
                return;
            }

            lanternClock.Update(State, lanternEvent);
            if (SetUtil.HasStung(State, lanternEvent))
            {
                SetLantern(false, 0f);
                return;
            }

            if (lanternClock.Stage == 1)
            {
                // Faster, deeper stutter as the quiet runs down.
                float t = State.SimTime;
                float stutter = Mathf.Sin(t * 31f) * Mathf.Sin(t * 17f + 1f) > (0.5f - 0.1f * lanternClock.T) ? 1f : 0.15f;
                SetLantern(true, stutter);
            }
            else
            {
                SetLantern(true, 1f);
            }
        }

        void SetLantern(bool on, float level)
        {
            if (lanternFlicker != null)
            {
                lanternFlicker.enabled = on && level >= 1f;
            }

            if (lanternLight != null)
            {
                lanternLight.enabled = on && level > 0.2f;
                if (!(lanternFlicker != null && lanternFlicker.enabled))
                {
                    lanternLight.intensity = (lanternFlicker != null ? lanternFlicker.baseIntensity : 1.5f) * level;
                }
            }

            if (lanternBulb != null)
            {
                Material m = on && level > 0.2f ? lanternOn : lanternOff;
                if (m != null)
                {
                    lanternBulb.sharedMaterial = m;
                }
            }
        }

        // ------------------------------------------------------------ ghost

        void TickGhost()
        {
            if (ghost == null || ghostEvent < 0)
            {
                return;
            }

            ghostClock.Update(State, ghostEvent);
            if (ghostClock.Stage == 0)
            {
                Show(ghost, false);
                Show(wisp, false);
                ScareView.SetLight(ghostLight, 0f);
                ScareView.SetLight(wispLight, 0f);
                ghostTrack.Reset();
                ghostLookCue.Release();
                return;
            }

            ICoasterRail rail = State.Rail;
            float evD = State.Events[ghostEvent].distance;
            Vector3 homeFace = ScareView.RailPoint(rail, evD + ghostAhead, ghostSide, ghostHeight);
            if (ghostClock.Stage == 1)
            {
                Show(ghost, false);
                ScareView.SetLight(ghostLight, 0f);
                bool on = ghostClock.T > 0.3f;
                Show(wisp, on);
                if (on && wisp != null)
                {
                    float bob = State.SimTime;
                    Vector3 wp = ScareView.RailPoint(rail, evD + ghostAhead + 6f, ghostSide + 1f, 1.0f + 0.25f * Mathf.Sin(bob * 2.3f));
                    wisp.position = wp;
                    float amp = SetUtil.Smooth((ghostClock.T - 0.3f) / 0.5f) * (0.7f + 0.3f * Mathf.Sin(bob * 9f));
                    ScareView.SetLight(wispLight, 1.6f * amp);
                    wisp.localScale = Vector3.one * Mathf.Max(0.05f, amp);
                    ghostLookCue.Quiet(wp, 0.3f);
                }

                return;
            }

            float since = ghostClock.SinceSting;
            if (since > 1.9f)
            {
                Show(ghost, false);
                ScareView.SetLight(ghostLight, 0f);
                ghostLookCue.Release();
                return;
            }

            Show(wisp, false);
            ScareView.SetLight(wispLight, 0f);
            Show(ghost, true);
            ScareView.Eye(State, out Vector3 eye, out Quaternion eyeRot);
            Quaternion homeRot = SetUtil.Look(State.CartPosition - homeFace, Vector3.up);
            Vector3 endFace = eye + eyeRot * new Vector3(0.1f, 0.0f, 0.95f);
            Quaternion endRot = eyeRot * Quaternion.Euler(0f, 180f, -8f);
            Vector3 retreat = -State.CartForward - Vector3.Cross(State.CartUp, State.CartForward) * 0.5f;
            ghostTrack.Sample(since, homeFace, homeRot, endFace, endRot, retreat, out Vector3 facePos, out Quaternion rot);
            float k = ScareView.EaseOutCubic(since / ghostTrack.lungeSeconds);
            float scale = Mathf.Lerp(1f, 1.1f, k);
            ghost.rotation = rot;
            ghost.localScale = Vector3.one * scale;
            ghost.position = ScareView.RootForFace(facePos, rot, GhostFaceLocal, scale);
            if (ghostMouth != null)
            {
                ghostMouth.localScale = new Vector3(mouthScale.x * (1f + 0.8f * k), mouthScale.y * (1f + 2.6f * k), mouthScale.z);
            }

            float fade = 1f - SetUtil.Smooth((since - 1.2f) / 0.6f);
            ScareView.SetLight(ghostLight, 5f * ScareView.EaseOutCubic(since / 0.08f) * fade);
            if (since < ghostTrack.holdSeconds)
            {
                ghostLookCue.Sting(facePos);
            }
            else
            {
                ghostLookCue.Release();
            }
        }
    }
}
