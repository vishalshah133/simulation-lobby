using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// 3D counterpart of <see cref="TriggerZone2D"/>: records which colliders entered a trigger since
    /// the simulation last looked. Physics callbacks only queue; the simulation consumes the queue
    /// inside its own fixed tick.
    /// </summary>
    /// <remarks>
    /// Entries come back in arrival order, with a collider that re-enters inside one buffer window
    /// listed once. Callback order within one physics step is still not something to rely on, so
    /// callers needing a reproducible order sort by their own stable key.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class TriggerZone3D : MonoBehaviour
    {
        readonly List<Collider> _entered = new List<Collider>();

        /// <summary>Entries buffered since the last <see cref="Drain"/>.</summary>
        public int Count => _entered.Count;

        void OnTriggerEnter(Collider other)
        {
            // Linear scan on purpose: buffers are tiny and a set would add hash-order concerns.
            if (!_entered.Contains(other))
            {
                _entered.Add(other);
            }
        }

        /// <summary>Moves everything that entered since the last call into <paramref name="into"/>.</summary>
        public void Drain(List<Collider> into)
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
