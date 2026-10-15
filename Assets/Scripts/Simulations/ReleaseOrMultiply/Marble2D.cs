using UnityEngine;

namespace SimulationLobby.Simulations.ReleaseOrMultiply
{
    /// <summary>
    /// One physical marble. It stands for <see cref="Weight"/> marbles: once the body budget is spent,
    /// a multiplier makes a marble heavier instead of adding clones, so the count stays exact while the
    /// number of physics bodies stays capped. Pure state -- the simulation writes it, presentation reads it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Marble2D : MonoBehaviour
    {
        [SerializeField] Transform _visual;
        [SerializeField] SpriteRenderer _renderer;

        public Rigidbody2D Body { get; private set; }

        public CircleCollider2D Collider { get; private set; }

        /// <summary>Stable creation-order id, used to sort simultaneous events reproducibly.</summary>
        public int Serial { get; internal set; }

        /// <summary>How many marbles this body stands for. Always at least 1 while alive.</summary>
        public long Weight { get; internal set; }

        /// <summary>The last gate row this marble was counted at, so a clone born in a gate can't pass it twice.</summary>
        public int LastGateRow { get; internal set; }

        internal int StillTicks { get; set; }

        /// <summary>Weight the presentation last styled this body for; presentation-owned cache, never read by the sim.</summary>
        public long StyledWeight { get; set; }

        /// <summary>Child sprite. Presentation may scale and tint it; the collider is on the root.</summary>
        public Transform Visual => _visual;

        public SpriteRenderer Renderer => _renderer;

        void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<CircleCollider2D>();
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(Transform visual, SpriteRenderer renderer)
        {
            _visual = visual;
            _renderer = renderer;
        }
    }
}
