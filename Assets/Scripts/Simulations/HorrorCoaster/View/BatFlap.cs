using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Flaps two wing transforms about their local Z axis. Cosmetic.</summary>
    public sealed class BatFlap : MonoBehaviour
    {
        public Transform wingLeft;
        public Transform wingRight;
        public float speed = 14f;
        public float amplitudeDegrees = 38f;
        public float phase;

        void Update()
        {
            float a = Mathf.Sin(Time.time * speed + phase) * amplitudeDegrees;
            if (wingLeft != null)
            {
                wingLeft.localRotation = Quaternion.Euler(0f, 0f, a);
            }

            if (wingRight != null)
            {
                wingRight.localRotation = Quaternion.Euler(0f, 0f, -a);
            }
        }
    }
}
