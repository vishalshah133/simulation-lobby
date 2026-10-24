using System;
using SimulationLobby.Simulations.Convergence.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>
    /// <c>conv</c> topic: bodies fired from a point inside an ellipse. From a focus they all meet at the
    /// other focus; nudge the start off the focus and they scatter. Defaults are the conv-001 plan at x1.25 (revision 2026-10-14: red/blue story, 3 rounds). The base config's text and pacing defaults are already this topic's, so they are not overridden here.
    /// </summary>
    /// <remarks>
    /// The scene builder sets <c>fixedTimestep</c> to 1/120 when it creates the asset: smooth motion at
    /// 60 fps, and the meet window is sampled every 8 ms. The base default is left alone.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "EllipseFocusConfig",
        menuName = "Simulation Lobby/Configs/Math Convergence: Ellipse Focus (conv)",
        order = 16)]
    public sealed class EllipseFocusConfig : ConvergenceConfig
    {
        [Serializable]
        public struct EllipseRound
        {
            public ConvergenceRoundInfo info;

            [Tooltip("Half the ellipse width (x semi-axis), world units.")]
            public float halfWidth;

            [Tooltip("Half the ellipse height (y semi-axis), world units. Equal to halfWidth is a circle.")]
            public float halfHeight;

            [Tooltip("Start position relative to the positive focus (the centre for a circle).")]
            public Vector2 startOffset;

            [Tooltip("Target position relative to the negative focus (the centre for a circle). Non-zero leaves the " +
                     "blue dot where it was while the ellipse changes under it.")]
            public Vector2 targetOffset;

            [Tooltip("Constant speed of every body, units/second.")]
            public float speed;
        }

        public EllipseRound[] rounds =
        {
            new EllipseRound
            {
                info = new ConvergenceRoundInfo { caption = "WATCH BLUE", result = "ALL {n}. AT THE SAME TIME.", expected = ConvergenceAnswer.Meet, runSeconds = 2.08f, trailSeconds = 0.4f },
                halfWidth = 3.75f, halfHeight = 5f, startOffset = Vector2.zero, speed = 5f
            },
            new EllipseRound
            {
                info = new ConvergenceRoundInfo { caption = "NOW MOVE RED A LITTLE...", result = "ONLY {n} HIT.", expected = ConvergenceAnswer.Miss, runSeconds = 7f, trailSeconds = 1.5f },
                halfWidth = 3.75f, halfHeight = 5f, startOffset = new Vector2(0.7f, 0f), speed = 5f
            },
            new EllipseRound
            {
                info = new ConvergenceRoundInfo { caption = "PUT IT BACK...", result = "ALL {n} AGAIN.", expected = ConvergenceAnswer.Meet, runSeconds = 2.08f, trailSeconds = 0.4f },
                halfWidth = 3.75f, halfHeight = 5f, startOffset = Vector2.zero, speed = 5f
            }
        };

        public override int RoundCount => rounds != null ? rounds.Length : 0;

        public override ConvergenceRoundInfo GetRound(int index) => rounds[index].info;

        public override IConvergenceKernel CreateKernel(int index)
        {
            EllipseRound r = rounds[index];
            return Make(r);
        }

        static EllipseBilliardKernel Make(EllipseRound r)
        {
            return new EllipseBilliardKernel(
                r.halfWidth, r.halfHeight,
                new Double2(r.startOffset.x, r.startOffset.y),
                new Double2(r.targetOffset.x, r.targetOffset.y),
                r.speed);
        }

        public override string DescribeRound(int index)
        {
            EllipseRound r = rounds[index];
            return $"half {r.halfWidth:0.##} x {r.halfHeight:0.##}, " +
                   $"red offset ({r.startOffset.x:0.##}, {r.startOffset.y:0.##}), " +
                   $"blue offset ({r.targetOffset.x:0.##}, {r.targetOffset.y:0.##})";
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            for (int i = 0; i < rounds.Length; i++)
            {
                EllipseRound r = rounds[i];
                if (r.halfWidth <= 0f || r.halfHeight <= 0f || r.speed <= 0f)
                {
                    error = $"Round {i + 1}: halfWidth, halfHeight and speed must be greater than zero.";
                    return false;
                }

                EllipseBilliardKernel kernel = Make(r);
                double sx = kernel.Start.X / r.halfWidth;
                double sy = kernel.Start.Y / r.halfHeight;
                if (sx * sx + sy * sy >= 1.0)
                {
                    error = $"Round {i + 1}: the start point is not inside the ellipse.";
                    return false;
                }

                double tx = kernel.Target.X / r.halfWidth;
                double ty = kernel.Target.Y / r.halfHeight;
                if (tx * tx + ty * ty >= 1.0)
                {
                    error = $"Round {i + 1}: the target point is not inside the ellipse.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
