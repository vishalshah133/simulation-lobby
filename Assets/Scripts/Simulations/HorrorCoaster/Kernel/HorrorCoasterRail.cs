using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Builds the shared <see cref="CoasterRail"/> from this format's config.</summary>
    public static class HorrorCoasterRail
    {
        public static CoasterRail FromConfig(HorrorCoasterConfig cfg)
        {
            return new CoasterRail(cfg.railPoints, cfg.railBankDeg, cfg.railLevelWeight);
        }
    }
}
