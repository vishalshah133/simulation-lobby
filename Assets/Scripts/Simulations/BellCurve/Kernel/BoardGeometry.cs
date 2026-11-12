using UnityEngine;

namespace SimulationLobby.Simulations.BellCurve.Kernel
{
    /// <summary>
    /// Pure board maths for the <c>bell</c> format: where a marble is at a given age, given its coin-flip path.
    /// Board space: x = 0 on the spout's centre line, y = 0 at the marble-centre height on the first peg, up is
    /// positive. Row r holds r+1 pegs; a marble that has gone right j times sits at x = (j - r/2) * dx on row r.
    /// There is no physics solver: the path is the outcome of seeded coin flips and the motion is an arc between
    /// the pegs, so a run is exactly reproducible and costs nothing per marble.
    /// </summary>
    public readonly struct BoardGeometry
    {
        public readonly int Rows;
        public readonly float Dx;
        public readonly float Dy;
        public readonly float HopSeconds;
        public readonly float Bump;
        public readonly float DropHeight;
        public readonly float Gravity;

        public BoardGeometry(int rows, float dx, float dy, float hopSeconds, float bump, float dropHeight, float gravity)
        {
            Rows = rows;
            Dx = dx;
            Dy = dy;
            HopSeconds = hopSeconds;
            Bump = bump;
            DropHeight = dropHeight;
            Gravity = gravity;
        }

        public int Bins => Rows + 1;

        /// <summary>Spout to the first peg, free fall.</summary>
        public float DropSeconds => Mathf.Sqrt(2f * DropHeight / Gravity);

        /// <summary>Age at which a marble leaves the last peg row and starts its final fall.</summary>
        public float FallStartSeconds => DropSeconds + Rows * HopSeconds;

        public float PegX(int row, int rights) => (rights - row * 0.5f) * Dx;

        public float RowY(int row) => -row * Dy;

        /// <summary>Bin k collects the marbles that went right k times; its centre is the virtual peg of row <see cref="Rows"/>.</summary>
        public float BinX(int bin) => PegX(Rows, bin);

        public float FallSeconds(float fallDistance) => Mathf.Sqrt(2f * Mathf.Max(0f, fallDistance) / Gravity);

        /// <summary>Age at which a marble that ends on <paramref name="targetY"/> lands.</summary>
        public float LandAge(float targetY) => FallStartSeconds + FallSeconds(RowY(Rows) - targetY);

        /// <summary>Right turns among the first <paramref name="steps"/> flips of <paramref name="bits"/>.</summary>
        public static int RightsInFirst(uint bits, int steps)
        {
            int count = 0;
            for (int i = 0; i < steps; i++)
            {
                count += (int)((bits >> i) & 1u);
            }

            return count;
        }

        /// <summary>
        /// Position of a marble <paramref name="age"/> seconds after it left the spout. <paramref name="hop"/> is
        /// -1 while it falls to the first peg, 0..Rows-1 on the hop that starts at that row, and <see cref="Rows"/>
        /// in the final fall. Returns false before the marble exists (age &lt; 0).
        /// </summary>
        public bool TryPose(uint bits, int bin, float age, out Vector2 position, out int hop)
        {
            position = default;
            hop = -2;
            if (age < 0f)
            {
                return false;
            }

            float drop = DropSeconds;
            if (age < drop)
            {
                hop = -1;
                position = new Vector2(0f, DropHeight - 0.5f * Gravity * age * age);
                return true;
            }

            float a = age - drop;
            int row = (int)(a / HopSeconds);
            if (row < Rows)
            {
                float u = a / HopSeconds - row;
                int rights = RightsInFirst(bits, row);
                float sign = ((bits >> row) & 1u) != 0u ? 0.5f : -0.5f;
                float x = PegX(row, rights) + sign * Dx * u;
                float y = RowY(row) - Dy * u + 4f * Bump * u * (1f - u);
                position = new Vector2(x, y);
                hop = row;
                return true;
            }

            float t = a - Rows * HopSeconds;
            position = new Vector2(BinX(bin), RowY(Rows) - 0.5f * Gravity * t * t);
            hop = Rows;
            return true;
        }
    }
}
