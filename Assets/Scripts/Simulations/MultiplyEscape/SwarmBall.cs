using UnityEngine;

namespace SimulationLobby.Simulations.MultiplyEscape
{
    /// <summary>
    /// One ball in the swarm. Flags a hit on the arena wall (not on another ball) for the simulation to
    /// drain in its tick. Pure state: the simulation writes it, presentation reads it.
    /// </summary>
    /// <remarks>
    /// Its own flag rather than <c>BounceDetector2D</c>: that keeps only the first collision of a step,
    /// so in a crowd a ball-ball contact would hide the wall hit that should have made a clone.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class SwarmBall : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _renderer;

        Rigidbody2D _body;

        public Rigidbody2D Body => _body != null ? _body : _body = GetComponent<Rigidbody2D>();

        /// <summary>Birth order within the round, 0 for the first ball. Stable, so colours replay.</summary>
        public int Serial { get; internal set; }

        /// <summary>Inside the exit channel, past the inner wall, heading out.</summary>
        public bool Exiting { get; internal set; }

        /// <summary>Left the circle. Still flying, no longer counted or cloned.</summary>
        public bool Escaped { get; internal set; }

        /// <summary>Serial the presentation last coloured this body for; presentation-owned, never read by the sim.</summary>
        public int StyledSerial { get; set; } = -1;

        public SpriteRenderer Renderer => _renderer;

        internal Transform Wall { get; set; }

        bool _wallHit;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (Wall != null && collision.collider.transform.parent == Wall)
            {
                _wallHit = true;
            }
        }

        /// <summary>True once per step in which this ball hit the wall. Call once per tick.</summary>
        internal bool ConsumeWallHit()
        {
            bool hit = _wallHit;
            _wallHit = false;
            return hit;
        }

        internal void ResetState(int serial)
        {
            Serial = serial;
            Exiting = false;
            Escaped = false;
            _wallHit = false;
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(SpriteRenderer renderer)
        {
            _renderer = renderer;
        }
    }
}
