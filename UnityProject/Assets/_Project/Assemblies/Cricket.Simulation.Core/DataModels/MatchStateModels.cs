using System;
using System.Collections.Generic;

namespace Cricket.Simulation.Core.DataModels
{
    [Serializable]
    public sealed class BatterInningsState
    {
        public string PlayerId { get; }
        public int Runs { get; internal set; }
        public int BallsFaced { get; internal set; }
        public int Fours { get; internal set; }
        public int Sixes { get; internal set; }
        public bool IsOut { get; internal set; }
        public DismissalRecord? Dismissal { get; internal set; }

        public float StrikeRate => BallsFaced > 0 ? (Runs * 100f) / BallsFaced : 0f;

        public BatterInningsState(string playerId)
        {
            PlayerId = playerId;
        }
    }

    [Serializable]
    public sealed class BowlerSpellState
    {
        public string PlayerId { get; }
        public int CompletedOvers { get; internal set; }
        public int LegalBallsInCurrentOver { get; internal set; }
        public int Maidens { get; internal set; }
        public int RunsConceded { get; internal set; }
        public int Wickets { get; internal set; }
        public int Wides { get; internal set; }
        public int NoBalls { get; internal set; }

        public BowlerSpellState(string playerId)
        {
            PlayerId = playerId;
        }

        public float GetOversDecimal(int ballsPerOver)
        {
            return CompletedOvers + (LegalBallsInCurrentOver / (float)ballsPerOver);
        }

        public float GetEconomyRate(int ballsPerOver)
        {
            float overs = GetOversDecimal(ballsPerOver);
            return overs > 0f ? RunsConceded / overs : 0f;
        }
    }

    [Serializable]
    public sealed class InningsState
    {
        public int InningsNumber { get; }
        public string BattingTeamId { get; }
        public string BowlingTeamId { get; }
        public int? TargetRuns { get; }
        public bool IsSuperOver { get; }
        public int SuperOverRound { get; }
        public int MaxWickets { get; }
        public int? MaxOversOverride { get; }
        public IReadOnlyList<string>? NominatedBattingOrder { get; }
        public int TotalRuns { get; internal set; }
        public int WicketsLost { get; internal set; }
        public int CompletedOvers { get; internal set; }
        public int LegalBallsInCurrentOver { get; internal set; }
        public int TotalExtras { get; internal set; }
        public int Wides { get; internal set; }
        public int NoBalls { get; internal set; }
        public int Byes { get; internal set; }
        public int LegByes { get; internal set; }
        public int PenaltyRuns { get; internal set; }
        public string? StrikerId { get; internal set; }
        public string? NonStrikerId { get; internal set; }
        public string? CurrentBowlerId { get; internal set; }
        public string? PreviousOverBowlerId { get; internal set; }
        public int NextBatterOrderIndex { get; internal set; }
        public bool IsFreeHitPending { get; internal set; }
        public int BouncersInCurrentOver { get; internal set; }
        public int RunsConcededInCurrentOverByBowler { get; internal set; }
        public int WicketsInCurrentOver { get; internal set; }
        public bool IsCompleted { get; internal set; }
        public InningsEndReason EndReason { get; internal set; }

        public Dictionary<string, BatterInningsState> BatterStates { get; } = new Dictionary<string, BatterInningsState>(StringComparer.Ordinal);
        public Dictionary<string, BowlerSpellState> BowlerStates { get; } = new Dictionary<string, BowlerSpellState>(StringComparer.Ordinal);
        public List<BallEventRecord> BallLog { get; } = new List<BallEventRecord>(128);

        public InningsState(
            int inningsNumber,
            string battingTeamId,
            string bowlingTeamId,
            int? targetRuns,
            bool isSuperOver = false,
            int superOverRound = 0,
            int maxWickets = 10,
            int? maxOversOverride = null,
            IReadOnlyList<string>? nominatedBattingOrder = null)
        {
            InningsNumber = inningsNumber;
            BattingTeamId = battingTeamId;
            BowlingTeamId = bowlingTeamId;
            TargetRuns = targetRuns;
            IsSuperOver = isSuperOver;
            SuperOverRound = superOverRound;
            MaxWickets = System.Math.Max(1, maxWickets);
            MaxOversOverride = maxOversOverride;
            NominatedBattingOrder = nominatedBattingOrder != null ? new List<string>(nominatedBattingOrder) : null;
        }

        public int GetTotalLegalBalls(int ballsPerOver)
        {
            return (CompletedOvers * ballsPerOver) + LegalBallsInCurrentOver;
        }
    }

    [Serializable]
    public sealed class MatchConfig
    {
        public string MatchId { get; }
        public CompetitionRulesProfile Rules { get; }
        public TeamData HomeTeam { get; }
        public TeamData AwayTeam { get; }
        public PlayingLineup HomeLineup { get; }
        public PlayingLineup AwayLineup { get; }
        public string StadiumId { get; }
        public bool IsOfflineSinglePlayer { get; }

        public MatchConfig(
            string matchId,
            CompetitionRulesProfile rules,
            TeamData homeTeam,
            TeamData awayTeam,
            PlayingLineup homeLineup,
            PlayingLineup awayLineup,
            string stadiumId,
            bool isOfflineSinglePlayer = true)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("MatchId cannot be empty.", nameof(matchId));
            }

            MatchId = matchId;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            HomeTeam = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
            AwayTeam = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
            HomeLineup = homeLineup ?? throw new ArgumentNullException(nameof(homeLineup));
            AwayLineup = awayLineup ?? throw new ArgumentNullException(nameof(awayLineup));
            StadiumId = string.IsNullOrWhiteSpace(stadiumId) ? "default_stadium" : stadiumId;
            IsOfflineSinglePlayer = isOfflineSinglePlayer;
        }

        public PlayingLineup GetLineupForTeam(string teamId)
        {
            if (string.Equals(teamId, HomeTeam.TeamId, StringComparison.Ordinal))
            {
                return HomeLineup;
            }

            if (string.Equals(teamId, AwayTeam.TeamId, StringComparison.Ordinal))
            {
                return AwayLineup;
            }

            throw new ArgumentException($"Unknown teamId '{teamId}'.", nameof(teamId));
        }

        public string GetOpponentTeamId(string teamId)
        {
            if (string.Equals(teamId, HomeTeam.TeamId, StringComparison.Ordinal))
            {
                return AwayTeam.TeamId;
            }

            if (string.Equals(teamId, AwayTeam.TeamId, StringComparison.Ordinal))
            {
                return HomeTeam.TeamId;
            }

            throw new ArgumentException($"Unknown teamId '{teamId}'.", nameof(teamId));
        }
    }

    [Serializable]
    public sealed class MatchResult
    {
        public MatchResultType ResultType { get; }
        public string? WinnerTeamId { get; }
        public int Margin { get; }
        public string Summary { get; }

        public MatchResult(MatchResultType resultType, string? winnerTeamId, int margin, string summary)
        {
            ResultType = resultType;
            WinnerTeamId = winnerTeamId;
            Margin = margin;
            Summary = summary ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class MatchState
    {
        public MatchConfig Config { get; }
        public string? TossWinnerTeamId { get; internal set; }
        public TossDecision? TossDecision { get; internal set; }
        public string? BattingFirstTeamId { get; internal set; }
        public string? BowlingFirstTeamId { get; internal set; }
        public List<InningsState> Innings { get; } = new List<InningsState>(4);
        public List<InningsState> SuperOverInnings { get; } = new List<InningsState>(2);
        public int CurrentInningsIndex { get; internal set; } = -1;
        internal InningsState? ActiveSuperOverInnings { get; set; }
        public MatchResult Result { get; internal set; } = new MatchResult(MatchResultType.InProgress, null, 0, "Match in progress");

        public InningsState? CurrentInnings =>
            ActiveSuperOverInnings ??
            (CurrentInningsIndex >= 0 && CurrentInningsIndex < Innings.Count
                ? Innings[CurrentInningsIndex]
                : null);

        public MatchState(MatchConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }
    }
}
