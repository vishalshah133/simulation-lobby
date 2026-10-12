using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Records which colliders entered a trigger since the simulation last looked. The trigger
    /// counterpart to <see cref="BounceDetector2D"/>: physics callbacks only queue, and the
    /// simulation consumes the queue inside its own fixed tick -- so game logic never runs from a
    /// physics callback.
    /// </summary>
    /// <remarks>
    /// Callback order within one physics step is not something to rely on. Callers that need a
    /// reproducible order must sort what <see cref="Drain"/> returns by their own stable key.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TriggerZone2D : MonoBehaviour
    {
        readonly List<Collider2D> _entered = new List<Collider2D>();

        void OnTriggerEnter2D(Collider2D other)
        {
            _entered.Add(other);
        }

        /// <summary>Moves everything that entered since the last call into <paramref name="into"/>.</summary>
        public void Drain(List<Collider2D> into)
        {
            for (int i = 0; i < _entered.Count; i++)
            {
                if (_entered[i] != null)
                {
                    into.Add(_entered[i]);
                }
            }

            _entered.Clear();
        }

        public void Clear()
        {
            _entered.Clear();
        }
    }
}
