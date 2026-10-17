using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A rising column of dark smoke with a few orange embers, from a point on the ground, for a wrecked object.
    /// Code-built ParticleSystems, pooled. <see cref="Begin"/> returns a handle; <see cref="End"/> stops emission and
    /// lets the live particles fade (never pops).
    /// </summary>
    public sealed class SmokePlumeFx : MonoBehaviour
    {
        sealed class Plume
        {
            public GameObject root;
            public ParticleSystem smoke;
            public ParticleSystem embers;
            public bool active;
            public bool ending;
        }

        [Range(1, 8)] public int poolSize = 4;

        [Tooltip("Smoke puffs per second at scale 1.")]
        [Min(1f)] public float smokeRate = 14f;

        [Tooltip("Rise height of the column in metres at scale 1.")]
        [Min(0.5f)] public float riseHeight = 2.5f;

        [Tooltip("Embers per second at scale 1.")]
        [Min(0f)] public float emberRate = 5f;

        [Tooltip("Smoke brightness (0 black, 1 white). Mid-dark grey reads on both bright and dark ground.")]
        [Range(0f, 1f)] public float smokeShade = 0.22f;

        const float SmokeLifetime = 3f;

        Plume[] _plumes;
        Material _smokeMaterial;
        Material _emberMaterial;
        Texture2D _softTexture;

        /// <summary>Start a plume. <paramref name="scale"/> 1 suits an object about 1.5 m wide. Returns a handle (-1 if exhausted).</summary>
        public int Begin(Vector3 worldPosition, float scale = 1f)
        {
            EnsurePool();
            scale = Mathf.Max(0.1f, scale);
            for (int i = 0; i < _plumes.Length; i++)
            {
                Plume p = _plumes[i];
                if (p.active && p.ending && !p.smoke.IsAlive(true) && !p.embers.IsAlive(true))
                {
                    p.active = false;
                }

                if (p.active)
                {
                    continue;
                }

                p.active = true;
                p.ending = false;
                p.root.transform.position = worldPosition;
                p.root.SetActive(true);
                Configure(p, scale);
                p.smoke.Clear(true);
                p.embers.Clear(true);
                p.smoke.Play(true);
                p.embers.Play(true);
                return i;
            }

            return -1;
        }

        public void End(int handle)
        {
            if (_plumes == null || handle < 0 || handle >= _plumes.Length)
            {
                return;
            }

            Plume p = _plumes[handle];
            if (!p.active || p.ending)
            {
                return;
            }

            p.ending = true;
            p.smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            p.embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void Update()
        {
            if (_plumes == null)
            {
                return;
            }

            for (int i = 0; i < _plumes.Length; i++)
            {
                Plume p = _plumes[i];
                if (p.active && p.ending && !p.smoke.IsAlive(true) && !p.embers.IsAlive(true))
                {
                    p.active = false;
                    p.root.SetActive(false);
                }
            }
        }

        void OnDestroy()
        {
            if (_plumes != null)
            {
                for (int i = 0; i < _plumes.Length; i++) FxFactory.Destroy(_plumes[i].root);
            }

            FxFactory.Destroy(_smokeMaterial);
            FxFactory.Destroy(_emberMaterial);
            FxFactory.Destroy(_softTexture);
            _plumes = null;
        }

        void Configure(Plume p, float scale)
        {
            // Smoke: rise riseHeight over its life while growing; size and spread follow the object scale.
            ParticleSystem.MainModule m = p.smoke.main;
            m.startSize = new ParticleSystem.MinMaxCurve(0.55f * scale, 0.9f * scale);
            m.startSpeed = new ParticleSystem.MinMaxCurve(riseHeight * scale / SmokeLifetime * 0.85f, riseHeight * scale / SmokeLifetime * 1.15f);
            ParticleSystem.EmissionModule e = p.smoke.emission;
            e.rateOverTime = smokeRate * Mathf.Sqrt(scale);
            ParticleSystem.ShapeModule sh = p.smoke.shape;
            sh.radius = 0.25f * scale;
            ParticleSystem.VelocityOverLifetimeModule v = p.smoke.velocityOverLifetime;
            v.x = new ParticleSystem.MinMaxCurve(0.18f * scale, 0.4f * scale);

            ParticleSystem.MainModule em = p.embers.main;
            em.startSize = new ParticleSystem.MinMaxCurve(0.06f * scale, 0.11f * scale);
            em.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.4f * scale);
            ParticleSystem.EmissionModule ee = p.embers.emission;
            ee.rateOverTime = emberRate * Mathf.Sqrt(scale);
            ParticleSystem.ShapeModule es = p.embers.shape;
            es.radius = 0.2f * scale;
        }

        void EnsurePool()
        {
            if (_plumes != null)
            {
                return;
            }

            _softTexture = CloudFx.BuildSoftTexture();
            _smokeMaterial = CreateParticleMaterial("SmokePuff (runtime)", _softTexture, false);
            _emberMaterial = CreateParticleMaterial("SmokeEmber (runtime)", _softTexture, true);

            _plumes = new Plume[poolSize];
            for (int i = 0; i < _plumes.Length; i++)
            {
                var p = new Plume();
                p.root = new GameObject("Plume " + i.ToString("00"));
                p.root.transform.SetParent(transform, false);
                p.smoke = BuildSmoke(p.root.transform, 1000 + i);
                p.embers = BuildEmbers(p.root.transform, 5000 + i);
                p.root.SetActive(false);
                _plumes[i] = p;
            }
        }

        ParticleSystem BuildSmoke(Transform parent, int seed)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)seed;

            ParticleSystem.MainModule m = ps.main;
            m.duration = 5f;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(SmokeLifetime * 0.85f, SmokeLifetime * 1.1f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 0f;
            m.maxParticles = 120;
            m.startColor = new Color(smokeShade, smokeShade, smokeShade * 1.05f, 1f);

            ParticleSystem.EmissionModule e = ps.emission;
            e.enabled = true;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 7f;
            sh.rotation = new Vector3(-90f, 0f, 0f);   // cone emits along +Z; turn it to +Y

            ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            v.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.35f, 0.85f), new Keyframe(1f, 1.6f)));

            ParticleSystem.ColorOverLifetimeModule c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.85f, 0.9f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            c.color = g;

            ParticleSystem.RotationOverLifetimeModule r = ps.rotationOverLifetime;
            r.enabled = true;
            r.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = _smokeMaterial;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        ParticleSystem BuildEmbers(Transform parent, int seed)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)seed;

            ParticleSystem.MainModule m = ps.main;
            m.duration = 5f;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            m.gravityModifier = 0.15f;
            m.maxParticles = 40;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.12f, 1f), new Color(1f, 0.8f, 0.3f, 1f));

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 28f;
            sh.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));

            ParticleSystem.ColorOverLifetimeModule c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            c.color = g;

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = _emberMaterial;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        static Material CreateParticleMaterial(string name, Texture2D tex, bool additive)
        {
            Material m = FxFactory.CreateParticleMaterial(name);
            if (m == null)
            {
                return null;
            }

            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_ALPHAADDITIVE_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            return m;
        }
    }
}
