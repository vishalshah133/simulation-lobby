using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Circles its parent's local origin in a horizontal ring with a vertical bob (ambient bats). Cosmetic.</summary>
    public sealed class Orbit : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 6f;
        public float angularSpeed = 0.7f;
        public float bob = 0.8f;
        public float phase;

        void Update()
        {
            float a = Time.time * angularSpeed + phase;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 2.3f) * bob, Mathf.Sin(a) * radius);
            Vector3 next = center + new Vector3(Mathf.Cos(a + 0.05f) * radius, Mathf.Sin((a + 0.05f) * 2.3f) * bob, Mathf.Sin(a + 0.05f) * radius);
            transform.localPosition = p;
            Vector3 dir = next - p;
            if (dir.sqrMagnitude > 1e-8f)
            {
                transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }
}
