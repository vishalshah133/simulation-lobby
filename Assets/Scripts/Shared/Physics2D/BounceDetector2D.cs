using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Counts collisions on a 2D body and hands them to the simulation one fixed step at a time.
    /// </summary>
    /// <remarks>
    /// Exists to solve two collision-safety problems at once:
    /// <list type="bullet">
    /// <item>One bounce can report several contact points. Counting contacts instead of collisions
    /// doubles an escalation curve, so a step is deduped to at most one per fixed tick.</item>
    /// <item>Scaling a collider inside a collision callback can interpenetrate on that frame. The
    /// simulation drains this in its tick, *after* the bounce has resolved.</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BounceDetector2D : MonoBehaviour
    {
        [Tooltip("Total collisions this run. Read-only — shown for debugging in the Inspector.")]
        [SerializeField] int _totalBounces;

        bool _bouncedThisStep;

        /// <summary>Total collisions since the last <see cref="Reset"/>.</summary>
        public int TotalBounces => _totalBounces;

        /// <summary>
        /// Impact speed of the most recent collision, for audio velocity and near-miss scoring.
        /// </summary>
        public float LastImpactSpeed { get; private set; }

        /// <summary>Contact point of the most recent collision, in world space.</summary>
        public Vector2 LastContactPoint { get; private set; }

        /// <summary>
        /// What the most recent counted collision hit, for formats where the wall itself reacts (a
        /// segment that breaks). Only the first collision in a tick is kept, matching the dedupe.
        /// </summary>
        public Collider2D LastCollider { get; private set; }

        void OnCollisionEnter2D(Collision2D collision)
        {
            // Flag, not increment: several contact points in one collision must still be one bounce.
            if (_bouncedThisStep)
            {
                return;
            }

            _bouncedThisStep = true;
            _totalBounces++;
            LastImpactSpeed = collision.relativeVelocity.magnitude;
            LastCollider = collision.collider;

            if (collision.contactCount > 0)
            {
                LastContactPoint = collision.GetContact(0).point;
            }
        }

        /// <summary>
        /// Returns true once per tick in which a collision happened, and clears the flag. Call exactly
        /// once per simulation tick — calling it twice loses a bounce, and never calling it makes
        /// bounces pile into one.
        /// </summary>
        public bool ConsumeBounce()
        {
            if (!_bouncedThisStep)
            {
                return false;
            }

            _bouncedThisStep = false;
            return true;
        }

        /// <summary>Clear all state. Called from a simulation's Initialize so re-running a seed is clean.</summary>
        public void Reset()
        {
            _totalBounces = 0;
            _bouncedThisStep = false;
            LastImpactSpeed = 0f;
            LastContactPoint = Vector2.zero;
            LastCollider = null;
        }
    }
}
