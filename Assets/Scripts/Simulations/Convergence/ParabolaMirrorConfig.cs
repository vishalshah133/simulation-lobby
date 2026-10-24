using System;
using SimulationLobby.Simulations.Convergence.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>
    /// <c>conv</c> topic: a row of balls dropped straight down into a parabolic mirror. Every ball crosses the
    /// focus at the same instant, bounces again and rises out as a flat vertical line. Rounds only escalate the
    /// ball count (conv-003: 5, 10, 50, 100, 500, 1000). The base config's text and pacing defaults are not
    /// overridden here; the scene builder sets them when it creates the asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ParabolaMirrorConfig",
        menuName = "Simulation Lobby/Configs/Math Convergence: Parabola Mirror (conv)",
        order = 17)]
    public sealed class ParabolaMirrorConfig : ConvergenceConfig
    {
        [Serializable]
        public struct ParabolaRound
        {
            public ConvergenceRoundInfo info;

            [Tooltip("Focal length f of the parabola y = vertexY + x^2 / 4f, world units. The focus is at (0, vertexY + f).")]
            public float focalLength;

            [Tooltip("The wall spans x = -rimHalfWidth..+rimHalfWidth and is open above the rim, world units.")]
            public float rimHalfWidth;

            [Tooltip("Height of the parabola's vertex (the bottom of the bowl), world units.")]
            public float vertexY;

            [Tooltip("Height of the row the balls drop from. Must be above the rim.")]
            public float dropY;

            [Tooltip("Constant speed of every ball, units/second.")]
            public float speed;
        }

        [Tooltip("Fraction of the rim half-width kept clear inside the rim at each end of the drop row.")]
        [Range(0f, 0.2f)] public float dropMargin = 0.06f;

        public ParabolaRound[] rounds =
        {
            Round("5 BALLS. GUESS?", "ALL {n}! SAME MOMENT.", 1.0f, 5),
            Round("10 BALLS...", "ALL {n}!", 1.0f, 10),
            Round("50 BALLS...", "ALL {n}!", 0.8f, 50),
            Round("100 BALLS...", "ALL {n}!", 0.7f, 100),
            Round("500 BALLS...", "ALL {n}!", 0.5f, 500),
            Round("1000 BALLS?!", "ALL {n}. EVERY SINGLE ONE.", 0.5f, 1000)
        };

        static ParabolaRound Round(string caption, string result, float trail, int balls)
        {
            return new ParabolaRound
            {
                info = new ConvergenceRoundInfo
                {
                    caption = caption,
                    result = result,
                    expected = ConvergenceAnswer.Meet,
                    runSeconds = 6.125f,
                    trailSeconds = trail,
                    ballCount = balls
                },
                focalLength = 0.8f, rimHalfWidth = 4f, vertexY = -4.5f, dropY = 4.5f, speed = 3.2f
            };
        }

        public override int RoundCount => rounds != null ? rounds.Length : 0;

        public override ConvergenceRoundInfo GetRound(int index) => rounds[index].info;

        public override string DescribeRound(int index)
        {
            ParabolaRound r = rounds[index];
            return $"f {r.focalLength:0.##}, rim ±{r.rimHalfWidth:0.##}, vertex y {r.vertexY:0.##}, " +
                   $"drop y {r.dropY:0.##}, speed {r.speed:0.##}";
        }

        public override IConvergenceKernel CreateKernel(int index)
        {
            ParabolaRound r = rounds[index];
            return new ParabolaMirrorKernel(r.focalLength, r.rimHalfWidth, r.vertexY, r.dropY, r.speed, dropMargin);
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            for (int i = 0; i < rounds.Length; i++)
            {
                ParabolaRound r = rounds[i];
                if (r.focalLength <= 0f || r.rimHalfWidth <= 0f || r.speed <= 0f)
                {
                    error = $"Round {i + 1}: focalLength, rimHalfWidth and speed must be greater than zero.";
                    return false;
                }

                double rimHeight = r.vertexY + (double)r.rimHalfWidth * r.rimHalfWidth / (4.0 * r.focalLength);
                if (r.dropY <= rimHeight)
                {
                    error = $"Round {i + 1}: dropY must be above the rim height ({rimHeight:0.##}).";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
