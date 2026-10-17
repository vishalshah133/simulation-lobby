using System;

namespace SimulationLobby.Simulations.PiClacks.Kernel
{
    /// <summary>What a clack was: the small block hitting the wall, or the two blocks meeting.</summary>
    public enum ClackKind
    {
        Wall = 0,
        Blocks = 1
    }

    /// <summary>One collision. <see cref="X"/> is the contact point, measured from the wall face (x = 0).</summary>
    public readonly struct ClackEvent
    {
        public readonly ClackKind Kind;
        public readonly double X;

        public ClackEvent(ClackKind kind, double x)
        {
            Kind = kind;
            X = x;
        }
    }

    /// <summary>
    /// Exact event-driven model of the Galperin billiard: a small block between a wall and a big block that
    /// slides toward it, every collision perfectly elastic, no friction. Pure double maths, no engine types.
    /// </summary>
    /// <remarks>
    /// Wall face at x = 0; positions are the blocks' LEFT faces. Only the velocities decide how many collisions
    /// happen, so the gaps are free tunables for framing and pacing (scaling every gap by s scales every event
    /// time by s). Each contact snaps the positions (small left face = 0 at the wall, small right face = big left
    /// face at a block hit) so rounding can't accumulate across the 3 million events of the heaviest round.
    /// The inner loop allocates nothing.
    /// </remarks>
    public sealed class BlockCollisionKernel
    {
        /// <summary>Hard stop on events inside one Step, so a rounding fault can never hang the editor.</summary>
        const long MaxEventsPerStep = 50_000_000;

        readonly double _smallMass;
        readonly double _bigMass;
        readonly double _smallSize;

        double _time;

        public BlockCollisionKernel(double smallMass, double bigMass, double smallSize,
                                    double smallGap, double bigGap, double speed)
        {
            _smallMass = smallMass;
            _bigMass = bigMass;
            _smallSize = smallSize;
            SmallX = smallGap;
            BigX = smallGap + smallSize + bigGap;
            SmallV = 0.0;
            BigV = -speed;
        }

        /// <summary>Small block's left face.</summary>
        public double SmallX { get; private set; }

        /// <summary>Big block's left face.</summary>
        public double BigX { get; private set; }

        public double SmallV { get; private set; }

        public double BigV { get; private set; }

        /// <summary>Collisions so far (wall plus block-block).</summary>
        public long Count { get; private set; }

        /// <summary>No collision can ever happen again: both blocks heading right, big at least as fast as small.</summary>
        public bool IsDone => SmallV >= 0.0 && BigV >= SmallV;

        /// <summary>Seconds since construction at the most recent collision (0 before the first).</summary>
        public double LastClackTime { get; private set; }

        /// <summary>
        /// Advance <paramref name="dt"/> seconds, resolving every collision inside it in order. Returns the
        /// collisions this step. <paramref name="onClack"/> (may be null) is called per collision, in order.
        /// </summary>
        public int Step(double dt, Action<ClackEvent> onClack)
        {
            // A zero or slightly negative dt from rounding just means "no time passes". NaN also lands here.
            double remaining = dt > 0.0 ? dt : 0.0;
            int clacks = 0;
            long guard = 0;

            while (true)
            {
                // Time to the next wall touch (small moving left) and the next block touch (small catching big).
                double tWall = double.PositiveInfinity;
                if (SmallV < 0.0)
                {
                    tWall = SmallX / -SmallV;
                }

                double tBlocks = double.PositiveInfinity;
                if (SmallV > BigV)
                {
                    tBlocks = (BigX - (SmallX + _smallSize)) / (SmallV - BigV);
                }

                bool wallFirst = tWall <= tBlocks;
                double tNext = wallFirst ? tWall : tBlocks;

                // Nothing left to happen, or the next event is beyond this step: just drift.
                if (double.IsPositiveInfinity(tNext) || tNext > remaining)
                {
                    SmallX += SmallV * remaining;
                    BigX += BigV * remaining;
                    _time += remaining;
                    return clacks;
                }

                if (tNext < 0.0)
                {
                    tNext = 0.0; // contact already (marginally) overlapped: resolve it now
                }

                SmallX += SmallV * tNext;
                BigX += BigV * tNext;
                _time += tNext;
                remaining -= tNext;

                ClackEvent clack;
                if (wallFirst)
                {
                    SmallV = -SmallV;
                    SmallX = 0.0;
                    clack = new ClackEvent(ClackKind.Wall, 0.0);
                }
                else
                {
                    double total = _smallMass + _bigMass;
                    double newSmall = ((_smallMass - _bigMass) * SmallV + 2.0 * _bigMass * BigV) / total;
                    double newBig = ((_bigMass - _smallMass) * BigV + 2.0 * _smallMass * SmallV) / total;
                    SmallV = newSmall;
                    BigV = newBig;
                    SmallX = BigX - _smallSize;
                    clack = new ClackEvent(ClackKind.Blocks, BigX);
                }

                Count++;
                clacks++;
                LastClackTime = _time;
                onClack?.Invoke(clack);

                if (++guard >= MaxEventsPerStep)
                {
                    return clacks; // runaway guard; never reached by a healthy round
                }
            }
        }

        /// <summary>
        /// Total collisions for a mass ratio M/m: ceil(pi / atan(sqrt(m/M))) - 1. For ratio 100^k this is the
        /// first k+1 digits of pi (3, 31, 314, ...).
        /// </summary>
        public static long ExpectedCount(double massRatio)
        {
            double steps = Math.PI / Math.Atan(Math.Sqrt(1.0 / massRatio));
            // The epsilon keeps an exactly-integer quotient (ratio 1 gives 4) from rounding up to the next integer.
            return (long)Math.Ceiling(steps - 1e-9) - 1;
        }
    }
}
