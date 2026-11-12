using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Data for <c>coast</c> (Haunted Coaster): the rail, the acts along it, the scare schedule and the ride physics.
    /// A published video = scene + this asset + seed. Use the context menu "Apply storyboard defaults" to fill it
    /// with the coast-001 storyboard.
    /// </summary>
    [CreateAssetMenu(fileName = "HorrorCoasterConfig", menuName = "Simulation Lobby/Horror Coaster Config")]
    public sealed class HorrorCoasterConfig : SimulationConfig
    {
        public HorrorCoasterConfig()
        {
            // The base class says 120 s; a 7 minute ride needs headroom.
            maxDurationSeconds = 600f;
        }

        [Header("Riders")]
        [Tooltip("Counter at the start: five riders ahead plus the viewer.")]
        [Range(2, 12)] public int ridersAtStart = 6;

        [Header("Schedule (distances in rail arc-length metres)")]
        [Tooltip("Scare events along the rail. Sorted by distance at run start.")]
        public ScareEvent[] events;

        [Tooltip("Where each act starts. Ordered by ride order; an act ends where the next begins.")]
        public ActSpan[] acts;

        [Header("Rail (control points, centripetal Catmull-Rom)")]
        public Vector3[] railPoints;

        [Tooltip("Bank per control point in degrees; positive leans the cart toward the right (inside of a right turn). " +
                 "Same length as railPoints. Interpolated with a smoothstep between points.")]
        public float[] railBankDeg;

        [Tooltip("Per control point 0..1. 0 = pure parallel transport (loops and corkscrews stay flip-free), 1 = the " +
                 "accumulated twist is unwound toward the level horizon so the cart is upright again. Optional: " +
                 "empty means 0 everywhere.")]
        public float[] railLevelWeight;

        [Header("Zones (arc-length metres)")]
        [Tooltip("Chain-lift zones: constant liftSpeed.")]
        public float[] liftStarts;
        public float[] liftEnds;

        [Tooltip("Brake / drive zones: speed is driven toward the matching target (braking is quick, speeding up is gentle). " +
                 "Used for the cruise zones (9-15 m/s) and the station approach.")]
        public float[] brakeStarts;
        public float[] brakeEnds;
        public float[] brakeSpeeds;

        [Header("Ride physics")]
        [Min(0.1f)] public float liftSpeed = 6f;
        [Min(1f)] public float maxSpeed = 30f;
        [Min(0.1f)] public float gravity = 9.81f;
        [Tooltip("Quadratic drag: deceleration = drag * v^2 (1/m).")]
        [Min(0f)] public float drag = 0.0008f;
        [Tooltip("Rolling friction coefficient: deceleration = rollingFriction * g * cos(slope).")]
        [Min(0f)] public float rollingFriction = 0.006f;
        [Tooltip("The cart never drops below this on a free-running stretch, so it can never stall on a hill.")]
        [Min(0.05f)] public float minCrawlSpeed = 1.5f;
        [Tooltip("Deceleration (m/s^2) inside a brake zone.")]
        [Min(0.5f)] public float brakeDecel = 5f;
        [Tooltip("Acceleration (m/s^2) toward a brake/drive zone's target when it is above the current speed.")]
        [Min(0.1f)] public float driveAccel = 4f;
        [Tooltip("Acceleration (m/s^2) toward liftSpeed when entering or leaving a lift zone.")]
        [Min(0.1f)] public float liftAccel = 3f;
        [Tooltip("The cart sits still in the station for this long before the ride starts.")]
        [Min(0f)] public float stationDwellSeconds = 6f;
        [Tooltip("After the cart stops at the station the run stays alive this long (operator line, end card).")]
        [Min(0f)] public float endHoldSeconds = 8f;

        [Header("Scare timing")]
        [Tooltip("How long the Sting phase lasts (seconds of sim time).")]
        [Min(0.1f)] public float stingSeconds = 0.6f;
        [Tooltip("How long the Aftermath phase lasts.")]
        [Min(0.1f)] public float aftermathSeconds = 2f;
        [Tooltip("Fear01 starts rising this many seconds before a BIG scare.")]
        [Min(1f)] public float fearRiseSeconds = 12f;
        [Tooltip("Fear01 rises this many seconds before a MICRO jump scare (short, so it feels sudden).")]
        [Min(0.5f)] public float microFearRiseSeconds = 3f;

        [Header("Scare ease (the only deliberate slow-down)")]
        [Tooltip("Before the sting of a BIG scare (rider-taking or named) the cart eases over this many seconds.")]
        [Min(0.1f)] public float easeSeconds = 1.5f;
        [Tooltip("The ease never slows the cart below this (m/s).")]
        [Min(0.5f)] public float easeMinSpeed = 5f;
        [Tooltip("The ease aims for this fraction of the entry speed, floored at easeMinSpeed.")]
        [Range(0.3f, 1f)] public float easeFraction = 0.6f;

        [Header("Feel")]
        [Tooltip("Vertical wobble amplitude in metres. Roll is 30 deg per metre, lateral half of it, pitch a third of roll.")]
        [Range(0f, 0.3f)] public float wobbleAmplitude = 0.04f;

        [Header("Targets")]
        [Tooltip("Planned ride length. The timing test compares against it.")]
        public float totalTargetSeconds = 420f;
        [Tooltip("Draw a fresh seed on every Play (house default). The host logs it.")]
        public bool randomizeSeedOnStart = true;

        /// <summary>Arc-length metres where the act starts; float.MaxValue if the act is not in <see cref="acts"/>.</summary>
        public float ActStart(ActId act)
        {
            if (acts == null) return float.MaxValue;
            for (int i = 0; i < acts.Length; i++)
            {
                if (acts[i].act == act) return acts[i].startDistance;
            }
            return float.MaxValue;
        }

        /// <summary>
        /// Arc-length metres where the act ends (the next act's start). The last act returns float.MaxValue, because the
        /// config does not know the rail length.
        /// </summary>
        public float ActEnd(ActId act)
        {
            if (acts == null) return float.MaxValue;
            for (int i = 0; i < acts.Length; i++)
            {
                if (acts[i].act != act) continue;
                return i + 1 < acts.Length ? acts[i + 1].startDistance : float.MaxValue;
            }
            return float.MaxValue;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;

            if (railPoints == null || railPoints.Length < 4)
            {
                error = "railPoints needs at least 4 control points. Use the context menu 'Apply storyboard defaults'.";
                return false;
            }
            if (railBankDeg != null && railBankDeg.Length != 0 && railBankDeg.Length != railPoints.Length)
            {
                error = "railBankDeg must be empty or the same length as railPoints.";
                return false;
            }
            if (railLevelWeight != null && railLevelWeight.Length != 0 && railLevelWeight.Length != railPoints.Length)
            {
                error = "railLevelWeight must be empty or the same length as railPoints.";
                return false;
            }
            if (acts == null || acts.Length == 0)
            {
                error = "acts is empty.";
                return false;
            }
            if (!ZonesValid(liftStarts, liftEnds, null, out error)) return false;
            if (!ZonesValid(brakeStarts, brakeEnds, brakeSpeeds, out error)) return false;

            error = null;
            return true;
        }

        static bool ZonesValid(float[] starts, float[] ends, float[] speeds, out string error)
        {
            int a = starts != null ? starts.Length : 0;
            int b = ends != null ? ends.Length : 0;
            if (a != b || (speeds != null && speeds.Length != a))
            {
                error = "Zone arrays (starts / ends / speeds) must have equal length.";
                return false;
            }
            error = null;
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Apply storyboard defaults")]
        void ApplyStoryboardDefaults()
        {
            HorrorCoasterDefaults.Apply(this);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
