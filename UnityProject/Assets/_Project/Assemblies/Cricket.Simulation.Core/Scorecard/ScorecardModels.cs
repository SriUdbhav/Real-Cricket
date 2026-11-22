using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Scorecard
{
    [Serializable]
    public sealed class ExtrasBreakdown
    {
        public int Wides { get; }
        public int NoBalls { get; }
        public int Byes { get; }
        public int LegByes { get; }
        public int PenaltyRuns { get; }
        public int Total => Wides + NoBalls + Byes + LegByes + PenaltyRuns;

        public ExtrasBreakdown(int wides, int noBalls, int byes, int legByes, int penaltyRuns)
        {
            Wides = wides;
            NoBalls = noBalls;
            Byes = byes;
            LegByes = legByes;
            PenaltyRuns = penaltyRuns;
        }
    }

    [Serializable]
    public sealed class BatterScorecardEntry
    {
        public string PlayerId { get; }
        public int Runs { get; }
        public int BallsFaced { get; }
        public int Fours { get; }
        public int Sixes { get; }
        public int DotBalls { get; }
        public bool IsOut { get; }
        public DismissalRecord? Dismissal { get; }
        public double StrikeRate => BallsFaced > 0 ? (Runs * 100.0) / BallsFaced : 0.0;

        public BatterScorecardEntry(
            string playerId,
            int runs,
            int ballsFaced,
            int fours,
            int sixes,
            int dotBalls,
            bool isOut,
            DismissalRecord? dismissal)
        {
            PlayerId = playerId;
            Runs = runs;
            BallsFaced = ballsFaced;
            Fours = fours;
            Sixes = sixes;
            DotBalls = dotBalls;
            IsOut = isOut;
            Dismissal = dismissal;
        }
    }

    [Serializable]
    public sealed class BowlerScorecardEntry
    {
        public string PlayerId { get; }
        public int CompletedOvers { get; }
        public int BallsInPartialOver { get; }
        public int Maidens { get; }
        public int RunsConceded { get; }
        public int Wickets { get; }
        public int Wides { get; }
        public int NoBalls { get; }
        public int DotBalls { get; }
        public string OversDisplay { get; }
        public double EconomyRate { get; }

        public BowlerScorecardEntry(
            string playerId,
            int completedOvers,
            int ballsInPartialOver,
            int maidens,
            int runsConceded,
            int wickets,
            int wides,
            int noBalls,
            int dotBalls,
            int ballsPerOver)
        {
            PlayerId = playerId;
            CompletedOvers = completedOvers;
            BallsInPartialOver = ballsInPartialOver;
            Maidens = maidens;
            RunsConceded = runsConceded;
            Wickets = wickets;
            Wides = wides;
            NoBalls = noBalls;
            DotBalls = dotBalls;
            OversDisplay = BallsInPartialOver > 0 ? $"{CompletedOvers}.{BallsInPartialOver}" : $"{CompletedOvers}";
            double oversDecimal = CompletedOvers + (BallsInPartialOver / (double)ballsPerOver);
            EconomyRate = oversDecimal > 0.0 ? RunsConceded / oversDecimal : 0.0;
        }
    }

    [Serializable]
    public sealed class FallOfWicketEntry
    {
        public int WicketNumber { get; }
        public int ScoreAtFall { get; }
        public int OverNumber { get; }
        public int LegalBallInOver { get; }
        public string OverDisplay { get; }
        public string OutBatterId { get; }
        public DismissalType DismissalType { get; }
        public string BowlerId { get; }
        public string? FielderId { get; }
        public long BallSequenceId { get; }

        public FallOfWicketEntry(
            int wicketNumber,
            int scoreAtFall,
            int overNumber,
            int legalBallInOver,
            string overDisplay,
            string outBatterId,
            DismissalType dismissalType,
            string bowlerId,
            string? fielderId,
            long ballSequenceId)
        {
            WicketNumber = wicketNumber;
            ScoreAtFall = scoreAtFall;
            OverNumber = overNumber;
            LegalBallInOver = legalBallInOver;
            OverDisplay = overDisplay;
            OutBatterId = outBatterId;
            DismissalType = dismissalType;
            BowlerId = bowlerId;
            FielderId = fielderId;
            BallSequenceId = ballSequenceId;
        }
    }

    [Serializable]
    public sealed class PartnershipEntry
    {
        public int WicketNumber { get; }
        public string BatterAId { get; }
        public string BatterBId { get; }
        public int TotalRuns { get; }
        public int LegalBalls { get; }
        public int BatterARuns { get; }
        public int BatterBRuns { get; }
        public int ExtrasRuns { get; }
        public bool IsUnbroken { get; }

        public PartnershipEntry(
            int wicketNumber,
            string batterAId,
            string batterBId,
            int totalRuns,
            int legalBalls,
            int batterARuns,
            int batterBRuns,
            int extrasRuns,
            bool isUnbroken)
        {
            WicketNumber = wicketNumber;
            BatterAId = batterAId;
            BatterBId = batterBId;
            TotalRuns = totalRuns;
            LegalBalls = legalBalls;
            BatterARuns = batterARuns;
            BatterBRuns = batterBRuns;
            ExtrasRuns = extrasRuns;
            IsUnbroken = isUnbroken;
        }
    }

    [Serializable]
    public sealed class OverSummaryEntry
    {
        public int OverNumber { get; }
        public IReadOnlyList<string> BowlerIds { get; }
        public int RunsInOver { get; }
        public int WicketsInOver { get; }
        public int LegalBalls { get; }
        public int ExtrasInOver { get; }
        public bool IsMaiden { get; }
        public int CumulativeScore { get; }
        public int CumulativeWickets { get; }
        public IReadOnlyList<string> BallNotations { get; }

        public OverSummaryEntry(
            int overNumber,
            IReadOnlyList<string> bowlerIds,
            int runsInOver,
            int wicketsInOver,
            int legalBalls,
            int extrasInOver,
            bool isMaiden,
            int cumulativeScore,
            int cumulativeWickets,
            IReadOnlyList<string> ballNotations)
        {
            OverNumber = overNumber;
            BowlerIds = bowlerIds;
            RunsInOver = runsInOver;
            WicketsInOver = wicketsInOver;
            LegalBalls = legalBalls;
            ExtrasInOver = extrasInOver;
            IsMaiden = isMaiden;
            CumulativeScore = cumulativeScore;
            CumulativeWickets = cumulativeWickets;
            BallNotations = ballNotations;
        }
    }

    /// <summary>
    /// Pure read-only input for projecting an innings scorecard solely from immutable BallEventRecord history.
    /// </summary>
    [Serializable]
    public sealed class InningsProjectionInput
    {
        public int InningsNumber { get; }
        public string BattingTeamId { get; }
        public string BowlingTeamId { get; }
        public bool IsSuperOver { get; }
        public int SuperOverRound { get; }
        public int? TargetRuns { get; }
        public InningsEndReason EndReason { get; }
        public IReadOnlyList<string> BattingOrderPlayerIds { get; }
        public IReadOnlyList<BallEventRecord> BallLog { get; }

        public InningsProjectionInput(
            int inningsNumber,
            string battingTeamId,
            string bowlingTeamId,
            bool isSuperOver,
            int superOverRound,
            int? targetRuns,
            InningsEndReason endReason,
            IReadOnlyList<string> battingOrderPlayerIds,
            IReadOnlyList<BallEventRecord> ballLog)
        {
            InningsNumber = inningsNumber;
            BattingTeamId = battingTeamId ?? string.Empty;
            BowlingTeamId = bowlingTeamId ?? string.Empty;
            IsSuperOver = isSuperOver;
            SuperOverRound = superOverRound;
            TargetRuns = targetRuns;
            EndReason = endReason;
            BattingOrderPlayerIds = battingOrderPlayerIds ?? Array.Empty<string>();
            BallLog = ballLog ?? Array.Empty<BallEventRecord>();
        }
    }

    [Serializable]
    public sealed class InningsScorecardSnapshot
    {
        public int InningsNumber { get; }
        public string BattingTeamId { get; }
        public string BowlingTeamId { get; }
        public bool IsSuperOver { get; }
        public int SuperOverRound { get; }
        public int? TargetRuns { get; }
        public InningsEndReason EndReason { get; }
        public int TotalRuns { get; }
        public int WicketsLost { get; }
        public int CompletedOvers { get; }
        public int LegalBallsInCurrentOver { get; }
        public string OversDisplay { get; }
        public double RunRate { get; }
        public ExtrasBreakdown Extras { get; }
        public IReadOnlyList<BatterScorecardEntry> BattingEntries { get; }
        public IReadOnlyList<string> DidNotBatPlayerIds { get; }
        public IReadOnlyList<BowlerScorecardEntry> BowlingEntries { get; }
        public IReadOnlyList<FallOfWicketEntry> FallOfWickets { get; }
        public IReadOnlyList<PartnershipEntry> Partnerships { get; }
        public IReadOnlyList<OverSummaryEntry> OverSummaries { get; }

        public InningsScorecardSnapshot(
            int inningsNumber,
            string battingTeamId,
            string bowlingTeamId,
            bool isSuperOver,
            int superOverRound,
            int? targetRuns,
            InningsEndReason endReason,
            int totalRuns,
            int wicketsLost,
            int completedOvers,
            int legalBallsInCurrentOver,
            int ballsPerOver,
            ExtrasBreakdown extras,
            IReadOnlyList<BatterScorecardEntry> battingEntries,
            IReadOnlyList<string> didNotBatPlayerIds,
            IReadOnlyList<BowlerScorecardEntry> bowlingEntries,
            IReadOnlyList<FallOfWicketEntry> fallOfWickets,
            IReadOnlyList<PartnershipEntry> partnerships,
            IReadOnlyList<OverSummaryEntry> overSummaries)
        {
            InningsNumber = inningsNumber;
            BattingTeamId = battingTeamId;
            BowlingTeamId = bowlingTeamId;
            IsSuperOver = isSuperOver;
            SuperOverRound = superOverRound;
            TargetRuns = targetRuns;
            EndReason = endReason;
            TotalRuns = totalRuns;
            WicketsLost = wicketsLost;
            CompletedOvers = completedOvers;
            LegalBallsInCurrentOver = legalBallsInCurrentOver;
            OversDisplay = LegalBallsInCurrentOver > 0 ? $"{CompletedOvers}.{LegalBallsInCurrentOver}" : $"{CompletedOvers}";
            double oversDecimal = CompletedOvers + (LegalBallsInCurrentOver / (double)ballsPerOver);
            RunRate = oversDecimal > 0.0 ? TotalRuns / oversDecimal : 0.0;
            Extras = extras;
            BattingEntries = battingEntries;
            DidNotBatPlayerIds = didNotBatPlayerIds;
            BowlingEntries = bowlingEntries;
            FallOfWickets = fallOfWickets;
            Partnerships = partnerships;
            OverSummaries = overSummaries;
        }
    }

    [Serializable]
    public sealed class ScorecardSnapshot
    {
        public string MatchId { get; }
        public MatchResult Result { get; }
        public IReadOnlyList<InningsScorecardSnapshot> InningsScorecards { get; }
        public IReadOnlyList<InningsScorecardSnapshot> SuperOverScorecards { get; }

        public ScorecardSnapshot(
            string matchId,
            MatchResult result,
            IReadOnlyList<InningsScorecardSnapshot> inningsScorecards,
            IReadOnlyList<InningsScorecardSnapshot> superOverScorecards)
        {
            MatchId = matchId ?? string.Empty;
            Result = result ?? throw new ArgumentNullException(nameof(result));
            InningsScorecards = inningsScorecards ?? Array.Empty<InningsScorecardSnapshot>();
            SuperOverScorecards = superOverScorecards ?? Array.Empty<InningsScorecardSnapshot>();
        }
    }
}
