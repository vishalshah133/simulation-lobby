using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A celebration: two confetti cannons that fire up and inward, then a short rain from the top of
    /// the frame. Format-agnostic: it knows a palette and where to fire from, nothing about who won.
    /// </summary>
    /// <remarks>
    /// Runs on a <see cref="ParticleSystem"/> that this component configures itself, emitting by hand so
    /// each piece gets its own position, velocity, colour and size. Purely visual: it uses
    /// <c>UnityEngine.Random</c> and <c>Time.deltaTime</c>, which is fine because nothing here feeds
    /// back into a run.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ConfettiBurst2D : MonoBehaviour
    {
        [Header("Cannons — world positions, fired up and inward")]
        public Vector2 leftCannon = new Vector2(-5.1f, -2.5f);
        public Vector2 rightCannon = new Vector2(5.1f, -2.5f);

        [Header("Rain from the top")]
        public float rainY = 10.5f;
        [Min(1f)] public float rainHalfWidth = 5.8f;
        [Min(0f)] public float rainSeconds = 3.2f;
        [Min(0f)] public float rainPerSecond = 55f;

        [Header("Burst")]
        [Range(10, 400)] public int burstCount = 160;
        public Vector2 burstSpeed = new Vector2(9f, 16f);
        [Range(0f, 60f)] public float minInwardAngle = 8f;
        [Range(0f, 60f)] public float maxInwardAngle = 38f;
        public int sortingOrder = 40;

        ParticleSystem _particles;
        Color[] _palette = { Color.white };
        float _rainLeft;
        float _rainCarry;

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
            main.gravityModifier = 0.6f;
            main.maxParticles = 1200;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startRotation3D = false;

            // Every piece is emitted by hand, so the built-in emitter and shape stay off.
            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;

            // Slow the bursts down and let the pieces flutter, so they drift instead of falling like stones.
            var limit = _particles.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 3.2f;
            limit.dampen = 0.09f;

            var noise = _particles.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.45f;

            var spin = _particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

            // Fade out over the last fifth of a piece's life.
            var color = _particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = sortingOrder;
        }

        /// <summary>Fires the cannons and starts the rain. The palette is whatever colours the moment calls for.</summary>
        public void Celebrate(params Color[] palette)
        {
            Build();
            _palette = palette != null && palette.Length > 0 ? palette : new[] { Color.white };

            for (int i = 0; i < burstCount; i++)
            {
                bool left = i % 2 == 0;
                Vector2 origin = left ? leftCannon : rightCannon;
                float inward = Random.Range(minInwardAngle, maxInwardAngle) * Mathf.Deg2Rad;
                float speed = Random.Range(burstSpeed.x, burstSpeed.y);
                float side = left ? 1f : -1f;
                var velocity = new Vector3(side * Mathf.Sin(inward), Mathf.Cos(inward), 0f) * speed;
                Emit(origin + Random.insideUnitCircle * 0.25f, velocity);
            }

            _rainLeft = rainSeconds;
            _rainCarry = 0f;
        }

        /// <summary>Removes every piece, e.g. when a new run starts.</summary>
        public void Clear()
        {
            Build();
            _particles.Clear();
            _rainLeft = 0f;
        }

        void Update()
        {
            if (_rainLeft <= 0f)
            {
                return;
            }

            float dt = Time.deltaTime;
            _rainLeft -= dt;
            _rainCarry += rainPerSecond * dt;
            int count = Mathf.FloorToInt(_rainCarry);
            _rainCarry -= count;

            for (int i = 0; i < count; i++)
            {
                var position = new Vector2(Random.Range(-rainHalfWidth, rainHalfWidth), rainY);
                Emit(position, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-3.5f, -1.5f), 0f));
            }
        }

        void Emit(Vector2 position, Vector3 velocity)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y, 0f),
                velocity = velocity,
                startColor = _palette[Random.Range(0, _palette.Length)],
                startLifetime = Random.Range(3f, 4.6f),
                startSize3D = new Vector3(Random.Range(0.14f, 0.24f), Random.Range(0.07f, 0.13f), 1f),
                rotation3D = new Vector3(0f, 0f, Random.Range(0f, 360f)),
                applyShapeToPosition = false
            };
            _particles.Emit(emit, 1);
        }
    }
}
