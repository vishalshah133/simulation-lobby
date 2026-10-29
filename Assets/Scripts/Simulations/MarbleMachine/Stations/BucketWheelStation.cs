using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// A ferris wheel of open-fronted copper buckets that shovel balls from a pit and tip them onto an apron at the top.
    /// Angle <c>a</c> runs from the bottom (0) and the RISING side is +Z: a bucket sits at
    /// (wheelCentre.y - R cos a, wheelCentre.z + R sin a) and moves toward +Z along the bottom, toward -Z over the top.
    /// Each bucket is hung level and a cam tilts it about X (angles are "tilt toward the back wall", the -Z side, so the
    /// Unity rotation is the negative of these numbers): a slight raise of the open front through the scoop, a hold-back
    /// tilt while it climbs, a big tip over the top, then back to level on the way down. Everything is posed from sim time.
    /// </summary>
    public sealed class BucketWheelStation : MachineStation
    {
        [Tooltip("One kinematic body per bucket (compound box collider).")]
        public Rigidbody[] buckets;
        [Tooltip("Visual-only rotating group (rim, spokes, gear). Rotated about local X.")]
        public Transform wheelVisual;
        [Tooltip("Axle position in station-local space.")]
        public Vector3 wheelCentre = new Vector3(0f, 3.9f, 2.6f);
        public float radius = 4f;

        [Header("Tilt cam (degrees of wheel angle from the bottom, going round; tilts are toward the back wall)")]
        public float scoopTiltDegrees = 6f;
        public float carryTiltDegrees = 25f;
        public float dumpTiltDegrees = 150f;
        public float dumpReturnTiltDegrees = 40f;
        public float scoopStartDeg = -30f;
        public float scoopEndDeg = 10f;
        public float carryFullDeg = 40f;
        public float dumpStartDeg = 156f;
        public float dumpRampDegrees = 18f;
        public float dumpHoldDegrees = 16f;
        public float levelAgainDeg = 240f;

        float _phaseDeg, _degPerSecond = 36f;
        float[] _lastAngle;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _degPerSecond = config.bucketWheelRpm * 6f;
            _phaseDeg = rng.Branch(402).NextFloat() * 360f;
            _lastAngle = buckets != null ? new float[buckets.Length] : null;
        }

        /// <summary>Bucket tilt about +X in degrees (negative tips the open front up and the mouth toward -Z) at wheel angle <paramref name="aDeg"/>.</summary>
        public float TiltAt(float aDeg)
        {
            float a = Wrap(aDeg + 60f, 360f) - 60f;     // -60 .. 300
            float dumpUp = dumpStartDeg + dumpRampDegrees;
            float dumpEnd = dumpUp + dumpHoldDegrees;
            float t;
            if (a < scoopStartDeg) t = Seg(a, -60f, scoopStartDeg, 0f, scoopTiltDegrees);
            else if (a < scoopEndDeg) t = scoopTiltDegrees;
            else if (a < carryFullDeg) t = Seg(a, scoopEndDeg, carryFullDeg, scoopTiltDegrees, carryTiltDegrees);
            else if (a < dumpStartDeg) t = carryTiltDegrees;
            else if (a < dumpUp) t = Seg(a, dumpStartDeg, dumpUp, carryTiltDegrees, dumpTiltDegrees);
            else if (a < dumpEnd) t = dumpTiltDegrees;
            else if (a < dumpEnd + dumpRampDegrees) t = Seg(a, dumpEnd, dumpEnd + dumpRampDegrees, dumpTiltDegrees, dumpReturnTiltDegrees);
            else if (a < levelAgainDeg) t = Seg(a, dumpEnd + dumpRampDegrees, levelAgainDeg, dumpReturnTiltDegrees, 0f);
            else t = 0f;
            return -t;
        }

        /// <summary>Bucket pivot position in station-local space at wheel angle <paramref name="aDeg"/>.</summary>
        public Vector3 PivotAt(float aDeg)
        {
            float rad = aDeg * Mathf.Deg2Rad;
            return new Vector3(wheelCentre.x, wheelCentre.y - radius * Mathf.Cos(rad), wheelCentre.z + radius * Mathf.Sin(rad));
        }

        public override void Step(float simTime, float dt)
        {
            if (buckets == null || buckets.Length == 0) return;
            float a0 = _phaseDeg + _degPerSecond * simTime;
            if (wheelVisual != null) wheelVisual.localRotation = Quaternion.Euler(-a0, 0f, 0f);
            float spacing = 360f / buckets.Length;
            Quaternion rootRot = transform.rotation;
            float pulseAt = dumpStartDeg + dumpRampDegrees * 0.8f;
            for (int i = 0; i < buckets.Length; i++)
            {
                Rigidbody rb = buckets[i];
                if (rb == null) continue;
                float a = a0 + i * spacing;
                Vector3 local = PivotAt(a);
                rb.MovePosition(transform.TransformPoint(local));
                rb.MoveRotation(rootRot * Quaternion.Euler(TiltAt(a), 0f, 0f));

                if (_lastAngle != null)
                {
                    float w = Wrap(a, 360f);
                    if (_lastAngle[i] < pulseAt && w >= pulseAt && w - _lastAngle[i] < 90f)
                        RaisePulse(transform.TransformPoint(local), 0.6f);
                    _lastAngle[i] = w;
                }
            }
        }

        static float Seg(float a, float a0, float a1, float t0, float t1)
        {
            float u = Mathf.Clamp01((a - a0) / Mathf.Max(0.0001f, a1 - a0));
            return Mathf.Lerp(t0, t1, u * u * (3f - 2f * u));
        }

        static float Wrap(float d, float period) { d %= period; return d < 0f ? d + period : d; }
    }
}
