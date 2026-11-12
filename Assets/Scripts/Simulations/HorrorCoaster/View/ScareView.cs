using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Shared helpers for the jump-scare entities: the real eye pose, a lunge-and-hold track that ends in front of the lens, a
    /// look-target latch and a light ramp. Read-only on the simulation: it only reads <see cref="ICoasterState"/> and moves view objects.
    /// </summary>
    public static class ScareView
    {
        /// <summary>Right, up, forward of the viewer's eye relative to the cart (mirrors the ride camera's right-seat pose).</summary>
        public static readonly Vector3 SeatOffset = new Vector3(0.4f, 1.3f, -0.3f);

        /// <summary>The eye the viewer sees through. Camera.main when there is one (so shake and flinch are included), else the right-seat pose.</summary>
        public static void Eye(ICoasterState s, out Vector3 pos, out Quaternion rot)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Transform t = cam.transform;
                pos = t.position;
                rot = t.rotation;
                return;
            }

            Vector3 f = s.CartForward;
            Vector3 u = s.CartUp;
            Vector3 r = Vector3.Cross(u, f);
            pos = s.CartPosition + u * SeatOffset.y + r * SeatOffset.x + f * SeatOffset.z;
            rot = SetUtil.Look(f, u);
        }

        /// <summary>Where a rail frame at distance <paramref name="d"/> (plus lateral/vertical offsets) sits in the world.</summary>
        public static Vector3 RailPoint(ICoasterRail rail, float d, float lateral, float vertical, float alongExtra = 0f)
        {
            float clamped = Mathf.Clamp(d, 0f, rail.Length);
            SetUtil.Frame(rail, clamped, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
            return p + f * (d - clamped + alongExtra) + r * lateral + u * vertical;
        }

        /// <summary>World point where the entity's face (given in its local space) lands when the root has this pose and scale.</summary>
        public static Vector3 RootForFace(Vector3 facePos, Quaternion rot, Vector3 faceLocal, float scale)
        {
            return facePos - rot * (faceLocal * scale);
        }

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float k = 1f - t;
            return 1f - k * k * k;
        }

        /// <summary>Smoothly ramps a light toward a target intensity and switches it off at ~0. Call every frame.</summary>
        public static void SetLight(Light l, float intensity)
        {
            if (l == null)
            {
                return;
            }

            bool on = intensity > 0.02f;
            if (l.enabled != on)
            {
                l.enabled = on;
            }

            if (on)
            {
                l.intensity = intensity;
            }
        }
    }

    /// <summary>
    /// A hard lunge: the entity flies from its home pose to a pose fixed to the camera in well under 0.2 s (cubic ease-out), holds
    /// there while the sting lasts, then is left behind (frozen in the world and pushed back) as the cart carries on. The camera
    /// pose is passed in every frame, so the entity stays on the lens whatever the speed of the cart.
    /// </summary>
    public sealed class LungeTrack
    {
        public float lungeSeconds = 0.16f;
        public float holdSeconds = 0.62f;
        public float retreatSpeed = 6f;

        Vector3 last;
        Quaternion lastRot = Quaternion.identity;
        Vector3 frozen;
        Quaternion frozenRot = Quaternion.identity;
        float frozenAt;
        bool isFrozen;

        public bool Held { get; private set; }

        public void Reset()
        {
            isFrozen = false;
            Held = false;
        }

        /// <summary>Pose at <paramref name="since"/> seconds after the sting began. <paramref name="retreatDir"/> is where it is flung afterwards.</summary>
        public void Sample(float since, Vector3 home, Quaternion homeRot, Vector3 end, Quaternion endRot, Vector3 retreatDir, out Vector3 pos, out Quaternion rot)
        {
            if (since < holdSeconds)
            {
                isFrozen = false;
                Held = true;
                float k = ScareView.EaseOutCubic(since / Mathf.Max(0.02f, lungeSeconds));
                pos = Vector3.Lerp(home, end, k);
                rot = Quaternion.Slerp(homeRot, endRot, k);
                last = pos;
                lastRot = rot;
                return;
            }

            Held = false;
            if (!isFrozen)
            {
                isFrozen = true;
                frozen = last;
                frozenRot = lastRot;
                frozenAt = since;
            }

            pos = frozen + retreatDir * (retreatSpeed * (since - frozenAt));
            rot = frozenRot;
        }
    }

    /// <summary>Latches the shared <see cref="CoasterLookTarget"/> for one entity: modest weight in the quiet, full weight at the sting, cleared afterwards.</summary>
    public sealed class LookCue
    {
        bool set;

        public void Quiet(Vector3 point, float weight = 0.35f)
        {
            CoasterLookTarget.Set(point, weight);
            set = true;
        }

        public void Sting(Vector3 point)
        {
            CoasterLookTarget.Set(point, 1f);
            set = true;
        }

        public void Release()
        {
            if (set)
            {
                CoasterLookTarget.Clear();
                set = false;
            }
        }
    }
}
