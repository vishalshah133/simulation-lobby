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
        DeepDip = 5
    }
}
