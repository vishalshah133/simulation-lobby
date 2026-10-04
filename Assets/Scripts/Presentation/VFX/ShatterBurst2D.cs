using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Breaking-glass effect for 2D walls: tumbling debris that falls under gravity, fast streaking
    /// sparks, and an expanding shockwave ring for the big moments. Format-agnostic: callers pass a
    /// position, an outward direction, a colour, and optionally which debris shape to use.
    /// </summary>
    /// <remarks>
    /// One particle system per debris shape (triangle shard, sliver, arc chunk, diamond, star…), so a
    /// caller can give each wall its own signature debris: variation across a video without any
    /// randomness the viewer would read as noise. Everything is emitted by hand, so each burst is
    /// exactly where and when the simulation says. Randomness comes from a seeded
    /// <see cref="System.Random"/> plus fixed particle-system seeds, so a re-render of the same run
    /// produces the same debris. Presentation only: it never reads or writes physics.
    /// </remarks>
    public sealed class ShatterBurst2D : MonoBehaviour
    {
        [Header("Debris")]
        [Range(1, 40)] public int shardsPerBurst = 9;
        public Vector2 shardSpeed = new Vector2(1.2f, 4.2f);
        public Vector2 shardSize = new Vector2(0.08f, 0.22f);
        public Vector2 shardLifetime = new Vector2(0.7f, 1.4f);
        [Range(0f, 3f)] public float shardGravity = 0.9f;

        [Tooltip("Share of a burst's debris that uses the requested shape; the rest mixes in other shapes.")]
        [Range(0f, 1f)] public float signatureShare = 0.8f;

        [Header("Colour")]
        [Tooltip("Per-piece brightness spread, so debris reads as glass catching light rather than flat confetti.")]
        [Range(0f, 0.5f)] public float brightnessJitter = 0.18f;

        [Range(0f, 0.1f)] public float hueJitter = 0.025f;

        [Tooltip("Chance a piece is a bright glint (pushed toward white).")]
        [Range(0f, 0.5f)] public float glintChance = 0.12f;

        [Header("Sparks")]
        [Range(0, 40)] public int sparksPerBurst = 7;
        public Vector2 sparkSpeed = new Vector2(4f, 9f);
        public Vector2 sparkLifetime = new Vector2(0.15f, 0.4f);

        [Header("Shockwave")]
        [Range(0.1f, 2f)] public float shockwaveSeconds = 0.55f;

        [Tooltip("Seed for debris variation. Same seed + same bursts = identical footage.")]
        public int randomSeed = 4242;

        [SerializeField] ParticleSystem[] _debris;
        [SerializeField] ParticleSystem _sparks;
        [SerializeField] ParticleSystem _shockwave;

        System.Random _random;

        /// <summary>Number of debris shapes available to <see cref="Burst"/>.</summary>
        public int ShapeCount => _debris != null ? _debris.Length : 0;

        void Awake()
        {
            _random = new System.Random(randomSeed);
        }

        /// <summary>
        /// One segment breaking. <paramref name="intensity"/> scales the debris count — 1 for a full
        /// break, lower for a crack that leaves the segment standing. <paramref name="shape"/> picks
        /// the signature debris shape (wrapped to the available count); -1 mixes all shapes.
        /// </summary>
        public void Burst(Vector2 position, Vector2 outward, Color color, float intensity = 1f, int shape = -1)
        {
            if (_random == null)
            {
                _random = new System.Random(randomSeed);
            }

            if (ShapeCount == 0)
            {
                return;
            }

            int signature = shape >= 0 ? shape % ShapeCount : -1;
            int shards = Mathf.Max(1, Mathf.RoundToInt(shardsPerBurst * intensity));
            for (int i = 0; i < shards; i++)
            {
                int pick = signature >= 0 && Range(0f, 1f) < signatureShare
                    ? signature
                    : _random.Next(ShapeCount);

                // Mostly outward, with a wide spread, so debris reads as blown out of the wall.
                Vector2 direction = Rotate(outward, Range(-75f, 75f));
                float size = Range(shardSize.x, shardSize.y);
                var emit = new ParticleSystem.EmitParams
                {
                    position = position + Rotate(outward, 90f) * Range(-0.12f, 0.12f),
                    velocity = direction * Range(shardSpeed.x, shardSpeed.y),
                    startSize3D = new Vector3(size, size * Range(1f, 1.7f), 1f),
                    rotation3D = new Vector3(0f, 0f, Range(0f, 360f)),
                    angularVelocity3D = new Vector3(0f, 0f, Range(-540f, 540f)),
                    startLifetime = Range(shardLifetime.x, shardLifetime.y),
                    startColor = Vary(color)
                };
                _debris[pick].Emit(emit, 1);
            }

            int sparks = Mathf.RoundToInt(sparksPerBurst * intensity);
            for (int i = 0; i < sparks; i++)
            {
                Vector2 direction = Rotate(outward, Range(-60f, 60f));
                var emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = direction * Range(sparkSpeed.x, sparkSpeed.y),
                    startSize = Range(0.04f, 0.08f),
                    startLifetime = Range(sparkLifetime.x, sparkLifetime.y),
                    // Hot version of the wall's colour, not white: the sparks should say whose they are.
                    startColor = Color.Lerp(color, Color.white, 0.25f)
                };
                _sparks.Emit(emit, 1);
            }
        }

        /// <summary>An expanding ring centred on <paramref name="center"/>, ending at <paramref name="radius"/>.</summary>
        public void Shockwave(Vector2 center, float radius, Color color)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = center,
                startSize = radius * 2f,
                startLifetime = shockwaveSeconds,
                startColor = Color.Lerp(color, Color.white, 0.1f)
            };
            _shockwave.Emit(emit, 1);
        }

        /// <summary>Clear all debris — for a new run starting in the same scene.</summary>
        public void Clear()
        {
            _random = new System.Random(randomSeed);
            if (_debris != null)
            {
                foreach (ParticleSystem system in _debris)
                {
                    system.Clear();
                }
            }

            _sparks.Clear();
            _shockwave.Clear();
        }

        /// <summary>
        /// Create and configure the particle systems: one per debris shape, plus sparks and the
        /// shockwave. Called by a scene builder; safe to re-run.
        /// </summary>
        public void Apply(Texture[] debrisShapes, Texture dotTexture, Texture ringTexture, int sortingOrder)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            _debris = new ParticleSystem[debrisShapes.Length];
            for (int i = 0; i < debrisShapes.Length; i++)
            {
                ParticleSystem debris = CreateSystem($"Debris_{debrisShapes[i].name}", debrisShapes[i], sortingOrder, 10 + i);
                ParticleSystem.MainModule main = debris.main;
                main.startSize3D = true;
                main.startRotation3D = true;
                main.gravityModifier = shardGravity;
                main.maxParticles = 1500;
                FadeOut(debris, 0.6f);
                ParticleSystem.SizeOverLifetimeModule shrink = debris.sizeOverLifetime;
                shrink.enabled = true;
                shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
                ParticleSystem.LimitVelocityOverLifetimeModule drag = debris.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = 1.2f;
                _debris[i] = debris;
            }

            _sparks = CreateSystem("Sparks", dotTexture, sortingOrder + 1, 2);
            ParticleSystem.MainModule sparksMain = _sparks.main;
            sparksMain.maxParticles = 2000;
            FadeOut(_sparks, 0.3f);
            ParticleSystem.LimitVelocityOverLifetimeModule sparkDrag = _sparks.limitVelocityOverLifetime;
            sparkDrag.enabled = true;
            sparkDrag.drag = 4f;
            var sparkRenderer = _sparks.GetComponent<ParticleSystemRenderer>();
            // Streaks, not dots: stretched along velocity, so a spark reads as speed.
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = 0.06f;
            sparkRenderer.lengthScale = 1.5f;

            _shockwave = CreateSystem("Shockwave", ringTexture, sortingOrder - 1, 3);
            ParticleSystem.MainModule waveMain = _shockwave.main;
            waveMain.maxParticles = 32;
            FadeOut(_shockwave, 0f);
            ParticleSystem.SizeOverLifetimeModule grow = _shockwave.sizeOverLifetime;
            grow.enabled = true;
            // Fast out, easing to the full radius: a blast, not a ripple.
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.25f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
        }

        /// <summary>The wall's own colour, nudged per piece in hue and brightness, with the odd bright glint.</summary>
        Color Vary(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            h = Mathf.Repeat(h + Range(-hueJitter, hueJitter), 1f);
            v = Mathf.Clamp01(v * Range(1f - brightnessJitter, 1f + brightnessJitter));
            Color varied = Color.HSVToRGB(h, s, v);

            if (Range(0f, 1f) < glintChance)
            {
                varied = Color.Lerp(varied, Color.white, 0.55f);
            }

            varied.a = color.a;
            return varied;
        }

        ParticleSystem CreateSystem(string childName, Texture texture, int order, int seedOffset)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            var system = child.AddComponent<ParticleSystem>();

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = (uint)Mathf.Abs(randomSeed + seedOffset);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = order;
            // Sprites/Default rather than a URP particle shader: present in every pipeline, so the
            // effect can't break if render settings change between videos (same as AmbientDriftField).
            var material = new Material(Shader.Find("Sprites/Default")) { name = $"Shatter_{childName}" };
            material.mainTexture = texture;
            renderer.sharedMaterial = material;

            return system;
        }

        static void FadeOut(ParticleSystem system, float holdUntil)
        {
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        float Range(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
