using System;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Where a scare entity wants the rider to look during the quiet before its sting. Scare entities call
    /// <see cref="Set"/> when they appear and <see cref="Clear"/> when done; the ride camera eases its look toward the point
    /// through critically damped springs during PreQuiet, and on the first frame of the Sting phase of a real scare the view
    /// SNAPS toward it within 0.12 s (weight-limited to 35 degrees yaw / 25 degrees pitch off the track direction), holds about
    /// 0.5 s and eases back over 1 s. So: call <c>Set(point, weight)</c> as soon as the entity appears (PreQuiet) or at the latest
    /// before the Sting frame, keep updating the point if the entity moves, and <c>Clear()</c> when done (the camera holds the
    /// snap angle it captured, so clearing at the Sting is harmless). Weight 1 = look straight at it, 0.5 = half way.
    /// Read by the camera only; nothing here touches simulation state.
    /// </summary>
    public static class CoasterLookTarget
    {
        public static bool Active { get; private set; }
        public static Vector3 Point { get; private set; }
        /// <summary>0..1: how far toward the point the head turns (1 = look straight at it, within the yaw/pitch limits).</summary>
        public static float Weight { get; private set; }

        public static void Set(Vector3 worldPoint, float weight)
        {
            Point = worldPoint;
            Weight = Mathf.Clamp01(weight);
            Active = true;
        }

        /// <summary>Shorthand for <c>Set(point, 1)</c>: look straight at it (within the 35 degree limit).</summary>
        public static void Set(Vector3 worldPoint)
        {
            Set(worldPoint, 1f);
        }

        public static void Clear()
        {
            Active = false;
            Weight = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Clear();
        }
    }


    /// <summary>Tunables for the ride camera. Lives on the rig so the scene builder can leave defaults and a take can tweak them.</summary>
    [Serializable]
    public sealed class RideCameraSettings
    {
        [Header("Seat (the viewer sits in the RIGHT lane of the last car)")]
        [Tooltip("Eye height above the cart floor along the cart up vector (m).")]
        public float eyeHeight = 1.3f;
        [Tooltip("Eye offset to the cart's right (m): the viewer's seat is the right lane, local x = +0.4.")]
        public float eyeRightOffset = 0.4f;
        [Tooltip("Eye offset along the cart forward (m); negative = slightly back, clear of the rider seats ahead.")]
        public float eyeForwardOffset = -0.3f;
        [Tooltip("Default pitch below the track direction (deg).")]
        public float pitchDownDegrees = 3f;

        [Header("Field of view")]
        public float fovBase = 72f;
        [Tooltip("Extra FOV at full speed (smooth).")]
        public float fovBoost = 16f;
        [Tooltip("Extra FOV at full fear.")]
        public float fovFear = 2f;

        [Header("Head sway and bob (all scale with speed)")]
        public float swayMeters = 0.05f;
        public float bobMeters = 0.035f;
        public float swayRollDegrees = 1.1f;

        [Header("Wind buffet (grows above this speed)")]
        public float buffetStartSpeed = 18f;
        public float buffetFullSpeed = 30f;
        public float buffetDegrees = 0.55f;
        public float buffetMeters = 0.012f;

        [Header("Look-around drift")]
        public float driftYawDegrees = 4f;
        public float driftPitchDegrees = 2.2f;
        [Tooltip("Drift grows by this factor at full fear (more nervous glancing).")]
        public float driftFearGain = 0.8f;

        [Header("Look toward the expected scare (max angles off the track direction)")]
        public float lookMaxYawDegrees = 35f;
        public float lookMaxPitchDegrees = 25f;
        [Tooltip("Spring omega (1/s) of the slow PreQuiet look-around. Lower = slower, heavier head.")]
        public float lookOmega = 2f;
        public float lookMaxRateDegPerSec = 20f;
        public float lookMaxAccelDegPerSec2 = 45f;

        [Header("Jump-scare camera (every real scare, on the Sting frame)")]
        public float punchFov = 14f;
        public float punchRiseSeconds = 0.07f;
        public float punchSettleSeconds = 0.5f;
        [Tooltip("Head pitch kick (deg, negative = chin up / recoil).")]
        public float flinchPitchDegrees = -7f;
        public float flinchRollDegrees = 6f;
        [Tooltip("Omega of the flinch spring: 16 settles in about 0.4 s. No velocity cap, by design.")]
        public float flinchOmega = 16f;
        public float stingShakeSeconds = 0.35f;
        public float stingShakeMeters = 0.06f;
        public float stingShakeDegrees = 1.5f;
        [Tooltip("The look-target snap: omega (settles in about 0.12 s), hold and return times.")]
        public float snapOmega = 38f;
        public float snapHoldSeconds = 0.5f;
        public float snapReturnSeconds = 1f;

        [Header("Fear shake")]
        public float shakeDegrees = 0.35f;
        public float shakeMeters = 0.006f;

        [Header("Trip act roll")]
        [Tooltip("Extra oscillating roll during the Trip (deg). Capped at 12.")]
        public float tripRollDegrees = 10f;
        [Tooltip("0 = outside the Trip the head stays level; 1 = the head always follows the banked rail up vector. The Trip itself is always 1.")]
        [Range(0f, 1f)] public float bankFollowOutsideTrip = 0.7f;
        [Tooltip("Spring omega of the Trip roll blend in and out.")]
        public float tripBlendOmega = 1.4f;
    }

    /// <summary>One frame of input to <see cref="RideCameraCore"/>. Plain data, no engine objects.</summary>
    public struct RideInput
    {
        public Vector3 cartPosition;
        public Vector3 cartForward;
        public Vector3 cartUp;
        /// <summary>Speed divided by the reference max speed, 0..1.</summary>
        public float speed01;
        /// <summary>Speed in m/s (drives the wind buffet).</summary>
        public float speedMps;
        public float fear01;
        public bool trip;
        /// <summary>PreQuiet or Sting.</summary>
        public bool scareActive;
        public bool hasLook;
        public Vector3 lookPoint;
        public float lookWeight;
        /// <summary>True for exactly one frame: the first frame of the Sting phase of a real scare.</summary>
        public bool stingTrigger;
        /// <summary>-1 = scare on the left, +1 = on the right (the head flinches away from it).</summary>
        public float stingSide;
        /// <summary>0..1.3 scale of the jump-scare camera kick.</summary>
        public float stingStrength;
    }

    public struct RidePose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
    }

    /// <summary>
    /// The POV camera maths as a pure class (no MonoBehaviour, no Time, no Physics) so it can be run over a synthetic trace.
    /// Pose comes straight from the cart (already smooth). On top: head sway and bob that grow with speed, wind buffet, look-around
    /// drift, the slow turn toward an expected scare, the fear shake, the Trip roll and the FOV. Every slow term goes through its
    /// own critically damped spring with velocity and acceleration caps (<see cref="CameraSpring"/>), stepped at ~120 Hz, so the
    /// result does not depend on the frame rate. The jump-scare kick (FOV punch, flinch, shake, look snap) is deliberately
    /// exempt from those caps: it is impulse- or envelope-driven from the trigger time only, so it is also frame-rate independent.
    /// </summary>
    public sealed class RideCameraCore
    {
        const float SubstepSeconds = 1f / 120f;
        const float MaxFrameSeconds = 0.1f;
        const float BigAccel = 1e6f;
        const float E = 2.7182818f;

        readonly RideCameraSettings _s;

        bool _init;
        float _t;

        // Springs: value, velocity.
        float _yaw, _yawV, _pitch, _pitchV, _roll, _rollV;
        float _swayX, _swayXV, _bobY, _bobYV;
        float _shY, _shYV, _shP, _shPV, _shR, _shRV, _shX, _shXV, _shU, _shUV;
        float _fov, _fovV;
        float _tripW, _tripWV;
        float _gate, _gateV;
        float _speedF, _speedFV, _fearF, _fearFV;

        // Jump-scare kick.
        float _flP, _flPV, _flR, _flRV;
        float _stingAge = 100f, _stingAmp;
        float _punchAge = 100f, _punchAmp;
        float _snapAge = 100f, _snapYaw, _snapPitch;
        float _snapOffY, _snapOffYV, _snapOffP, _snapOffPV;
        float _stShY, _stShP, _stShR, _stShX, _stShU;
        float _buffet;

        // Per-frame look target in degrees (head-relative), before the gate.
        float _lookYawT, _lookPitchT;

        public RideCameraCore(RideCameraSettings settings)
        {
            _s = settings ?? new RideCameraSettings();
        }

        public void Reset()
        {
            _init = false;
        }

        public RidePose Step(in RideInput inp, float dt)
        {
            dt = Mathf.Clamp(dt, 0f, MaxFrameSeconds);
            if (!_init)
            {
                Snap(inp);
            }

            ComputeLookTarget(inp);
            if (inp.stingTrigger)
            {
                Trigger(inp);
            }

            if (_snapAge < _s.snapHoldSeconds && inp.hasLook)
            {
                // Follow a moving entity while the view is held on it.
                _snapYaw = _lookYawT;
                _snapPitch = _lookPitchT;
            }

            if (dt > 1e-5f)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(dt / SubstepSeconds));
                float h = dt / n;
                for (int i = 0; i < n; i++)
                {
                    Substep(inp, h);
                }
            }

            return Compose(inp);
        }

        void Snap(in RideInput inp)
        {
            _init = true;
            _t = 0f;
            _yaw = _yawV = _pitch = _pitchV = _roll = _rollV = 0f;
            _swayX = _swayXV = _bobY = _bobYV = 0f;
            _shY = _shYV = _shP = _shPV = _shR = _shRV = _shX = _shXV = _shU = _shUV = 0f;
            _flP = _flPV = _flR = _flRV = 0f;
            _stingAge = _punchAge = _snapAge = 100f;
            _snapOffY = _snapOffYV = _snapOffP = _snapOffPV = 0f;
            _stShY = _stShP = _stShR = _stShX = _stShU = 0f;
            _buffet = 0f;
            _gate = _gateV = 0f;
            _tripW = inp.trip ? 1f : 0f;
            _tripWV = 0f;
            _speedF = inp.speed01;
            _speedFV = 0f;
            _fearF = inp.fear01;
            _fearFV = 0f;
            _fov = _s.fovBase;
            _fovV = 0f;
        }

        void Trigger(in RideInput inp)
        {
            float k = Mathf.Clamp(inp.stingStrength <= 0f ? 1f : inp.stingStrength, 0f, 1.3f);
            float side = Mathf.Clamp(inp.stingSide, -1f, 1f);
            float om = _s.flinchOmega;
            // Critically damped from rest with initial velocity v0 peaks at v0 / (omega * e): choose v0 for the wanted peak.
            _flPV += _s.flinchPitchDegrees * k * om * E;
            _flRV += _s.flinchRollDegrees * k * side * om * E;
            _stingAge = 0f;
            _stingAmp = k;
            _punchAge = 0f;
            _punchAmp = k;
            if (inp.hasLook)
            {
                _snapAge = 0f;
                _snapYaw = _lookYawT;
                _snapPitch = _lookPitchT;
            }
        }

        /// <summary>The eye before sway: seat offsets in the cart's own frame. right = cross(up, forward) is the cart's right.</summary>
        Vector3 EyeBase(in RideInput inp)
        {
            Vector3 fwd = inp.cartForward.sqrMagnitude > 1e-6f ? inp.cartForward.normalized : Vector3.forward;
            Vector3 up = inp.cartUp.sqrMagnitude > 1e-6f ? inp.cartUp.normalized : Vector3.up;
            Vector3 right = Vector3.Cross(up, fwd);
            right = right.sqrMagnitude < 1e-6f ? Vector3.right : right.normalized;
            return inp.cartPosition + up * _s.eyeHeight + right * _s.eyeRightOffset + fwd * _s.eyeForwardOffset;
        }

        Quaternion BaseRotation(in RideInput inp)
        {
            Vector3 fwd = inp.cartForward.sqrMagnitude > 1e-6f ? inp.cartForward.normalized : Vector3.forward;
            Vector3 up = inp.cartUp.sqrMagnitude > 1e-6f ? inp.cartUp.normalized : Vector3.up;
            Vector3 level = Vector3.up - fwd * Vector3.Dot(Vector3.up, fwd);
            float w = Mathf.Lerp(_s.bankFollowOutsideTrip, 1f, Mathf.Clamp01(_tripW));
            Vector3 camUp = level.sqrMagnitude < 0.01f ? up : Vector3.Slerp(level.normalized, up, w);
            return Quaternion.LookRotation(fwd, camUp);
        }

        void ComputeLookTarget(in RideInput inp)
        {
            if (!inp.hasLook)
            {
                _lookYawT = 0f;
                _lookPitchT = 0f;
                return;
            }

            Quaternion baseRot = BaseRotation(inp);
            Vector3 eye = EyeBase(inp);
            Vector3 local = Quaternion.Inverse(baseRot) * (inp.lookPoint - eye);
            float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, Mathf.Max(1e-4f, flat)) * Mathf.Rad2Deg;
            float w = Mathf.Clamp01(inp.lookWeight);
            _lookYawT = Mathf.Clamp(yaw * w, -_s.lookMaxYawDegrees, _s.lookMaxYawDegrees);
            _lookPitchT = Mathf.Clamp(pitch * w, -_s.lookMaxPitchDegrees, _s.lookMaxPitchDegrees);
        }

        float SnapEnvelope()
        {
            if (_snapAge < _s.snapHoldSeconds)
            {
                return 1f;
            }

            float u = (_snapAge - _s.snapHoldSeconds) / Mathf.Max(0.05f, _s.snapReturnSeconds);
            return u >= 1f ? 0f : 1f - CameraSpring.SmoothStep01(u);
        }

        void Substep(in RideInput inp, float h)
        {
            _t += h;
            _stingAge += h;
            _punchAge += h;
            _snapAge += h;
            float t = _t;

            // Slow context filters.
            CameraSpring.Step(ref _speedF, ref _speedFV, inp.speed01, 0f, 1.5f, -1f, 1f, 2f, h);
            CameraSpring.Step(ref _fearF, ref _fearFV, inp.fear01, 0f, 1.5f, -1f, 1f, 2f, h);
            CameraSpring.Step(ref _tripW, ref _tripWV, inp.trip ? 1f : 0f, 0f, _s.tripBlendOmega, -1f, 1f, 4f, h);
            float gateTarget = inp.hasLook && inp.scareActive ? 1f : 0f;
            CameraSpring.Step(ref _gate, ref _gateV, gateTarget, 0f, 1.6f, -1f, 1f, 3f, h);

            float sp = Mathf.Clamp01(_speedF);
            float fe = Mathf.Clamp01(_fearF);
            float trip = Mathf.Clamp01(_tripW);
            float gate = Mathf.Clamp01(_gate);
            float buffetT = Mathf.Clamp01((inp.speedMps - _s.buffetStartSpeed) / Mathf.Max(1f, _s.buffetFullSpeed - _s.buffetStartSpeed));
            _buffet += (buffetT - _buffet) * (1f - Mathf.Exp(-h / 0.4f));

            // Look-around: slow sinusoid drift plus the scare look, through one spring per axis.
            float nerve = 1f + _s.driftFearGain * fe;
            float driftYaw = _s.driftYawDegrees * nerve * (Mathf.Sin(0.41f * t) + 0.5f * Mathf.Sin(1.07f * t + 1.3f)) / 1.5f;
            float driftPitch = _s.driftPitchDegrees * nerve * (Mathf.Sin(0.57f * t + 0.4f) + 0.5f * Mathf.Sin(1.31f * t + 2.1f)) / 1.5f;
            float rate = _s.lookMaxRateDegPerSec;
            float acc = _s.lookMaxAccelDegPerSec2;
            CameraSpring.Step(ref _yaw, ref _yawV, driftYaw + _lookYawT * gate, 0f, _s.lookOmega, -rate, rate, acc, h);
            CameraSpring.Step(ref _pitch, ref _pitchV, driftPitch + _lookPitchT * gate, 0f, _s.lookOmega, -rate, rate, acc, h);

            // Look snap on top of the slow head: a fast spring toward (snap angle - slow angle), exempt from the caps.
            float env = SnapEnvelope();
            CameraSpring.Step(ref _snapOffY, ref _snapOffYV, env * (_snapYaw - _yaw), 0f, _s.snapOmega, -4000f, 4000f, BigAccel, h);
            CameraSpring.Step(ref _snapOffP, ref _snapOffPV, env * (_snapPitch - _pitch), 0f, _s.snapOmega, -4000f, 4000f, BigAccel, h);

            // Flinch: impulse-driven, settles to zero by itself.
            CameraSpring.Step(ref _flP, ref _flPV, 0f, 0f, _s.flinchOmega, -4000f, 4000f, BigAccel, h);
            CameraSpring.Step(ref _flR, ref _flRV, 0f, 0f, _s.flinchOmega, -4000f, 4000f, BigAccel, h);

            // Sting shake: 0.35 s decaying envelope, 23-31 Hz so it reads as a jolt, not a wobble.
            float tau = 2f * Mathf.PI;
            float sk = 0f;
            if (_stingAge < _s.stingShakeSeconds)
            {
                float u = 1f - _stingAge / _s.stingShakeSeconds;
                sk = u * u * _stingAmp;
            }

            _stShY = sk * _s.stingShakeDegrees * Mathf.Sin(tau * 27f * _stingAge);
            _stShP = sk * _s.stingShakeDegrees * Mathf.Sin(tau * 31f * _stingAge + 1.3f);
            _stShR = sk * _s.stingShakeDegrees * 0.6f * Mathf.Sin(tau * 23f * _stingAge + 2.1f);
            _stShX = sk * _s.stingShakeMeters * Mathf.Sin(tau * 29f * _stingAge + 0.4f);
            _stShU = sk * _s.stingShakeMeters * Mathf.Sin(tau * 25f * _stingAge + 1.9f);

            // Head sway and bob.
            float swayT = _s.swayMeters * sp * (Mathf.Sin(1.9f * t) + 0.6f * Mathf.Sin(3.3f * t + 0.7f));
            float bobT = _s.bobMeters * (0.25f + sp) * (0.5f * Mathf.Sin(7.1f * t) + Mathf.Sin(2.3f * t + 1.9f));
            CameraSpring.Step(ref _swayX, ref _swayXV, swayT, 0f, 7f, -1f, 1f, 8f, h);
            CameraSpring.Step(ref _bobY, ref _bobYV, bobT, 0f, 9f, -1f, 1f, 10f, h);

            // Roll: sway roll plus the Trip's gentle oscillation (hard cap 12 deg).
            float tripRoll = trip * Mathf.Min(_s.tripRollDegrees, 9.5f) * Mathf.Sin(0.62f * t);
            tripRoll += trip * Mathf.Min(_s.tripRollDegrees * 0.3f, 2.5f) * Mathf.Sin(1.7f * t + 1f);
            tripRoll = Mathf.Clamp(tripRoll, -12f, 12f);
            float swayRoll = _s.swayRollDegrees * sp * Mathf.Sin(1.3f * t + 2f);
            CameraSpring.Step(ref _roll, ref _rollV, tripRoll + swayRoll, 0f, 3f, -25f, 25f, 60f, h);

            // Fear shake plus wind buffet: smooth high-frequency terms (11-19 Hz) through a fast but still critically damped spring.
            float amp = fe * fe;
            float a = amp * _s.shakeDegrees + _buffet * _s.buffetDegrees;
            float m = amp * _s.shakeMeters + _buffet * _s.buffetMeters;
            CameraSpring.Step(ref _shY, ref _shYV, a * Mathf.Sin(tau * 13.7f * t), 0f, 45f, -400f, 400f, BigAccel, h);
            CameraSpring.Step(ref _shP, ref _shPV, a * Mathf.Sin(tau * 17.3f * t + 1.1f), 0f, 45f, -400f, 400f, BigAccel, h);
            CameraSpring.Step(ref _shR, ref _shRV, 0.5f * a * Mathf.Sin(tau * 11.1f * t + 2.2f), 0f, 45f, -400f, 400f, BigAccel, h);
            CameraSpring.Step(ref _shX, ref _shXV, m * Mathf.Sin(tau * 19.3f * t + 0.5f), 0f, 45f, -5f, 5f, BigAccel, h);
            CameraSpring.Step(ref _shU, ref _shUV, m * Mathf.Sin(tau * 15.9f * t + 1.7f), 0f, 45f, -5f, 5f, BigAccel, h);

            // FOV: base + a smooth speed boost + fear (the punch is added in Compose).
            float fovT = _s.fovBase + _s.fovBoost * Mathf.SmoothStep(0f, 1f, sp) + _s.fovFear * fe;
            CameraSpring.Step(ref _fov, ref _fovV, fovT, 0f, 2.5f, -20f, 20f, 40f, h);
        }

        float PunchFov()
        {
            if (_punchAge >= _s.punchRiseSeconds + _s.punchSettleSeconds)
            {
                return 0f;
            }

            float k = _s.punchFov * _punchAmp;
            if (_punchAge < _s.punchRiseSeconds)
            {
                return k * CameraSpring.SmoothStep01(_punchAge / _s.punchRiseSeconds);
            }

            return k * (1f - CameraSpring.SmoothStep01((_punchAge - _s.punchRiseSeconds) / _s.punchSettleSeconds));
        }

        RidePose Compose(in RideInput inp)
        {
            Vector3 up = inp.cartUp.sqrMagnitude > 1e-6f ? inp.cartUp.normalized : Vector3.up;
            Vector3 fwd = inp.cartForward.sqrMagnitude > 1e-6f ? inp.cartForward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(up, fwd);
            right = right.sqrMagnitude < 1e-6f ? Vector3.right : right.normalized;

            Quaternion baseRot = BaseRotation(inp);
            Vector3 eye = EyeBase(inp)
                          + up * (_bobY + _shU + _stShU)
                          + right * (_swayX + _shX + _stShX);

            float pitch = _s.pitchDownDegrees + _pitch + _snapOffP + _shP + _stShP + _flP;
            float yaw = _yaw + _snapOffY + _shY + _stShY;
            float roll = _roll + _shR + _stShR + _flR;
            Quaternion local = Quaternion.Euler(pitch, yaw, roll);
            return new RidePose
            {
                position = eye,
                rotation = baseRot * local,
                fov = Mathf.Clamp(_fov + PunchFov() - 7f * (1f - ScareSlowMo.Current), 40f, 110f) // slow-mo push-in
            };
        }
    }

    /// <summary>
    /// First-person camera in the last car, viewer in the RIGHT seat. Put it on the scene camera; <see cref="CoasterRigBuilder"/>
    /// does. Reads the state in <c>LateUpdate</c> and only moves the camera transform and FOV, never simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RideCameraRig : MonoBehaviour
    {
        // Serialized under new names on purpose: a scene built with the first version keeps stale values (eye 1.1, FOV 75, ref speed 28)
        // under the old names, and renaming makes Unity fall back to these defaults.
        [SerializeField] RideCameraSettings _rideSettings = new RideCameraSettings();
        [Tooltip("Eye height above the cart floor (m). Copied into the settings on Bind.")]
        [SerializeField] float eyeHeight = 1.3f;
        [Tooltip("Eye offset to the cart's right (m): the viewer's seat is the right lane.")]
        [SerializeField] float eyeRightOffset = 0.4f;
        [Tooltip("Speed that counts as full speed for sway and FOV (the config maxSpeed, about 30 m/s).")]
        [Min(1f)] [SerializeField] float _referenceSpeedMps = 30f;
        [Tooltip("Optional: a transform the head turns toward during PreQuiet/Sting. CoasterLookTarget.Set does the same without a reference.")]
        public Transform lookTarget;
        [Range(0f, 1f)] public float lookTargetWeight = 0.8f;

        ICoasterState _state;
        RideCameraCore _core;
        Camera _camera;
        ScarePhase _lastPhase;
        int _lastIndex = -1;

        public RidePose LastPose { get; private set; }

        public void Bind(ICoasterState state)
        {
            _state = state;
            _rideSettings.eyeHeight = eyeHeight;
            _rideSettings.eyeRightOffset = eyeRightOffset;
            _core = new RideCameraCore(_rideSettings);
            _core.Reset();
            _lastPhase = ScarePhase.Idle;
            _lastIndex = -1;
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
        }

        public void SetReferenceSpeed(float metersPerSecond)
        {
            _referenceSpeedMps = Mathf.Max(1f, metersPerSecond);
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        static float Hash01(int a)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        void LateUpdate()
        {
            if (_state == null || _core == null)
            {
                return;
            }

            var input = new RideInput
            {
                cartPosition = _state.CartPosition,
                cartForward = _state.CartForward,
                cartUp = _state.CartUp,
                speed01 = Mathf.Clamp01(_state.Speed / _referenceSpeedMps),
                speedMps = _state.Speed,
                fear01 = Mathf.Clamp01(_state.Fear01),
                trip = _state.Act == ActId.Trip,
                scareActive = _state.Phase == ScarePhase.PreQuiet || _state.Phase == ScarePhase.Sting
            };

            if (lookTarget != null)
            {
                input.hasLook = true;
                input.lookPoint = lookTarget.position;
                input.lookWeight = lookTargetWeight;
            }
            else if (CoasterLookTarget.Active)
            {
                input.hasLook = true;
                input.lookPoint = CoasterLookTarget.Point;
                input.lookWeight = CoasterLookTarget.Weight;
            }

            // Sting detection: first frame in the Sting phase of a real scare (not Fake*, not TreatDoor).
            ScarePhase phase = _state.Phase;
            int index = _state.CurrentEventIndex;
            bool entered = phase == ScarePhase.Sting && (_lastPhase != ScarePhase.Sting || index != _lastIndex);
            _lastPhase = phase;
            _lastIndex = index;
            if (entered)
            {
                ScareEvent ev = _state.CurrentEvent;
                if (!ev.IsFake && ev.kind != ScareKind.TreatDoor)
                {
                    input.stingTrigger = true;
                    input.stingStrength = ev.kind.IsMicro() ? 0.85f : 1f;
                    float side;
                    if (input.hasLook)
                    {
                        Vector3 right = Vector3.Cross(_state.CartUp, _state.CartForward);
                        side = Vector3.Dot(input.lookPoint - _state.CartPosition, right) >= 0f ? 1f : -1f;
                    }
                    else
                    {
                        side = Hash01(index) > 0.5f ? 1f : -1f;
                    }

                    input.stingSide = side;
                }
            }

            RidePose pose = _core.Step(input, Time.deltaTime);
            LastPose = pose;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (_camera != null)
            {
                _camera.fieldOfView = pose.fov;
            }
        }
    }
}
