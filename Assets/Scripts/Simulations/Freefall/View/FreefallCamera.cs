using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// Smooths a requested camera position and look target and applies a roll about the view axis. The
    /// presenter decides the shots (descent, reveal pull-back, outro pull-back); this only makes them glide.
    /// Runs in <c>LateUpdate</c>, reads nothing from the simulation.
    /// </summary>
    internal sealed class FreefallCameraRig
    {
        readonly Camera _camera;
        Vector3 _position;
        Vector3 _positionVelocity;
        Vector3 _look;
        Vector3 _lookVelocity;
        Vector3 _upReference = Vector3.up;
        bool _initialised;

        public FreefallCameraRig(Camera camera)
        {
            _camera = camera;
        }

        public Vector3 Position => _position;

        public Vector3 Look => _look;

        public float DistanceToLook => (_look - _position).magnitude;

        public void Apply(Vector3 position, Vector3 look, float rollDegrees, float dt, float smoothTime)
        {
            if (_camera == null)
            {
                return;
            }

            if (!_initialised)
            {
                _initialised = true;
                _position = position;
                _look = look;
            }
            else
            {
                dt = Mathf.Max(dt, 1e-4f);
                _position = Vector3.SmoothDamp(_position, position, ref _positionVelocity, smoothTime, float.PositiveInfinity, dt);
                _look = Vector3.SmoothDamp(_look, look, ref _lookVelocity, smoothTime * 0.8f, float.PositiveInfinity, dt);
            }

            Vector3 forward = _look - _position;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = Vector3.down;
            }

            forward.Normalize();

            // Looking almost straight down makes "up" undefined; keep the last good reference in that case.
            if (Mathf.Abs(forward.y) < 0.985f)
            {
                _upReference = Vector3.up;
            }

            Vector3 up = Vector3.ProjectOnPlane(_upReference, forward);
            if (up.sqrMagnitude < 1e-6f)
            {
                up = Vector3.ProjectOnPlane(Vector3.forward, forward);
            }

            up = Quaternion.AngleAxis(rollDegrees, forward) * up.normalized;
            _camera.transform.SetPositionAndRotation(_position, Quaternion.LookRotation(forward, up));
        }
    }
}
