using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A flat ring lying on the ground (local XZ plane) that drains clockwise as a countdown: <see cref="SetFraction"/> 1 is a
    /// full ring, 0 is empty. Runtime mesh (never an asset) and an unlit material built in code, with a dim full "track"
    /// ring under the bright fill. Create with AddComponent, then call <see cref="Build"/>.
    /// Seen from above with local +Z up the screen, the fill starts at 12 o'clock and its trailing edge sweeps clockwise.
    /// </summary>
    public sealed class RadialTimerView : MonoBehaviour
    {
        const int Segments = 128;
        const float FractionSteps = 256f;
        const float FillLift = 0.006f;

        [SerializeField, HideInInspector] float _radius;
        [SerializeField, HideInInspector] float _thickness;
        [SerializeField, HideInInspector] Color _fillColor = Color.white;
        [SerializeField, HideInInspector] Color _trackColor = new Color(1f, 1f, 1f, 0.25f);

        GameObject _trackGo;
        GameObject _fillGo;
        MeshRenderer _trackRenderer;
        MeshRenderer _fillRenderer;
        Mesh _trackMesh;
        Mesh _fillMesh;
        Material _trackMaterial;
        Material _fillMaterial;
        Vector3[] _fillVerts;
        float _fraction = 1f;
        int _drawnStep = -1;
        bool _visible = true;

        public void Build(float radius, float thickness, Color fillColor, Color trackColor)
        {
            _radius = Mathf.Max(0.01f, radius);
            _thickness = Mathf.Clamp(thickness, 0.001f, _radius);
            _fillColor = fillColor;
            _trackColor = trackColor;
            Release();

            _trackMesh = BuildFullRing("TimerTrack (runtime)", _radius, _thickness);
            _trackMaterial = CreateMaterial("TimerTrack (runtime)", trackColor, 3000);

            _fillMesh = new Mesh { name = "TimerFill (runtime)", hideFlags = HideFlags.DontSave };
            _fillMesh.MarkDynamic();
            _fillVerts = new Vector3[(Segments + 1) * 2];
            var tris = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
                // Wound to face up (+Y) for increasing clockwise angle; the material is two-sided anyway.
                tris[i * 6] = a; tris[i * 6 + 1] = c; tris[i * 6 + 2] = b;
                tris[i * 6 + 3] = b; tris[i * 6 + 4] = c; tris[i * 6 + 5] = d;
            }

            _fillMesh.vertices = _fillVerts;
            _fillMesh.triangles = tris;
            _fillMaterial = CreateMaterial("TimerFill (runtime)", fillColor, 3001);

            _trackGo = MakeChild("Track", _trackMesh, _trackMaterial, 0f, out _trackRenderer);
            _fillGo = MakeChild("Fill", _fillMesh, _fillMaterial, FillLift, out _fillRenderer);

            _drawnStep = -1;
            Redraw(true);
            ApplyVisibility();
        }

        public void SetFraction(float fraction01)
        {
            _fraction = float.IsNaN(fraction01) ? 0f : Mathf.Clamp01(fraction01);
            Redraw(false);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        void OnEnable()
        {
            // Scene saved after an edit-mode Build: meshes and materials are not serialised, so rebuild from the stored numbers.
            if (_trackMesh == null && _radius > 0f)
            {
                Build(_radius, _thickness, _fillColor, _trackColor);
            }
        }

        void OnDestroy()
        {
            Release();
        }

        void Release()
        {
            FxFactory.Destroy(_trackGo);
            FxFactory.Destroy(_fillGo);
            FxFactory.Destroy(_trackMesh);
            FxFactory.Destroy(_fillMesh);
            FxFactory.Destroy(_trackMaterial);
            FxFactory.Destroy(_fillMaterial);
            _trackGo = _fillGo = null;
            _trackRenderer = _fillRenderer = null;
            _trackMesh = _fillMesh = null;
            _trackMaterial = _fillMaterial = null;
        }

        GameObject MakeChild(string childName, Mesh mesh, Material material, float lift, out MeshRenderer renderer)
        {
            var go = new GameObject(childName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, lift, 0f);
            renderer = FxFactory.AddMesh(go, mesh, material);
            return go;
        }

        void ApplyVisibility()
        {
            if (_trackRenderer != null) _trackRenderer.enabled = _visible;
            if (_fillRenderer != null) _fillRenderer.enabled = _visible && _drawnStep > 0;
        }

        void Redraw(bool force)
        {
            if (_fillMesh == null)
            {
                return;
            }

            int step = Mathf.RoundToInt(_fraction * FractionSteps);
            if (!force && step == _drawnStep)
            {
                return;
            }

            _drawnStep = step;
            if (step > 0)
            {
                float arc = step / FractionSteps * Mathf.PI * 2f;
                float start = Mathf.PI * 2f - arc;   // the end stays at 12 o'clock; the start sweeps clockwise as it drains
                float rOut = _radius;
                float rIn = _radius - _thickness;
                for (int i = 0; i <= Segments; i++)
                {
                    float ang = start + arc * (i / (float)Segments);
                    float s = Mathf.Sin(ang), c = Mathf.Cos(ang);   // angle from +Z toward +X
                    _fillVerts[i * 2] = new Vector3(s * rOut, 0f, c * rOut);
                    _fillVerts[i * 2 + 1] = new Vector3(s * rIn, 0f, c * rIn);
                }

                _fillMesh.vertices = _fillVerts;
                _fillMesh.RecalculateBounds();
            }

            ApplyVisibility();
        }

        static Mesh BuildFullRing(string meshName, float radius, float thickness)
        {
            int n = Segments;
            var verts = new Vector3[n * 2];
            var tris = new int[n * 6];
            float rIn = radius - thickness;
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * Mathf.PI * 2f;
                float s = Mathf.Sin(ang), c = Mathf.Cos(ang);
                verts[i * 2] = new Vector3(s * radius, 0f, c * radius);
                verts[i * 2 + 1] = new Vector3(s * rIn, 0f, c * rIn);
                int j = (i + 1) % n;
                int a = i * 2, b = i * 2 + 1, cc = j * 2, d = j * 2 + 1;
                tris[i * 6] = a; tris[i * 6 + 1] = cc; tris[i * 6 + 2] = b;
                tris[i * 6 + 3] = b; tris[i * 6 + 4] = cc; tris[i * 6 + 5] = d;
            }

            var mesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material CreateMaterial(string materialName, Color color, int queue)
        {
            Material m = FxFactory.CreateGlowMaterial(materialName);
            if (m == null)
            {
                return null;
            }

            m.SetFloat("_Blend", 0f);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);   // plain alpha blend, so a dim track really is dim
            m.renderQueue = queue;
            FxFactory.SetColor(m, color);
            return m;
        }
    }
}
