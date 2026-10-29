using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// An Archimedes screw in an inclined glass tube. The rotor (shaft + helical flight, one compound kinematic body) turns
    /// about the tube axis at <c>screwDegreesPerSecond</c>. Sign: the flight is wound so that a POSITIVE rate drives its
    /// surface up the tube (toward +Z and up); a negative rate would carry balls back down. Axial speed = pitch x rate / 360.
    /// </summary>
    public sealed class ScrewLiftStation : MachineStation
    {
        [Tooltip("Kinematic rotor: shaft capsule + flight boxes. Its local +Y is the tube axis.")]
        public Rigidbody rotor;
        [Tooltip("Station-local rotation that maps the rotor's local +Y onto the tube axis.")]
        public Quaternion frameRotation = Quaternion.identity;
        [Tooltip("Axial distance between flight turns, metres (for logs and tuning).")]
        public float pitch = 0.6f;

        [Tooltip("Radius of the flight's outer edge, metres. With Max Tip Speed it caps the turning rate.")]
        public float tipRadius;
        [Tooltip("Cap on the flight's edge speed, m/s (0 = no cap). A few big balls are dragged round by a fast flight instead of lifted by it.")]
        public float maxTipSpeed;

        float _degPerSecond = 120f, _phaseDeg;

        public float AxialSpeed => pitch * _degPerSecond / 360f;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _degPerSecond = config.screwDegreesPerSecond;
            if (maxTipSpeed > 0f && tipRadius > 0.01f)
            {
                _degPerSecond = Mathf.Min(_degPerSecond, maxTipSpeed / tipRadius * Mathf.Rad2Deg);
            }

            _phaseDeg = rng.Branch(404).NextFloat() * 360f;
        }

        public override void Step(float simTime, float dt)
        {
            if (rotor == null) return;
            // Unity yaw is positive from +Z toward +X, which moves a right-handed-in-(sin,cos) helix surface DOWN the axis, so
            // the angle is negated: a positive configured rate drives the flight (and the balls) up the tube.
            float theta = _phaseDeg - _degPerSecond * simTime;
            rotor.MoveRotation(transform.rotation * frameRotation * Quaternion.Euler(0f, theta, 0f));
        }
    }
}
