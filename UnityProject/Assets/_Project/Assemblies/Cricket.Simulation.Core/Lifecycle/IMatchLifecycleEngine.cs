using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Lifecycle
{
    /// <summary>
    /// Enforces the legal match lifecycle finite state machine from MatchSetup through Completed.
    /// </summary>
    public interface IMatchLifecycleEngine
    {
        MatchPhase CurrentPhase { get; }
        bool CanTransitionTo(MatchPhase targetPhase);
        void TransitionTo(MatchPhase targetPhase, string reason = "");
        void Reset();
    }
}
