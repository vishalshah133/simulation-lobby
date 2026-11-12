using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Trick or Treat Street. Event doors swing open as the cart nears: TREAT doors have a kid who tips a bucket and a candy shower
    /// flies into the cart; the DoorGhoul door shows a ghoul in the doorway during the quiet and it lunges at the taken rider in
    /// the sting. Door distances come from the event schedule. Every entity starts hidden and is reset in ResetSet.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class StreetSet : CoasterSet
    {
        public override ActId Act => ActId.TrickOrTreatStreet;

        [Serializable]
        public sealed class DoorRig
        {
            public ScareKind kind;
            [Tooltip("The authored event distance; matched to the schedule at bind.")]
            public float eventDistance;
            public Transform doorPivot;
            public float openDegrees = 105f;
            public Light doorwayLight;
            [Header("Treat")]
            public ParticleSystem candy;
            public Transform bucketPivot;
            public Transform kidArm;
            [Header("Ghoul")]
            public Transform ghoulRoot;
            public Transform ghoulMouth;
            public Light ghoulLight;
            [Tooltip("Pair of small glowing eyes in the dark doorway: the quiet-phase cue.")]
            public Transform cueEyes;
            [Tooltip("Face centre in the ghoul's local space.")]
            public Vector3 faceLocal = new Vector3(0f, 1.78f, 0.3f);
            [NonSerialized] public readonly LungeTrack track = new LungeTrack();
            [NonSerialized] public int eventIndex = -1;
            [NonSerialized] public bool candyFired;
            [NonSerialized] public Vector3 homePos;
            [NonSerialized] public Quaternion homeRot;
            [NonSerialized] public Vector3 mouthScale;
            [NonSerialized] public bool homed;
        }

        public DoorRig[] doors = new DoorRig[0];
        [Tooltip("Arms that wave on the decorative kids.")]
        public Transform[] wavingArms = new Transform[0];
        public float openLeadMeters = 30f;
        public float openRampMeters = 8f;
        public float candyLeadMeters = 13f;
        public float lungeSeconds = 0.35f;

        readonly EventStage clock = new EventStage();
        readonly LookCue look = new LookCue();

        protected override void ResetSet()
        {
            foreach (DoorRig d in doors)
            {
                if (d == null)
                {
                    continue;
                }

                d.eventIndex = FindEvent(d);
                d.candyFired = false;
                if (d.doorPivot != null)
                {
                    d.doorPivot.localRotation = Quaternion.identity;
                }

                if (d.ghoulRoot != null)
                {
                    if (!d.homed)
                    {
                        d.homePos = d.ghoulRoot.position;
                        d.homeRot = d.ghoulRoot.rotation;
                        d.mouthScale = d.ghoulMouth != null ? d.ghoulMouth.localScale : Vector3.one;
                        d.homed = true;
                    }

                    d.ghoulRoot.SetPositionAndRotation(d.homePos, d.homeRot);
                    d.ghoulRoot.localScale = Vector3.one;
                    Show(d.ghoulRoot, false);
                }

                d.track.Reset();
                Show(d.cueEyes, false);
                ScareView.SetLight(d.ghoulLight, 0f);

                if (d.candy != null)
                {
                    d.candy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                if (d.bucketPivot != null)
                {
                    d.bucketPivot.localRotation = Quaternion.identity;
                }

                if (d.doorwayLight != null)
                {
                    d.doorwayLight.enabled = false;
                }
            }

            clock.Reset();
            look.Release();
        }

        int FindEvent(DoorRig d)
        {
            int best = -1;
            float bestGap = 6f;
            for (int i = 0; i < State.Events.Count; i++)
            {
                if (State.Events[i].kind != d.kind)
                {
                    continue;
                }

                float gap = Mathf.Abs(State.Events[i].distance - d.eventDistance);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }

            return best;
        }

        protected override void Tick()
        {
            float dist = State.Distance;

            foreach (DoorRig d in doors)
            {
                if (d == null)
                {
                    continue;
                }

                float evDist = d.eventIndex >= 0 ? State.Events[d.eventIndex].distance : d.eventDistance;
                float lead = Mathf.Max(openLeadMeters, State.Speed * 2.4f);
                float ramp = Mathf.Max(openRampMeters, State.Speed * 0.7f);
                float open = SetUtil.Smooth((dist - (evDist - lead)) / Mathf.Max(0.5f, ramp));
                if (d.doorPivot != null)
                {
                    d.doorPivot.localRotation = Quaternion.Euler(0f, d.openDegrees * open, 0f);
                }

                if (d.doorwayLight != null)
                {
                    d.doorwayLight.enabled = open > 0.02f;
                }

                if (d.kind == ScareKind.TreatDoor)
                {
                    TickTreat(d, evDist, dist, open);
                }
            }

            float t = State.SimTime;
            for (int i = 0; i < wavingArms.Length; i++)
            {
                if (wavingArms[i] != null)
                {
                    wavingArms[i].localRotation = Quaternion.Euler(-150f + 22f * Mathf.Sin(t * 7f + i * 1.7f), 0f, 0f);
                }
            }
        }

        void TickTreat(DoorRig d, float evDist, float dist, float open)
        {
            if (d.kidArm != null)
            {
                float wave = open > 0.6f ? 1f : 0f;
                d.kidArm.localRotation = Quaternion.Euler(-150f + wave * 25f * Mathf.Sin(State.SimTime * 8f), 0f, 0f);
            }

            // The candy flies for ~1.4 s and should land in the cart as it passes the event point, whatever the speed.
            float trigger = evDist + 2.5f - Mathf.Max(candyLeadMeters, State.Speed * 1.4f);
            if (!d.candyFired && dist >= trigger)
            {
                d.candyFired = true;
                if (d.candy != null)
                {
                    d.candy.Clear(true);
                    d.candy.Play(true);
                }
            }
            else if (d.candyFired && dist < trigger - 2f)
            {
                d.candyFired = false;
                if (d.candy != null)
                {
                    d.candy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (d.bucketPivot != null)
            {
                float tip = SetUtil.Smooth((dist - (trigger - 1f)) / 3f);
                d.bucketPivot.localRotation = Quaternion.Euler(95f * tip, 0f, 0f);
            }
        }

        void LateUpdate()
        {
            if (State == null || !Near)
            {
                return;
            }

            foreach (DoorRig d in doors)
            {
                if (d != null && d.kind == ScareKind.DoorGhoul)
                {
                    TickGhoul(d);
                }
            }
        }

        /// <summary>
        /// PreQuiet: the door is open and two small green eyes glow in the dark doorway. Sting: the ghoul is in the doorway on frame 0 and
        /// bursts out onto the cart in under 0.2 s, face to the lens at ~1 m, lit by its own light. Aftermath: it is left behind.
        /// </summary>
        void TickGhoul(DoorRig d)
        {
            if (d.ghoulRoot == null)
            {
                return;
            }

            clock.Update(State, d.eventIndex);
            Transform g = d.ghoulRoot;
            if (clock.Stage == 0)
            {
                Show(g, false);
                Show(d.cueEyes, false);
                ScareView.SetLight(d.ghoulLight, 0f);
                d.track.Reset();
                look.Release();
                return;
            }

            Vector3 doorway = d.homePos + d.homeRot * d.faceLocal;
            if (clock.Stage == 1)
            {
                Show(g, false);
                ScareView.SetLight(d.ghoulLight, 0f);
                float blink = Mathf.Repeat(State.SimTime * 0.7f, 1f) < 0.07f ? 0.1f : 1f;
                bool eyesOn = clock.T > 0.25f;
                Show(d.cueEyes, eyesOn);
                if (eyesOn && d.cueEyes != null)
                {
                    d.cueEyes.localScale = new Vector3(1f, blink, 1f) * SetUtil.Smooth((clock.T - 0.25f) / 0.4f);
                    look.Quiet(d.cueEyes.position, 0.35f);
                }

                return;
            }

            float since = clock.SinceSting;
            if (since > 1.9f)
            {
                Show(g, false);
                ScareView.SetLight(d.ghoulLight, 0f);
                look.Release();
                return;
            }

            Show(d.cueEyes, false);
            Show(g, true);
            ScareView.Eye(State, out Vector3 eye, out Quaternion eyeRot);
            const float endScale = 1.6f;
            Vector3 endFace = eye + eyeRot * new Vector3(-0.05f, -0.02f, 1.0f);
            Quaternion endRot = eyeRot * Quaternion.Euler(0f, 180f, 10f);
            Vector3 retreat = -State.CartForward + Vector3.Cross(State.CartUp, State.CartForward) * 0.4f;
            d.track.Sample(since, doorway, d.homeRot, endFace, endRot, retreat, out Vector3 facePos, out Quaternion rot);
            float k = ScareView.EaseOutCubic(since / d.track.lungeSeconds);
            float scale = Mathf.Lerp(1f, endScale, k);
            g.rotation = rot;
            g.localScale = Vector3.one * scale;
            g.position = ScareView.RootForFace(facePos, rot, d.faceLocal, scale);
            if (d.ghoulMouth != null)
            {
                d.ghoulMouth.localScale = new Vector3(d.mouthScale.x * (1f + 0.6f * k), d.mouthScale.y * (1f + 2.2f * k), d.mouthScale.z);
            }

            float fade = 1f - SetUtil.Smooth((since - 1.2f) / 0.6f);
            ScareView.SetLight(d.ghoulLight, 5f * ScareView.EaseOutCubic(since / 0.08f) * fade);
            if (since < d.track.holdSeconds)
            {
                look.Sting(facePos);
            }
            else
            {
                look.Release();
            }
        }
    }
}
