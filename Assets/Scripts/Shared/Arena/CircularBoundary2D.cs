using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A circular wall built from many short thick segments, with one angular gap. Reusable by any
    /// format needing a round arena (<c>esc</c>, <c>surv</c>, funnel stages in <c>race</c>).
    /// </summary>
    /// <remarks>
    /// Segments rather than a thin ring collider, because wall thickness is the cheapest tunneling fix
    /// available and a ring has effectively none. The gap is real geometry — an absent segment — so a
    /// ball escapes through it by ordinary physics rather than by a scripted exception.
    /// </remarks>
    public sealed class CircularBoundary2D : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Inner radius — the surface the ball actually bounces off.")]
        [Min(0.1f)] public float radius = 5f;

        [Tooltip("Wall thickness. Keep at or above 2x the ball's largest radius.")]
        [Min(0.05f)] public float thickness = 1f;

        [Tooltip("Number of segments around the full circle. More is smoother and costs more colliders.")]
        [Range(16, 256)] public int segmentCount = 96;

        [Header("Gap — the escape route")]
        [Tooltip("Gap width in degrees. The main tension dial for the escape format.")]
        [Range(0f, 90f)] public float gapDegrees = 24f;

        [Tooltip("Where the gap sits, in degrees counter-clockwise from +X. 90 = top of the circle.")]
        [Range(0f, 360f)] public float gapCenterDegrees = 90f;

        [Header("Look")]
        [Tooltip("Square sprite used to draw each wall segment. Leave empty for colliders only.")]
        public Sprite wallSprite;

        public Color wallColor = new Color(0.55f, 0.58f, 0.68f, 1f);

        [Tooltip("Sorting order for wall segments. Keep below the ball so the ball reads on top.")]
        public int wallSortingOrder;

        readonly List<Transform> _segments = new List<Transform>();

        /// <summary>Arc width of the gap along the inner wall, in world units — what a ball must fit through.</summary>
        public float GapArcWidth => radius * gapDegrees * Mathf.Deg2Rad;

        /// <summary>
        /// Straight-line distance across the gap, which is what actually limits a ball's passage —
        /// always slightly less than the arc, and using the arc would overstate the opening.
        /// </summary>
        public float GapChordWidth => 2f * radius * Mathf.Sin(gapDegrees * Mathf.Deg2Rad * 0.5f);

        /// <summary>
        /// Gap centre in world space, accounting for the transform's own rotation. Segments are child
        /// objects so their colliders already rotate with the transform for free; this is what lets the
        /// script-side gap math (near-miss, escape check) agree with where the physical opening actually
        /// is when something (e.g. <c>esc</c>'s rotating-gap variant) spins the boundary at runtime.
        /// Static boundaries never rotate, so this is <c>gapCenterDegrees</c> unchanged for them.
        /// </summary>
        public float EffectiveGapCenterDegrees => gapCenterDegrees + transform.eulerAngles.z;

        /// <summary>Gap centre direction as a unit vector, for camera framing and HUD markers.</summary>
        public Vector2 GapDirection
        {
            get
            {
                float radians = EffectiveGapCenterDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        /// <summary>True if the given world direction falls inside the gap's angular span.</summary>
        public bool IsWithinGap(Vector2 fromCentre)
        {
            float angle = Mathf.Atan2(fromCentre.y, fromCentre.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(EffectiveGapCenterDegrees, angle);
            return Mathf.Abs(delta) <= gapDegrees * 0.5f;
        }

        /// <summary>
        /// Rebuild the wall. Safe to call in the editor and from a scene builder; clears any previous
        /// segments first so it is idempotent.
        /// </summary>
        public void Rebuild()
        {
            ClearSegments();

            float segmentArc = 360f / segmentCount;
            float halfGap = gapDegrees * 0.5f;

            // Segments sit at the mid-radius so `radius` stays the inner bounce surface regardless of
            // thickness — otherwise tuning thickness would silently change the arena size.
            float midRadius = radius + thickness * 0.5f;
            float segmentLength = 2f * Mathf.PI * midRadius / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i * segmentArc;
                if (Mathf.Abs(Mathf.DeltaAngle(gapCenterDegrees, angle)) <= halfGap)
                {
                    continue;
                }

                float radians = angle * Mathf.Deg2Rad;
                var segment = new GameObject($"Segment_{i:D3}");
                segment.transform.SetParent(transform, false);
                segment.transform.localPosition =
                    new Vector3(Mathf.Cos(radians) * midRadius, Mathf.Sin(radians) * midRadius, 0f);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                var box = segment.AddComponent<BoxCollider2D>();
                // Overlapped slightly (1.4x) so consecutive segments leave no seam a ball can catch on.
                Vector2 size = new Vector2(segmentLength * 1.4f, thickness);
                box.size = size;

                if (wallSprite != null)
                {
                    // Visual lives on a child so stretching it to the segment's size cannot also scale
                    // the collider — the wall must never look thicker than it physically is.
                    var visual = new GameObject("Visual");
                    visual.transform.SetParent(segment.transform, false);
                    visual.transform.localScale = new Vector3(size.x, size.y, 1f);

                    var renderer = visual.AddComponent<SpriteRenderer>();
                    renderer.sprite = wallSprite;
                    renderer.color = wallColor;
                    renderer.sortingOrder = wallSortingOrder;
                }

                _segments.Add(segment.transform);
            }
        }

        void ClearSegments()
        {
            _segments.Clear();

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

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 centre = transform.position;
            const int steps = 64;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i / (float)steps * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
                Gizmos.DrawLine(
                    centre + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius,
                    centre + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius);
            }

            // The gap is what the whole format hinges on, so make it unmissable while authoring.
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(centre, centre + (Vector3)GapDirection * (radius + thickness));
        }
    }
}
