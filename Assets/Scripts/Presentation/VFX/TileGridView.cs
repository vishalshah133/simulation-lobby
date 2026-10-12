using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A grid of bevelled tiles drawn as ONE runtime-generated mesh, coloured by owner through a
    /// point-filtered texture with one texel per tile. Recolouring the whole board is a texture upload,
    /// not a mesh rebuild. Format-agnostic ("owners", "cells"); a thin binder in the format pushes the
    /// ownership list in. Presentation only: it reads the list it is handed and never writes it.
    /// </summary>
    /// <remarks>
    /// The mesh and texture are created at runtime and never saved as assets, so a layout change can
    /// never leave a stale mesh on disk drawing the previous build's board.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TileGridView : MonoBehaviour
    {
        const int VerticesPerTile = 20;
        const int IndicesPerTile = 30;

        sealed class PulseSlot
        {
            public Transform transform;
            public Renderer renderer;
            public float birth;
            public Color color;
            public bool active;
        }

        [Header("Pulse")]
        [Range(1, 256)] public int pulsePoolSize = 64;
        [Min(0.05f)] public float pulseSeconds = 0.35f;
        [Min(1f)] public float pulsePopScale = 1.35f;
        [Min(1f)] public float pulseIntensity = 2.2f;
        [Tooltip("Bevel width as a fraction of the tile footprint.")]
        [Range(0f, 0.3f)] public float bevel = 0.09f;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        int _width;
        int _height;
        float _cellSize;
        float _tileHeight;
        float _footprint;
        Color32[] _palette = new Color32[0];
        Color32 _neutral;

        Mesh _mesh;
        Texture2D _texture;
        Material _material;
        Color32[] _pixels = new Color32[0];
        int _lastVersion = int.MinValue;

        // Reveal holds: _pixels is what is DISPLAYED, _target is the latest owner colour per cell.
        Color32[] _target = new Color32[0];
        float[] _revealAt = new float[0]; // -1 = no hold
        Color32[] _holdColor = new Color32[0];
        bool[] _holdPulse = new bool[0];
        int[] _active = new int[0];
        int _activeCount;

        PulseSlot[] _pulses = new PulseSlot[0];
        int _nextPulse;
        Material _pulseMaterial;
        Mesh _pulseMesh;
        MaterialPropertyBlock _block;

        public int Width => _width;
        public int Height => _height;

        public void Build(int width, int height, float cellSize, float tileHeight, float gap,
                          Color[] palette, Color neutral, Material material)
        {
            Cleanup();

            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _cellSize = cellSize;
            _tileHeight = tileHeight;
            _footprint = Mathf.Max(0.001f, cellSize - gap);
            _neutral = neutral;
            _palette = new Color32[palette != null ? palette.Length : 0];
            for (int i = 0; i < _palette.Length; i++)
            {
                _palette[i] = palette[i];
            }

            BuildMesh();
            BuildTexture(material);
            BuildPulsePool(material);
            int cells = _width * _height;
            _target = new Color32[cells];
            _holdColor = new Color32[cells];
            _holdPulse = new bool[cells];
            _revealAt = new float[cells];
            for (int i = 0; i < cells; i++)
            {
                _target[i] = _neutral;
                _revealAt[i] = -1f;
            }

            _active = new int[Mathf.Min(cells, 1024)];
            _activeCount = 0;
            _lastVersion = int.MinValue;
        }

        /// <summary>Re-uploads the owner texture, but only when <paramref name="version"/> differs from the last one seen.</summary>
        public void SetOwners(IReadOnlyList<int> owners, int version)
        {
            if (_texture == null || owners == null || version == _lastVersion)
            {
                return;
            }

            _lastVersion = version;
            int count = Mathf.Min(_pixels.Length, owners.Count);
            for (int i = 0; i < count; i++)
            {
                int owner = owners[i];
                Color32 c = owner >= 0 && owner < _palette.Length ? _palette[owner] : _neutral;
                _target[i] = c;
                _pixels[i] = _revealAt[i] >= 0f ? _holdColor[i] : c;
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        /// <summary>
        /// Each listed cell keeps its currently displayed colour until its delay (seconds from now) elapses, then shows
        /// the owner colour from the latest <see cref="SetOwners"/> (with a <see cref="Pulse"/> if asked). Call this
        /// BEFORE the SetOwners that uploads the change. A newer hold on the same cell replaces the older one.
        /// </summary>
        public void HoldAndReveal(IReadOnlyList<int> cells, IReadOnlyList<float> delays, bool pulse = true)
        {
            if (_texture == null || cells == null)
            {
                return;
            }

            int total = _width * _height;
            float now = Time.time;
            int n = cells.Count;
            for (int i = 0; i < n; i++)
            {
                int cell = cells[i];
                if (cell < 0 || cell >= total)
                {
                    continue;
                }

                float delay = delays != null && i < delays.Count ? Mathf.Max(0f, delays[i]) : 0f;
                if (_revealAt[cell] < 0f)
                {
                    _holdColor[cell] = _pixels[cell];
                    if (_activeCount == _active.Length)
                    {
                        System.Array.Resize(ref _active, Mathf.Max(16, _active.Length * 2));
                    }

                    _active[_activeCount++] = cell;
                }

                _revealAt[cell] = now + delay;
                _holdPulse[cell] = pulse;
            }
        }

        void ProcessHolds()
        {
            if (_activeCount == 0)
            {
                return;
            }

            float now = Time.time;
            bool changed = false;
            int i = 0;
            while (i < _activeCount)
            {
                int cell = _active[i];
                if (_revealAt[cell] > now)
                {
                    i++;
                    continue;
                }

                _revealAt[cell] = -1f;
                _pixels[cell] = _target[cell];
                changed = true;
                if (_holdPulse[cell])
                {
                    Pulse(cell, _target[cell]);
                }

                _active[i] = _active[--_activeCount];
            }

            if (changed)
            {
                _texture.SetPixels32(_pixels);
                _texture.Apply(false);
            }
        }

        /// <summary>Pop-and-fade overlay on one tile. Capped by the pool: when it is full the oldest is recycled.</summary>
        public void Pulse(int cellIndex, Color color)
        {
            if (_pulses.Length == 0 || cellIndex < 0 || cellIndex >= _width * _height)
            {
                return;
            }

            PulseSlot slot = _pulses[_nextPulse];
            _nextPulse = (_nextPulse + 1) % _pulses.Length;

            int x = cellIndex % _width;
            int y = cellIndex / _width;
            Vector3 center = CellCenterLocal(x, y);
            slot.transform.localPosition = center + Vector3.up * (_footprint * 0.04f);
            slot.birth = Time.time;
            slot.color = color;
            slot.active = true;
            slot.transform.gameObject.SetActive(true);
            ApplyPulse(slot, 0f);
        }

        /// <summary>Centre of a cell at the top-surface height, in this object's local space.</summary>
        public Vector3 CellCenterLocal(int x, int y)
        {
            return new Vector3((x + 0.5f) * _cellSize, _tileHeight, (y + 0.5f) * _cellSize);
        }

        void Update()
        {
            ProcessHolds();

            for (int i = 0; i < _pulses.Length; i++)
            {
                PulseSlot slot = _pulses[i];
                if (!slot.active)
                {
                    continue;
                }

                float t01 = (Time.time - slot.birth) / pulseSeconds;
                if (t01 >= 1f)
                {
                    slot.active = false;
                    slot.transform.gameObject.SetActive(false);
                    continue;
                }

                ApplyPulse(slot, t01);
            }
        }

        void OnDestroy()
        {
            Cleanup();
        }

        // ------------------------------------------------------------------ pulse

        void ApplyPulse(PulseSlot slot, float t01)
        {
            // Pops up fast (ease-out) from big to resting size while the alpha fades out.
            float pop = 1f - (1f - Mathf.Clamp01(t01 * 2.5f)) * (1f - Mathf.Clamp01(t01 * 2.5f));
            float scale = Mathf.Lerp(pulsePopScale, 1f, pop);
            float thickness = _footprint * 0.06f;
            slot.transform.localScale = new Vector3(_footprint * scale, thickness, _footprint * scale);

            float fade = 1f - t01;
            fade *= fade;
            Color c = slot.color;
            Color tint = new Color(c.r * pulseIntensity, c.g * pulseIntensity, c.b * pulseIntensity, fade);

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            slot.renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, tint);
            _block.SetColor(EmissionColorId, tint);
            slot.renderer.SetPropertyBlock(_block);
        }

        void BuildPulsePool(Material gridMaterial)
        {
            _pulseMesh = CreateCubeMesh();
            _pulseMaterial = CreatePulseMaterial(gridMaterial);

            _pulses = new PulseSlot[pulsePoolSize];
            for (int i = 0; i < _pulses.Length; i++)
            {
                var go = new GameObject($"Pulse {i:00}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _pulseMesh;
                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _pulseMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                go.SetActive(false);
                _pulses[i] = new PulseSlot { transform = go.transform, renderer = meshRenderer };
            }

            _nextPulse = 0;
        }

        static Material CreatePulseMaterial(Material fallback)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return fallback != null ? new Material(fallback) : null;
            }

            var material = new Material(shader) { name = "TileGridPulse (runtime)" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f); // two-sided: the overlay is thin, so winding must never hide it
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        static Mesh CreateCubeMesh()
        {
            // A unit cube, built by hand so no primitive GameObject (and its collider) is ever created.
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(36);
            Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            foreach (Vector3 n in normals)
            {
                Vector3 a = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 r = Vector3.Cross(a, n).normalized;
                Vector3 u = Vector3.Cross(n, r);
                int b = vertices.Count;
                vertices.Add((n - r - u) * 0.5f);
                vertices.Add((n - r + u) * 0.5f);
                vertices.Add((n + r + u) * 0.5f);
                vertices.Add((n + r - u) * 0.5f);
                triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }

            var mesh = new Mesh { name = "TileGridPulse Cube (runtime)" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ mesh and texture

        void BuildMesh()
        {
            int tiles = _width * _height;
            var vertices = new Vector3[tiles * VerticesPerTile];
            var normals = new Vector3[tiles * VerticesPerTile];
            var uvs = new Vector2[tiles * VerticesPerTile];
            var indices = new int[tiles * IndicesPerTile];

            float half = _footprint * 0.5f;
            float inner = Mathf.Max(0f, half - _footprint * bevel);
            float rise = _footprint * bevel;
            Vector3[] outward = { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };

            int v = 0;
            int ti = 0;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector3 center = new Vector3((x + 0.5f) * _cellSize, 0f, (y + 0.5f) * _cellSize);
                    Vector2 uv = new Vector2((x + 0.5f) / _width, (y + 0.5f) / _height);
                    Vector3 top = center + Vector3.up * _tileHeight;

                    // Top quad, inset by the bevel. Clockwise seen from above.
                    int b = v;
                    vertices[v] = top + new Vector3(-inner, 0f, -inner);
                    vertices[v + 1] = top + new Vector3(-inner, 0f, inner);
                    vertices[v + 2] = top + new Vector3(inner, 0f, inner);
                    vertices[v + 3] = top + new Vector3(inner, 0f, -inner);
                    for (int k = 0; k < 4; k++)
                    {
                        normals[v + k] = Vector3.up;
                        uvs[v + k] = uv;
                    }

                    v += 4;
                    indices[ti++] = b;
                    indices[ti++] = b + 1;
                    indices[ti++] = b + 2;
                    indices[ti++] = b;
                    indices[ti++] = b + 2;
                    indices[ti++] = b + 3;

                    // Four sloped sides from the full footprint at the base up to the inset top edge.
                    for (int s = 0; s < 4; s++)
                    {
                        Vector3 n = outward[s];
                        Vector3 r = Vector3.Cross(Vector3.up, -n);
                        Vector3 slope = (n * _tileHeight + Vector3.up * rise).normalized;

                        b = v;
                        vertices[v] = center + n * half - r * half;
                        vertices[v + 1] = top + n * inner - r * inner;
                        vertices[v + 2] = top + n * inner + r * inner;
                        vertices[v + 3] = center + n * half + r * half;
                        for (int k = 0; k < 4; k++)
                        {
                            normals[v + k] = slope;
                            uvs[v + k] = uv;
                        }

                        v += 4;
                        indices[ti++] = b;
                        indices[ti++] = b + 1;
                        indices[ti++] = b + 2;
                        indices[ti++] = b;
                        indices[ti++] = b + 2;
                        indices[ti++] = b + 3;
                    }
                }
            }

            _mesh = new Mesh { name = "TileGrid (runtime)", indexFormat = IndexFormat.UInt32 };
            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = indices;
            _mesh.RecalculateBounds();
            _mesh.hideFlags = HideFlags.DontSave;

            var filter = GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = gameObject.AddComponent<MeshFilter>();
            }

            filter.sharedMesh = _mesh;
        }

        void BuildTexture(Material source)
        {
            _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false)
            {
                name = "TileGrid Owners (runtime)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            _pixels = new Color32[_width * _height];
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = _neutral;
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            _material = new Material(source) { name = "TileGrid (runtime)" };
            _material.SetTexture(BaseMapId, _texture);
            _material.mainTexture = _texture;
            if (_material.HasProperty(BaseColorId))
            {
                _material.SetColor(BaseColorId, Color.white);
            }

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            meshRenderer.sharedMaterial = _material;
        }

        void Cleanup()
        {
            for (int i = 0; i < _pulses.Length; i++)
            {
                if (_pulses[i].transform != null)
                {
                    DestroyUnity(_pulses[i].transform.gameObject);
                }
            }

            _pulses = new PulseSlot[0];
            DestroyObject(ref _mesh);
            DestroyObject(ref _texture);
            DestroyObject(ref _material);
            DestroyObject(ref _pulseMaterial);
            DestroyObject(ref _pulseMesh);
        }

        static void DestroyObject<T>(ref T target) where T : Object
        {
            DestroyUnity(target);
            target = null;
        }

        static void DestroyUnity(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
