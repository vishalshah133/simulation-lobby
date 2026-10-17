using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A tiny ember pop at a world point: a quick orange flash of sparks, with some that rise and drift.
    /// For "something was swallowed" beats. Cosmetic only (uses <c>UnityEngine.Random</c>).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class EmberBurst : MonoBehaviour
    {
        public Color hot = new Color(1f, 0.85f, 0.35f, 1f);
        public Color cool = new Color(1f, 0.3f, 0.05f, 1f);
        [Min(0.05f)] public float lifetime = 0.9f;
        public Vector2 speed = new Vector2(1.5f, 4f);
        public Vector2 size = new Vector2(0.05f, 0.12f);
        [Tooltip("Slightly negative embers float up; positive ones fall.")]
        [Range(-1f, 2f)] public float gravity = -0.15f;

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
            main.maxParticles = 600;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;

            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;

            var noise = _particles.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.7f;

            var shrink = _particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            var color = _particles.colorOverLifetime;
            color.enabled = true;
            var burn = new Gradient();
            burn.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = burn;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _material = new Material(shader) { name = "EmberBurst" };
            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = _material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
        }

        /// <summary>Pops <paramref name="count"/> sparks at <paramref name="position"/>. <paramref name="upwardBias"/> 0 is a ball of sparks, 1 mostly rises.</summary>
        public void Pop(Vector3 position, int count = 18, float upwardBias = 0.5f)
        {
            Build();
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Lerp(direction.y, Mathf.Abs(direction.y) + 0.6f, upwardBias);
                var emit = new ParticleSystem.EmitParams
                {
                    position = position + Random.insideUnitSphere * 0.08f,
                    velocity = direction.normalized * Random.Range(speed.x, speed.y),
                    startColor = Color.Lerp(hot, cool, Random.value),
                    startLifetime = lifetime * Random.Range(0.6f, 1.2f),
                    startSize = Random.Range(size.x, size.y),
                    applyShapeToPosition = false
                };
                _particles.Emit(emit, 1);
            }
        }

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
