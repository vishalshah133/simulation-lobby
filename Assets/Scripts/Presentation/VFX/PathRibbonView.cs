using System.Collections.Generic;
using SimulationLobby.Shared;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A filled ribbon laid along a <see cref="TrackPath"/> between two arc-length distances: the lava
    /// front flowing down a track, a progress carpet, a paint trail. Format-agnostic: it knows a path and a
    /// span, nothing about what the span means.
    /// </summary>
    /// <remarks>
    /// Owns the MeshFilter/MeshRenderer on its GameObject and rebuilds one reused mesh when the span
    /// changes (call <see cref="SetSpan"/> from a presenter's <c>LateUpdate</c>). The ribbon's side vector is
    /// world-up crossed with the path tangent, so it lies flat across a sloped track without a stored up
    /// vector. UV.x is arc length on the path times <see cref="uvPerMetre"/> (fixed to the path, not the
    /// span, so a scrolling noise material flows instead of stretching); UV.y runs 0..1 across.
    /// Vertex colours are white, blending to <see cref="lipColor"/> over the leading
    /// <see cref="lipLength"/> metres, so a shader that multiplies vertex colour (or reads it as emissive)
    /// gets a brighter, slightly raised front edge. Presentation only: reads the path, never writes it.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PathRibbonView : MonoBehaviour
    {
        [Min(0f)] public float lipLength = 0.8f;
        [Tooltip("Extra height at the very front of the lip, on top of the height offset.")]
        [Min(0f)] public float lipRaise = 0.08f;
        public Color lipColor = new Color(1f, 0.85f, 0.35f, 1f);
        [Tooltip("UV.x units per metre of path.")]
        [Min(0.001f)] public float uvPerMetre = 0.25f;
        [Tooltip("Maximum spacing between ribbon cross-sections, in metres (path points add more).")]
        [Min(0.05f)] public float maxStep = 0.5f;

        TrackPath _path;
        float _width = 1f;
        float _height;
        AnimationCurve _byDistance;         // width (metres) over path distance; null = constant
        float _byDistanceScale = 1f;
        AnimationCurve _profile;            // width multiplier over 0 (tail) .. 1 (front); null = constant
        Mesh _mesh;
        MeshFilter _filter;
        MeshRenderer _renderer;
        float _from = float.NaN;
        float _to = float.NaN;
        bool _dirty = true;

        readonly List<float> _samples = new List<float>(256);
        Vector3[] _vertices = new Vector3[0];
        Vector2[] _uvs = new Vector2[0];
        Color[] _colors = new Color[0];
        int[] _triangles = new int[0];

        public TrackPath Path => _path;

        public void Bind(TrackPath path, float width, float heightOffset, Material material)
        {
            EnsureMesh();
            _path = path;
            _width = Mathf.Max(0.001f, width);
            _height = heightOffset;
            if (material != null)
            {
                _renderer.sharedMaterial = material;
            }

            _from = float.NaN;
            _to = float.NaN;
            _dirty = true;
        }

        /// <summary>Width multiplier along the span: 0 at the tail (fromDistance) to 1 at the front. Null restores a constant width.</summary>
        public void SetWidthProfile(AnimationCurve widthOverSpan)
        {
            _profile = widthOverSpan;
            _dirty = true;
        }

        /// <summary>
        /// Width in metres at a given path distance (curve.Evaluate(distance) x <paramref name="multiplier"/>), replacing the
        /// constant width. Null restores the constant width. Still multiplied by <see cref="SetWidthProfile"/> if one is set.
        /// </summary>
        public void SetWidthByDistance(AnimationCurve widthByPathDistance, float multiplier = 1f)
        {
            _byDistance = widthByPathDistance;
            _byDistanceScale = multiplier;
            _dirty = true;
            _from = float.NaN;
            _to = float.NaN;
        }

        /// <summary>Show the ribbon from <paramref name="fromDistance"/> to <paramref name="toDistance"/> metres along the path. An empty or reversed span hides it.</summary>
        public void SetSpan(float fromDistance, float toDistance)
        {
            if (!_dirty && Mathf.Approximately(fromDistance, _from) && Mathf.Approximately(toDistance, _to))
            {
                return;
            }

            _from = fromDistance;
            _to = toDistance;
            _dirty = false;
            Rebuild();
        }

        public void Hide()
        {
            _from = float.NaN;
            _to = float.NaN;
            _dirty = true;
            EnsureMesh();
            _mesh.Clear();
        }

        void EnsureMesh()
        {
            if (_mesh != null)
            {
                return;
            }

            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "PathRibbon", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        void Rebuild()
        {
            EnsureMesh();
            if (_path == null || _path.Count < 2)
            {
                _mesh.Clear();
                return;
            }

            float total = _path.TotalLength;
            float from = Mathf.Clamp(_from, 0f, total);
            float to = Mathf.Clamp(_to, 0f, total);
            float length = to - from;
            if (length < 0.01f)
            {
                _mesh.Clear();
                return;
            }

            // Cross-section distances: both ends, the lip start, every path point between, and filler steps.
            _samples.Clear();
            _samples.Add(from);
            float lipStart = to - Mathf.Min(lipLength, length);
            bool lipAdded = lipLength <= 0f || lipStart <= from + 0.01f;
            int first = _path.IndexAtDistance(from);
            int last = _path.IndexAtDistance(to);
            float prev = from;
            for (int i = first; i <= last; i++)
            {
                float d = Mathf.Clamp(_path.DistanceAt(i), from, to);
                if (!lipAdded && d > lipStart)
                {
                    AddWithFiller(lipStart, ref prev);
                    lipAdded = true;
                }

                if (d > prev + 0.01f && d < to - 0.01f)
                {
                    AddWithFiller(d, ref prev);
                }
            }

            if (!lipAdded)
            {
                AddWithFiller(lipStart, ref prev);
            }

            AddWithFiller(to, ref prev);

            int n = _samples.Count;
            int vertexCount = n * 2;
            int triCount = (n - 1) * 6;
            if (_vertices.Length < vertexCount)
            {
                int cap = Mathf.NextPowerOfTwo(vertexCount);
                _vertices = new Vector3[cap];
                _uvs = new Vector2[cap];
                _colors = new Color[cap];
            }

            if (_triangles.Length < triCount)
            {
                _triangles = new int[Mathf.NextPowerOfTwo(triCount)];
            }

            for (int i = 0; i < n; i++)
            {
                float d = _samples[i];
                Vector3 centre = _path.PositionAtDistance(d);
                Vector3 tangent = _path.TangentAt(_path.IndexAtDistance(d) - 1);
                Vector3 side = Vector3.Cross(Vector3.up, tangent);
                side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;

                float t = (d - from) / length;
                float lip = lipLength > 0f ? Mathf.Clamp01(1f - (to - d) / lipLength) : 0f;
                float widthScale = _profile != null ? Mathf.Max(0f, _profile.Evaluate(t)) : 1f;
                float baseWidth = _byDistance != null ? Mathf.Max(0f, _byDistance.Evaluate(d)) * _byDistanceScale : _width;
                float half = baseWidth * 0.5f * widthScale;
                centre += Vector3.up * (_height + lipRaise * lip * lip);

                Color colour = Color.Lerp(Color.white, lipColor, lip * lip);
                int v = i * 2;
                _vertices[v] = transform.InverseTransformPoint(centre - side * half);
                _vertices[v + 1] = transform.InverseTransformPoint(centre + side * half);
                float u = d * uvPerMetre;
                _uvs[v] = new Vector2(u, 0f);
                _uvs[v + 1] = new Vector2(u, 1f);
                _colors[v] = colour;
                _colors[v + 1] = colour;
            }

            int ti = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int v = i * 2;
                // Wound so the ribbon faces up for a path running away from the camera.
                _triangles[ti++] = v;
                _triangles[ti++] = v + 3;
                _triangles[ti++] = v + 1;
                _triangles[ti++] = v;
                _triangles[ti++] = v + 2;
                _triangles[ti++] = v + 3;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, vertexCount);
            _mesh.SetUVs(0, _uvs, 0, vertexCount);
            _mesh.SetColors(_colors, 0, vertexCount);
            _mesh.SetIndexBufferParams(triCount, IndexFormat.UInt32);
            _mesh.SetIndexBufferData(_triangles, 0, 0, triCount);
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, triCount), MeshUpdateFlags.DontRecalculateBounds);
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
        }

        void AddWithFiller(float target, ref float prev)
        {
            while (target - prev > maxStep * 1.5f)
            {
                prev += maxStep;
                _samples.Add(prev);
            }

            if (target > prev + 0.001f)
            {
                _samples.Add(target);
                prev = target;
            }
        }
    }
}
