using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.HauntedPumpkin
{
    /// <summary>
    /// One ghost in the swarm. Remembers which ring segments it touched this step so the simulation can
    /// drain them in its tick. Pure state: the simulation writes it, presentation reads it.
    /// </summary>
    /// <remarks>
    /// Its own component rather than <c>WallHitSensor2D</c>: that one matches a single wall root, but the
    /// pumpkin has several rings, and it keeps a single hit per step, which in a crowd would let a ghost-ghost
    /// contact hide the wall hit that should have broken a segment. Ring segments are told apart from other
    /// ghosts by their kinematic rigidbody (the ring root), which is cheaper than a component lookup per contact.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class GhostBody : MonoBehaviour
    {
        const int MaxHitsPerStep = 4;

        [SerializeField] Transform _visual;
        [SerializeField] SpriteRenderer _renderer;

        Rigidbody2D _body;
        readonly Collider2D[] _hits = new Collider2D[MaxHitsPerStep];
        int _hitCount;

        public Rigidbody2D Body => _body != null ? _body : _body = GetComponent<Rigidbody2D>();

        /// <summary>Birth order within the run, 0 for the first ghost.</summary>
        public int Serial { get; internal set; }

        /// <summary>How many ghosts this body stands for. Above 1 only once the body cap binds.</summary>
        public int Weight { get; internal set; } = 1;

        /// <summary>Rings this ghost is currently outside of (0 = inside the innermost ring).</summary>
        public int Zone { get; internal set; }

        public Transform Visual => _visual;

        public SpriteRenderer Renderer => _renderer;

        /// <summary>Weight the presentation last sized this body for; presentation-owned, never read by the sim.</summary>
        public int StyledWeight { get; set; } = -1;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_hitCount >= MaxHitsPerStep)
            {
                return;
            }

            Rigidbody2D other = collision.collider.attachedRigidbody;
            if (other == null || other.bodyType != RigidbodyType2D.Kinematic)
            {
                return;
            }

            _hits[_hitCount++] = collision.collider;
        }

        /// <summary>Copies this step's ring-segment contacts out and clears them. Call once per tick.</summary>
        internal void DrainHits(List<Collider2D> into)
        {
            for (int i = 0; i < _hitCount; i++)
            {
                into.Add(_hits[i]);
                _hits[i] = null;
            }

            _hitCount = 0;
        }

        internal void ResetState(int serial, int zone)
        {
            Serial = serial;
            Weight = 1;
            Zone = zone;
            StyledWeight = -1;
            for (int i = 0; i < _hitCount; i++)
            {
                _hits[i] = null;
            }

            _hitCount = 0;
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(Transform visual, SpriteRenderer renderer)
        {
            _visual = visual;
            _renderer = renderer;
        }
    }
}
