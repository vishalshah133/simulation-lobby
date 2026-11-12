using System;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer
{
    [Serializable]
    public sealed class TimerFinaleFxSettings
    {
        public int confettiBurst = 260;
        [Tooltip("Extra particles per second for emissionSeconds after the burst.")]
        public float confettiTrickle = 90f;
        [Min(0f)] public float emissionSeconds = 0.8f;
        public Vector2 speed = new Vector2(6f, 15f);
        [Range(5f, 89f)] public float coneAngle = 42f;
        public Vector2 size = new Vector2(0.22f, 0.42f);
        public Vector2 lifetime = new Vector2(3f, 4.6f);
        [Range(0f, 2f)] public float gravity = 1.1f;
        public int ringCount = 56;
        public float ringSpeed = 9f;
        public float ringSize = 0.5f;
        [Tooltip("Metres above the anchor the burst starts.")]
        public float lift = 0.5f;

        [Header("All in (every marble collected)")]
        [Tooltip("Confetti bursts spread over celebrationSeconds; the total is about bursts x particlesPerBurst.")]
        [Min(1)] public int celebrationBursts = 4;
        [Min(1)] public int particlesPerBurst = 300;
        [Min(0.1f)] public float celebrationSeconds = 3f;
        [Tooltip("Extra metres of lift per successive burst, so they fan out at different heights.")]
        public float burstLiftStep = 0.8f;
        public int sparkleCount = 70;
    }

    /// <summary>
    /// The 0:00 confetti: one burst of colourful paper flakes and a quick flat ring pop at the collector mouth, created in
    /// code. Cosmetic only; it observes the phase and never touches simulation state.
    /// </summary>
    public sealed class TimerFinaleFx : MonoBehaviour
    {
        [SerializeField] TimerSimulation sim;
        [SerializeField] Transform anchor;
        [SerializeField] TimerFinaleFxSettings settings = new TimerFinaleFxSettings();

        ParticleSystem _confetti;
        ParticleSystem _ring;
        ParticleSystem[] _celebration;
        ParticleSystem _sparkles;
        Material _material;
        bool _fired;
        bool _celebrated;
        TimerPhase _lastPhase = TimerPhase.Setup;

        public void Bind(TimerSimulation simulation, Transform anchorPoint)
        {
            sim = simulation;
            anchor = anchorPoint;
        }

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (_confetti != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _material = new Material(shader) { name = "TimerFinaleFx" };
            _confetti = CreateSystem("Confetti", Quaternion.Euler(-90f, 0f, 0f));
            _ring = CreateSystem("RingPop", Quaternion.Euler(-90f, 0f, 0f));
            ConfigureConfetti(_confetti, Palette(), settings.confettiBurst, settings.confettiTrickle, settings.emissionSeconds, 0f, settings.coneAngle);
            ConfigureRing(_ring);

            int bursts = Mathf.Max(1, settings.celebrationBursts);
            _celebration = new ParticleSystem[bursts];
            for (int i = 0; i < bursts; i++)
            {
                _celebration[i] = CreateSystem("CelebrationBurst" + i, Quaternion.Euler(-90f, 0f, 0f));
                float delay = bursts > 1 ? settings.celebrationSeconds * i / (bursts - 1) : 0f;
                Gradient colors = i % 3 == 0 ? Palette() : (i % 3 == 1 ? WarmPalette() : CoolPalette());
                ConfigureConfetti(_celebration[i], colors, settings.particlesPerBurst, 0f, 0.1f, delay, settings.coneAngle + 10f + 6f * i);
            }

            _sparkles = CreateSystem("CelebrationSparkles", Quaternion.Euler(-90f, 0f, 0f));
            ConfigureSparkles(_sparkles);
        }

        ParticleSystem CreateSystem(string name, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = rotation;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        void ConfigureConfetti(ParticleSystem ps, Gradient palette, int burst, float trickle, float emissionTime, float startDelay, float cone)
        {
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startDelay = startDelay;
            main.duration = Mathf.Max(0.1f, emissionTime);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = settings.gravity;
            main.maxParticles = 1500;
            main.startLifetime = new ParticleSystem.MinMaxCurve(settings.lifetime.x, settings.lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(settings.speed.x, settings.speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(settings.size.x, settings.size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(palette);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = trickle;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(burst, 0, short.MaxValue)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone;
            shape.radius = 0.5f;

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 4.5f;
            drag.dampen = 0.07f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.5f;

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-9f, 9f);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
        }

        void ConfigureRing(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = 400;
            main.startLifetime = 0.7f;
            main.startSpeed = settings.ringSpeed;
            main.startSize = settings.ringSize;
            main.startColor = new Color(1f, 0.9f, 0.35f, 1f);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(settings.ringCount, 0, short.MaxValue)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
        }

        // Gold stars: bright, a little bigger than the flakes, floating up and twinkling out.
        void ConfigureSparkles(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            main.maxParticles = 400;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.3f, 1f), new Color(1f, 1f, 0.85f, 1f));

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Clamp(settings.sparkleCount, 0, short.MaxValue)),
                new ParticleSystem.Burst(Mathf.Max(0.1f, settings.celebrationSeconds * 0.5f), (short)Mathf.Clamp(settings.sparkleCount, 0, short.MaxValue))
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f;

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.6f, 0.7f), new Keyframe(1f, 0f)));

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
        }

        static Gradient WarmPalette()
        {
            var g = new Gradient();
            g.mode = GradientMode.Fixed;
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.82f, 0.2f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.33f),
                    new GradientColorKey(new Color(1f, 0.3f, 0.35f), 0.66f),
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Gradient CoolPalette()
        {
            var g = new Gradient();
            g.mode = GradientMode.Fixed;
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.2f, 0.7f, 1f), 0.0f),
                    new GradientColorKey(new Color(0.35f, 0.9f, 0.5f), 0.33f),
                    new GradientColorKey(new Color(0.7f, 0.4f, 1f), 0.66f),
                    new GradientColorKey(new Color(1f, 1f, 1f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // Random-colour gradient for the flakes: saturated toy colours.
        static Gradient Palette()
        {
            var g = new Gradient();
            g.mode = GradientMode.Fixed;
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.25f, 0.25f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.12f), 0.17f),
                    new GradientColorKey(new Color(0.25f, 0.85f, 0.35f), 0.34f),
                    new GradientColorKey(new Color(0.2f, 0.6f, 1f), 0.51f),
                    new GradientColorKey(new Color(0.75f, 0.35f, 1f), 0.68f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.75f), 0.85f),
                    new GradientColorKey(new Color(1f, 1f, 1f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void Update()
        {
            if (sim == null)
            {
                return;
            }

            ITimerState state = sim;
            if (_lastPhase != TimerPhase.Setup && state.Phase == TimerPhase.Setup)
            {
                Build();
                _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                StopCelebration();
                _fired = false;
                _celebrated = false;
            }

            // Every marble in the bucket: the big celebration, the moment the last one lands (before or at 0:00).
            if (!_celebrated && state.Released && state.TotalMarbles > 0 && state.CollectedCount >= state.TotalMarbles)
            {
                Celebrate();
            }

            // The normal 0:00 confetti only when the celebration has not already happened.
            bool finale = state.Phase == TimerPhase.Finale || state.Phase == TimerPhase.Done;
            if (finale && !_fired && !_celebrated)
            {
                Fire();
            }

            _lastPhase = state.Phase;
        }

        void Fire()
        {
            Build();
            _fired = true;
            Vector3 at = (anchor != null ? anchor.position : transform.position) + Vector3.up * settings.lift;
            _confetti.transform.position = at;
            _ring.transform.position = at;
            _confetti.Play();
            _ring.Play();
        }

        void Celebrate()
        {
            Build();
            _celebrated = true;
            _fired = true;
            Vector3 at = (anchor != null ? anchor.position : transform.position) + Vector3.up * settings.lift;
            for (int i = 0; i < _celebration.Length; i++)
            {
                _celebration[i].transform.position = at + Vector3.up * (settings.burstLiftStep * i);
                _celebration[i].Play();
            }

            _sparkles.transform.position = at + Vector3.up * (settings.burstLiftStep * 1.5f);
            _sparkles.Play();
            _ring.transform.position = at;
            _ring.Play();
        }

        void StopCelebration()
        {
            if (_celebration == null)
            {
                return;
            }

            for (int i = 0; i < _celebration.Length; i++)
            {
                _celebration[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            _sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
