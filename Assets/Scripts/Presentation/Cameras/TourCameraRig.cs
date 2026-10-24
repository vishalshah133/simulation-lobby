using System;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A continuous one-shot camera: it only ever glides, never cuts. Each <see cref="GlideTo"/> eases from wherever the
    /// camera is now; position and look target are smoothed separately, so a glide issued mid-glide blends without a jolt.
    /// An optional slow orbital drift keeps a dwell alive. Format-agnostic. Runs in <c>LateUpdate</c> and never writes the world.
    /// </summary>
    public sealed class TourCameraRig : MonoBehaviour
    {
        [Tooltip("Extra smoothing on top of the eased glide. Higher is floatier and hides retargeting.")]
        [Min(0.01f)] public float smoothTime = 0.3f;

        Camera _camera;
        Func<Vector3> _tracker;

        Vector3 _fromPos;
        Vector3 _toPos;
        Vector3 _fromLook;
        Vector3 _toLook;
        float _glideTime;
        float _glideDuration;

        Vector3 _easedPos;
        Vector3 _easedLook;
        Vector3 _pos;
        Vector3 _look;
        Vector3 _posVel;
        Vector3 _lookVel;

        float _driftAmplitude;
        float _driftSpeed;
        float _driftBlend;
        float _driftClock;
        bool _hasPose;

        public bool IsGliding => _glideTime < _glideDuration;

        public Vector3 Position => _pos;
        public Vector3 LookTarget => _look;

        public void Bind(Camera camera)
        {
            _camera = camera;
        }

        /// <summary>Place the camera instantly. For the first frame only.</summary>
        public void SnapTo(Vector3 position, Vector3 lookAt)
        {
            _fromPos = _toPos = _easedPos = _pos = position;
            _fromLook = _toLook = _easedLook = _look = lookAt;
            _posVel = _lookVel = Vector3.zero;
            _glideTime = _glideDuration = 0f;
            _hasPose = true;
            Apply();
        }

        /// <summary>Ease (smooth in-out) from the current pose to a new one over <paramref name="seconds"/>.</summary>
        public void GlideTo(Vector3 position, Vector3 lookAt, float seconds)
        {
            if (!_hasPose)
            {
                SnapTo(position, lookAt);
                return;
            }

            _fromPos = _easedPos;
            _fromLook = _easedLook;
            _toPos = position;
            _toLook = lookAt;
            _glideTime = 0f;
            _glideDuration = Mathf.Max(0.05f, seconds);
        }

        /// <summary>A slow orbital float while dwelling. Amplitude in metres, speed in radians per second.</summary>
        public void SetDrift(float amplitude, float speed)
        {
            _driftAmplitude = Mathf.Max(0f, amplitude);
            _driftSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>Follow a moving point instead of the glide's fixed look target. Null stops following.</summary>
        public void SetLookTracking(Func<Vector3> tracker)
        {
            _tracker = tracker;
        }

        void LateUpdate()
        {
            if (!_hasPose || _camera == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (_glideTime < _glideDuration)
            {
                _glideTime = Mathf.Min(_glideDuration, _glideTime + dt);
            }

            if (_tracker != null)
            {
                _toLook = _tracker();
            }

            float u = _glideDuration > 0f ? Mathf.Clamp01(_glideTime / _glideDuration) : 1f;
            float eased = u * u * u * (u * (u * 6f - 15f) + 10f);
            _easedPos = Vector3.LerpUnclamped(_fromPos, _toPos, eased);
            _easedLook = Vector3.LerpUnclamped(_fromLook, _toLook, eased);

            _pos = Vector3.SmoothDamp(_pos, _easedPos, ref _posVel, smoothTime, float.PositiveInfinity, dt);
            _look = Vector3.SmoothDamp(_look, _easedLook, ref _lookVel, smoothTime, float.PositiveInfinity, dt);

            // The drift fades in once the glide is over, so it never fights the move.
            float wantDrift = IsGliding ? 0f : 1f;
            _driftBlend = Mathf.MoveTowards(_driftBlend, wantDrift, dt * 0.5f);
            _driftClock += dt * _driftSpeed;
            Apply();
        }

        void Apply()
        {
            Vector3 offset = Vector3.zero;
            if (_driftAmplitude > 0f)
            {
                offset = new Vector3(
                    Mathf.Sin(_driftClock),
                    Mathf.Sin(_driftClock * 0.7f + 1.3f) * 0.5f,
                    Mathf.Cos(_driftClock * 0.83f)) * (_driftAmplitude * _driftBlend);
            }

            Transform t = _camera.transform;
            t.position = _pos + offset;
            Vector3 forward = _look - t.position;
            if (forward.sqrMagnitude > 1e-6f)
            {
                t.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
    }
}
