using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Impact
{
    /// <summary>
    /// One take of a soft-drop sweep: a capsule dropped onto a lacquer plinth, at one softness. A
    /// sweep video is several of these differing only in <see cref="softness"/>.
    /// </summary>
    /// <remarks>
    /// Scene layout lives here too, so the solver's colliders and the scene builder's meshes are built
    /// from the same numbers. All lengths in metres; floor at y = 0, plinth centred on the Y axis.
    /// <para>
    /// The plan called for a spike. It was built and tested headless (2026-09-30) and dropped: a
    /// closed soft body can only slide off a convex point, needle or ball, however it's centred, and
    /// impaling it needs the skin to tear, which this solver can't do yet. A flat landing is where
    /// the solver is clean at every softness. See <c>KnowledgeBase/formats/impact.md</c>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Simulation Lobby/Impact/Soft Drop Config", fileName = "SoftDrop")]
    public sealed class SoftDropConfig : SimulationConfig
    {
        [Header("The swept value")]
        [Tooltip("0 = rigid glass, 1 = jelly. Everything in 'Softness mapping' is derived from this.")]
        [Range(0f, 1f)] public float softness;

        [Header("Capsule")]
        [Min(0.05f)] public float capsuleRadius = 0.45f;
        [Tooltip("Half the length of the straight section. 0 = a sphere.")]
        [Min(0f)] public float capsuleHalfLength = 0.2f;
        [Tooltip("Particles around the equator; the rest of the mesh matches its spacing. 40 ≈ 750 particles.")]
        [Range(12, 64)] public int capsuleSegments = 40;
        [Min(0.01f)] public float capsuleMass = 1f;

        [Header("Drop")]
        [Tooltip("Gap between the capsule's lowest point and the plinth top at release.")]
        [Min(0f)] public float dropHeight = 1f;
        [Tooltip("Sideways offset at release. Keep 0: an upright capsule landing off-centre topples and " +
                 "rolls off the plinth.")]
        public float dropOffsetX;
        [Tooltip("Tilt about the camera axis (Z) at release. Keep 0 for the same reason — tested at 8°, " +
                 "every take toppled and left the plinth.")]
        [Range(-45f, 45f)] public float dropTiltDegrees;

        [Header("Plinth (layout — shared with the scene builder)")]
        [Tooltip("2m wide: the 100% take spreads to ~0.92m from centre and must stay on top.")]
        public Vector3 plinthSize = new Vector3(2f, 0.75f, 2f);

        [Header("Solver")]
        [Range(1, 48)] public int substeps = 16;
        public float gravity = 9.81f;
        [Min(0.001f)] public float collisionThickness = 0.014f;
        [Tooltip("High: a splatting body should grip the lacquer and spread evenly, not skate.")]
        [Range(0f, 2f)] public float staticFriction = 1f;
        [Range(0f, 2f)] public float dynamicFriction = 0.7f;

        [Header("Bounce")]
        [Tooltip("0% only (the rigid take): glass off lacquer. ~0.3 gives a small hop and a clean clink.")]
        [Range(0f, 1f)] public float rigidRestitution = 0.3f;
        [Tooltip("Soft takes: how much of the arrival speed the body may leave with. Without a cap a soft " +
                 "body here behaves like a rubber ball (~70% rebound in testing).")]
        [Range(0f, 1f)] public float softRestitution = 0.05f;

        [Header("Softness mapping")]
        [Tooltip("Shape memory (pull back toward the capsule, per second) at 50% soft. The whole range " +
                 "follows rateAtHalf × ((1−s)/s)^exponent: 25% → 1.2, 50% → 0.3, 75% → 0.075, 100% → 0 " +
                 "(no memory at all — it splats like a water balloon). Shape memory is what makes it jelly " +
                 "rather than liquid; the skin barely matters once it's stretchy.")]
        [Min(0f)] public float shapeRateAtHalf = 0.3f;
        [Tooltip("How fast shape memory falls with softness. Higher = more contrast between neighbouring takes.")]
        [Range(0.2f, 4f)] public float shapeRateExponent = 1.26f;
        [Tooltip("Skin stretchiness (XPBD edge compliance). Must be high: squashing a body at constant " +
                 "volume stretches its surface, and a stiff skin (6e-4, the first build) held every take " +
                 "to ~15% squash. Stiff skins also ring at contact.")]
        [Min(1e-9f)] public float edgeCompliance = 1f;
        [Tooltip("0 = volume held exactly (fluid is incompressible).")]
        [Min(0f)] public float volumeCompliance;
        [Min(0f)] public float airDamping = 0.05f;
        [Tooltip("Deformation damping per second at the firm end. Low keeps the jiggle; ~3-5 gives 2-3s.")]
        [Min(0.01f)] public float wobbleDampingStiff = 5f;
        [Tooltip("Deformation damping per second at 100%.")]
        [Min(0.01f)] public float wobbleDampingSoft = 3f;

        [Header("Events")]
        [Tooltip("New contacts slower than this (m/s) are resting jitter, not an impact.")]
        [Min(0f)] public float impactMinSpeed = 0.35f;
        [Tooltip("Minimum gap between two counted impacts, so one landing is one sound.")]
        [Min(0f)] public float impactCooldownSeconds = 0.18f;

        // ---- Derived layout ----

        public float PlinthTopY => plinthSize.y;

        public float CapsuleHalfHeight => capsuleRadius + capsuleHalfLength;

        /// <summary>Capsule centre at release.</summary>
        public Vector3 DropCenter => new Vector3(dropOffsetX, PlinthTopY + dropHeight + CapsuleHalfHeight, 0f);

        /// <summary>Capsule centre when resting upright on the plinth — the subject's home position.</summary>
        public Vector3 RestCenter => new Vector3(0f, PlinthTopY + CapsuleHalfHeight, 0f);

        /// <summary>Free-fall time from release to touching the plinth — the whoosh length.</summary>
        public float FallSeconds => Mathf.Sqrt(2f * Mathf.Max(0.001f, dropHeight) / Mathf.Max(0.1f, gravity));

        public SoftBodyParameters BuildParameters()
        {
            float s = Mathf.Clamp01(softness);
            return new SoftBodyParameters
            {
                totalMass = capsuleMass,
                gravity = new Vector3(0f, -gravity, 0f),
                substeps = substeps,
                rigid = s <= 0f,
                restitution = s <= 0f ? rigidRestitution : softRestitution,
                shapeMatchingRate = ShapeRate(s),
                edgeCompliance = edgeCompliance,
                volumeCompliance = volumeCompliance,
                pressure = 1f,
                airDamping = airDamping,
                wobbleDamping = LogLerp(wobbleDampingStiff, wobbleDampingSoft, s),
                collisionThickness = collisionThickness
            };
        }

        /// <summary>Shape memory per second at softness <paramref name="s"/>; 0 at 100%.</summary>
        public float ShapeRate(float s)
        {
            if (s >= 1f)
            {
                return 0f;
            }

            return shapeRateAtHalf * Mathf.Pow((1f - s) / Mathf.Max(1e-4f, s), shapeRateExponent);
        }

        /// <summary>Interpolates in log space: stiffness spans orders of magnitude, so a linear lerp
        /// would put every setting above ~10% at "jelly".</summary>
        static float LogLerp(float a, float b, float t)
        {
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t));
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (Mathf.Abs(dropOffsetX) + capsuleRadius > plinthSize.x * 0.5f)
            {
                error = "The capsule would land off the edge of the plinth.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
