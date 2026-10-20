using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>Pad silhouette. Read by the View only; the kernel ignores it.</summary>
    public enum PadShape { Disc, Petal, Lily, Crystal, Crescent, Hex }

    /// <summary>How a world's walls are decorated. Read by the View only.</summary>
    public enum WallStyle { RingStack, Aurora, Mist, CrystalShards, Mandala, Starfield, Vortex, None }

    /// <summary>
    /// One world of the <c>fall</c> format: a melody played through <see cref="passes"/> times by one marble
    /// (or a canon of several), the well it falls in, and how it looks. The kernel reads the timing and
    /// geometry fields; the View reads the look fields.
    /// </summary>
    [Serializable]
    public sealed class FreefallWorld
    {
        public string worldName = "GLASS BELLS";

        [Tooltip("Rhythm comes from the note beats; the pitches drive hit radius and hue.")]
        public MelodySequence melody;

        public string composer = "BEETHOVEN";

        [Tooltip("Quarter-note beats per minute at the world's start.")]
        public float bpm = 120f;

        [Tooltip("Above bpm = accelerando. Tempo ramps linearly in beats across the world.")]
        public float bpmEnd = 120f;

        [Min(1)] public int passes = 2;

        [Tooltip("m/s^2. Lower = floatier arcs.")]
        public float gravity = 9.81f;

        public float wellRadius = 3.2f;

        [Range(0f, 1f)] public float innerRadiusFraction = 0.25f;
        [Range(0f, 1f)] public float outerRadiusFraction = 0.8f;

        [Tooltip("How fast the hit points wind around the axis.")]
        public float degreesPerSecond = 40f;

        [Range(0.05f, 0.95f)]
        [Tooltip("Fraction of 1/2 g dt^2 each note drops (below 1 guarantees an upward bounce).")]
        public float dropFraction = 0.5f;

        public float maxDropPerNote = 1.6f;

        [Min(1)]
        [Tooltip("Above 1 = the split: voice k starts k * canonDelayBeats late, rotated k * 360 / voices degrees.")]
        public int canonVoices = 1;

        public float canonDelayBeats = 8f;

        // Look (read by View only; the kernel ignores these)
        public PadShape padShape;
        public WallStyle wallStyle;
        public ProceduralInstrument.Timbre timbre;
        public Color skyTop = Color.black;
        public Color skyBottom = Color.black;
        public Color fogColor = Color.black;
        public Color wallColor = Color.white;

        [Tooltip("Pitch to hue: the lowest note is hueStart, the highest is hueStart + hueSpan.")]
        public float hueStart = 0.6f;
        public float hueSpan = 0.25f;

        public float padRadius = 0.32f;
        public float cameraRollDegreesPerSecond = 4f;
    }

    /// <summary>
    /// Config for the <c>fall</c> format (Freefall Symphony): one marble falls through a series of worlds and
    /// every bounce plays the next note of a song. A published video = this asset + a (cosmetic) seed.
    /// </summary>
    [CreateAssetMenu(fileName = "FreefallConfig", menuName = "Simulation Lobby/Configs/Freefall Config")]
    public sealed class FreefallConfig : SimulationConfig
    {
        [Header("Worlds")]
        public List<FreefallWorld> worlds = new List<FreefallWorld>();

        [Header("Timing")]
        [Tooltip("The marble falls from rest onto the first pad of the first world.")]
        public float introDropSeconds = 1.2f;

        [Tooltip("Fall between worlds. The finished world's reveal pull-back happens here.")]
        public float transitionSeconds = 4f;

        [Tooltip("Extra vertical metres between worlds, on top of the free fall.")]
        public float worldGap = 6f;

        [Tooltip("After the final note: core hit, white-out, final pull-back.")]
        public float outroSeconds = 5f;

        [Header("Marble and pads")]
        public float marbleRadius = 0.12f;

        [Tooltip("A pad fades in this long before it is hit (anticipation).")]
        public float padLeadSeconds = 0.9f;

        public FreefallConfig()
        {
            // Timing precision only: there is no physics, hits are scheduled analytically.
            fixedTimestep = 1f / 240f;
            maxDurationSeconds = 900f;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (worlds == null || worlds.Count < 1)
            {
                error = "Freefall needs at least one world.";
                return false;
            }

            for (int i = 0; i < worlds.Count; i++)
            {
                FreefallWorld w = worlds[i];
                if (w == null)
                {
                    error = $"World {i + 1} is null.";
                    return false;
                }

                string label = $"World {i + 1} ({w.worldName})";

                if (w.melody == null || w.melody.Count < 2)
                {
                    error = label + " needs a melody with at least 2 notes.";
                    return false;
                }

                for (int n = 0; n < w.melody.Count; n++)
                {
                    if (!(w.melody.notes[n].beats > 0f))
                    {
                        error = label + $": melody note {n} has non-positive beats.";
                        return false;
                    }
                }

                if (w.bpm < 10f || w.bpmEnd < 10f)
                {
                    error = label + ": bpm and bpmEnd must be at least 10.";
                    return false;
                }

                if (w.passes < 1 || w.canonVoices < 1)
                {
                    error = label + ": passes and canonVoices must be at least 1.";
                    return false;
                }

                if (w.gravity <= 0.1f)
                {
                    error = label + ": gravity must be positive.";
                    return false;
                }

                if (w.wellRadius <= 0.5f || w.padRadius <= 0.01f || w.padRadius * 2f >= w.wellRadius)
                {
                    error = label + ": wellRadius/padRadius out of range.";
                    return false;
                }

                if (w.outerRadiusFraction < w.innerRadiusFraction)
                {
                    error = label + ": outerRadiusFraction must be at least innerRadiusFraction.";
                    return false;
                }

                // Hit radius carries up to +4% jitter; the pad must still sit inside the wall.
                float maxHit = w.outerRadiusFraction * 1.04f * w.wellRadius + w.padRadius;
                if (maxHit > w.wellRadius)
                {
                    error = label + ": outerRadiusFraction puts pads outside the well.";
                    return false;
                }

                if (w.canonVoices > 1 && w.canonDelayBeats <= 0f)
                {
                    error = label + ": canonDelayBeats must be positive for a canon.";
                    return false;
                }

                if (w.maxDropPerNote <= 0f)
                {
                    error = label + ": maxDropPerNote must be positive.";
                    return false;
                }
            }

            if (introDropSeconds < 0.2f || transitionSeconds < 0.5f)
            {
                error = "introDropSeconds must be at least 0.2 and transitionSeconds at least 0.5.";
                return false;
            }

            if (marbleRadius <= 0f)
            {
                error = "marbleRadius must be positive.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
