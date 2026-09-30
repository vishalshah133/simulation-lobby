namespace SimulationLobby.Shared
{
    /// <summary>
    /// One thing escalation can change. A tiny implementation per target rather than a switch
    /// statement inside the rule — adding <c>WallLayerCount</c> later must not touch existing targets.
    /// </summary>
    public interface IEscalationTarget
    {
        /// <summary>Human-readable target name, recorded in results so a run's premise is legible.</summary>
        string TargetName { get; }

        /// <summary>The value escalation is driving. Read by the HUD; never written by it.</summary>
        float CurrentValue { get; }

        /// <summary>
        /// Set the driven value. The rule has already applied mode, magnitude and clamping, so an
        /// implementation only has to make the value real in the scene.
        /// </summary>
        void SetValue(float value);
    }
}
