using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Dls
{
    public enum DlsCalculationStatus
    {
        Succeeded = 0,
        DisabledByCompetitionProfile = 1,
        UnsupportedNoLicensedResourceTable = 2,
        InvalidInput = 3
    }

    /// <summary>
    /// Immutable record of a single weather/light interruption that reduces available overs.
    /// </summary>
    [Serializable]
    public sealed class RainInterruptionRecord
    {
        public int InningsNumber { get; }
        public int OversCompletedBeforeInterruption { get; }
        public int BallsInOverBeforeInterruption { get; }
        public int WicketsLostAtInterruption { get; }
        public int OversLost { get; }
        public string Reason { get; }

        public RainInterruptionRecord(
            int inningsNumber,
            int oversCompletedBeforeInterruption,
            int ballsInOverBeforeInterruption,
            int wicketsLostAtInterruption,
            int oversLost,
            string reason = "Rain")
        {
            InningsNumber = inningsNumber;
            OversCompletedBeforeInterruption = oversCompletedBeforeInterruption;
            BallsInOverBeforeInterruption = ballsInOverBeforeInterruption;
            WicketsLostAtInterruption = wicketsLostAtInterruption;
            OversLost = oversLost;
            Reason = string.IsNullOrWhiteSpace(reason) ? "Weather" : reason;
        }
    }

    [Serializable]
    public sealed class DlsResourceTableMetadata
    {
        public string TableId { get; }
        public string Version { get; }
        public bool IsVerifiedLicensedTable { get; }
        public ContentLicenseInfo LicenseInfo { get; }

        public DlsResourceTableMetadata(
            string tableId,
            string version,
            bool isVerifiedLicensedTable,
            ContentLicenseInfo licenseInfo)
        {
            TableId = tableId ?? string.Empty;
            Version = version ?? string.Empty;
            IsVerifiedLicensedTable = isVerifiedLicensedTable;
            LicenseInfo = licenseInfo ?? throw new ArgumentNullException(nameof(licenseInfo));
        }
    }

    [Serializable]
    public sealed class DlsTargetRequest
    {
        public string MatchId { get; }
        public int InitialOversPerInnings { get; }
        public int Team1TotalRuns { get; }
        public bool Team1WasAllOut { get; }
        public IReadOnlyList<RainInterruptionRecord> Team1Interruptions { get; }
        public int Team2RevisedMaxOvers { get; }
        public IReadOnlyList<RainInterruptionRecord> Team2Interruptions { get; }

        public DlsTargetRequest(
            string matchId,
            int initialOversPerInnings,
            int team1TotalRuns,
            bool team1WasAllOut,
            IReadOnlyList<RainInterruptionRecord>? team1Interruptions,
            int team2RevisedMaxOvers,
            IReadOnlyList<RainInterruptionRecord>? team2Interruptions)
        {
            MatchId = matchId ?? string.Empty;
            InitialOversPerInnings = initialOversPerInnings;
            Team1TotalRuns = team1TotalRuns;
            Team1WasAllOut = team1WasAllOut;
            Team1Interruptions = team1Interruptions ?? Array.Empty<RainInterruptionRecord>();
            Team2RevisedMaxOvers = team2RevisedMaxOvers;
            Team2Interruptions = team2Interruptions ?? Array.Empty<RainInterruptionRecord>();
        }
    }

    [Serializable]
    public sealed class DlsParScoreRequest
    {
        public string MatchId { get; }
        public int InitialOversPerInnings { get; }
        public int Team1TotalRuns { get; }
        public IReadOnlyList<RainInterruptionRecord> Team1Interruptions { get; }
        public int Team2MaxOvers { get; }
        public int Team2OversCompleted { get; }
        public int Team2BallsInCurrentOver { get; }
        public int Team2WicketsLost { get; }
        public IReadOnlyList<RainInterruptionRecord> Team2Interruptions { get; }

        public DlsParScoreRequest(
            string matchId,
            int initialOversPerInnings,
            int team1TotalRuns,
            IReadOnlyList<RainInterruptionRecord>? team1Interruptions,
            int team2MaxOvers,
            int team2OversCompleted,
            int team2BallsInCurrentOver,
            int team2WicketsLost,
            IReadOnlyList<RainInterruptionRecord>? team2Interruptions)
        {
            MatchId = matchId ?? string.Empty;
            InitialOversPerInnings = initialOversPerInnings;
            Team1TotalRuns = team1TotalRuns;
            Team1Interruptions = team1Interruptions ?? Array.Empty<RainInterruptionRecord>();
            Team2MaxOvers = team2MaxOvers;
            Team2OversCompleted = team2OversCompleted;
            Team2BallsInCurrentOver = team2BallsInCurrentOver;
            Team2WicketsLost = team2WicketsLost;
            Team2Interruptions = team2Interruptions ?? Array.Empty<RainInterruptionRecord>();
        }
    }

    [Serializable]
    public sealed class DlsCalculationResult
    {
        public DlsCalculationStatus Status { get; }
        public bool IsAvailable => Status == DlsCalculationStatus.Succeeded;
        public int? RevisedTargetRuns { get; }
        public int? ParScore { get; }
        public double? Team1ResourcePercentage { get; }
        public double? Team2ResourcePercentage { get; }
        public string Message { get; }
        public string ResourceTableId { get; }

        public DlsCalculationResult(
            DlsCalculationStatus status,
            string message,
            string resourceTableId = "",
            int? revisedTargetRuns = null,
            int? parScore = null,
            double? team1ResourcePercentage = null,
            double? team2ResourcePercentage = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            ResourceTableId = resourceTableId ?? string.Empty;
            RevisedTargetRuns = revisedTargetRuns;
            ParScore = parScore;
            Team1ResourcePercentage = team1ResourcePercentage;
            Team2ResourcePercentage = team2ResourcePercentage;
        }
    }
}
