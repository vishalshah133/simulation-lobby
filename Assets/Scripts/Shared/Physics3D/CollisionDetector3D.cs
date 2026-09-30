using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Counts collisions on a 3D body and hands them to the simulation one fixed step at a time.
    /// The 3D counterpart to <see cref="BounceDetector2D"/>, with one deliberate difference: this
    /// counts *distinct collisions* per tick rather than collapsing every tick to at most one. A
    /// ball plowing through a packed field can hit several different blocks within a single fixed
    /// step, and collapsing that to "a collision happened" would silently drop most of the impacts a
    /// format like <c>surv</c>'s Wall vs Ball exists to make satisfying. Contact points *within* one collision are
    /// still deduped (via Unity's own <c>OnCollisionEnter</c> semantics — one call per colliding body).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CollisionDetector3D : MonoBehaviour
    {
        [Tooltip("Total collisions this run. Read-only — shown for debugging in the Inspector.")]
        [SerializeField] int _totalCollisions;

        int _collisionsThisTick;

        /// <summary>Total collisions since the last <see cref="Reset"/>.</summary>
        public int TotalCollisions => _totalCollisions;

        /// <summary>Impact speed of the most recent collision.</summary>
        public float LastImpactSpeed { get; private set; }

        void OnCollisionEnter(Collision collision)
        {
            _collisionsThisTick++;
            _totalCollisions++;
            LastImpactSpeed = collision.relativeVelocity.magnitude;
        }

        /// <summary>
        /// Returns how many distinct collisions happened since the last call, and resets the count to
        /// zero. Call exactly once per simulation tick.
        /// </summary>
        public int ConsumeCollisionCount()
        {
            int count = _collisionsThisTick;
            _collisionsThisTick = 0;
            return count;
        }

        /// <summary>Clear all state. Called from a simulation's Initialize so re-running a seed is clean.</summary>
        public void Reset()
        {
            _totalCollisions = 0;
            _collisionsThisTick = 0;
            LastImpactSpeed = 0f;
        }
    }
}
