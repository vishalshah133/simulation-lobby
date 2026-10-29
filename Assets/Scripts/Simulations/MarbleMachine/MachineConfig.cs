using System;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>Where the run is in its arc. Drives the release rate, the camera and the HUD.</summary>
    public enum MachinePhase
    {
        Hold,
        Trickle,
        Steady,
        Flood,
        Finale,
        Won
    }

    [Serializable]
    public struct MachineColour
    {
        public string name;
        public Color color;

        public MachineColour(string name, Color color)
        {
            this.name = name;
            this.color = color;
        }
    }

    /// <summary>One step of the release schedule. Stages run in order; each starts at <see cref="startSeconds"/>.</summary>
    [Serializable]
    public struct ReleaseStage
    {
        public string label;
        [Min(0f)] public float startSeconds;
        [Tooltip("Target release rate from the silo's star feeder. Ignored while flood is on.")]
        [Min(0f)] public float ballsPerSecond;
        [Tooltip("Open the silo's flood gate: everything left pours out at once.")]
        public bool flood;
        public MachinePhase phase;

        public ReleaseStage(string label, float startSeconds, float ballsPerSecond, bool flood, MachinePhase phase)
        {
            this.label = label;
            this.startSeconds = startSeconds;
            this.ballsPerSecond = ballsPerSecond;
            this.flood = flood;
            this.phase = phase;
        }
    }

    /// <summary>
    /// Config for the marble machine format (<c>mach</c>). A video is this asset + the scene + a seed.
    /// Contract file for mach-001: work packages read it, they don't add fields to it.
    /// </summary>
    [CreateAssetMenu(menuName = "Simulation Lobby/Marble Machine Config", fileName = "MachineConfig")]
    public class MachineConfig : SimulationConfig
    {
        [Header("Balls")]
        [Min(10)] public int ballCount = 14000; // 2,000 of each of the 7 colours
        [Min(0.02f)] public float ballRadius = 0.065f;
        [Min(0.001f)] public float ballMass = 0.03f;
        [Range(0f, 1f)] public float ballBounciness = 0.25f;
        [Range(0f, 1f)] public float ballFriction = 0.3f;
        [Range(1, 20)] public int ballSolverIterations = 6;
        [Tooltip("Speed clamp per ball, m/s. Stops a pinched ball being shot through a wall.")]
        [Min(1f)] public float maxBallSpeed = 12f;

        [Header("Colours (one tube each)")]
        public MachineColour[] colours =
        {
            new MachineColour("RED", new Color(0.92f, 0.12f, 0.14f)),
            new MachineColour("ORANGE", new Color(1.00f, 0.50f, 0.05f)),
            new MachineColour("YELLOW", new Color(1.00f, 0.86f, 0.10f)),
            new MachineColour("GREEN", new Color(0.15f, 0.78f, 0.25f)),
            new MachineColour("BLUE", new Color(0.12f, 0.42f, 0.95f)),
            new MachineColour("PURPLE", new Color(0.58f, 0.22f, 0.88f)),
            new MachineColour("PINK", new Color(1.00f, 0.38f, 0.70f)),
        };

        [Header("Contest")]
        [Tooltip("Balls a tube needs to win. This is what sets the video's length.")]
        [Min(1)] public int tubeCapacity = 100;
        [Tooltip("Leader's fill fraction at which the Finale phase starts.")]
        [Range(0.5f, 0.99f)] public float finaleFraction = 0.85f;
        [Tooltip("Seconds the run keeps going after the win, for the payoff and the outro pull-back.")]
        [Min(0f)] public float outroSeconds = 14f;

        [Header("Flow schedule")]
        [Tooltip("Opening hold: the feeder is off and the scene sits still for the hook.")]
        [Min(0f)] public float holdSeconds = 20f;
        public ReleaseStage[] stages =
        {
            new ReleaseStage("Gate open", 20f, 0f, true, MachinePhase.Flood),
        };

        [Header("Geometry")]
        [Tooltip("Inner width of the whole track: every connector chute and every station entry and exit port, metres. Station builders scale their lateral sizes by chuteWidth / 0.9.")]
        [Min(0.3f)] public float chuteWidth = 1.8f;

        [Header("Mechanisms")]
        [Min(0.1f)] public float bucketWheelRpm = 6f;
        public float screwDegreesPerSecond = 330f;
        [Tooltip("Speed the boost pads (arrows on the helix entry and the trapdoor chute exit) push balls up to, m/s.")]
        [Min(0f)] public float boosterSurfaceSpeed = 7f;
        [Min(0f)] public float waveAmplitude = 0.24f;
        [Min(0.2f)] public float waveLength = 3f;
        [Min(0f)] public float waveFrequency = 0.9f;
        [Min(0.5f)] public float trapdoorCycleSeconds = 5f;
        [Min(0f)] public float trapdoorStaggerSeconds = 0.15f;
        [Tooltip("Balls in a trough that make it tip. Troughs are twice as wide and twice as long as the original, so about four times the capacity.")]
        [Min(1)] public int tipperThreshold = 200;
        [Min(0.2f)] public float skiJumpGap = 0.9f;
        public float pegWallSpinnerDegreesPerSecond = 90f;
        [Tooltip("Parallel sorter lanes, side by side over the tube row. Each is four balls wide (the sort is by collision filtering, not single file).")]
        [Min(1)] public int sorterLanes = 2;
        public float singulatorDegreesPerSecond = 270f;
        [Min(0.05f)] public float sorterFlapOpenSeconds = 0.10f;
        [Min(0.05f)] public float cannonIntervalSeconds = 0.2f;
        [Range(20f, 80f)] public float cannonLaunchAngle = 55f;
        [Min(0f)] public float cannonJitter = 0.25f;

        [Header("Safety")]
        public float killPlaneY = -2f;
        [Tooltip("A ball slower than 0.02 m/s for this long, outside the silo and tubes, gets a seeded nudge.")]
        [Min(1f)] public float stallSeconds = 6f;

        public int ColourCount => colours != null ? colours.Length : 0;

        void Reset()
        {
            fixedTimestep = 1f / 100f;
            maxDurationSeconds = 900f;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;
            if (ColourCount < 2)
            {
                error = "Need at least two colours.";
                return false;
            }

            if (tubeCapacity > ballCount / ColourCount)
            {
                error = $"tubeCapacity {tubeCapacity} exceeds balls per colour ({ballCount / ColourCount}); nobody can win.";
                return false;
            }

            if (stages == null || stages.Length == 0)
            {
                error = "stages is empty: the silo would never release.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
