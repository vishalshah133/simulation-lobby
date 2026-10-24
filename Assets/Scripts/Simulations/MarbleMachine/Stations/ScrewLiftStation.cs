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

        float _degPerSecond = 120f, _phaseDeg;

        public float AxialSpeed => pitch * _degPerSecond / 360f;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _degPerSecond = config.screwDegreesPerSecond;
            _phaseDeg = rng.Branch(404).NextFloat() * 360f;
        }

        public override void Step(float simTime, float dt)
        {
            if (rotor == null) return;
            float theta = _phaseDeg + _degPerSecond * simTime;
            rotor.MoveRotation(transform.rotation * frameRotation * Quaternion.Euler(0f, theta, 0f));
        }
    }
}
