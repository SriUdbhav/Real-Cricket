using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Dls
{
    /// <summary>
    /// Supplies verified, properly licensed DLS resource table lookups.
    /// No approximate or fabricated resource tables are permitted in production builds.
    /// </summary>
    public interface IDlsResourceTableProvider
    {
        DlsResourceTableMetadata Metadata { get; }
        bool IsLicensedTableAvailable { get; }

        bool TryGetRemainingResourcePercentage(
            int oversRemaining,
            int ballsRemainingInOver,
            int wicketsLost,
            out double resourcePercentage);

        bool TryGetCompetitionG50(MatchFormat format, out double g50Score);
    }

    /// <summary>
    /// Clean abstraction for rain-interrupted revised target and par-score calculations.
    /// Remains explicitly disabled until a verified licensed <see cref="IDlsResourceTableProvider"/> is supplied.
    /// </summary>
    public interface IDlsCalculator
    {
        bool IsOperational(CompetitionRulesProfile profile);
        DlsCalculationResult CalculateRevisedTarget(CompetitionRulesProfile profile, DlsTargetRequest request);
        DlsCalculationResult CalculateParScore(CompetitionRulesProfile profile, DlsParScoreRequest request);
    }
}
