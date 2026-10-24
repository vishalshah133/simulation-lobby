namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Inrun, kicker, gap and catch crate. Static: the risk comes from ball speed at the lip.
    /// Short jumps land in the net under the gap, which leads to <see cref="MachineStation.recycleExit"/>.
    /// </summary>
    public sealed class SkiJumpStation : MachineStation
    {
        public override void Step(float simTime, float dt) { }
    }
}
