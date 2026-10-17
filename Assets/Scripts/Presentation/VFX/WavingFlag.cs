using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A cloth flag: a subdivided quad (local XY, hoist edge at local x = 0, flying toward +X) whose vertices ripple in
    /// Update with the hoist edge pinned. Double-sided. The mesh lives on this object (never an asset); <see cref="Build"/>
    /// works in edit mode, and at runtime the component deforms its own copy.
    /// The quad is centred vertically on local y = 0 (y from -height/2 to +height/2), so the pole position sets the flag's middle.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WavingFlag : MonoBehaviour
    {
        [Min(0f)] public float waveAmplitude = 0.06f;
        [Min(0f)] public float waveSpeed = 2.2f;
        [Min(0.05f)] public float waveLength = 0.6f;

        [Tooltip("Phase offset so neighbouring flags do not wave in sync.")]
        public float phase;

        [Tooltip("How far the free end sags, as a fraction of the flag height.")]
        [Range(0f, 0.3f)] public float droop = 0.05f;

        [SerializeField, HideInInspector] Texture2D _texture;
        [SerializeField, HideInInspector] float _width;
        [SerializeField, HideInInspector] float _height;
        [SerializeField, HideInInspector] bool _flyLeft;
        [SerializeField, HideInInspector] int _segX;
        [SerializeField, HideInInspector] int _segY;

        Mesh _mesh;
        Material _material;
        Vector3[] _verts;
        float[] _restX;     // signed rest x per vertex
        float[] _restY;
        float[] _along;     // 0 at the hoist, 1 at the fly end

        /// <summary>
        /// Builds the mesh and a double-sided URP material showing <paramref name="texture"/>. With
        /// <paramref name="flyLeft"/> the cloth flies toward local -X from the hoist (x = 0). Texture u = 0 always stays at the
        /// pole, so a viewer on the local -Z side sees a left-flying flag's reverse (mirror image), exactly as with a real flag.
        /// </summary>
        public void Build(Texture2D texture, float width, float height, bool flyLeft = false, int segmentsX = 16, int segmentsY = 8)
        {
            _texture = texture;
            _width = Mathf.Max(0.01f, width);
            _height = Mathf.Max(0.01f, height);
            _flyLeft = flyLeft;
            _segX = Mathf.Clamp(segmentsX, 1, 64);
            _segY = Mathf.Clamp(segmentsY, 1, 64);
            Rebuild();
        }

        void OnEnable()
        {
            if (_width > 0f && (_mesh == null || _material == null))
            {
                Rebuild();
            }
        }

        void OnDestroy()
        {
            FxFactory.Destroy(_mesh);
            FxFactory.Destroy(_material);
            _mesh = null;
            _material = null;
        }

        void Update()
        {
            if (_mesh == null || !Application.isPlaying)
            {
                return;
            }

            Deform(Time.time);
        }

        void Rebuild()
        {
            FxFactory.Destroy(_mesh);
            FxFactory.Destroy(_material);

            int nx = _segX + 1, ny = _segY + 1;
            int count = nx * ny;
            _verts = new Vector3[count];
            _restX = new float[count];
            _restY = new float[count];
            _along = new float[count];
            var uvs = new Vector2[count];
            var tris = new int[_segX * _segY * 6];
            float dir = _flyLeft ? -1f : 1f;

            for (int j = 0; j < ny; j++)
            {
                float v = j / (float)_segY;
                for (int i = 0; i < nx; i++)
                {
                    float u = i / (float)_segX;
                    int idx = j * nx + i;
                    _restX[idx] = dir * u * _width;
                    _restY[idx] = (v - 0.5f) * _height;
                    _along[idx] = u;
                    _verts[idx] = new Vector3(_restX[idx], _restY[idx], 0f);
                    uvs[idx] = new Vector2(u, v);   // u = 0 stays at the pole in both fly directions
                }
            }

            int t = 0;
            for (int j = 0; j < _segY; j++)
            {
                for (int i = 0; i < _segX; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    if (_flyLeft)
                    {
                        tris[t++] = a; tris[t++] = b; tris[t++] = c;
                        tris[t++] = b; tris[t++] = d; tris[t++] = c;
                    }
                    else
                    {
                        tris[t++] = a; tris[t++] = c; tris[t++] = b;
                        tris[t++] = b; tris[t++] = c; tris[t++] = d;
                    }
                }
            }

            _mesh = new Mesh { name = "WavingFlag (runtime)", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            float pad = waveAmplitude + _height * 0.2f;
            _mesh.bounds = new Bounds(new Vector3(dir * _width * 0.5f, 0f, 0f), new Vector3(_width + pad, _height + pad, pad * 2f + 0.1f));

            // Unlit keeps the flag colours true; Cull Off makes the back readable (it shows the mirrored art, as a real flag does).
            Shader shader = FxFactory.FindUnlit();
            _material = new Material(shader) { name = "WavingFlag (runtime)", hideFlags = HideFlags.DontSave };
            if (_material.HasProperty("_Cull")) _material.SetFloat("_Cull", 0f);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Color.white);
            if (_texture != null)
            {
                if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", _texture);
                if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", _texture);
            }

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer r = GetComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            Deform(Application.isPlaying ? Time.time : 0f);
        }

        void Deform(float now)
        {
            float k = Mathf.PI * 2f / Mathf.Max(0.05f, waveLength);
            float w = waveSpeed * now - phase;
            for (int i = 0; i < _verts.Length; i++)
            {
                float env = _along[i];
                float dist = Mathf.Abs(_restX[i]);
                float z = waveAmplitude * env * Mathf.Sin(k * dist - w);
                float y = _restY[i] - droop * _height * env * env;
                _verts[i] = new Vector3(_restX[i], y, z);
            }

            _mesh.SetVertices(_verts);
        }
    }
}
