using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A ring wall cut into equal arc segments that can be switched off one at a time — the
    /// breakable counterpart to <see cref="CircularBoundary2D"/>. Used by <c>brk</c>; reusable by any
    /// format whose wall takes damage (a shrinking survival ring, a shield to break through).
    /// </summary>
    /// <remarks>
    /// Segments are true annular sectors (polygon colliders) that tile the ring exactly, not the
    /// overlapping boxes <see cref="CircularBoundary2D"/> uses. Overlap is harmless for a fixed gap,
    /// but here the hole a broken segment leaves *is* the gameplay, and overlapping neighbours would
    /// shrink every hole by a third. Adjacent chords meet concave-side-in, so the ball has no seam
    /// to catch on from inside.
    /// <para>
    /// Each segment is a child rotated to its angle, sharing one collider shape and one visual mesh
    /// per ring. The collider is simulation state; the child <c>Visual</c> is presentation and may be
    /// animated freely without touching physics.
    /// </para>
    /// </remarks>
    public sealed class SegmentedRing2D : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Inner radius — the surface a ball inside the ring bounces off.")]
        [Min(0.1f)] public float innerRadius = 2f;

        [Min(0.05f)] public float thickness = 0.25f;

        [Range(3, 256)] public int segmentCount = 24;

        [Tooltip("Polygon steps per segment along the arc. More is rounder and costs more.")]
        [Range(1, 16)] public int arcSteps = 4;

        [Header("Look")]
        [Tooltip("Visual-only gap between segments, as a fraction of a segment's arc. Colliders still " +
                 "tile exactly; this just lets the viewer count segments.")]
        [Range(0f, 0.5f)] public float visualGapFraction = 0.1f;

        public Color color = Color.white;

        [Tooltip("Shared by every segment. Vertex colours carry the ring colour, so one material serves all rings.")]
        public Material visualMaterial;

        public int sortingOrder;

        readonly List<Collider2D> _colliders = new List<Collider2D>();
        readonly List<Transform> _visuals = new List<Transform>();
        readonly Dictionary<Collider2D, int> _indexByCollider = new Dictionary<Collider2D, int>();

        public int SegmentCount
        {
            get
            {
                EnsureIndexed();
                return _colliders.Count;
            }
        }

        public float SegmentArcDegrees => 360f / segmentCount;

        public float OuterRadius => innerRadius + thickness;

        /// <summary>
        /// Width of the hole one broken segment leaves, at the inner surface — what a ball has to fit
        /// through. The inner chord, because that is the narrowest point of the opening.
        /// </summary>
        public float HoleChordWidth => 2f * innerRadius * Mathf.Sin(Mathf.PI / segmentCount);

        public Collider2D GetCollider(int index)
        {
            EnsureIndexed();
            return _colliders[index];
        }

        /// <summary>The segment's visual child, for presentation to animate. Never read by the simulation.</summary>
        public Transform GetVisual(int index)
        {
            EnsureIndexed();
            return _visuals[index];
        }

        public bool TryGetSegment(Collider2D collider, out int index)
        {
            EnsureIndexed();
            return _indexByCollider.TryGetValue(collider, out index);
        }

        /// <summary>Segment whose angular span contains a world point, accounting for the ring's rotation.</summary>
        public int SegmentIndexAt(Vector2 worldPoint)
        {
            Vector2 local = transform.InverseTransformPoint(worldPoint);
            float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return Mathf.Clamp(Mathf.FloorToInt(angle / SegmentArcDegrees), 0, segmentCount - 1);
        }

        /// <summary>World-space centre of a segment, mid-thickness.</summary>
        public Vector2 SegmentCenter(int index)
        {
            float radians = (index + 0.5f) * SegmentArcDegrees * Mathf.Deg2Rad;
            float mid = innerRadius + thickness * 0.5f;
            return transform.TransformPoint(new Vector3(Mathf.Cos(radians) * mid, Mathf.Sin(radians) * mid, 0f));
        }

        /// <summary>World-space outward direction through a segment's centre.</summary>
        public Vector2 SegmentOutward(int index)
        {
            return (SegmentCenter(index) - (Vector2)transform.position).normalized;
        }

        /// <summary>Rebuild every segment. Idempotent; safe from a scene builder.</summary>
        public void Rebuild()
        {
            ClearSegments();

            float arc = SegmentArcDegrees;
            Vector2[] colliderShape = SectorOutline(arc, 0f);
            Mesh visualMesh = SectorMesh(arc, visualGapFraction);

            for (int i = 0; i < segmentCount; i++)
            {
                var segment = new GameObject($"Segment_{i:D3}");
                segment.transform.SetParent(transform, false);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, (i + 0.5f) * arc);

                var polygon = segment.AddComponent<PolygonCollider2D>();
                polygon.SetPath(0, colliderShape);

                var visual = new GameObject("Visual");
                visual.transform.SetParent(segment.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = visualMesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = visualMaterial;
                renderer.sortingOrder = sortingOrder;
            }

            _indexed = false;
            EnsureIndexed();
        }

        bool _indexed;

        void EnsureIndexed()
        {
            if (_indexed && _colliders.Count == segmentCount)
            {
                return;
            }

            _colliders.Clear();
            _visuals.Clear();
            _indexByCollider.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith("Segment_"))
                {
                    continue;
                }

                var collider = child.GetComponent<Collider2D>();
                _indexByCollider[collider] = _colliders.Count;
                _colliders.Add(collider);
                _visuals.Add(child.Find("Visual"));
            }

            _indexed = true;
        }

        /// <summary>Annular sector centred on local +X, spanning ±arc/2, inset by a fraction of the arc on each side.</summary>
        Vector2[] SectorOutline(float arcDegrees, float insetFraction)
        {
            float half = arcDegrees * 0.5f * (1f - insetFraction);
            float outer = OuterRadius;
            var points = new Vector2[(arcSteps + 1) * 2];

            for (int s = 0; s <= arcSteps; s++)
            {
                float a = Mathf.Lerp(-half, half, s / (float)arcSteps) * Mathf.Deg2Rad;
                points[s] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * innerRadius;
                points[points.Length - 1 - s] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer;
            }

            return points;
        }

        Mesh SectorMesh(float arcDegrees, float insetFraction)
        {
            float half = arcDegrees * 0.5f * (1f - insetFraction);
            int columns = arcSteps + 1;
            var vertices = new Vector3[columns * 2];
            var colors = new Color[columns * 2];
            var triangles = new int[arcSteps * 6];

            // Slightly brighter outer edge: a cheap bevel that makes each segment read as a solid tile.
            Color inner = color;
            Color outerEdge = Color.Lerp(color, Color.white, 0.25f);

            for (int s = 0; s < columns; s++)
            {
                float a = Mathf.Lerp(-half, half, s / (float)arcSteps) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices[s * 2] = direction * innerRadius;
                vertices[s * 2 + 1] = direction * OuterRadius;
                colors[s * 2] = inner;
                colors[s * 2 + 1] = outerEdge;
            }

            for (int s = 0; s < arcSteps; s++)
            {
                int v = s * 2;
                int t = s * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = $"{name}_Segment" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        void ClearSegments()
        {
            _colliders.Clear();
            _visuals.Clear();
            _indexByCollider.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (!child.name.StartsWith("Segment_"))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
