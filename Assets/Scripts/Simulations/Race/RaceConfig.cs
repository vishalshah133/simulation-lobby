using System;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.Race
{
    /// <summary>One competitor: a name and a colour.</summary>
    [Serializable]
    public struct RaceContender
    {
        public string name;
        public Color color;

        public RaceContender(string name, Color color)
        {
            this.name = name;
            this.color = color;
        }
    }

    /// <summary>
    /// Tunables for the marble race: the roster, the marble physics, the start and the run-time safety nets.
    /// A published video is this asset plus a scene plus a seed.
    /// </summary>
    [CreateAssetMenu(fileName = "RaceConfig", menuName = "Simulation Lobby/Configs/Race", order = 32)]
    public sealed class RaceConfig : SimulationConfig
    {
        [Header("Track")]
        [Tooltip("The shape of the course. Baked into meshes by the scene builder: edit it, then rebuild the scene.")]
        public RaceTrackLayout track;

        [Header("Roster")]
        public RaceContender[] roster = DefaultRoster();

        [Header("Marbles")]
        [Min(0.03f)] public float marbleRadius = 0.11f;
        [Min(0.01f)] public float marbleMass = 0.5f;
        [Min(0f)] public float linearDamping = 0.02f;
        [Min(0f)] public float angularDamping = 0.04f;

        [Tooltip("Largest speed Unity may use to push two overlapping marbles apart. Keeps the pen from spitting marbles out.")]
        [Range(0.1f, 10f)] public float maxDepenetrationVelocity = 2f;

        [Header("Start")]
        [Min(1)] public int gridColumns = 4;
        [Min(0.2f)] public float gridRowSpacing = 0.32f;

        [Tooltip("Random offset of each marble from its grid slot, as a fraction of the marble radius.")]
        [Range(0f, 0.6f)] public float gridJitter = 0.25f;

        [Tooltip("Seconds the field sits against the closed gate before it opens: the setup beat, and the camera's reveal.")]
        [Min(0f)] public float preReleaseHoldSeconds = 4f;

        [Header("Finish")]
        [Tooltip("After the winner crosses, how long the others get to finish before the race is called.")]
        [Min(1f)] public float finishGraceSeconds = 14f;

        [Header("Safety nets (the run must always end)")]
        [Tooltip("A marble this far from the track centreline has left the track and is put back.")]
        [Min(1f)] public float offTrackDistance = 3f;

        [Tooltip("Below this speed a marble counts as stalled.")]
        [Min(0.001f)] public float stalledSpeed = 0.08f;

        [Tooltip("Stalled for this long, a marble gets a push down the track.")]
        [Min(0.5f)] public float stalledSeconds = 2.5f;

        [Min(0.1f)] public float stalledNudgeSpeed = 1.5f;

        void Reset()
        {
            fixedTimestep = 1f / 120f;
            maxDurationSeconds = 240f;
        }

        public static RaceContender[] DefaultRoster()
        {
            // Jewel tones, picked to stay apart on piano-black at 480px: no two neighbours share a hue.
            return new[]
            {
                new RaceContender("RUBY",     new Color(0.86f, 0.07f, 0.16f)),
                new RaceContender("EMERALD",  new Color(0.05f, 0.62f, 0.30f)),
                new RaceContender("SAPPHIRE", new Color(0.08f, 0.28f, 0.90f)),
                new RaceContender("TOPAZ",    new Color(0.98f, 0.76f, 0.08f)),
                new RaceContender("AMETHYST", new Color(0.58f, 0.22f, 0.86f)),
                new RaceContender("PEARL",    new Color(0.96f, 0.93f, 0.86f)),
                new RaceContender("CORAL",    new Color(1.00f, 0.38f, 0.30f)),
                new RaceContender("AQUA",     new Color(0.10f, 0.80f, 0.82f)),
                new RaceContender("CITRINE",  new Color(0.62f, 0.86f, 0.12f)),
                new RaceContender("ROSE",     new Color(1.00f, 0.42f, 0.70f)),
                new RaceContender("COBALT",   new Color(0.30f, 0.55f, 1.00f)),
                new RaceContender("AMBER",    new Color(1.00f, 0.50f, 0.06f))
            };
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (roster == null || roster.Length < 2)
            {
                error = "A race needs at least two contenders.";
                return false;
            }

            if (gridColumns < 1)
            {
                error = "gridColumns must be at least 1.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
