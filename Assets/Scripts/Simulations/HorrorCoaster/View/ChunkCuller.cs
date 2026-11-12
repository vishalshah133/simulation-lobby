using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Distance culling for the dense set dressing. The builders group props into chunks along the rail (about 100 m each); at runtime
    /// only the chunks within <see cref="aheadMeters"/> ahead and <see cref="behindMeters"/> behind the cart are active. The rail is
    /// about 3.8 km, so this is what keeps renderer and light counts sane. Reads the cart distance only.
    /// </summary>
    public sealed class ChunkCuller : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public Transform chunk;
            public float d0;
            public float d1;
        }

        public Entry[] entries = new Entry[0];
        public float aheadMeters = 190f;
        public float behindMeters = 55f;

        HorrorCoasterHost host;

        void Update()
        {
            if (host == null)
            {
                host = FindAnyObjectByType<HorrorCoasterHost>();
            }

            if (host == null || !host.Ready || host.State == null)
            {
                return;
            }

            float dist = host.State.Distance;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (e.chunk == null)
                {
                    continue;
                }

                bool on = e.d1 >= dist - behindMeters && e.d0 <= dist + aheadMeters;
                if (e.chunk.gameObject.activeSelf != on)
                {
                    e.chunk.gameObject.SetActive(on);
                }
            }
        }
    }
}
