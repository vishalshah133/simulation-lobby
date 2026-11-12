using SimulationLobby.Core;
using SimulationLobby.Simulations.PerfectFit.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.PerfectFit
{
    /// <summary>One round of the <c>fit</c> format: which shape, how tight the hole is, how fast the wall spins.</summary>
    [System.Serializable]
    public struct PerfectFitRound
    {
        public ProfileKind kind;

        [Tooltip("Shown in the question: WILL THE {name} FIT?")]
        public string shapeName;

        [Tooltip("Air between the shape and the hole, in millimetres, the same all the way round. The shape is 0.55 m in radius.")]
        [Min(0.05f)] public float gapMillimetres;

        [Tooltip("Wall spin in degrees per second. The shape spins at exactly this rate once it has locked on.")]
        [Min(5f)] public float spinDegreesPerSecond;

        [Tooltip("Caption once it is through.")]
        public string result;
    }

    /// <summary>
    /// Config for the <c>fit</c> format (Perfect Fit): a spinning wall with a hole cut to a shape, the shape flying in
    /// out of step and locking onto the wall's spin just before it arrives. A published video = this asset + a seed
    /// (the seed only picks each round's spin direction, start offset and tumble).
    /// </summary>
    [CreateAssetMenu(fileName = "PerfectFitConfig", menuName = "Simulation Lobby/Perfect Fit Config")]
    public sealed class PerfectFitConfig : SimulationConfig
    {
        [Header("Rounds")]
        public PerfectFitRound[] rounds = DefaultRounds();

        [Header("Geometry (metres)")]
        [Min(0.1f)] public float shapeRadius = 0.55f;
        [Min(0.05f)] public float shapeDepth = 0.5f;
        [Min(0.5f)] public float wallRadius = 1.25f;
        [Min(0.05f)] public float wallThickness = 0.3f;

        [Tooltip("Shape centre's start distance in front of the wall's front face.")]
        [Min(0.5f)] public float startDistance = 4.2f;

        [Tooltip("Shape centre's resting distance behind the wall's back face. Must clear the wall: shapeDepth / 2 or more.")]
        [Min(0.3f)] public float exitDistance = 1.0f;

        [Header("Misalignment (decays to zero by the wall)")]
        [Range(20f, 170f)] public float startOffsetMinDegrees = 50f;
        [Range(20f, 170f)] public float startOffsetMaxDegrees = 150f;
        [Range(0f, 45f)] public float tiltMaxDegrees = 28f;
        [Range(0f, 10f)] public float wobbleDegrees = 4f;

        [Tooltip("The shape is fully aligned this fraction of the flight before it touches the wall plane.")]
        [Range(0.01f, 0.2f)] public float alignLeadFraction = 0.05f;

        [Header("Pacing (seconds)")]
        [Tooltip("Static beat before round 1: the hook.")]
        [Min(0f)] public float firstHoldSeconds = 1.8f;

        [Tooltip("Hold before a later round's flight; includes the slide-in.")]
        [Min(0.5f)] public float holdSeconds = 1.7f;

        [Tooltip("Flight from the start distance to rest. Decelerates the whole way, so the pass through the wall is the slow part.")]
        [Min(1f)] public float flightSeconds = 5.2f;

        [Tooltip("Result line up, shape still turning in step with the wall.")]
        [Min(0f)] public float resultSeconds = 1.5f;

        [Tooltip("Next wall slides in while the old one slides out. Must be shorter than holdSeconds.")]
        [Min(0.2f)] public float slideSeconds = 0.9f;

        [Min(0f)] public float endCardSeconds = 4.5f;

        [Tooltip("How far the walls slide sideways, metres. Wide enough to clear the frame.")]
        [Min(1f)] public float slideDistance = 3.4f;

        [Header("Camera (looks down +Z at the wall; positions in metres, wall at the origin)")]
        public Vector3 widePosition = new Vector3(0f, 0.1f, -10.3f);
        public Vector3 wideTarget = new Vector3(0f, 0.55f, 0f);
        public Vector3 closePosition = new Vector3(0.28f, 0.3f, -5.2f);
        public Vector3 closeTarget = new Vector3(0f, 0.1f, 0.2f);
        [Range(15f, 50f)] public float fieldOfView = 28f;

        [Header("Text")]
        public string title = "PERFECT FIT";
        public string endCard = "{n} SHAPES. ZERO TOUCHES.";
        public string endLine = "SAME SHAPE, SAME SPIN: THE GAP NEVER CLOSES.";

        public int RoundCount => rounds != null ? rounds.Length : 0;

        public float HoldFor(int round)
        {
            return round == 0 ? firstHoldSeconds : holdSeconds;
        }

        /// <summary>Hold + flight + result for one round.</summary>
        public float RoundSeconds(int round)
        {
            return HoldFor(round) + flightSeconds + resultSeconds;
        }

        public float SeriesSeconds
        {
            get
            {
                float total = endCardSeconds;
                for (int i = 0; i < RoundCount; i++)
                {
                    total += RoundSeconds(i);
                }

                return total;
            }
        }

        /// <summary>Length is structural, so the cap is the series length plus a margin.</summary>
        public override int MaxTicks => Mathf.Max(1, Mathf.CeilToInt((SeriesSeconds + 5f) / fixedTimestep));

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (RoundCount <= 0)
            {
                error = "A fit video needs at least one round.";
                return false;
            }

            if (exitDistance < shapeDepth * 0.5f)
            {
                error = "exitDistance must be at least shapeDepth / 2 so the shape ends fully through the wall.";
                return false;
            }

            if (slideSeconds >= holdSeconds)
            {
                error = "slideSeconds must be shorter than holdSeconds, or the flight starts before the wall has landed.";
                return false;
            }

            if (startOffsetMinDegrees > startOffsetMaxDegrees)
            {
                error = "startOffsetMinDegrees is above startOffsetMaxDegrees.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Seven rounds, the gap shrinking from 30 mm to 0.5 mm while the spin climbs from 40 to 200 degrees per second.
        /// About 66 s of footage at the default pacing.
        /// </summary>
        public static PerfectFitRound[] DefaultRounds()
        {
            return new[]
            {
                Round(ProfileKind.Triangle, "TRIANGLE", 30f, 40f, "FITS."),
                Round(ProfileKind.Cross, "CROSS", 15f, 60f, "STILL FITS."),
                Round(ProfileKind.Star5, "STAR", 8f, 85f, "NOT A TOUCH."),
                Round(ProfileKind.Gear, "GEAR", 4f, 110f, "CLEAN."),
                Round(ProfileKind.Flower, "FLOWER", 2f, 140f, "STILL CLEAN."),
                Round(ProfileKind.Shuriken, "NINJA STAR", 1f, 170f, "ONE MILLIMETRE."),
                Round(ProfileKind.Star12, "SPIKE STAR", 0.5f, 210f, "HALF A MILLIMETRE.")
            };
        }

        static PerfectFitRound Round(ProfileKind kind, string name, float gap, float spin, string result)
        {
            return new PerfectFitRound
            {
                kind = kind,
                shapeName = name,
                gapMillimetres = gap,
                spinDegreesPerSecond = spin,
                result = result
            };
        }
    }
}
