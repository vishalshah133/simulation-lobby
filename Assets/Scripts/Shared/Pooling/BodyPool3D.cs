using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A pool of identical 3D physics bodies, cloned from a template -- the 3D mirror of
    /// <see cref="BodyPool2D"/>. By default it is fixed-size, and that hard cap keeps a multiplying format
    /// inside a body budget. Constructed with <c>growable</c>, it instead clones one more body from the
    /// template whenever it is exhausted, so there is no cap and <see cref="Capacity"/> is the current size.
    /// </summary>
    /// <remarks>
    /// <see cref="TryGet"/> always returns the lowest free index, whatever order bodies were released
    /// in, so a run that releases the same bodies in a different order still draws the same ones next.
    /// A growable pool appends at the next index, so existing indices never change.
    /// The pool knows nothing about what a body represents.
    /// </remarks>
    public sealed class BodyPool3D
    {
        readonly List<GameObject> _all;
        readonly List<bool> _isFree;
        readonly Dictionary<GameObject, int> _indexOf;
        readonly GameObject _template;
        readonly Transform _parent;
        readonly bool _growable;
        int _lowestFree;

        /// <summary>Current number of bodies (fixed unless the pool is growable).</summary>
        public int Capacity => _all.Count;

        public int ActiveCount { get; private set; }

        public int FreeCount => _all.Count - ActiveCount;

        /// <summary>
        /// Clones <paramref name="template"/> <paramref name="capacity"/> times, all inactive. With
        /// <paramref name="growable"/> the pool clones more on demand instead of running dry.
        /// </summary>
        public BodyPool3D(GameObject template, Transform parent, int capacity, bool growable = false)
        {
            capacity = Mathf.Max(1, capacity);
            _template = template;
            _parent = parent;
            _growable = growable;
            _all = new List<GameObject>(capacity);
            _isFree = new List<bool>(capacity);
            _indexOf = new Dictionary<GameObject, int>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                AddBody();
            }
        }

        void AddBody()
        {
            int i = _all.Count;
            GameObject body = Object.Instantiate(_template, _parent);
            body.name = $"{_template.name}_{i:000}";
            Deactivate(body);
            _all.Add(body);
            _isFree.Add(true);
            _indexOf[body] = i;
        }

        /// <summary>
        /// Lowest-index free body, placed and live. When exhausted: null for a fixed pool, a freshly cloned
        /// body at the next index for a growable one.
        /// </summary>
        public GameObject TryGet(Vector3 position)
        {
            int index = _lowestFree;
            while (index < _all.Count && !_isFree[index])
            {
                index++;
            }

            if (index >= _all.Count)
            {
                if (!_growable)
                {
                    _lowestFree = _all.Count;
                    return null;
                }

                index = _all.Count;
                AddBody();
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
            for (int i = 0; i < _all.Count; i++)
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
