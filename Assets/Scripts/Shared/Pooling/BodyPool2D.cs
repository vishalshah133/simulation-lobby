using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A fixed-size pool of identical 2D physics bodies, cloned once from a template. The hard cap is
    /// the point: a format that multiplies things (<c>rom</c>) must never allocate or exceed a body
    /// budget mid-run, or the frame rate -- and with it the footage -- falls apart.
    /// </summary>
    /// <remarks>
    /// Free bodies are handed out in a fixed order (a stack, filled in creation order), so a run that
    /// draws the same bodies in the same order is reproducible. The pool knows nothing about what a
    /// body represents; callers keep their own bookkeeping.
    /// </remarks>
    public sealed class BodyPool2D
    {
        readonly Stack<GameObject> _free;
        readonly GameObject[] _all;

        public int Capacity => _all.Length;

        public int ActiveCount { get; private set; }

        public int FreeCount => _free.Count;

        /// <summary>Clones <paramref name="template"/> <paramref name="capacity"/> times, all inactive.</summary>
        public BodyPool2D(GameObject template, Transform parent, int capacity)
        {
            capacity = Mathf.Max(1, capacity);
            _all = new GameObject[capacity];
            _free = new Stack<GameObject>(capacity);

            for (int i = capacity - 1; i >= 0; i--)
            {
                GameObject body = Object.Instantiate(template, parent);
                body.name = $"{template.name}_{i:000}";
                Deactivate(body);
                _all[i] = body;
                _free.Push(body);
            }
        }

        /// <summary>Next free body, placed and live, or null if the pool is exhausted.</summary>
        public GameObject TryGet(Vector2 position)
        {
            if (_free.Count == 0)
            {
                return null;
            }

            GameObject body = _free.Pop();
            ActiveCount++;

            body.transform.position = position;
            body.transform.rotation = Quaternion.identity;
            body.SetActive(true);

            if (body.TryGetComponent(out Rigidbody2D rigidbody))
            {
                rigidbody.simulated = true;
                rigidbody.position = position;
                rigidbody.rotation = 0f;
                rigidbody.linearVelocity = Vector2.zero;
                rigidbody.angularVelocity = 0f;
            }

            return body;
        }

        public void Release(GameObject body)
        {
            if (body == null || !body.activeSelf)
            {
                return;
            }

            Deactivate(body);
            _free.Push(body);
            ActiveCount--;
        }

        /// <summary>Returns every body to the pool. Called at the start of a run.</summary>
        public void ReleaseAll()
        {
            // Walks the backing array, not the free stack, so the free order is rebuilt identically
            // every time and a restarted run hands out bodies exactly as the first one did.
            _free.Clear();
            ActiveCount = 0;
            for (int i = _all.Length - 1; i >= 0; i--)
            {
                Deactivate(_all[i]);
                _free.Push(_all[i]);
            }
        }

        static void Deactivate(GameObject body)
        {
            if (body.TryGetComponent(out Rigidbody2D rigidbody))
            {
                rigidbody.linearVelocity = Vector2.zero;
                rigidbody.simulated = false;
            }

            body.SetActive(false);
        }
    }
}
