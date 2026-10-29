using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The course as a closed polyline through every station's entry and exit, in flow order. Pure geometry: it turns a world
    /// position into "how far round the lap" (0..1 by arc length) and "which station is this". Segment 2i is station i
    /// (entry to exit); segment 2i+1 is the connector from station i's exit to station i+1's entry (the last connector
    /// closes the loop back to station 0). Used by <see cref="MachineRaceSimulation"/>; reads transforms only.
    /// </summary>
    public sealed class RaceProgress
    {
        readonly Vector3[] _a;
        readonly Vector3[] _b;
        readonly float[] _len;
        readonly float[] _cum;
        readonly string[] _names;
        readonly int _stations;
        readonly float _total;

        public int SegmentCount => _a.Length;
        public int StationCount => _stations;
        public float TotalLength => _total;

        /// <summary>Start point of segment <paramref name="segment"/> (0..SegmentCount-1); the course polyline's vertices, for the race camera.</summary>
        public Vector3 Vertex(int segment) => _a[segment];

        /// <summary>First segment index of the last station: a racer here or beyond has cleared the course and may cross the lap line.</summary>
        public int LastStationSegment => 2 * (_stations - 1);

        public RaceProgress(IReadOnlyList<MachineStation> stations)
        {
            _stations = stations.Count;
            int n = _stations;
            _a = new Vector3[2 * n];
            _b = new Vector3[2 * n];
            _len = new float[2 * n];
            _cum = new float[2 * n];
            _names = new string[n];

            var entry = new Vector3[n];
            var exit = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                MachineStation s = stations[i];
                _names[i] = s != null && !string.IsNullOrEmpty(s.displayName) ? s.displayName : (s != null ? s.name : "");
                Vector3 centre = s != null ? s.activityBounds.center : Vector3.zero;
                entry[i] = s != null && s.entry != null ? s.entry.position : (s != null ? s.transform.position : centre);
                exit[i] = s != null && s.exit != null ? s.exit.position : entry[i];
            }

            float run = 0f;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                _a[2 * i] = entry[i];
                _b[2 * i] = exit[i];
                _a[2 * i + 1] = exit[i];
                _b[2 * i + 1] = entry[next];
            }

            for (int s = 0; s < _a.Length; s++)
            {
                _len[s] = Vector3.Distance(_a[s], _b[s]);
                _cum[s] = run;
                run += _len[s];
            }

            _total = Mathf.Max(0.01f, run);
        }

        /// <summary>Projects <paramref name="p"/> onto one segment: parameter t in 0..1, distance, and lap fraction 0..1.</summary>
        public float Project(Vector3 p, int segment, out float t, out float lapFraction)
        {
            Vector3 d = _b[segment] - _a[segment];
            float l2 = d.sqrMagnitude;
            t = l2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - _a[segment], d) / l2) : 0f;
            Vector3 q = _a[segment] + d * t;
            lapFraction = (_cum[segment] + _len[segment] * t) / _total;
            return Vector3.Distance(p, q);
        }

        /// <summary>
        /// Best segment for a position. With <paramref name="window"/> true only the segments from one behind to
        /// <paramref name="ahead"/> ahead of <paramref name="current"/> (cyclic) are considered, so overlapping parts of the
        /// course (a lift beside a chute) cannot steal a racer; otherwise every segment is.
        /// </summary>
        public int Locate(Vector3 p, int current, bool window, int ahead, out float distance, out float t, out float lapFraction)
        {
            int count = _a.Length;
            int best = -1;
            float bestD = float.MaxValue;
            t = 0f;
            lapFraction = 0f;
            int lo = window ? -1 : 0;
            int hi = window ? ahead : count - 1;
            for (int k = lo; k <= hi; k++)
            {
                int s = window ? ((current + k) % count + count) % count : k;
                float d = Project(p, s, out float st, out float sf);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                    t = st;
                    lapFraction = sf;
                }
            }

            distance = bestD;
            return best;
        }

        /// <summary>Station index a position on <paramref name="segment"/> at parameter <paramref name="t"/> belongs to (connectors go to the nearer end).</summary>
        public int StationOfSegment(int segment, float t)
        {
            if ((segment & 1) == 0) return segment >> 1;
            return t < 0.5f ? segment >> 1 : ((segment >> 1) + 1) % _stations;
        }

        public string StationName(int station) => station >= 0 && station < _names.Length ? _names[station] : "";
    }
}
