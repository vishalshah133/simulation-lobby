using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// The centreline of a track as a polyline in world space, plus cumulative distance. Gives any
    /// format a way to ask "how far along is this body?" without knowing how the track was built.
    /// </summary>
    /// <remarks>
    /// Static data, written once by a scene builder and only read at runtime. Points are world-space,
    /// so the owning object must never move.
    /// </remarks>
    public sealed class TrackPath : MonoBehaviour
    {
        [SerializeField] Vector3[] _points = new Vector3[0];
        [SerializeField] float[] _cumulative = new float[0];

        public int Count => _points.Length;
        public float TotalLength => _cumulative.Length > 0 ? _cumulative[_cumulative.Length - 1] : 0f;

        public void Set(Vector3[] points)
        {
            _points = points;
            _cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
            {
                _cumulative[i] = _cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
        }

        public Vector3 PointAt(int index) => _points[Mathf.Clamp(index, 0, _points.Length - 1)];

        public float DistanceAt(int index) => _cumulative[Mathf.Clamp(index, 0, _cumulative.Length - 1)];

        public Vector3 TangentAt(int index)
        {
            int a = Mathf.Clamp(index, 0, _points.Length - 2);
            return (_points[a + 1] - _points[a]).normalized;
        }

        /// <summary>Index of the first point at or beyond <paramref name="distance"/> metres along the path.</summary>
        public int IndexAtDistance(float distance)
        {
            int lo = 0;
            int hi = _cumulative.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_cumulative[mid] < distance)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        public Vector3 PositionAtDistance(float distance)
        {
            int i = IndexAtDistance(distance);
            if (i <= 0)
            {
                return _points[0];
            }

            float span = _cumulative[i] - _cumulative[i - 1];
            float t = span > 1e-5f ? (distance - _cumulative[i - 1]) / span : 0f;
            return Vector3.Lerp(_points[i - 1], _points[i], Mathf.Clamp01(t));
        }

        /// <summary>
        /// Finds the nearest path point to <paramref name="position"/>, searching only a window around
        /// <paramref name="hint"/>. The window is what keeps a helix, where the path passes under itself,
        /// from snapping a body onto the wrong loop.
        /// </summary>
        public int Nearest(Vector3 position, int hint, int windowBack, int windowForward, out float sqrDistance)
        {
            int start = Mathf.Max(0, hint - windowBack);
            int end = Mathf.Min(_points.Length - 1, hint + windowForward);
            int best = Mathf.Clamp(hint, 0, _points.Length - 1);
            float bestSqr = float.MaxValue;
            for (int i = start; i <= end; i++)
            {
                float sqr = (_points[i] - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            sqrDistance = bestSqr;
            return best;
        }

        /// <summary>Distance along the path of <paramref name="position"/>, projected onto the segment after <paramref name="index"/>.</summary>
        public float ProgressAt(Vector3 position, int index)
        {
            if (_points.Length < 2)
            {
                return 0f;
            }

            int a = Mathf.Clamp(index, 0, _points.Length - 2);
            Vector3 segment = _points[a + 1] - _points[a];
            float length = segment.magnitude;
            if (length < 1e-5f)
            {
                return _cumulative[a];
            }

            float along = Vector3.Dot(position - _points[a], segment / length);
            return _cumulative[a] + Mathf.Clamp(along, -length, length);
        }
    }
}
