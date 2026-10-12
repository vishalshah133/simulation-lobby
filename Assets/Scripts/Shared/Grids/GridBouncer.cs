using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A ball that lives in grid space and paints the cells it bounces off. Plain data: the
    /// simulation owns the array of these and advances them from its fixed tick.
    /// </summary>
    public struct GridBall
    {
        public Vector2 position;
        public Vector2 velocity;
        public int owner;
        public int health;
        public bool alive;
        public float age;
    }

    /// <summary>
    /// Advances a <see cref="GridBall"/> across an <see cref="OwnershipGrid"/>. No physics engine:
    /// float math in a fixed order, so the same inputs give the same flips.
    /// </summary>
    public static class GridBouncer
    {
        const float Epsilon = 1e-4f;
        const int MaxSubSteps = 4096;

        /// <summary>
        /// Moves the ball by velocity * dt in sub-steps of at most <paramref name="maxStep"/> cells.
        /// Reflects off the outer edges. Entering a cell owned by anyone else (including unowned) paints
        /// it, costs one health, keeps the ball in its previous cell and negates the crossed axis; x is
        /// resolved before y. Health reaching 0 kills the ball and stops. Returns cells painted.
        /// </summary>
        public static int Step(ref GridBall ball, OwnershipGrid grid, float dt, int tick, float maxStep = 0.25f)
        {
            if (!ball.alive)
            {
                return 0;
            }

            ball.age += dt;

            float dist = Mathf.Max(Mathf.Abs(ball.velocity.x), Mathf.Abs(ball.velocity.y)) * dt;
            if (!(dist > 0f))
            {
                return 0;
            }

            maxStep = Mathf.Max(maxStep, 0.01f);
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / maxStep), 1, MaxSubSteps);
            float subDt = dt / steps;
            float maxX = grid.Width - Epsilon;
            float maxY = grid.Height - Epsilon;
            int painted = 0;

            for (int s = 0; s < steps; s++)
            {
                int oldX = Mathf.Clamp((int)Mathf.Floor(ball.position.x), 0, grid.Width - 1);
                int oldY = Mathf.Clamp((int)Mathf.Floor(ball.position.y), 0, grid.Height - 1);
                float nx = ball.position.x + ball.velocity.x * subDt;
                float ny = ball.position.y + ball.velocity.y * subDt;
                Vector2 v = ball.velocity;

                // Outer edges: clamp just inside and bounce.
                if (nx < 0f) { nx = Epsilon; v.x = -v.x; }
                else if (nx > maxX) { nx = maxX; v.x = -v.x; }

                if (ny < 0f) { ny = Epsilon; v.y = -v.y; }
                else if (ny > maxY) { ny = maxY; v.y = -v.y; }

                // x crossing: test the cell beside us, on our current row.
                int newX = (int)Mathf.Floor(nx);
                if (newX != oldX)
                {
                    if (grid.GetOwner(newX, oldY) != ball.owner)
                    {
                        grid.SetOwner(newX, oldY, ball.owner, tick);
                        painted++;
                        ball.health--;
                        v.x = -v.x;
                        nx = newX > oldX ? oldX + 1f - Epsilon : oldX + Epsilon;
                    }
                }

                // y crossing: test using the (possibly corrected) x.
                if (ball.health > 0)
                {
                    int curX = (int)Mathf.Floor(nx);
                    int newY = (int)Mathf.Floor(ny);
                    if (newY != oldY && grid.GetOwner(curX, newY) != ball.owner)
                    {
                        grid.SetOwner(curX, newY, ball.owner, tick);
                        painted++;
                        ball.health--;
                        v.y = -v.y;
                        ny = newY > oldY ? oldY + 1f - Epsilon : oldY + Epsilon;
                    }
                }

                ball.position = new Vector2(nx, ny);
                ball.velocity = v;

                if (ball.health <= 0)
                {
                    ball.alive = false;
                    break;
                }
            }

            return painted;
        }
    }
}
