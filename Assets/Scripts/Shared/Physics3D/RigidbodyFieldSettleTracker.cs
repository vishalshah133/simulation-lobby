using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Tracks whether every <see cref="Rigidbody"/> in a field has settled — velocity below a
    /// threshold for a run of consecutive ticks. The "everything stopped moving" completion
    /// condition, generalized so any format needing it (the surv wall, a wars endgame, a future impact scatter)
    /// doesn't reimplement the same threshold-and-hysteresis logic.
    /// </summary>
    /// <remarks>
    /// Simulation state, not presentation — a format calls <see cref="Tick"/> once per fixed step,
    /// same discipline as everything else under <c>Core</c>/<c>Shared</c>. Requiring consecutive
    /// ticks (not just "below threshold this frame") avoids false-positive settles on the single
    /// frame a fast-moving body happens to cross zero velocity mid-collision.
    /// </remarks>
    public sealed class RigidbodyFieldSettleTracker
    {
        readonly IReadOnlyList<Rigidbody> _bodies;
        readonly float _velocitySqrThreshold;
        readonly int _ticksRequired;

        int _consecutiveSettledTicks;

        public RigidbodyFieldSettleTracker(IReadOnlyList<Rigidbody> bodies, float velocityThreshold, int ticksRequired)
        {
            _bodies = bodies;
            _velocitySqrThreshold = velocityThreshold * velocityThreshold;
            _ticksRequired = Mathf.Max(1, ticksRequired);
        }

        /// <summary>True once every tracked body has been under threshold for the required run of ticks.</summary>
        public bool IsSettled { get; private set; }

        /// <summary>Call once per fixed step. Idempotent after <see cref="IsSettled"/> goes true.</summary>
        public void Tick()
        {
            if (IsSettled)
            {
                return;
            }

            bool allBelowThreshold = true;
            for (int i = 0; i < _bodies.Count; i++)
            {
                Rigidbody body = _bodies[i];
                if (body == null)
                {
                    continue;
                }

                if (body.linearVelocity.sqrMagnitude > _velocitySqrThreshold)
                {
                    allBelowThreshold = false;
                    break;
                }
            }

            _consecutiveSettledTicks = allBelowThreshold ? _consecutiveSettledTicks + 1 : 0;
            IsSettled = _consecutiveSettledTicks >= _ticksRequired;
        }
    }
}
