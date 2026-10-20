using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// One drawn marble: a glass shell around a white HDR core, a soft halo, a trail tinted by the last note's
    /// hue and (for the hero) a small point light that travels with it and lights the nearby pads. Canon voices
    /// are the same marble at 85% scale. Position and visibility are pushed in by the presenter each frame.
    /// </summary>
    internal sealed class MarbleView
    {
        static MaterialPropertyBlock _block;
        // Lazy: Unity forbids creating one while a MonoBehaviour is being constructed (static init runs then).
        static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        readonly GameObject _root;
        readonly Transform _halo;
        readonly MeshRenderer _haloRenderer;
        readonly TrailRenderer _trail;
        readonly Light _light;
        readonly float _radius;
        readonly float _baseScale;
        readonly Gradient _gradient = new Gradient();
        readonly GradientColorKey[] _colourKeys = new GradientColorKey[2];
        readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];

        Color _hue = Color.white;
        bool _wasActive;
        float _lastFade = -1f;

        public bool Active => _wasActive;
        public Vector3 Position => _root.transform.position;

        public MarbleView(Transform parent, int voice, FreefallConfig config, FreefallMaterials mats)
        {
            _radius = config.marbleRadius;
            _baseScale = voice == 0 ? 1f : 0.85f;
            _root = new GameObject(voice == 0 ? "Marble (hero)" : "Marble (voice " + voice + ")");
            _root.transform.SetParent(parent, false);

            // Shell: glass sphere at the marble radius. Core: a white HDR sphere inside it.
            Mesh sphere = FreefallMeshes.Sphere();
            MeshRenderer shell = AddMesh("Shell", sphere, mats.marbleGlass);
            shell.transform.localScale = Vector3.one * (_radius * 2f);
            MeshRenderer core = AddMesh("Core", sphere, mats.marbleCore);
            core.transform.localScale = Vector3.one * (_radius * 2f * 0.62f);
            Block.Clear();
            Block.SetColor(FreefallColors.BaseColor, new Color(6f, 6f, 6f, 1f));
            core.SetPropertyBlock(Block);

            MeshRenderer halo = AddMesh("Halo", FreefallMeshes.Quad(), mats.glowSoft);
            _halo = halo.transform;
            _haloRenderer = halo;

            _trail = _root.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = mats.glowFlat;
            _trail.time = 0.85f;
            _trail.minVertexDistance = 0.015f;
            _trail.alignment = LineAlignment.View;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            // Half the marble's width and pinched at the head, so the white marble reads as a dot ahead of a
            // coloured ribbon instead of the bright end of a stick. Kept below bloom threshold: it's a guide, not a light.
            _trail.widthMultiplier = _radius * 1.0f * _baseScale;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.06f, 1f), new Keyframe(1f, 0f));
            Block.Clear();
            Block.SetColor(FreefallColors.BaseColor, new Color(1.1f, 1.1f, 1.1f, 1f));
            _trail.SetPropertyBlock(Block);
            _trail.emitting = false;

            if (voice == 0)
            {
                var lightObject = new GameObject("Marble Light");
                lightObject.transform.SetParent(_root.transform, false);
                _light = lightObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.range = 5.5f;
                // Low: it sits a hair above glossy pads, and anything brighter blooms a hotspot right under the marble.
                _light.intensity = 0.2f;
                _light.shadows = LightShadows.None;
                _light.color = Color.white;
            }

            _root.SetActive(false);
        }

        MeshRenderer AddMesh(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        /// <summary>
        /// Push this frame's state. <paramref name="fade"/> is 1 for a full marble and shrinks to 0 as a canon voice
        /// merges into the hero.
        /// </summary>
        public void Set(bool active, Vector3 position, float fade, Color hueTarget, float dt)
        {
            if (!active || fade <= 0.01f)
            {
                if (_wasActive)
                {
                    _root.SetActive(false);
                    _trail.emitting = false;
                    _wasActive = false;
                }

                return;
            }

            if (!_wasActive)
            {
                // Appearing (or reappearing): jump to the spot without dragging a trail across the well.
                _root.SetActive(true);
                _root.transform.position = position;
                _trail.Clear();
                _trail.emitting = true;
                _wasActive = true;
                _hue = hueTarget;
                _lastFade = -1f;
            }

            _root.transform.position = position;
            _hue = Color.Lerp(_hue, hueTarget, 1f - Mathf.Exp(-dt * 9f));

            if (!Mathf.Approximately(fade, _lastFade))
            {
                _lastFade = fade;
                _root.transform.localScale = Vector3.one * (_baseScale * Mathf.Lerp(0.35f, 1f, fade));
            }

            // Trail: the pure hue of the last note (no white), fading to nothing at the tail. Only the marble is white.
            _colourKeys[0] = new GradientColorKey(_hue, 0f);
            _colourKeys[1] = new GradientColorKey(_hue, 1f);
            _alphaKeys[0] = new GradientAlphaKey(0.55f * fade, 0f);
            _alphaKeys[1] = new GradientAlphaKey(0f, 1f);
            _gradient.SetKeys(_colourKeys, _alphaKeys);
            _trail.colorGradient = _gradient;

            Block.Clear();
            Block.SetColor(FreefallColors.BaseColor, FreefallColors.Hdr(Color.Lerp(Color.white, _hue, 0.6f), 1.4f, 0.3f * fade));
            _haloRenderer.SetPropertyBlock(Block);

            if (_light != null)
            {
                _light.color = Color.Lerp(Color.white, _hue, 0.5f);
                _light.intensity = 0.2f * fade;
            }
        }

        /// <summary>Turn the halo quad to face the camera.</summary>
        public void Billboard(Camera camera)
        {
            if (!_wasActive || camera == null)
            {
                return;
            }

            _halo.rotation = camera.transform.rotation;
            _halo.localScale = Vector3.one * (_radius * 2f * 3f);
        }

        /// <summary>Flash the light and halo on contact (the hero only has a light).</summary>
        public void Kick(Color hue)
        {
            _hue = Color.Lerp(_hue, hue, 0.8f);
        }
    }

    /// <summary>
    /// A global spark burst plus a pool of expanding ripple rings, fired at each contact. Crystal pads spray
    /// quick sparkles; every pad gets a small puff; lily pads also send concentric ripples across the pad.
    /// </summary>
    internal sealed class HitEffects
    {
        sealed class Ripple
        {
            public Transform transform;
            public MeshRenderer renderer;
            public float start = -10f;
            public float delay;
            public float radius;
            public Color colour;
        }

        static MaterialPropertyBlock _block;
        // Lazy: Unity forbids creating one while a MonoBehaviour is being constructed (static init runs then).
        static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        readonly ParticleSystem _burst;
        readonly Ripple[] _ripples = new Ripple[14];
        int _nextRipple;

        public HitEffects(Transform parent, FreefallMaterials mats)
        {
            _burst = MistWall.MakeSystem(parent, "Hit Burst", mats.glowSoft, FreefallColors.Hdr(Color.white, 2.6f), 700);
            ParticleSystem.MainModule main = _burst.main;
            main.maxParticles = 700;
            ParticleSystem.ColorOverLifetimeModule over = _burst.colorOverLifetime;
            over.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            over.color = gradient;
            ParticleSystem.SizeOverLifetimeModule size = _burst.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));

            Mesh ring = FreefallMeshes.FlatRing(0.88f);
            for (int i = 0; i < _ripples.Length; i++)
            {
                var go = new GameObject("Ripple");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = ring;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mats.glowFlat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                _ripples[i] = new Ripple { transform = go.transform, renderer = renderer };
            }
        }

        public void Spawn(in FreefallHit hit, FreefallWorld world, FreefallConfig config, Color hue, float time, System.Random rng)
        {
            Vector3 normal = hit.normal.sqrMagnitude > 1e-6f ? hit.normal.normalized : Vector3.up;
            Vector3 surface = hit.position - normal * config.marbleRadius;

            bool crystal = world.padShape == PadShape.Crystal;
            int count = crystal ? 16 : 6;
            float speed = crystal ? 2.6f : 1.3f;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                // A cone of sparks about the pad normal.
                Vector3 random = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                Vector3 direction = (normal * 1.1f + random * 1.7f).normalized;
                emit.position = surface + normal * 0.05f;
                emit.velocity = direction * speed * (0.5f + (float)rng.NextDouble());
                emit.startSize = (crystal ? 0.07f : 0.06f) * (0.6f + (float)rng.NextDouble());
                emit.startLifetime = crystal ? 0.75f : 0.5f;
                Color c = Color.Lerp(hue, Color.white, 0.35f);
                emit.startColor = new Color(c.r, c.g, c.b, 1f);
                _burst.Emit(emit, 1);
            }

            if (world.padShape == PadShape.Lily)
            {
                for (int k = 0; k < 3; k++)
                {
                    Ripple ripple = _ripples[_nextRipple];
                    _nextRipple = (_nextRipple + 1) % _ripples.Length;
                    ripple.start = time;
                    ripple.delay = k * 0.16f;
                    ripple.radius = world.padRadius * 1.15f * (k == 0 ? 1.55f : 1.3f);
                    ripple.colour = hue;
                    ripple.transform.position = surface + normal * 0.012f;
                    ripple.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                    ripple.renderer.enabled = true;
                }
            }
        }

        public void Tick(float time)
        {
            const float life = 0.95f;
            for (int i = 0; i < _ripples.Length; i++)
            {
                Ripple r = _ripples[i];
                if (!r.renderer.enabled)
                {
                    continue;
                }

                float age = time - r.start - r.delay;
                if (age < 0f)
                {
                    r.transform.localScale = Vector3.zero;
                    continue;
                }

                float u = age / life;
                if (u >= 1f)
                {
                    r.renderer.enabled = false;
                    continue;
                }

                float scale = r.radius * (0.2f + 1.0f * FreefallColors.EaseOut(u));
                r.transform.localScale = new Vector3(scale, scale, scale);
                float alpha = (1f - u) * (1f - u) * 0.9f;
                Block.Clear();
                Block.SetColor(FreefallColors.BaseColor, FreefallColors.Hdr(Color.Lerp(r.colour, Color.white, 0.1f), 1.4f, alpha));
                r.renderer.SetPropertyBlock(Block);
            }
        }

        public void Destroy()
        {
            if (_burst != null)
            {
                Object.Destroy(_burst.gameObject);
            }

            for (int i = 0; i < _ripples.Length; i++)
            {
                if (_ripples[i].transform != null)
                {
                    Object.Destroy(_ripples[i].transform.gameObject);
                }
            }
        }
    }
}
