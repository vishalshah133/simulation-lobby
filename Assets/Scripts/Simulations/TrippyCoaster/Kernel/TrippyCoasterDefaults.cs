using System.Collections.Generic;
using SimulationLobby.Shared.Coaster;
using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>
    /// The trip-001 storyboard as data: a turtle course in seven worlds (each a run of <see cref="CourseKey"/>s) and the look of
    /// each world. World start distances are the cumulative key length at the world's first key.
    /// </summary>
    public static class TrippyCoasterDefaults
    {
        /// <summary>Bump when the course or physics defaults change: the scene builder re-applies the defaults to a config with an older version.</summary>
        public const int Version = 4;

        public static void Apply(TrippyCoasterConfig cfg)
        {
            var keys = new List<CourseKey>();
            var starts = new List<float>();
            float run = 0f;
            float roll = 0f;     // accumulated barrel-roll offset so later banks stay in the rolled frame
            float pitchOff = 0f; // accumulated vertical-loop offset (360 per loop) so later pitches stay level

            void World() { starts.Add(run); }
            void K(float len, float pitch, float yaw, float bank, bool linear = false, float drift = 0f)
            {
                keys.Add(new CourseKey(len, pitch + pitchOff, yaw, bank + roll, linear, drift));
                run += len;
            }

            // A vertical loop of radius r: eased entry, a constant-rate body (a circle), eased exit. The sideways drift offsets the exit
            // from the entry so the track never crosses itself.
            void Loop(float r, float drift)
            {
                K(0.92f * r, 35, 0, 0, false, drift);
                K(290f / 57.2958f * r, 325, 0, 0, true, drift);
                K(0.92f * r, 360, 0, 0, false, 0f);
                pitchOff += 360f;
            }

            // 1 NEON GATE: the station hold, a long lift through glowing gates, a crest, then a crazy near-vertical plunge and a double loop.
            World();
            K(60, 0, 0, 0);
            K(60, 35, 0, 0);
            K(520, 35, 0, 0);
            float liftEnd = run;
            K(220, 0, 0.1f, 0);
            K(80, 0, 0, 0);
            K(120, -78, 0, 0);
            K(450, -78, 0, 0);
            K(700, 0, 0, 0);
            K(150, 0, 0, 0);
            Loop(100f, 0.07f);
            K(250, 0, 0, 0);
            Loop(100f, -0.07f);
            K(300, 0, 0, 0);

            // 2 UV TUNNEL: fast banked S-curves, a single loop, airtime hills, then a second big drop.
            World();
            K(300, 14, 0.2f, 30);
            K(300, -10, 0.2f, 30);
            K(300, 12, -0.22f, -32);
            K(300, -14, -0.22f, -32);
            K(250, 0, 0, 0);
            Loop(110f, 0.08f);          // single loop inside the tube cage
            K(250, 0, 0, 0);
            K(200, 40, 0, 0);
            K(150, 0, 0, 0);
            K(400, -60, 0, 0);
            K(500, 0, 0, 0);

            // 3 KALEIDOSCOPE: a long banked helix (under one full turn, so it never crosses its own tunnel).
            World();
            K(250, -5, 0.15f, 36);
            K(1200, -5, 0.17f, 38);
            K(250, 6, 0.1f, 30);
            K(300, 10, 0, 0);
            K(200, 0, 0, 0);

            // 4 INVERTED SKY: two camelback drops, then a full barrel roll between a floating floor and ceiling.
            World();
            K(200, 16, 0, 0);
            K(250, -28, 0, 0);
            K(200, 18, 0, 0);
            K(250, -24, 0, 0);
            roll = 360f;
            K(500, -3, 0, 0);
            K(300, 12, 0, 0);
            K(300, -20, 0, 0);
            K(300, 0, 0, 0);

            // 5 LIQUID LIGHT: tight serpentine over airtime hills, a double loop, then a plunge.
            World();
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                K(250, 8 * side * -1f, 0.25f * side, 34 * side);
            }
            K(250, 0, 0, 0);
            Loop(100f, 0.08f);          // double loop through the rippling plasma
            K(250, 0, 0, 0);
            Loop(100f, -0.08f);
            K(250, 0, 0, 0);
            K(300, -45, 0, 0);
            K(400, 0, 0, 0);

            // 6 FRACTAL BLOOM: another drop, a second double loop in open space, then a spiral descent.
            World();
            K(300, -35, 0, 0);
            K(400, 0, 0, 0);
            K(200, 0, 0, 0);
            Loop(90f, 0.07f);
            K(220, 0, 0, 0);
            Loop(90f, -0.07f);
            K(200, 0, 0, 0);
            K(100, -6, 0.25f, 35);
            K(900, -8, 0.3f, 40);
            K(300, 0, 0, 0);

            // 7 SINGULARITY: climb, hang, the longest plunge down the closing funnel, then a last loop.
            World();
            K(500, 28, 0, 0);
            K(250, 0, 0, 0);
            K(150, -75, 0, 0);
            K(600, -75, 0, 0);
            K(500, -10, 0, 0);
            K(400, 0, 0, 0);
            Loop(110f, 0.1f);           // final single loop inside the closing funnel
            K(450, 0, 0, 0);
            float total = run;

            RailCourse.Build(keys, 12f, out Vector3[] points, out float[] bank);
            cfg.railPoints = points;
            cfg.railBankDeg = bank;

            cfg.liftStarts = new[] { 0f };
            cfg.liftEnds = new[] { liftEnd };
            cfg.brakeStarts = new[] { total - 450f };
            cfg.brakeEnds = new[] { total + 100f };
            cfg.brakeSpeeds = new[] { 25f };

            cfg.defaultsVersion = Version;
            cfg.liftSpeed = 32f;
            cfg.liftAccel = 16f;
            cfg.maxSpeed = 110f;
            cfg.minSpeed = 38f;     // 137 km/h: after the first drop the cart never slows below this
            cfg.gravity = 9.81f;
            cfg.drag = 0.0001f;
            cfg.rollingFriction = 0.002f;
            cfg.driveAccel = 14f;
            cfg.brakeDecel = 12f;
            cfg.startHoldSeconds = 3f;
            cfg.endHoldSeconds = 3f;
            cfg.wobbleDegrees = 0.7f;
            cfg.baseFov = 80f;
            cfg.speedFov = 24f;
            cfg.warningSeconds = 3.5f;
            cfg.hookQuestion = "CAN YOU SURVIVE THE TRIP?";
            cfg.intent = "trip-001: single cart through seven shader-driven worlds. No riders, no scares: the stake is the ride itself.";

            Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

            cfg.worlds = new[]
            {
                new TripWorldDef
                {
                    name = "NEON GATE", startDistance = starts[0], wall = TripWallShape.None, pattern = TripPattern.Glow,
                    wallRadius = 9f, wallRadius2 = 0f, lobes = 0, patternScale = 1f, patternSpeed = 0.4f,
                    palA = C(0.5f, 0.5f, 0.5f), palB = C(0.5f, 0.5f, 0.5f), palC = C(1f, 1f, 1f), palD = C(0.0f, 0.33f, 0.67f),
                    glow = 2.2f, hueSpeed = 0.06f, props = TripPropKind.GateRings, pulse = 0.06f, intensity = 0.35f,
                    rootSemitone = 0, tempoBpm = 100f
                },
                new TripWorldDef
                {
                    name = "UV TUNNEL", startDistance = starts[1], wall = TripWallShape.Circle, pattern = TripPattern.Stripes,
                    wallRadius = 8.5f, wallRadius2 = 0f, lobes = 0, patternScale = 2.5f, patternSpeed = 0.55f,
                    palA = C(0.45f, 0.2f, 0.6f), palB = C(0.5f, 0.45f, 0.55f), palC = C(1f, 1f, 1f), palD = C(0.55f, 0.15f, 0.25f),
                    glow = 2.6f, hueSpeed = 0.07f, props = TripPropKind.UvTubes, pulse = 0.05f, intensity = 0.5f,
                    rootSemitone = 3, tempoBpm = 108f
                },
                new TripWorldDef
                {
                    name = "KALEIDOSCOPE", startDistance = starts[2], wall = TripWallShape.Star, pattern = TripPattern.Kaleido,
                    wallRadius = 10f, wallRadius2 = 2.2f, lobes = 6, patternScale = 2.5f, patternSpeed = 0.5f,
                    palA = C(0.55f, 0.5f, 0.5f), palB = C(0.5f, 0.5f, 0.5f), palC = C(1f, 0.7f, 0.4f), palD = C(0f, 0.15f, 0.2f),
                    glow = 2.4f, hueSpeed = 0.08f, props = TripPropKind.Crystals, pulse = 0.08f, intensity = 0.65f,
                    rootSemitone = 5, tempoBpm = 112f
                },
                new TripWorldDef
                {
                    name = "INVERTED SKY", startDistance = starts[3], wall = TripWallShape.Slab, pattern = TripPattern.Grid,
                    wallRadius = 70f, wallRadius2 = 13f, lobes = 0, patternScale = 3f, patternSpeed = 0.35f,
                    palA = C(0.3f, 0.5f, 0.55f), palB = C(0.5f, 0.5f, 0.5f), palC = C(1f, 1f, 0.5f), palD = C(0.8f, 0.9f, 0.3f),
                    glow = 2.3f, hueSpeed = 0.05f, props = TripPropKind.Spheres, pulse = 0.07f, intensity = 0.6f,
                    rootSemitone = 7, tempoBpm = 96f
                },
                new TripWorldDef
                {
                    name = "LIQUID LIGHT", startDistance = starts[4], wall = TripWallShape.Ripple, pattern = TripPattern.Plasma,
                    wallRadius = 8f, wallRadius2 = 2.2f, lobes = 7, patternScale = 2f, patternSpeed = 0.7f,
                    palA = C(0.5f, 0.5f, 0.5f), palB = C(0.5f, 0.5f, 0.5f), palC = C(2f, 1f, 0f), palD = C(0.5f, 0.2f, 0.25f),
                    glow = 2.5f, hueSpeed = 0.1f, props = TripPropKind.Orbs, pulse = 0.1f, intensity = 0.8f,
                    rootSemitone = 2, tempoBpm = 120f
                },
                new TripWorldDef
                {
                    name = "FRACTAL BLOOM", startDistance = starts[5], wall = TripWallShape.None, pattern = TripPattern.Glow,
                    wallRadius = 9f, wallRadius2 = 0f, lobes = 0, patternScale = 1f, patternSpeed = 0.5f,
                    palA = C(0.8f, 0.5f, 0.4f), palB = C(0.2f, 0.4f, 0.2f), palC = C(2f, 1f, 1f), palD = C(0f, 0.25f, 0.25f),
                    glow = 2.4f, hueSpeed = 0.08f, props = TripPropKind.Fractals, pulse = 0.09f, intensity = 0.75f,
                    rootSemitone = 9, tempoBpm = 116f
                },
                new TripWorldDef
                {
                    name = "SINGULARITY", startDistance = starts[6], wall = TripWallShape.Funnel, pattern = TripPattern.Kaleido,
                    wallRadius = 15f, wallRadius2 = 3.4f, lobes = 0, patternScale = 3.5f, patternSpeed = 0.9f,
                    palA = C(0.5f, 0.5f, 0.5f), palB = C(0.5f, 0.5f, 0.5f), palC = C(1f, 1f, 1f), palD = C(0.3f, 0.2f, 0.2f),
                    glow = 2.8f, hueSpeed = 0.12f, props = TripPropKind.Vortex, pulse = 0.12f, intensity = 1f,
                    rootSemitone = 0, tempoBpm = 128f
                }
            };
        }
    }
}
