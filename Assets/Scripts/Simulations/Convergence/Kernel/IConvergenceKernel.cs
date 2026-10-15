using System.Collections.Generic;

namespace SimulationLobby.Simulations.Convergence.Kernel
{
    /// <summary>A point in double precision. Kernels stay off <c>UnityEngine</c> so the answer is exact.</summary>
    public readonly struct Double2
    {
        public readonly double X;
        public readonly double Y;

        public Double2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Double2 operator +(Double2 a, Double2 b) => new Double2(a.X + b.X, a.Y + b.Y);

        public static Double2 operator -(Double2 a, Double2 b) => new Double2(a.X - b.X, a.Y - b.Y);

        public static Double2 operator *(Double2 a, double s) => new Double2(a.X * s, a.Y * s);

        public double Length => System.Math.Sqrt(X * X + Y * Y);
    }

    /// <summary>One body touching the boundary inside a step. Presentation turns these into notes.</summary>
    public readonly struct WallHit
    {
        public readonly int Particle;
        public readonly Double2 Point;

        /// <summary>
        /// 0..1 along the boundary, chosen by the kernel so a pass reads as a rising sweep: 0 at the
        /// boundary point nearest where the bodies left from, 1 at the farthest.
        /// </summary>
        public readonly double Pitch01;

        public WallHit(int particle, Double2 point, double pitch01)
        {
            Particle = particle;
            Point = point;
            Pitch01 = pitch01;
        }
    }

    /// <summary>
    /// The motion model for one <c>conv</c> round: which bodies move, how, inside what, and where
    /// the maths says they meet. Each topic (ellipse focus, cycloid, pendulum wave) is a new kernel;
    /// the round shell, scoring, HUD and presentation never change.
    /// </summary>
    /// <remarks>
    /// Must be deterministic and free of <c>UnityEngine</c>: doubles only, no randomness of its own.
    /// Anything seeded is passed in through <see cref="Launch"/>.
    /// </remarks>
    public interface IConvergenceKernel
    {
        /// <summary>Bodies in play. Zero before <see cref="Launch"/>.</summary>
        int Count { get; }

        /// <summary>Where the bodies start. Valid before launch, for the anticipation marker.</summary>
        Double2 Start { get; }

        /// <summary>
        /// Where <see cref="Start"/> would have to be for the theorem to hold (for the ellipse: the focus).
        /// Equal to <see cref="Start"/> unless a round deliberately moves the start off it.
        /// </summary>
        Double2 IdealStart { get; }

        /// <summary>Where the theorem says they all arrive together.</summary>
        Double2 Target { get; }

        /// <summary>Seconds from launch to the first arrival at <see cref="Target"/>.</summary>
        double ExpectedMeetSeconds { get; }

        /// <summary>
        /// Place <paramref name="count"/> bodies at <see cref="Start"/> and set them moving. The
        /// angle offset (radians) is the one seeded, cosmetic input.
        /// </summary>
        void Launch(int count, double angleOffsetRadians);

        /// <summary>Advance <paramref name="dt"/> seconds. Appends every boundary contact to <paramref name="hits"/>.</summary>
        void Step(double dt, List<WallHit> hits);

        Double2 GetPosition(int index);

        /// <summary>How many bodies are within <paramref name="radius"/> of <paramref name="point"/>.</summary>
        int CountWithin(Double2 point, double radius);

        /// <summary>
        /// The boundary to draw, as exactly <paramref name="samples"/> points (closed shapes do not
        /// repeat the first point). A fixed count lets presentation morph one round's outline into the next.
        /// </summary>
        void SampleOutline(int samples, List<Double2> points);

        /// <summary>True if the outline is a closed loop.</summary>
        bool OutlineClosed { get; }
    }
}
