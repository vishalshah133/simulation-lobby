using System;
using System.Collections.Generic;

namespace SimulationLobby.Shared
{
    /// <summary>One logged change of a cell's owner. Immutable, so a replay log can be shared safely.</summary>
    public readonly struct CellChange
    {
        public readonly int tick;
        public readonly int index;
        public readonly int previousOwner;
        public readonly int newOwner;

        public CellChange(int tick, int index, int previousOwner, int newOwner)
        {
            this.tick = tick;
            this.index = index;
            this.previousOwner = previousOwner;
            this.newOwner = newOwner;
        }
    }

    /// <summary>
    /// A rectangular grid where every cell has at most one owner (a small integer, or
    /// <see cref="Unowned"/>). Pure data: no Unity objects, no clock, no randomness, so a run that
    /// paints the same cells in the same order ends in the same state.
    /// </summary>
    /// <remarks>
    /// Per-owner counts are maintained incrementally so share-of-board reads are O(1). Presentation
    /// watches <see cref="Version"/> and re-uploads only when it moves.
    /// </remarks>
    public sealed class OwnershipGrid
    {
        public const int Unowned = -1;

        readonly int[] _owners;
        readonly int[] _counts;
        readonly List<CellChange> _changes = new List<CellChange>();

        public int Width { get; }

        public int Height { get; }

        public int CellCount { get; }

        public int OwnerCount { get; }

        /// <summary>Increments whenever any cell actually changes owner (logged or not).</summary>
        public int Version { get; private set; }

        /// <summary>Row-major owners, index = y * Width + x. Live view of the internal array.</summary>
        public IReadOnlyList<int> Owners => _owners;

        /// <summary>Append-only log; only <c>SetOwner(..., logged: true)</c> writes it.</summary>
        public IReadOnlyList<CellChange> Changes => _changes;

        /// <summary>Creates a grid with every cell <see cref="Unowned"/>.</summary>
        public OwnershipGrid(int width, int height, int ownerCount)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Grid must be at least 1x1.");
            }

            if (ownerCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerCount), "Need at least one owner.");
            }

            Width = width;
            Height = height;
            CellCount = width * height;
            OwnerCount = ownerCount;
            _owners = new int[CellCount];
            _counts = new int[ownerCount];
            for (int i = 0; i < _owners.Length; i++)
            {
                _owners[i] = Unowned;
            }
        }

        public int Index(int x, int y)
        {
            return y * Width + x;
        }

        public void Coords(int index, out int x, out int y)
        {
            y = index / Width;
            x = index - y * Width;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public int GetOwner(int x, int y)
        {
            return _owners[y * Width + x];
        }

        public int GetOwner(int index)
        {
            return _owners[index];
        }

        /// <summary>Paints one cell. A no-op (no version bump, no log) if the owner is unchanged.</summary>
        public void SetOwner(int x, int y, int owner, int tick, bool logged = true)
        {
            int index = y * Width + x;
            int previous = _owners[index];
            if (previous == owner)
            {
                return;
            }

            ValidateOwner(owner);
            Apply(index, previous, owner);
            Version++;

            if (logged)
            {
                _changes.Add(new CellChange(tick, index, previous, owner));
            }
        }

        /// <summary>Setup helper: paints a rectangle (clipped to the grid). Not logged.</summary>
        public void FillRect(int x0, int y0, int w, int h, int owner)
        {
            ValidateOwner(owner);
            int xEnd = Math.Min(x0 + w, Width);
            int yEnd = Math.Min(y0 + h, Height);
            bool changed = false;

            for (int y = Math.Max(y0, 0); y < yEnd; y++)
            {
                for (int x = Math.Max(x0, 0); x < xEnd; x++)
                {
                    int index = y * Width + x;
                    int previous = _owners[index];
                    if (previous == owner)
                    {
                        continue;
                    }

                    Apply(index, previous, owner);
                    changed = true;
                }
            }

            if (changed)
            {
                Version++;
            }
        }

        /// <summary>Cells owned by <paramref name="owner"/>. <see cref="Unowned"/> is not counted: returns 0.</summary>
        public int CountOf(int owner)
        {
            return owner >= 0 && owner < OwnerCount ? _counts[owner] : 0;
        }

        public float ShareOf(int owner)
        {
            return (float)CountOf(owner) / CellCount;
        }

        void Apply(int index, int previous, int owner)
        {
            if (previous >= 0)
            {
                _counts[previous]--;
            }

            if (owner >= 0)
            {
                _counts[owner]++;
            }

            _owners[index] = owner;
        }

        void ValidateOwner(int owner)
        {
            if (owner < Unowned || owner >= OwnerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), $"Owner {owner} outside 0..{OwnerCount - 1}.");
            }
        }
    }
}
