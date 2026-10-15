using System;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Bulk painting on an <see cref="OwnershipGrid"/>: discs, rows and columns. Static, format-agnostic and
    /// deterministic (fixed iteration order: y ascending, then x ascending). Every change is logged through
    /// <see cref="OwnershipGrid.SetOwner"/>; <see cref="OwnershipGrid.Unowned"/> is a legal owner.
    /// </summary>
    public static class GridPainter
    {
        /// <summary>
        /// Paints every cell within <paramref name="radius"/> (Euclidean, inclusive) of (cx, cy), skipping cells
        /// for which <paramref name="isProtected"/> (called with the cell index; may be null) returns true.
        /// Returns the number of cells whose owner actually changed.
        /// </summary>
        public static int PaintDisc(OwnershipGrid grid, int cx, int cy, int radius, int owner, int tick,
            Func<int, bool> isProtected)
        {
            int changed = 0;
            int r = Math.Max(0, radius);
            int r2 = r * r;
            int y0 = Math.Max(0, cy - r);
            int y1 = Math.Min(grid.Height - 1, cy + r);
            int x0 = Math.Max(0, cx - r);
            int x1 = Math.Min(grid.Width - 1, cx + r);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy > r2)
                    {
                        continue;
                    }

                    changed += PaintCell(grid, x, y, owner, tick, isProtected);
                }
            }

            return changed;
        }

        /// <summary>
        /// Paints a full-width band of <paramref name="thickness"/> rows starting at row <c>index - thickness / 2</c>
        /// (so thickness 1 is exactly <paramref name="index"/>, thickness 2 is index-1 and index). Returns cells changed.
        /// </summary>
        public static int PaintRow(OwnershipGrid grid, int index, int thickness, int owner, int tick,
            Func<int, bool> isProtected)
        {
            int t = Math.Max(1, thickness);
            int start = index - t / 2;
            int changed = 0;
            for (int y = Math.Max(0, start); y < Math.Min(grid.Height, start + t); y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    changed += PaintCell(grid, x, y, owner, tick, isProtected);
                }
            }

            return changed;
        }

        /// <summary>Column counterpart of <see cref="PaintRow"/>: a full-height band, same thickness rule.</summary>
        public static int PaintColumn(OwnershipGrid grid, int index, int thickness, int owner, int tick,
            Func<int, bool> isProtected)
        {
            int t = Math.Max(1, thickness);
            int start = index - t / 2;
            int changed = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = Math.Max(0, start); x < Math.Min(grid.Width, start + t); x++)
                {
                    changed += PaintCell(grid, x, y, owner, tick, isProtected);
                }
            }

            return changed;
        }

        static int PaintCell(OwnershipGrid grid, int x, int y, int owner, int tick, Func<int, bool> isProtected)
        {
            int index = grid.Index(x, y);
            if (grid.GetOwner(index) == owner || (isProtected != null && isProtected(index)))
            {
                return 0;
            }

            grid.SetOwner(x, y, owner, tick);
            return 1;
        }
    }
}
