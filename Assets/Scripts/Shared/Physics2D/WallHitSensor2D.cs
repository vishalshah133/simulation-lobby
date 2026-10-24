using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Flags a collision between this body and a child collider of a named "wall" transform (e.g. the
    /// segments of a <see cref="CircularBoundary2D"/>), remembering where it touched. The simulation
    /// drains the flag once per tick.
    /// </summary>
    /// <remarks>
    /// Its own flag rather than <see cref="BounceDetector2D"/>: that keeps only the first collision of a
    /// step, so in a crowd a ball-ball contact would hide the wall hit that should have made a clone.
    /// Format-agnostic: it knows nothing about what a wall hit means. Pure state; the simulation reads it.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class WallHitSensor2D : MonoBehaviour
    {
        bool _hit;
        Vector2 _point;

        /// <summary>The wall's root transform. Set by the simulation when the body is handed out.</summary>
        public Transform Wall { get; set; }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (Wall == null || _hit || collision.collider.transform.parent != Wall)
            {
                return;
            }

            _hit = true;
            _point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        }

        /// <summary>
        /// True once per step in which this body touched the wall; <paramref name="worldPoint"/> is the
        /// first contact point. Call once per tick.
        /// </summary>
        public bool Consume(out Vector2 worldPoint)
        {
            bool hit = _hit;
            worldPoint = _point;
            _hit = false;
            return hit;
        }

        /// <summary>Drops any pending hit, e.g. when a pooled body is reused.</summary>
        public void Clear()
        {
            _hit = false;
        }
    }
}
