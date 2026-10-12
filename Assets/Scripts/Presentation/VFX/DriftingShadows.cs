using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Slides a few soft shadows across the frame and wraps them round, so a sunny field has clouds
    /// passing over it. Purely visual: it runs on <c>Time.deltaTime</c> and reads nothing from a run.
    /// </summary>
    public sealed class DriftingShadows : MonoBehaviour
    {
        [SerializeField] Transform[] _shadows;

        [Tooltip("World units per second along +x. Slow: a cloud, not a car.")]
        [Range(0f, 3f)] public float speed = 0.35f;

        [Tooltip("A shadow that passes this x re-enters on the opposite side.")]
        [Min(1f)] public float wrapX = 11f;

        void Update()
        {
            if (_shadows == null)
            {
                return;
            }

            float step = speed * Time.deltaTime;
            for (int i = 0; i < _shadows.Length; i++)
            {
                if (_shadows[i] == null)
                {
                    continue;
                }

                Vector3 p = _shadows[i].position;
                p.x += step * (0.7f + 0.3f * (i % 3));
                if (p.x > wrapX)
                {
                    p.x = -wrapX;
                }

                _shadows[i].position = p;
            }
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(Transform[] shadows)
        {
            _shadows = shadows;
        }
    }
}
