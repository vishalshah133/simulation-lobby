using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared.Coaster
{
    /// <summary>
    /// One waypoint of a coaster described as a "turtle" path. Each key is a segment of <see cref="length"/> metres that
    /// eases (smoothstep) from the previous key's pitch, turn rate and bank to this key's, so a course is a list of feelings
    /// (climb, crest, drop, sweeping right turn) instead of a list of coordinates.
    /// </summary>
    [Serializable]
    public struct CourseKey
    {
        [Tooltip("Arc length of this segment in metres.")]
        public float length;
        [Tooltip("Climb angle in degrees at the end of the segment (negative = descending).")]
        public float pitchDeg;
        [Tooltip("Turn rate in degrees per metre at the end of the segment (positive = right).")]
        public float yawPerMeter;
        [Tooltip("Bank in degrees at the end of the segment (positive leans right). 360 = one full barrel roll; hold the value to stay rolled.")]
        public float bankDeg;
        [Tooltip("Constant rate across the segment instead of a smoothstep ease. Use it for the body of a vertical loop (a steady pitch rate is a circle); ease the entry and exit with ordinary keys.")]
        public bool linear;
        [Tooltip("Sideways shift in metres per metre travelled (positive = right of the heading). Offsets the exit of a vertical loop from its entry so the track never crosses itself.")]
        public float drift;

        public CourseKey(float length, float pitchDeg, float yawPerMeter, float bankDeg, bool linear = false, float drift = 0f)
        {
            this.length = length;
            this.pitchDeg = pitchDeg;
            this.yawPerMeter = yawPerMeter;
            this.bankDeg = bankDeg;
            this.linear = linear;
            this.drift = drift;
        }
    }

    /// <summary>Turns a list of <see cref="CourseKey"/> into the control points and bank angles <see cref="CoasterRail"/> takes. Pure.</summary>
    public static class RailCourse
    {
        const float IntegrationStep = 1f;

        public static void Build(IList<CourseKey> keys, float pointSpacing, out Vector3[] points, out float[] bankDeg)
        {
            var pts = new List<Vector3>();
            var banks = new List<float>();
            Vector3 pos = Vector3.zero;
            float pitch = 0f, yaw = 0f, yawRate = 0f, bank = 0f, driftRate = 0f;
            float sinceEmit = pointSpacing; // emit the very first point

            for (int k = 0; k < keys.Count; k++)
            {
                CourseKey key = keys[k];
                float p0 = pitch, y0 = yawRate, b0 = bank, d0 = driftRate;
                int steps = Mathf.Max(1, Mathf.CeilToInt(key.length / IntegrationStep));
                float step = key.length / steps;
                for (int i = 0; i < steps; i++)
                {
                    if (sinceEmit >= pointSpacing)
                    {
                        pts.Add(pos);
                        banks.Add(bank);
                        sinceEmit = 0f;
                    }
                    float w = (i + 0.5f) / steps;
                    if (!key.linear) w = w * w * (3f - 2f * w);
                    pitch = Mathf.Lerp(p0, key.pitchDeg, w);
                    yawRate = Mathf.Lerp(y0, key.yawPerMeter, w);
                    bank = Mathf.Lerp(b0, key.bankDeg, w);
                    driftRate = Mathf.Lerp(d0, key.drift, w);
                    yaw += yawRate * step;
                    float pr = pitch * Mathf.Deg2Rad;
                    float yr = yaw * Mathf.Deg2Rad;
                    pos += new Vector3(Mathf.Cos(pr) * Mathf.Sin(yr), Mathf.Sin(pr), Mathf.Cos(pr) * Mathf.Cos(yr)) * step;
                    pos += new Vector3(Mathf.Cos(yr), 0f, -Mathf.Sin(yr)) * (driftRate * step);
                    sinceEmit += step;
                }
            }

            pts.Add(pos);
            banks.Add(bank);
            points = pts.ToArray();
            bankDeg = banks.ToArray();
        }
    }
}
