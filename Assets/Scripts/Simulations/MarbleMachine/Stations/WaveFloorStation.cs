using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// A bed of kinematic pistons whose tops follow a travelling sine wave toward +Z, so balls surf along the floor.
    /// Each piston's height is posed from simTime alone; the bed is tilted slightly downhill as a safety margin.
    /// </summary>
    public sealed class WaveFloorStation : MachineStation
    {
        [Tooltip("One kinematic body per piston.")]
        public Rigidbody[] pistons;
        [Tooltip("Rest (h = 0) local position of each piston's box centre.")]
        public Vector3[] restLocal;
        [Tooltip("Local z of each piston along the bed, used for the wave phase.")]
        public float[] pistonZ;
        [Tooltip("Local rotation of the bed (tilt).")]
        public Quaternion bedRotation = Quaternion.identity;

        float _amplitude = 0.25f, _wavelength = 2f, _frequency = 0.6f, _phase;
        Vector3 _up = Vector3.up;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _amplitude = config.waveAmplitude;
            _wavelength = config.waveLength;
            _frequency = config.waveFrequency;
            _phase = rng.Branch(401).NextFloat();
            _up = bedRotation * Vector3.up;
        }

        public override void Step(float simTime, float dt)
        {
            if (pistons == null) return;
            _up = bedRotation * Vector3.up;
            Quaternion worldRot = transform.rotation * bedRotation;
            float baseCycles = _frequency * simTime + _phase;
            for (int i = 0; i < pistons.Length; i++)
            {
                Rigidbody rb = pistons[i];
                if (rb == null) continue;
                float h = _amplitude * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (baseCycles - pistonZ[i] / _wavelength)));
                rb.MovePosition(transform.TransformPoint(restLocal[i] + _up * h));
                rb.MoveRotation(worldRot);
            }
        }
    }
}
