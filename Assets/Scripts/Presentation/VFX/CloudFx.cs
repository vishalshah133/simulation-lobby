using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A soft tinted storm cloud (a cluster of transparent unlit puffs that bob and drift slowly) hovering over a point.
    /// Several can be up at once; each <see cref="Show"/> returns a handle for <see cref="Hide"/>. Builds its own materials.
    /// Puffs are camera-facing quads with a generated soft radial texture, so they read as fluffy from any angle.
    /// </summary>
    public sealed class CloudFx : MonoBehaviour
    {
        const int PuffsPerCloud = 10;

        sealed class Cloud
        {
            public GameObject root;
            public Transform[] puffs;
            public Material[] materials;
            public Vector3[] basePos;     // local, in units of radius
            public float[] baseSize;      // in units of radius
            public float[] phase;
            public float[] lightness;     // 0 = dark underside, 1 = lit top
            public bool active;
            public float radius;
            public Color tint;
            public float alpha;
            public float target;
            public float fadeSeconds;
        }

        [Range(1, 8)] public int poolSize = 4;

        [Tooltip("Peak opacity of each puff (they overlap, so the cluster reads denser).")]
        [Range(0.1f, 1f)] public float puffOpacity = 0.55f;

        [Tooltip("How far the bobbing and drifting reaches, as a fraction of the cloud radius.")]
        [Range(0f, 0.3f)] public float motionAmount = 0.06f;

        Cloud[] _clouds;
        Mesh _quad;
        Texture2D _softTexture;
        Transform _camTransform;

        /// <summary>Fade a cloud in at <paramref name="centre"/>, about <paramref name="radiusWorld"/> across. Returns a handle (-1 if the pool is exhausted).</summary>
        public int Show(Vector3 centre, float radiusWorld, Color tint, float fadeSeconds = 0.5f)
        {
            EnsurePool();
            for (int i = 0; i < _clouds.Length; i++)
            {
                Cloud c = _clouds[i];
                if (c.active)
                {
                    continue;
                }

                c.active = true;
                c.radius = Mathf.Max(0.05f, radiusWorld);
                c.tint = tint;
                c.alpha = 0f;
                c.target = 1f;
                c.fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
                c.root.transform.position = centre;
                c.root.SetActive(true);
                Apply(c, Time.time);
                return i;
            }

            return -1;
        }

        /// <summary>Fade the cloud out. Unknown or already hidden handles are ignored.</summary>
        public void Hide(int handle, float fadeSeconds = 0.8f)
        {
            if (_clouds == null || handle < 0 || handle >= _clouds.Length)
            {
                return;
            }

            Cloud c = _clouds[handle];
            if (!c.active)
            {
                return;
            }

            c.target = 0f;
            c.fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
        }

        void LateUpdate()
        {
            if (_clouds == null)
            {
                return;
            }

            float now = Time.time;
            float dt = Time.deltaTime;
            for (int i = 0; i < _clouds.Length; i++)
            {
                Cloud c = _clouds[i];
                if (!c.active)
                {
                    continue;
                }

                c.alpha = Mathf.MoveTowards(c.alpha, c.target, dt / c.fadeSeconds);
                if (c.target <= 0f && c.alpha <= 0f)
                {
                    c.active = false;
                    c.root.SetActive(false);
                    continue;
                }

                Apply(c, now);
            }
        }

        void OnDestroy()
        {
            if (_clouds != null)
            {
                for (int i = 0; i < _clouds.Length; i++)
                {
                    for (int m = 0; m < _clouds[i].materials.Length; m++) FxFactory.Destroy(_clouds[i].materials[m]);
                    FxFactory.Destroy(_clouds[i].root);
                }
            }

            FxFactory.Destroy(_quad);
            FxFactory.Destroy(_softTexture);
            _clouds = null;
        }

        void Apply(Cloud c, float now)
        {
            Quaternion face = Quaternion.identity;
            if (_camTransform == null)
            {
                Camera cam = Camera.main;
                if (cam != null) _camTransform = cam.transform;
            }

            if (_camTransform != null) face = _camTransform.rotation;

            float eased = c.alpha * c.alpha * (3f - 2f * c.alpha);
            Color dark = Color.Lerp(c.tint, new Color(0.12f, 0.13f, 0.18f, 1f), 0.5f);
            Color light = Color.Lerp(c.tint, Color.white, 0.55f);
            for (int i = 0; i < PuffsPerCloud; i++)
            {
                float ph = c.phase[i];
                Vector3 p = c.basePos[i];
                p.x += Mathf.Sin(now * 0.31f + ph) * motionAmount * 1.5f;
                p.y += Mathf.Sin(now * 0.8f + ph * 1.7f) * motionAmount;
                p.z += Mathf.Cos(now * 0.23f + ph) * motionAmount;

                Transform t = c.puffs[i];
                t.localPosition = p * c.radius;
                t.rotation = face;
                float size = c.baseSize[i] * c.radius * (1f + 0.04f * Mathf.Sin(now * 0.6f + ph));
                t.localScale = new Vector3(size, size, 1f);

                Color col = Color.Lerp(dark, light, c.lightness[i]);
                col.a = puffOpacity * eased;
                FxFactory.SetColor(c.materials[i], col);
            }
        }

        void EnsurePool()
        {
            if (_clouds != null)
            {
                return;
            }

            _quad = BuildQuad();
            _softTexture = BuildSoftTexture();

            _clouds = new Cloud[poolSize];
            for (int i = 0; i < _clouds.Length; i++)
            {
                var c = new Cloud();
                c.root = new GameObject("Cloud " + i.ToString("00"));
                c.root.transform.SetParent(transform, false);
                c.puffs = new Transform[PuffsPerCloud];
                c.materials = new Material[PuffsPerCloud];
                c.basePos = new Vector3[PuffsPerCloud];
                c.baseSize = new float[PuffsPerCloud];
                c.phase = new float[PuffsPerCloud];
                c.lightness = new float[PuffsPerCloud];

                // Fixed layout (a cheap LCG, so every cloud is the same recognisable shape but each is offset in phase).
                uint state = 12345u + (uint)i * 977u;
                for (int p = 0; p < PuffsPerCloud; p++)
                {
                    float a = (p / (float)PuffsPerCloud) * Mathf.PI * 2f + Next(ref state) * 0.5f;
                    float ring = p == 0 ? 0f : 0.45f + 0.4f * Next(ref state);
                    float y = 0.05f + 0.3f * Next(ref state) + (p == 0 ? 0.15f : 0f);
                    c.basePos[p] = new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring * 0.65f);
                    c.baseSize[p] = (p == 0 ? 1.0f : 0.7f + 0.3f * Next(ref state)) * 1.05f;
                    c.phase[p] = Next(ref state) * 6.28f;
                    c.lightness[p] = Mathf.Clamp01(y / 0.4f);

                    var go = new GameObject("Puff");
                    go.transform.SetParent(c.root.transform, false);
                    Material m = CreateAlphaMaterial("CloudPuff (runtime)", _softTexture);
                    FxFactory.AddMesh(go, _quad, m);
                    c.puffs[p] = go.transform;
                    c.materials[p] = m;
                }

                c.root.SetActive(false);
                _clouds[i] = c;
            }
        }

        static float Next(ref uint s)
        {
            s = s * 1664525u + 1013904223u;
            return ((s >> 8) & 0xFFFF) / 65535f;
        }

        static Material CreateAlphaMaterial(string name, Texture2D tex)
        {
            Material m = FxFactory.CreateGlowMaterial(name);
            if (m == null)
            {
                return null;
            }

            m.SetFloat("_Blend", 0f);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)RenderQueue.Transparent;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            return m;
        }

        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "CloudPuffQuad (runtime)", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Texture2D BuildSoftTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "CloudSoft (runtime)",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
