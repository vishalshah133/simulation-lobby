using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A 3D confetti pop: <see cref="Burst"/> throws tinted paper flakes up and out from a world point.
    /// The 3D counterpart of <see cref="ConfettiBurst2D"/>; format-agnostic, any number can overlap.
    /// </summary>
    /// <remarks>
    /// One <see cref="ParticleSystem"/> configured in code, emitted by hand, with an unlit vertex-coloured
    /// particle material (URP Particles/Unlit, falling back to Sprites/Default). Purely cosmetic: it uses
    /// <c>UnityEngine.Random</c> and real time, which is fine because nothing here feeds back into a run.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ConfettiBurst3D : MonoBehaviour
    {
        [Min(0.1f)] public float lifetime = 2.6f;
        public Vector2 speed = new Vector2(4f, 9f);
        [Tooltip("Cone half-angle around straight up, in degrees.")]
        [Range(5f, 89f)] public float coneAngle = 55f;
        public Vector2 flakeSize = new Vector2(0.07f, 0.14f);
        [Range(0f, 2f)] public float gravity = 1f;
        [Tooltip("Per-flake brightness variation around the colour passed to Burst.")]
        [Range(0f, 0.5f)] public float brightnessJitter = 0.2f;

        ParticleSystem _particles;
        Material _material;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (_particles != null)
            {
                return;
            }

            _particles = GetComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = 1500;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 0.1f;

            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;

            var drag = _particles.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 4f;
            drag.dampen = 0.08f;

            var noise = _particles.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.5f;

            var spin = _particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-9f, 9f);

            var color = _particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _material = new Material(shader) { name = "ConfettiBurst3D" };
            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = _material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
        }

        /// <summary>Throws <paramref name="count"/> flakes in <paramref name="color"/> from a world-space point.</summary>
        public void Burst(Vector3 position, Color color, int count)
        {
            Build();
            for (int i = 0; i < count; i++)
            {
                Vector2 disc = Random.insideUnitCircle * Mathf.Tan(coneAngle * Mathf.Deg2Rad);
                Vector3 direction = new Vector3(disc.x, 1f, disc.y).normalized;
                float shade = 1f + Random.Range(-brightnessJitter, brightnessJitter);
                Color tint = new Color(
                    Mathf.Clamp01(color.r * shade), Mathf.Clamp01(color.g * shade), Mathf.Clamp01(color.b * shade), color.a);

                var emit = new ParticleSystem.EmitParams
                {
                    position = position + Random.insideUnitSphere * 0.1f,
                    velocity = direction * Random.Range(speed.x, speed.y),
                    startColor = tint,
                    startLifetime = lifetime * Random.Range(0.75f, 1.15f),
                    startSize = Random.Range(flakeSize.x, flakeSize.y),
                    rotation = Random.Range(0f, 360f),
                    applyShapeToPosition = false
                };
                _particles.Emit(emit, 1);
            }
        }

        /// <summary>Removes every flake, e.g. when a new run starts.</summary>
        public void Clear()
        {
            Build();
            _particles.Clear();
        }

        void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
