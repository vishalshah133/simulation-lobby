using System;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Maps grid space to world space and back. Grid units: 1.0 is one cell, and cell (x, y) spans
    /// [x, x+1) by [y, y+1). Lets the simulation work in plain grid numbers while the board sits
    /// anywhere, at any scale, in a 3D scene.
    /// </summary>
    [Serializable]
    public struct GridFrame
    {
        [Tooltip("World position of the grid's (0,0) corner.")]
        public Vector3 origin;

        [Tooltip("World direction of +x in grid space (unit length).")]
        public Vector3 right;

        [Tooltip("World direction of +y in grid space (unit length).")]
        public Vector3 forward;

        [Tooltip("World size of one cell.")]
        public float cellSize;

        /// <summary>Frame whose origin, +x and +y follow <paramref name="corner"/>'s position, right and forward.</summary>
        public static GridFrame FromTransform(Transform corner, float cellSize)
        {
            return new GridFrame
            {
                origin = corner.position,
                right = corner.right,
                forward = corner.forward,
                cellSize = cellSize,
            };
        }

        /// <summary>World point for a grid position, lifted <paramref name="height"/> along world up.</summary>
        public Vector3 ToWorld(Vector2 gridPosition, float height = 0f)
        {
            return origin
                   + right * (gridPosition.x * cellSize)
                   + forward * (gridPosition.y * cellSize)
                   + Vector3.up * height;
        }

        /// <summary>Grid position under a world point (height is ignored).</summary>
        public Vector2 ToGrid(Vector3 world)
        {
            Vector3 d = world - origin;
            return new Vector2(
                Vector3.Dot(d, right) / cellSize,
                Vector3.Dot(d, forward) / cellSize);
        }

        public Vector3 CellCenterWorld(int x, int y, float height = 0f)
        {
            return ToWorld(new Vector2(x + 0.5f, y + 0.5f), height);
        }
    }
}
