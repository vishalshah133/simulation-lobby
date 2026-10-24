using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// A tray whose floor is a row of hinged trapdoors. Every cycle the doors swing open in a travelling wave from the far
    /// end back to the entry end, drop the pile into the brick chute below, then close in the same order.
    /// </summary>
    public sealed class TrapdoorRunStation : MachineStation
    {
        [Tooltip("Door bodies, index 0 nearest the entry. Door N-1 (far end) opens first.")]
        public Rigidbody[] doors;
        [Tooltip("Hinge pin position of each door in tray-frame space.")]
        public Vector3[] hingeBed;
        [Tooltip("Panel centre relative to its hinge when closed, tray-frame space.")]
        public Vector3 panelOffset;
        [Tooltip("Tray-frame tilt (slope toward +Z).")]
        public Quaternion bedRotation = Quaternion.identity;
        public float openDegrees = 80f;
        public float swingSeconds = 0.12f;
        public float openSeconds = 0.7f;

        float _cycle = 5f, _stagger = 0.15f, _phase;
        bool[] _wasOpen;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _cycle = config.trapdoorCycleSeconds;
            _stagger = config.trapdoorStaggerSeconds;
            _phase = rng.Branch(403).NextFloat() * _cycle;
            _wasOpen = doors != null ? new bool[doors.Length] : null;
        }

        public override void Step(float simTime, float dt)
        {
            if (doors == null) return;
            float tc = (simTime + _phase) % _cycle;
            int n = doors.Length;
            Quaternion worldBed = transform.rotation * bedRotation;
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = doors[i];
                if (rb == null) continue;
                float u = tc - (n - 1 - i) * _stagger;
                float f = 0f;
                if (u > 0f)
                {
                    if (u < swingSeconds) f = Mathf.SmoothStep(0f, 1f, u / swingSeconds);
                    else if (u < swingSeconds + openSeconds) f = 1f;
                    else if (u < 2f * swingSeconds + openSeconds) f = 1f - Mathf.SmoothStep(0f, 1f, (u - swingSeconds - openSeconds) / swingSeconds);
                }

                Quaternion hinge = Quaternion.Euler(0f, 0f, openDegrees * f);
                Vector3 centre = hingeBed[i] + hinge * panelOffset;
                rb.MovePosition(transform.TransformPoint(bedRotation * centre));
                rb.MoveRotation(worldBed * hinge);

                if (_wasOpen != null)
                {
                    bool open = f > 0.05f;
                    if (open && !_wasOpen[i]) RaisePulse(rb.position, 0.5f);
                    _wasOpen[i] = open;
                }
            }
        }
    }
}
