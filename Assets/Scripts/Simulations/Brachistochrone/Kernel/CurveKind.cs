namespace SimulationLobby.Simulations.Brachistochrone.Kernel
{
    /// <summary>The track shapes a bead can roll down. All run from A = (0,0) to B = (bx, by), y = drop.</summary>
    public enum CurveKind
    {
        Straight = 0,
        Cycloid = 1,
        CircleArc = 2,
        Parabola = 3,
        SteepDrop = 4,
        DeepDip = 5,

        /// <summary>The cycloid with an uphill bump dented into it (comp-001 sabotage round). shape = bump height in world units.</summary>
        BumpedCycloid = 6,

        /// <summary>y = by sin(pi/2 u): steep start, flat finish. Added for the 8-shape round.</summary>
        Sine = 7,

        /// <summary>A straight run with a sine wobble. Added for the 8-shape round.</summary>
        Wave = 8
    }
}
