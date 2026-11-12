using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Gentle pendulum sway about the local rest rotation (hanging lanterns, bunting, signs). Cosmetic.</summary>
    public sealed class Sway : MonoBehaviour
    {
        public Vector3 amplitudeDegrees = new Vector3(4f, 0f, 4f);
        public float speed = 1.2f;
        public float phase;
        Quaternion rest;

        void Awake()
        {
            rest = transform.localRotation;
        }

        void Update()
        {
            float s = Mathf.Sin(Time.time * speed + phase);
            float c = Mathf.Cos(Time.time * speed * 0.83f + phase);
            transform.localRotation = rest * Quaternion.Euler(amplitudeDegrees.x * s, amplitudeDegrees.y * c, amplitudeDegrees.z * c);
        }
    }
}
