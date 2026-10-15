using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A physical slot drum: a regular N-gon prism rolling about its LOCAL X axis, one 3D TextMeshPro label
    /// per flat face. Face i sits at angle i * (360/N) around X; the "front" is local -Z (put the camera
    /// there). Spin() rolls it, with an ease-out, to land a requested face exactly at the front.
    /// Presentation only: it never reads or writes simulation state, the caller decides the result.
    /// Works in edit mode (Build / SetFaces / SetIdle / SetAccent) and at runtime (Spin).
    /// </summary>
    public sealed class DrumReelView : MonoBehaviour
    {
        const string RollName = "Drum Roll";
        const string BodyName = "Drum Body";
        const string FacesName = "Drum Faces";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Stand / rim renderers, tinted by SetAccent.")]
        public Renderer[] accentRenderers;
        public Color faceColor = new Color32(0xFF, 0x4F, 0xA0, 255);
        public Color textColor = Color.white;
        [Tooltip("Outline colour of the face text.")]
        public Color textOutlineColor = new Color32(0x30, 0x08, 0x1C, 255);
        [Tooltip("Fraction of a face's height the text may fill.")]
        [Range(0.3f, 0.95f)] public float textFill = 0.7f;
        [Tooltip("Minimum full turns during a Spin.")]
        [Min(2)] public int minimumTurns = 2;

        [SerializeField] Renderer _body;
        [SerializeField] int _faceCount;
        [SerializeField] float _radius = 1f;
        [SerializeField] float _width = 2f;
        [SerializeField] TextMeshPro[] _texts = new TextMeshPro[0];
        [SerializeField] float _angle;   // current roll about local X, degrees (rotation = AngleAxis(_angle, X))

        [SerializeField] Transform _roll;
        bool _spinning;
        float _startAngle;
        float _totalRoll;     // positive magnitude; roll direction is negative (faces advance in index order)
        float _elapsed;
        float _duration;
        int _result;
        int _lastBoundary;

        /// <summary>The prism body renderer (child "Drum Body"); the builder assigns its material here.</summary>
        public Renderer Body => _body;

        public bool IsSpinning => _spinning;

        public event Action FacePassed;
        public event Action<int> Landed;

        public int FaceCount => _faceCount;

        /// <summary>Builds (or rebuilds) the prism and one label per face. Radius is the circumradius.</summary>
        public void Build(int faceCount, float radius, float width)
        {
            faceCount = Mathf.Max(3, faceCount);
            _faceCount = faceCount;
            _radius = Mathf.Max(0.01f, radius);
            _width = Mathf.Max(0.01f, width);
            _spinning = false;

            // Roll pivot: the part that rotates, so this transform's own orientation stays the caller's.
            Transform roll = transform.Find(RollName);
            if (roll == null)
            {
                roll = new GameObject(RollName).transform;
                roll.SetParent(transform, false);
            }

            _roll = roll;

            // Body: keep an existing child (and its material) across rebuilds, replace only the mesh.
            Transform bodyT = roll.Find(BodyName);
            if (bodyT == null)
            {
                var go = new GameObject(BodyName);
                bodyT = go.transform;
                bodyT.SetParent(roll, false);
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
            }

            _body = bodyT.GetComponent<Renderer>();
            var filter = bodyT.GetComponent<MeshFilter>();
            if (filter.sharedMesh != null && filter.sharedMesh.name == "DrumPrism")
            {
                DestroyObject(filter.sharedMesh);
            }

            filter.sharedMesh = BuildMesh(faceCount, _radius, _width);

            // Face labels: always recreated.
            Transform old = roll.Find(FacesName);
            if (old != null)
            {
                DestroyObject(old.gameObject);
            }

            var facesRoot = new GameObject(FacesName).transform;
            facesRoot.SetParent(roll, false);

            float step = 360f / faceCount;
            float apothem = _radius * Mathf.Cos(Mathf.PI / faceCount);
            float faceHeight = 2f * _radius * Mathf.Sin(Mathf.PI / faceCount);
            _texts = new TextMeshPro[faceCount];
            for (int i = 0; i < faceCount; i++)
            {
                float a = i * step;
                var go = new GameObject($"Face {i}");
                go.transform.SetParent(facesRoot, false);
                Quaternion rot = Quaternion.AngleAxis(a, Vector3.right);
                go.transform.localRotation = rot;
                go.transform.localPosition = rot * new Vector3(0f, 0f, -(apothem + _radius * 0.01f));

                var t = go.AddComponent<TextMeshPro>();
                t.text = i.ToString();
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow;
                t.enableAutoSizing = true;
                float maxSize = faceHeight * textFill * 10f / 0.75f; // TMP 3D: size 10 ~ 1 unit; cap height ~0.75
                t.fontSizeMax = maxSize;
                t.fontSizeMin = maxSize * 0.2f;
                t.fontSize = maxSize;
                t.color = textColor;
                t.outlineWidth = 0.22f;
                t.outlineColor = textOutlineColor;
                t.rectTransform.sizeDelta = new Vector2(_width * 0.9f, faceHeight * 0.95f);
                _texts[i] = t;
            }

            ApplyFaceColor();
            SetIdle(0);
        }

        /// <summary>Relabels the faces. The count must match the built face count.</summary>
        public void SetFaces(string[] labels)
        {
            if (labels == null || _texts == null || labels.Length != _texts.Length)
            {
                Debug.LogWarning($"DrumReelView.SetFaces: expected {(_texts == null ? 0 : _texts.Length)} labels, got {(labels == null ? 0 : labels.Length)}.", this);
                return;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                if (_texts[i] != null)
                {
                    _texts[i].text = labels[i];
                    _texts[i].color = textColor;
                }
            }

            ApplyFaceColor();
        }

        /// <summary>Snaps the drum so face <paramref name="faceIndex"/> points toward local -Z.</summary>
        public void SetIdle(int faceIndex)
        {
            _spinning = false;
            if (_faceCount <= 0)
            {
                return;
            }

            int i = ((faceIndex % _faceCount) + _faceCount) % _faceCount;
            ApplyAngle(-i * (360f / _faceCount));
        }

        /// <summary>Rolls from the current angle so face <paramref name="resultIndex"/> lands at -Z at exactly <paramref name="seconds"/>.</summary>
        public void Spin(int resultIndex, float seconds)
        {
            if (_faceCount <= 0)
            {
                return;
            }

            _result = ((resultIndex % _faceCount) + _faceCount) % _faceCount;
            float step = 360f / _faceCount;
            float targetAngle = -_result * step;
            // Positive distance rolled in the decreasing-angle direction to reach the target (0..360).
            float d = Mathf.Repeat(_angle - targetAngle, 360f);
            _totalRoll = d + 360f * Mathf.Max(2, minimumTurns);
            _startAngle = _angle;
            _duration = Mathf.Max(0.01f, seconds);
            _elapsed = 0f;
            _lastBoundary = BoundaryIndex(_startAngle);
            _spinning = true;
        }

        /// <summary>Tints <see cref="accentRenderers"/> via a MaterialPropertyBlock (_BaseColor).</summary>
        public void SetAccent(Color color)
        {
            if (accentRenderers == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            foreach (Renderer r in accentRenderers)
            {
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        void Update()
        {
            if (!_spinning)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(_elapsed / _duration);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            float angle = u >= 1f ? _startAngle - _totalRoll : _startAngle - _totalRoll * eased;
            ApplyAngle(angle);

            int boundary = BoundaryIndex(angle);
            while (_lastBoundary < boundary)
            {
                _lastBoundary++;
                FacePassed?.Invoke();
            }

            if (u >= 1f)
            {
                _spinning = false;
                ApplyAngle(-_result * (360f / _faceCount)); // exact, no float drift
                Landed?.Invoke(_result);
            }
        }

        // Number of face boundaries (half-steps between faces) the front has crossed, going in index order.
        int BoundaryIndex(float angle)
        {
            float step = 360f / _faceCount;
            return Mathf.FloorToInt((_startAngle - angle) / step + 0.5f);
        }

        void ApplyAngle(float angle)
        {
            _angle = angle;
            if (_roll != null)
            {
                _roll.localRotation = Quaternion.AngleAxis(angle, Vector3.right);
            }
        }


        void ApplyFaceColor()
        {
            if (_body == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            _body.GetPropertyBlock(block);
            block.SetColor(BaseColorId, faceColor);
            block.SetColor(ColorId, faceColor);
            _body.SetPropertyBlock(block);
        }

        static void DestroyObject(UnityEngine.Object o)
        {
            if (Application.isPlaying)
            {
                Destroy(o);
            }
            else
            {
                DestroyImmediate(o);
            }
        }

        static Mesh BuildMesh(int n, float radius, float width)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float step = 2f * Mathf.PI / n;
            float hx = width * 0.5f;

            for (int i = 0; i < n; i++)
            {
                float a0 = i * step - step * 0.5f;
                float a1 = i * step + step * 0.5f;
                float am = i * step;
                Vector3 normal = Dir(am);
                Vector3 p0 = Dir(a0) * radius;
                Vector3 p1 = Dir(a1) * radius;
                int b = verts.Count;
                verts.Add(p0 + Vector3.left * hx);   // x = -hx
                verts.Add(p1 + Vector3.left * hx);
                verts.Add(p1 + Vector3.right * hx);
                verts.Add(p0 + Vector3.right * hx);
                for (int k = 0; k < 4; k++)
                {
                    norms.Add(normal);
                }

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                AddTri(verts, tris, b, b + 1, b + 2, normal);
                AddTri(verts, tris, b, b + 2, b + 3, normal);
            }

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -hx : hx;
                Vector3 normal = side == 0 ? Vector3.left : Vector3.right;
                int centre = verts.Count;
                verts.Add(new Vector3(x, 0f, 0f));
                norms.Add(normal);
                uvs.Add(new Vector2(0.5f, 0.5f));
                int ring = verts.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = Dir(i * step - step * 0.5f) * radius;
                    verts.Add(new Vector3(x, p.y, p.z));
                    norms.Add(normal);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                }

                for (int i = 0; i < n; i++)
                {
                    AddTri(verts, tris, centre, ring + i, ring + (i + 1) % n, normal);
                }
            }

            var mesh = new Mesh { name = "DrumPrism" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Face-angle direction in the YZ plane: angle 0 is -Z, increasing toward +Y (matches AngleAxis about +X).
        static Vector3 Dir(float a) => new Vector3(0f, Mathf.Sin(a), -Mathf.Cos(a));

        static void AddTri(List<Vector3> verts, List<int> tris, int a, int b, int c, Vector3 wantNormal)
        {
            // Unity front faces wind clockwise: cross(b-a, c-a) points along the outward normal.
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, wantNormal) >= 0f)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            else
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
        }
    }
}
