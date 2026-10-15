using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Presentation
{
    /// <summary>Runtime mesh and material builders shared by the pooled power-action effects. Nothing is saved as an asset.</summary>
    internal static class FxFactory
    {
        public static Shader FindUnlit()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        /// <summary>Transparent, additive-looking, two-sided, no depth write.</summary>
        public static Material CreateGlowMaterial(string name)
        {
            Shader shader = FindUnlit();
            if (shader == null)
            {
                return null;
            }

            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        public static Material CreateParticleMaterial(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = FindUnlit();
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                return null;
            }

            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static Mesh CreateCube(string name)
        {
            var vertices = new Vector3[24];
            var triangles = new int[36];
            Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int v = 0, t = 0;
            foreach (Vector3 n in normals)
            {
                Vector3 a = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 r = Vector3.Cross(a, n).normalized;
                Vector3 u = Vector3.Cross(n, r);
                vertices[v] = (n - r - u) * 0.5f;
                vertices[v + 1] = (n - r + u) * 0.5f;
                vertices[v + 2] = (n + r + u) * 0.5f;
                vertices[v + 3] = (n + r - u) * 0.5f;
                triangles[t++] = v; triangles[t++] = v + 1; triangles[t++] = v + 2;
                triangles[t++] = v; triangles[t++] = v + 2; triangles[t++] = v + 3;
                v += 4;
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Unit-radius annulus on the XZ plane, visible from above and below (duplicate winding).</summary>
        public static Mesh CreateAnnulus(string name, float innerRatio, int segments)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                vertices[i * 2] = new Vector3(c, 0f, s);
                vertices[i * 2 + 1] = new Vector3(c * innerRatio, 0f, s * innerRatio);
            }

            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                int o0 = i * 2, i0 = i * 2 + 1, o1 = j * 2, i1 = j * 2 + 1;
                triangles[ti++] = o0; triangles[ti++] = o1; triangles[ti++] = i0;
                triangles[ti++] = i0; triangles[ti++] = o1; triangles[ti++] = i1;
                triangles[ti++] = o0; triangles[ti++] = i0; triangles[ti++] = o1;
                triangles[ti++] = i0; triangles[ti++] = i1; triangles[ti++] = o1;
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateSphere(string name)
        {
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh copy = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            copy.name = name;
            copy.hideFlags = HideFlags.DontSave;
            if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
            return copy;
        }

        public static MeshRenderer AddMesh(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }

    /// <summary>
    /// Pooled explosion: expanding ground shockwave ring, a short bright flash sphere and a burst of small cubes
    /// thrown up and falling. Several can play at once. Presentation only; it never touches simulation state.
    /// Positions are WORLD space, so this component may sit anywhere.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlastFx : MonoBehaviour
    {
        const float FlashSeconds = 0.2f;
        const float DebrisSeconds = 1.2f;

        sealed class Slot
        {
            public GameObject root;
            public Transform ring;
            public Transform flash;
            public Material ringMaterial;
            public Material flashMaterial;
            public ParticleSystem debris;
            public bool active;
            public float birth;
            public float seconds;
            public float radius;
            public Color color;
        }

        [Range(1, 24)] public int poolSize = 6;
        [Min(1f)] public float ringIntensity = 1.6f;
        [Range(0.05f, 1f)] public float ringThickness = 0.22f;
        [Range(0.1f, 1f)] public float flashSizeFraction = 0.35f;

        Slot[] _slots;
        int _next;
        Mesh _ringMesh;
        Mesh _sphereMesh;
        Mesh _cubeMesh;
        Material _debrisMaterial;

        public void Play(Vector3 worldCenter, float radiusWorld, Color color, float seconds = 0.7f)
        {
            EnsurePool();
            if (_slots.Length == 0)
            {
                return;
            }

            Slot slot = null;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].active) { slot = _slots[i]; break; }
            }

            if (slot == null)
            {
                slot = _slots[_next];
                _next = (_next + 1) % _slots.Length;
            }

            slot.active = true;
            slot.birth = Time.time;
            slot.seconds = Mathf.Max(0.05f, seconds);
            slot.radius = Mathf.Max(0.01f, radiusWorld);
            slot.color = color;
            slot.root.transform.position = worldCenter;
            slot.root.SetActive(true);
            slot.ring.localPosition = new Vector3(0f, 0.03f, 0f);
            ApplySlot(slot, 0f);

            ParticleSystem ps = slot.debris;
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.startColor = color;
                main.startSize = new ParticleSystem.MinMaxCurve(slot.radius * 0.03f, slot.radius * 0.07f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(slot.radius * 1.2f, slot.radius * 2.6f);
                ps.Play(true);
            }
        }

        void Update()
        {
            if (_slots == null)
            {
                return;
            }

            float now = Time.time;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (!s.active)
                {
                    continue;
                }

                float elapsed = now - s.birth;
                if (elapsed >= Mathf.Max(s.seconds, DebrisSeconds + 0.1f))
                {
                    s.active = false;
                    s.root.SetActive(false);
                    continue;
                }

                ApplySlot(s, elapsed);
            }
        }

        void OnDestroy()
        {
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    FxFactory.Destroy(_slots[i].ringMaterial);
                    FxFactory.Destroy(_slots[i].flashMaterial);
                    FxFactory.Destroy(_slots[i].root);
                }
            }

            FxFactory.Destroy(_ringMesh);
            FxFactory.Destroy(_sphereMesh);
            FxFactory.Destroy(_cubeMesh);
            FxFactory.Destroy(_debrisMaterial);
            _slots = null;
        }

        void ApplySlot(Slot s, float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / s.seconds);
            float eased = 1f - (1f - t) * (1f - t);

            // Ring: ~0 to radius, ease-out, fading.
            float r = Mathf.Max(0.001f, s.radius * eased);
            s.ring.localScale = new Vector3(r, 1f, r);
            float ringAlpha = Mathf.Clamp01(1f - t);
            ringAlpha *= ringAlpha;
            FxFactory.SetColor(s.ringMaterial, new Color(s.color.r * ringIntensity, s.color.g * ringIntensity, s.color.b * ringIntensity, ringAlpha));

            // Flash: pops quickly and fades.
            float f = Mathf.Clamp01(elapsed / FlashSeconds);
            if (f >= 1f)
            {
                if (s.flash.gameObject.activeSelf) s.flash.gameObject.SetActive(false);
            }
            else
            {
                if (!s.flash.gameObject.activeSelf) s.flash.gameObject.SetActive(true);
                float pop = 1f - (1f - f) * (1f - f);
                float size = s.radius * flashSizeFraction * Mathf.Lerp(0.4f, 1.2f, pop) * 2f;
                s.flash.localScale = new Vector3(size, size, size);
                float a = 1f - f;
                FxFactory.SetColor(s.flashMaterial, new Color(Mathf.Lerp(s.color.r, 1f, 0.6f), Mathf.Lerp(s.color.g, 1f, 0.6f), Mathf.Lerp(s.color.b, 1f, 0.6f), a));
            }
        }

        void EnsurePool()
        {
            if (_slots != null)
            {
                return;
            }

            _ringMesh = FxFactory.CreateAnnulus("BlastRing (runtime)", 1f - ringThickness, 64);
            _sphereMesh = FxFactory.CreateSphere("BlastFlash (runtime)");
            _cubeMesh = FxFactory.CreateCube("BlastDebris (runtime)");
            _debrisMaterial = FxFactory.CreateParticleMaterial("BlastDebris (runtime)");

            _slots = new Slot[poolSize];
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = new Slot();
                slot.root = new GameObject("Blast " + i.ToString("00"));
                slot.root.transform.SetParent(transform, false);

                var ring = new GameObject("Ring");
                ring.transform.SetParent(slot.root.transform, false);
                slot.ringMaterial = FxFactory.CreateGlowMaterial("BlastRing (runtime)");
                FxFactory.AddMesh(ring, _ringMesh, slot.ringMaterial);
                slot.ring = ring.transform;

                var flash = new GameObject("Flash");
                flash.transform.SetParent(slot.root.transform, false);
                slot.flashMaterial = FxFactory.CreateGlowMaterial("BlastFlash (runtime)");
                FxFactory.AddMesh(flash, _sphereMesh, slot.flashMaterial);
                slot.flash = flash.transform;

                BuildDebris(slot);
                slot.root.SetActive(false);
                _slots[i] = slot;
            }
        }

        void BuildDebris(Slot slot)
        {
            var go = new GameObject("Debris");
            go.transform.SetParent(slot.root.transform, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // cone emits along +Z; point it up

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = DebrisSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = DebrisSeconds;
            main.gravityModifier = 1.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)30) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.05f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = _cubeMesh;
            pr.sharedMaterial = _debrisMaterial;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;

            slot.debris = ps;
        }
    }
}
