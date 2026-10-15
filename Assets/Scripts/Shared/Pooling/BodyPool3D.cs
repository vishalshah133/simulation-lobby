using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A fixed-size pool of identical 3D physics bodies, cloned once from a template -- the 3D mirror
    /// of <see cref="BodyPool2D"/>. The hard cap keeps a multiplying format inside a body budget.
    /// </summary>
    /// <remarks>
    /// <see cref="TryGet"/> always returns the lowest free index, whatever order bodies were released
    /// in, so a run that releases the same bodies in a different order still draws the same ones next.
    /// The pool knows nothing about what a body represents.
    /// </remarks>
    public sealed class BodyPool3D
    {
        readonly GameObject[] _all;
        readonly bool[] _isFree;
        readonly Dictionary<GameObject, int> _indexOf;
        int _lowestFree;

        public int Capacity => _all.Length;

        public int ActiveCount { get; private set; }

        public int FreeCount => _all.Length - ActiveCount;

        /// <summary>Clones <paramref name="template"/> <paramref name="capacity"/> times, all inactive.</summary>
        public BodyPool3D(GameObject template, Transform parent, int capacity)
        {
            capacity = Mathf.Max(1, capacity);
            _all = new GameObject[capacity];
            _isFree = new bool[capacity];
            _indexOf = new Dictionary<GameObject, int>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                GameObject body = Object.Instantiate(template, parent);
                body.name = $"{template.name}_{i:000}";
                Deactivate(body);
                _all[i] = body;
                _isFree[i] = true;
                _indexOf[body] = i;
            }
        }

        /// <summary>Lowest-index free body, placed and live, or null if the pool is exhausted.</summary>
        public GameObject TryGet(Vector3 position)
        {
            int index = _lowestFree;
            while (index < _all.Length && !_isFree[index])
            {
                index++;
            }

            if (index >= _all.Length)
            {
                _lowestFree = _all.Length;
                return null;
            }

            _isFree[index] = false;
            _lowestFree = index + 1;
            ActiveCount++;

            GameObject body = _all[index];
            body.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.SetActive(true);

            if (body.TryGetComponent(out Rigidbody rigidbody))
            {
                rigidbody.position = position;
                rigidbody.rotation = Quaternion.identity;
                if (!rigidbody.isKinematic)
                {
                    rigidbody.linearVelocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }
            }

            return body;
        }

        public void Release(GameObject body)
        {
            if (body == null || !_indexOf.TryGetValue(body, out int index) || _isFree[index])
            {
                return;
            }

            Deactivate(body);
            _isFree[index] = true;
            ActiveCount--;
            if (index < _lowestFree)
            {
                _lowestFree = index;
            }
        }

        /// <summary>Returns every body to the pool. Called at the start of a run.</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _all.Length; i++)
            {
                Deactivate(_all[i]);
                _isFree[i] = true;
            }

            ActiveCount = 0;
            _lowestFree = 0;
        }

        /// <summary>Stable 0..Capacity-1 index of a pooled body, or -1 if it isn't one of ours.</summary>
        public int IndexOf(GameObject body)
        {
            return body != null && _indexOf.TryGetValue(body, out int index) ? index : -1;
        }

        static void Deactivate(GameObject body)
        {
            if (body.TryGetComponent(out Rigidbody rigidbody) && !rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            body.SetActive(false);
        }
    }
}
