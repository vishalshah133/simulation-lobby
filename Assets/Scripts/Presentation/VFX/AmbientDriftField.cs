using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A slow field of drifting motes behind the arena. Gives the frame depth and motion during the
    /// quiet stretches between bounces without competing with anything the viewer needs to read.
    /// </summary>
    /// <remarks>
    /// Purely decorative and strictly behind the action — the ball and the gap must always win the
    /// frame, so this runs at low alpha, low count and low speed. If you notice it while watching the
    /// ball, it is turned up too far.
    /// <para>
    /// The particle system is <b>seeded</b> (<c>useAutoRandomSeed = false</c>). Unity would otherwise
    /// re-roll the drift every run, and a video re-rendered from its seed months later would have a
    /// visibly different background — the same re-renderability rule the simulation follows.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class AmbientDriftField : MonoBehaviour
    {
        [Header("Field")]
        [Tooltip("Area the motes spawn across, in world units. Make it larger than the camera frame.")]
        public Vector2 fieldSize = new Vector2(14f, 24f);

        [Tooltip("Motes on screen at once. Subtlety comes from keeping this low.")]
        [Range(4, 400)] public int particleCount = 90;

        [Tooltip("Seed for the drift pattern. Fixed so re-renders match.")]
        public int randomSeed = 7717;

        [Header("Look")]
        public Color tint = new Color(0.55f, 0.65f, 1f, 1f);

        [Tooltip("Peak opacity. Above ~0.2 the motes start pulling focus from the ball.")]
        [Range(0.01f, 0.5f)] public float maxAlpha = 0.13f;

        public Vector2 sizeRange = new Vector2(0.05f, 0.2f);

        [Tooltip("Drift speed in units/second. Slow enough to read as ambience, not weather.")]
        [Range(0.01f, 1f)] public float driftSpeed = 0.14f;

        [Tooltip("Render order. Must be below the arena walls so nothing floats over the action.")]
        public int sortingOrder = -50;

        /// <summary>
        /// Configure the attached particle system. Called by a scene builder; safe to re-run.
        /// </summary>
        /// <param name="dotTexture">Soft round texture for a mote. A hard-edged one reads as dust specks.</param>
        public void Apply(Texture dotTexture)
        {
            var system = GetComponent<ParticleSystem>();

            // Seed lives on the system, not the main module, and only takes effect while stopped —
            // setting it on a playing system silently does nothing.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = (uint)Mathf.Abs(randomSeed);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = 12f;
            main.startLifetime = 14f;
            main.startSpeed = driftSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startColor = new Color(tint.r, tint.g, tint.b, maxAlpha);
            main.maxParticles = particleCount;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // Without prewarm the video opens on an empty background that slowly fills — the first
            // seconds are the ones that decide whether anyone keeps watching.
            main.prewarm = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = particleCount / main.startLifetime.constant;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(fieldSize.x, fieldSize.y, 0.01f);
            shape.position = Vector3.zero;

            // Fade in and out rather than popping at spawn and death.
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // Gentle curl so the motes wander instead of travelling in straight lines.
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.06f;
            noise.damping = true;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;

            if (dotTexture != null)
            {
                // Sprites/Default rather than a URP particle shader: it is present in every pipeline,
                // so the backdrop cannot break if render settings change between videos.
                var material = new Material(Shader.Find("Sprites/Default"));
                material.mainTexture = dotTexture;
                renderer.sharedMaterial = material;
            }
        }
    }
}
