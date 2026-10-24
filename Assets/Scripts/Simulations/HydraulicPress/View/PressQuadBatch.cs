using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.HydraulicPress
{
    /// <summary>
    /// One dynamic mesh of axis-aligned textured quads with per-vertex colour: a single draw call for any
    /// number of balls (or burst rings). Rebuilt every rendered frame from the quads handed to <see cref="Add"/>.
    /// Capacity grows by doubling; the index and UV buffers are static and only extended on growth.
    /// </summary>
    public sealed class PressQuadBatch
    {
        readonly Mesh _mesh;
        readonly MeshRenderer _renderer;
        Vector3[] _vertices;
        Vector2[] _uvs;
        Color32[] _colors;
        int[] _indices;
        int _capacity;
        int _quads;

        public GameObject Root { get; }

        public int QuadCount => _quads;

        public PressQuadBatch(Transform parent, string name, Material material, int sortingOrder, int initialCapacity)
        {
            Root = new GameObject(name);
            Root.transform.SetParent(parent, false);
            MeshFilter filter = Root.AddComponent<MeshFilter>();
            _renderer = Root.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.sortingOrder = sortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _mesh = new Mesh { name = name + " Mesh" };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;

            Grow(Mathf.Max(16, initialCapacity));
        }

        void Grow(int capacity)
        {
            int old = _capacity;
            System.Array.Resize(ref _vertices, capacity * 4);
            System.Array.Resize(ref _uvs, capacity * 4);
            System.Array.Resize(ref _colors, capacity * 4);
            System.Array.Resize(ref _indices, capacity * 6);
            for (int q = old; q < capacity; q++)
            {
                int v = q * 4;
                _uvs[v] = new Vector2(0f, 0f);
                _uvs[v + 1] = new Vector2(1f, 0f);
                _uvs[v + 2] = new Vector2(1f, 1f);
                _uvs[v + 3] = new Vector2(0f, 1f);
                int i = q * 6;
                _indices[i] = v;
                _indices[i + 1] = v + 2;
                _indices[i + 2] = v + 1;
                _indices[i + 3] = v;
                _indices[i + 4] = v + 3;
                _indices[i + 5] = v + 2;
            }

            _capacity = capacity;
        }

        /// <summary>Start a new frame's worth of quads.</summary>
        public void Begin()
        {
            _quads = 0;
        }

        /// <summary>Append a quad centred on (cx, cy) with half extents (hw, hh), in the batch's local space.</summary>
        public void Add(float cx, float cy, float hw, float hh, Color32 color)
        {
            if (_quads == _capacity)
            {
                Grow(_capacity * 2);
            }

            int v = _quads * 4;
            _vertices[v] = new Vector3(cx - hw, cy - hh, 0f);
            _vertices[v + 1] = new Vector3(cx + hw, cy - hh, 0f);
            _vertices[v + 2] = new Vector3(cx + hw, cy + hh, 0f);
            _vertices[v + 3] = new Vector3(cx - hw, cy + hh, 0f);
            _colors[v] = color;
            _colors[v + 1] = color;
            _colors[v + 2] = color;
            _colors[v + 3] = color;
            _quads++;
        }

        /// <summary>Upload. With zero quads the renderer is switched off.</summary>
        public void End()
        {
            _renderer.enabled = _quads > 0;
            _mesh.Clear(true);
            if (_quads == 0)
            {
                return;
            }

            int vertexCount = _quads * 4;
            _mesh.SetVertices(_vertices, 0, vertexCount);
            _mesh.SetColors(_colors, 0, vertexCount);
            _mesh.SetUVs(0, _uvs, 0, vertexCount);
            _mesh.SetIndices(_indices, 0, _quads * 6, MeshTopology.Triangles, 0, false);
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(200f, 200f, 1f));
        }

        public void Destroy()
        {
            if (_mesh != null)
            {
                Object.Destroy(_mesh);
            }
        }
    }
}
