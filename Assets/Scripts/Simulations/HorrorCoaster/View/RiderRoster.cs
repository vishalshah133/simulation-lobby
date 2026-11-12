using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// The train. The viewer rides in the RIGHT seat of the last car; the five riders ahead each sit in the LEFT seat of their own car
    /// (the right seat is empty), so the centre of the frame stays clear and the riders read in the left third. Cars are placed along
    /// the rail at fixed offsets so they bend with the track. A taken rider is snatched upward (a violent stretch-and-lift in 0.3 s)
    /// while a screaming silhouette streaks up after them. Also drives the viewer's own gloved hands, which turn to bone at the end.
    /// Plain MonoBehaviour: finds <see cref="HorrorCoasterHost"/> and reads its state once ready. Read-only.
    /// </summary>
    public sealed class RiderRoster : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Seat
        {
            public Transform car;
            public Transform rider;
            public Transform head;
            public Transform[] puffs;
            public Transform ghost;
            public Transform streak;
            [Tooltip("Metres ahead of the viewer's cart along the rail.")]
            public float offset = 2.6f;

            [System.NonSerialized] public float goneAt = -1f;
            [System.NonSerialized] public float headYaw;
            [System.NonSerialized] public Vector3 riderRest;
            [System.NonSerialized] public bool restKnown;
            [System.NonSerialized] public ScareKind takenBy;
        }

        /// <summary>Metres between consecutive cars. Seat i sits at offset SeatSpacing * (i + 1) ahead of the viewer.</summary>
        public const float SeatSpacing = 2.6f;

        /// <summary>Car-local x of the left seat the riders sit in (the viewer sits right, at +0.4).</summary>
        public const float SeatX = -0.55f;

        /// <summary>Seconds the snatch takes: the rider is gone from the car inside this.</summary>
        public const float YankSeconds = 0.3f;

        public Seat[] seats = new Seat[0];
        [Tooltip("The viewer's own car (low, nothing in the central view corridor).")]
        public Transform ownCar;

        [Header("Viewer hands")]
        public Renderer[] handRenderers = new Renderer[0];
        public Material gloveMaterial;
        public Material boneMaterial;
        [Tooltip("Hands turn to bone this many metres before the end of the rail (the station reveal).")]
        public float handsBoneMetersFromEnd = 30f;

        [Header("Taking")]
        public float poofSeconds = 0.8f;
        public float ghostSeconds = 0.6f;

        HorrorCoasterHost host;
        ICoasterState state;
        bool bone;

        void OnEnable()
        {
            host = FindAnyObjectByType<HorrorCoasterHost>();
        }

        /// <summary>Places every car (and the viewer's own) for a cart at <paramref name="distance"/>. Also used by the editor clearance test.</summary>
        public void PoseCars(ICoasterRail rail, float distance, Vector3 wobble)
        {
            rail.Sample(distance, out Vector3 cartPos, out Vector3 cartFwd, out Vector3 cartUp);
            if (ownCar != null)
            {
                ownCar.SetPositionAndRotation(cartPos + wobble, SetUtil.Look(cartFwd, cartUp));
            }

            for (int i = 0; i < seats.Length; i++)
            {
                Seat seat = seats[i];
                if (seat == null || seat.car == null)
                {
                    continue;
                }

                float want = distance + seat.offset;
                float clamped = Mathf.Min(want, rail.Length);
                rail.Sample(clamped, out Vector3 p, out Vector3 f, out Vector3 u);
                p += f * (want - clamped) + wobble;
                seat.car.SetPositionAndRotation(p, SetUtil.Look(f, u));
            }
        }

        void LateUpdate()
        {
            if (state == null)
            {
                if (host == null)
                {
                    host = FindAnyObjectByType<HorrorCoasterHost>();
                }

                if (host == null || !host.Ready || host.State == null)
                {
                    return;
                }

                state = host.State;
                ResetAll();
            }

            ICoasterRail rail = state.Rail;
            float now = state.SimTime;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            // Cart wobble is shared by every car so the train moves as one body.
            rail.Sample(state.Distance, out Vector3 basePos, out _, out _);
            Vector3 wobble = state.CartPosition - basePos;
            PoseCars(rail, state.Distance, wobble);
            if (ownCar != null)
            {
                ownCar.SetPositionAndRotation(state.CartPosition, SetUtil.Look(state.CartForward, state.CartUp));
            }

            for (int i = 0; i < seats.Length; i++)
            {
                Seat seat = seats[i];
                if (seat == null || seat.car == null)
                {
                    continue;
                }

                bool present = i < state.RiderPresent.Count && state.RiderPresent[i];
                UpdateRider(seat, i, present, now, dt);
            }

            // Hands: bone for the reveal. Distance based, so it does not depend on act timing.
            bool wantBone = state.Distance > rail.Length - handsBoneMetersFromEnd;
            if (wantBone != bone)
            {
                bone = wantBone;
                Material m = bone ? boneMaterial : gloveMaterial;
                if (m != null)
                {
                    foreach (Renderer r in handRenderers)
                    {
                        if (r != null)
                        {
                            r.sharedMaterial = m;
                        }
                    }
                }
            }
        }

        void ResetAll()
        {
            bone = false;
            foreach (Renderer r in handRenderers)
            {
                if (r != null && gloveMaterial != null)
                {
                    r.sharedMaterial = gloveMaterial;
                }
            }

            foreach (Seat seat in seats)
            {
                if (seat == null)
                {
                    continue;
                }

                seat.goneAt = -1f;
                seat.headYaw = 0f;
                RestoreRider(seat);
                SetUtil.SetActive(seat.ghost, false);
                SetUtil.SetActive(seat.streak, false);
                HidePuffs(seat);
            }
        }

        static void RestoreRider(Seat seat)
        {
            if (seat.rider == null)
            {
                return;
            }

            if (!seat.restKnown)
            {
                seat.riderRest = seat.rider.localPosition;
                seat.restKnown = true;
            }

            seat.rider.localPosition = seat.riderRest;
            seat.rider.localRotation = Quaternion.identity;
            seat.rider.localScale = Vector3.one;
            SetUtil.SetActive(seat.rider, true);
        }

        void UpdateRider(Seat seat, int index, bool present, float now, float dt)
        {
            if (!seat.restKnown && seat.rider != null)
            {
                seat.riderRest = seat.rider.localPosition;
                seat.restKnown = true;
            }

            if (present)
            {
                if (seat.goneAt >= 0f)
                {
                    seat.goneAt = -1f;
                    RestoreRider(seat);
                    SetUtil.SetActive(seat.ghost, false);
                    SetUtil.SetActive(seat.streak, false);
                    HidePuffs(seat);
                }

                // The cue before a taking: that rider glances toward the side the threat comes from.
                float targetYaw = 0f;
                if (state.TakenSeat == index && (state.Phase == ScarePhase.PreQuiet || state.Phase == ScarePhase.Sting))
                {
                    float ramp = SetUtil.Smooth(state.PhaseTime / 0.8f);
                    targetYaw = (index % 2 == 0 ? 1f : -1f) * 55f * ramp;
                }

                seat.headYaw = Mathf.LerpAngle(seat.headYaw, targetYaw, 1f - Mathf.Exp(-8f * dt));
                if (seat.head != null)
                {
                    seat.head.localRotation = Quaternion.Euler(0f, seat.headYaw, 0f);
                }

                return;
            }

            if (seat.goneAt < 0f)
            {
                seat.goneAt = now;
                seat.takenBy = state.Phase != ScarePhase.Idle ? state.CurrentEvent.kind : ScareKind.DoorGhoul;
            }

            float t = Mathf.Max(0f, now - seat.goneAt);

            // The snatch: the rider is dragged straight up, stretching, head thrown back, gone inside YankSeconds.
            // A cobra covers the rider (gone at once); a spike wall leaves them slumped and pinned for a beat.
            bool snake = seat.takenBy == ScareKind.SnakeStrike;
            bool spiked = seat.takenBy == ScareKind.SpikeWall;
            bool special = snake || spiked;
            bool yanking = snake ? t < 0.1f : (spiked ? t < 0.9f : t < YankSeconds);
            if (seat.rider != null)
            {
                SetUtil.SetActive(seat.rider, yanking);
                if (yanking && spiked)
                {
                    float k = Mathf.Clamp01(t / 0.12f);
                    seat.rider.localPosition = seat.riderRest + new Vector3(0.25f * k, 0.1f * k, 0f);
                    seat.rider.localRotation = Quaternion.Euler(-20f * k, 0f, -28f * k);
                    seat.rider.localScale = Vector3.one;
                }
                else if (yanking && !snake)
                {
                    float k = t / YankSeconds;
                    float lift = 7.5f * k * k;
                    seat.rider.localPosition = seat.riderRest + new Vector3(0f, lift, 0f);
                    seat.rider.localRotation = Quaternion.Euler(-30f * k, 0f, 22f * k);
                    seat.rider.localScale = new Vector3(1f - 0.3f * k, 1f + 1.2f * k, 1f - 0.3f * k);
                }
            }

            if (seat.head != null && yanking)
            {
                seat.head.localRotation = Quaternion.Euler(spiked ? -50f : -35f, 0f, 0f);
            }

            // The scream: a bright silhouette streaking up out of the seat, trailing a smear of light.
            bool ghostOn = !special && t < ghostSeconds;
            if (seat.ghost != null)
            {
                SetUtil.SetActive(seat.ghost, ghostOn);
                if (ghostOn)
                {
                    float k = t / ghostSeconds;
                    float rise = 1.0f + 12f * SetUtil.EaseOut(k);
                    seat.ghost.localPosition = new Vector3(SeatX, rise, -0.15f);
                    seat.ghost.localScale = new Vector3(1.2f, 1.1f + 1.6f * k, 1.2f);
                }
            }

            if (seat.streak != null)
            {
                bool streakOn = !special && t < ghostSeconds * 0.85f;
                SetUtil.SetActive(seat.streak, streakOn);
                if (streakOn)
                {
                    float k = t / (ghostSeconds * 0.85f);
                    float top = 1.0f + 12f * SetUtil.EaseOut(k);
                    float len = Mathf.Max(0.2f, top - 0.4f);
                    seat.streak.localPosition = new Vector3(SeatX, 0.4f + len * 0.5f, -0.15f);
                    seat.streak.localScale = new Vector3(0.28f * (1f - k), len * 0.5f, 0.28f * (1f - k));
                }
            }

            bool anim = !snake && t < poofSeconds && t > 0.06f && (!spiked || t > 0.9f);
            if (seat.puffs != null)
            {
                for (int j = 0; j < seat.puffs.Length; j++)
                {
                    Transform puff = seat.puffs[j];
                    if (puff == null)
                    {
                        continue;
                    }

                    SetUtil.SetActive(puff, anim);
                    if (!anim)
                    {
                        continue;
                    }

                    float k = (t - 0.06f) / (poofSeconds - 0.06f);
                    float a = SetUtil.Hash(index, j) * Mathf.PI * 2f;
                    float spread = 0.35f + 0.5f * SetUtil.Hash(index, j + 40);
                    puff.localPosition = new Vector3(SeatX + Mathf.Cos(a) * spread * k, 0.9f + 1.4f * k * (0.6f + SetUtil.Hash(j, 3)), Mathf.Sin(a) * spread * k);
                    puff.localScale = Vector3.one * (Mathf.Sin(k * Mathf.PI) * (0.35f + 0.25f * SetUtil.Hash(j, index)));
                }
            }
        }

        static void HidePuffs(Seat seat)
        {
            if (seat.puffs == null)
            {
                return;
            }

            foreach (Transform t in seat.puffs)
            {
                SetUtil.SetActive(t, false);
            }
        }
    }
}
