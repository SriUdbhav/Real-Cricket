using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Rules
{
    /// <summary>
    /// Authoritative, competition-profile-driven cricket rules and scoring engine.
    /// Completely independent of visual animations, rendering, and network transport.
    /// </summary>
    public interface ICricketRulesEngine
    {
        MatchState State { get; }

        void InitializeMatch(MatchConfig config);
        void RecordToss(string tossWinnerTeamId, TossDecision decision);
        InningsState StartNextInnings(bool enforceFollowOn = false);
        InningsState StartSuperOverInnings(IReadOnlyList<string> nominatedBatters);
        void StartOver(string bowlerId);
        BallEventRecord EvaluateAndCommitDelivery(DeliveryResolutionInput input);
        void DeclareCurrentInnings();
        bool IsFollowOnEligible();
        FieldValidationResult ValidateFieldPlacement(IReadOnlyList<FielderPlacement> movableFielders, int oneBasedOverNumber);

        /// <summary>
        /// Calculates Net Run Rate (NRR) for tournament standings using the active competition profile.
        /// </summary>
        double CalculateNetRunRate(
            int totalRunsScored,
            int legalBallsFaced,
            bool wasAllOutInAnyInningsAdjusted,
            int fullQuotaBallsForAdjustedInnings,
            int totalRunsConceded,
            int legalBallsBowled,
            bool opponentAllOutAdjusted,
            int fullQuotaBallsForOpponentAdjusted);
    }
}
