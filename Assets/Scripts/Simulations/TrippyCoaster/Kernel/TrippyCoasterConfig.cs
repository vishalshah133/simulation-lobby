using System;
using SimulationLobby.Core;
using SimulationLobby.Shared.Coaster;
using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>Cross-section of the wall mesh wrapped around the rail in a world. None = open space (props only).</summary>
    public enum TripWallShape { None, Circle, Star, Slab, Ripple, Funnel }

    /// <summary>Which procedural pattern the world's wall shader paints. Values match the shader's _Mode.</summary>
    public enum TripPattern { Plasma = 0, Kaleido = 1, Stripes = 2, Grid = 3, Glow = 4 }

    /// <summary>What floats around the rail in a world.</summary>
    public enum TripPropKind { GateRings, UvTubes, Crystals, Spheres, Orbs, Fractals, Vortex }

    /// <summary>One "world" the cart passes through. A world is a look: wall shape, pattern, palette, props, music key.</summary>
    [Serializable]
    public struct TripWorldDef
    {
        public string name;
        [Tooltip("Rail arc length (m) where this world begins. It ends where the next begins.")]
        public float startDistance;

        [Header("Walls")]
        public TripWallShape wall;
        public TripPattern pattern;
        [Min(1f)] public float wallRadius;
        [Tooltip("Funnel: radius at the end of the world. Slab: height. Ripple/Star: lobe depth in metres.")]
        public float wallRadius2;
        [Tooltip("Lobes (Star) or ripples around the ring (Ripple).")]
        public int lobes;
        [Tooltip("Pattern repeats per 100 m of track. Keep it low (2-4): at ride speed a higher value reads as a strobe.")]
        public float patternScale;
        [Tooltip("Pattern flow speed (kept low: flash safe).")]
        public float patternSpeed;

        [Header("Colour (cosine palette: a + b * cos(2 pi (c * t + d)))")]
        public Color palA;
        public Color palB;
        public Color palC;
        public Color palD;
        [Tooltip("HDR brightness multiplier; above 1 blooms.")]
        public float glow;
        [Tooltip("Palette scroll speed (cycles/second). Keep under about 0.15.")]
        public float hueSpeed;

        [Header("Props / feel")]
        public TripPropKind props;
        [Tooltip("Props breathe in and out by this fraction of their size.")]
        public float pulse;
        [Tooltip("Post-process strength 0..1 (chromatic aberration, lens warp).")]
        [Range(0f, 1f)] public float intensity;
        [Tooltip("Music root note, semitones above A2.")]
        public int rootSemitone;
        public float tempoBpm;
    }

    /// <summary>
    /// Data for <c>trip</c> (Trippy Coaster): a single-cart ride through seven shader-driven worlds. A published video =
    /// scene + this asset + seed. Use the scene builder's Reset Config to refill it with the storyboard defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "TrippyCoasterConfig", menuName = "Simulation Lobby/Trippy Coaster Config")]
    public sealed class TrippyCoasterConfig : SimulationConfig
    {
        public TrippyCoasterConfig()
        {
            maxDurationSeconds = 600f;
        }

        [HideInInspector] public int defaultsVersion;

        [Header("Worlds (ride order)")]
        public TripWorldDef[] worlds;

        [Header("Rail (generated from the course keys by the defaults; control points, centripetal Catmull-Rom)")]
        public Vector3[] railPoints;
        public float[] railBankDeg;

        [Header("Zones (arc-length metres)")]
        public float[] liftStarts;
        public float[] liftEnds;
        public float[] brakeStarts;
        public float[] brakeEnds;
        public float[] brakeSpeeds;

        [Header("Ride physics")]
        [Min(0.1f)] public float liftSpeed = 7f;
        [Min(1f)] public float maxSpeed = 30f;
        [Min(1f)] public float minSpeed = 11f;
        [Min(0.1f)] public float gravity = 9.81f;
        [Min(0f)] public float drag = 0.0008f;
        [Min(0f)] public float rollingFriction = 0.006f;
        [Min(0.5f)] public float brakeDecel = 5f;
        [Min(0.1f)] public float driveAccel = 4f;
        [Min(0.1f)] public float liftAccel = 3f;
        [Tooltip("The cart waits at the top of the world 1 gate before pulling out: the anticipation beat.")]
        [Min(0f)] public float startHoldSeconds = 3f;
        [Tooltip("Seconds the camera keeps rolling after the cart reaches the end of the rail.")]
        [Min(0f)] public float endHoldSeconds = 3f;

        [Header("Cart feel (seeded)")]
        [Tooltip("Roll / pitch shake in degrees. Small: this ride is dizzy enough.")]
        [Range(0f, 3f)] public float wobbleDegrees = 0.7f;

        [Header("Presentation")]
        public float baseFov = 78f;
        [Tooltip("Extra FOV at maxSpeed.")]
        public float speedFov = 14f;
        [Tooltip("Seconds the flashing-colours warning card stays up.")]
        public float warningSeconds = 3.5f;
        public string hookQuestion = "CAN YOU SURVIVE THE TRIP?";

        public RideParams RideParameters => new RideParams
        {
            gravity = gravity, drag = drag, rollingFriction = rollingFriction, liftSpeed = liftSpeed, liftAccel = liftAccel,
            maxSpeed = maxSpeed, minSpeed = minSpeed, brakeDecel = brakeDecel, driveAccel = driveAccel,
            startHoldSeconds = startHoldSeconds, liftStarts = liftStarts, liftEnds = liftEnds,
            brakeStarts = brakeStarts, brakeEnds = brakeEnds, brakeSpeeds = brakeSpeeds
        };

        public CoasterRail BuildRail()
        {
            return new CoasterRail(railPoints, railBankDeg, null);
        }

        public float WorldEnd(int i, float railLength)
        {
            return i + 1 < worlds.Length ? worlds[i + 1].startDistance : railLength;
        }

        public int WorldAt(float distance)
        {
            int idx = 0;
            for (int i = 0; i < worlds.Length; i++)
            {
                if (distance >= worlds[i].startDistance) idx = i;
            }
            return idx;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;
            if (railPoints == null || railPoints.Length < 2) { error = "railPoints needs at least 2 points (run Reset Config)."; return false; }
            if (railBankDeg == null || railBankDeg.Length != railPoints.Length) { error = "railBankDeg must match railPoints."; return false; }
            if (worlds == null || worlds.Length == 0) { error = "At least one world is required."; return false; }
            for (int i = 1; i < worlds.Length; i++)
            {
                if (worlds[i].startDistance <= worlds[i - 1].startDistance) { error = "worlds must be in ascending startDistance order."; return false; }
            }
            error = null;
            return true;
        }
    }
}
