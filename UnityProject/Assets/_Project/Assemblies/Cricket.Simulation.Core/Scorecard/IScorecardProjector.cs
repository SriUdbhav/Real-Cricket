using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Scorecard
{
    /// <summary>
    /// Stateless projection service that rebuilds complete match and innings scorecards
    /// purely from immutable <see cref="BallEventRecord"/> event logs without modifying match state.
    /// </summary>
    public interface IScorecardProjector
    {
        InningsScorecardSnapshot ProjectInnings(InningsProjectionInput input, CompetitionRulesProfile rules);
        ScorecardSnapshot ProjectMatch(MatchState matchState);
    }
}
