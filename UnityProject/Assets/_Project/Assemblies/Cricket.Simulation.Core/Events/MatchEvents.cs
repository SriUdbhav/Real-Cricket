using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Events
{
    public readonly struct MatchPhaseChangedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public MatchPhase PreviousPhase { get; }
        public MatchPhase CurrentPhase { get; }
        public string Reason { get; }

        public MatchPhaseChangedEvent(long sequenceNumber, MatchPhase previousPhase, MatchPhase currentPhase, string reason)
        {
            SequenceNumber = sequenceNumber;
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
            Reason = reason ?? string.Empty;
        }
    }

    public readonly struct TossCompletedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public string TossWinnerTeamId { get; }
        public TossDecision Decision { get; }
        public string BattingFirstTeamId { get; }
        public string BowlingFirstTeamId { get; }

        public TossCompletedEvent(
            long sequenceNumber,
            string tossWinnerTeamId,
            TossDecision decision,
            string battingFirstTeamId,
            string bowlingFirstTeamId)
        {
            SequenceNumber = sequenceNumber;
            TossWinnerTeamId = tossWinnerTeamId;
            Decision = decision;
            BattingFirstTeamId = battingFirstTeamId;
            BowlingFirstTeamId = bowlingFirstTeamId;
        }
    }

    public readonly struct InningsStartedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int InningsNumber { get; }
        public string BattingTeamId { get; }
        public string BowlingTeamId { get; }
        public int? TargetRuns { get; }

        public InningsStartedEvent(
            long sequenceNumber,
            int inningsNumber,
            string battingTeamId,
            string bowlingTeamId,
            int? targetRuns)
        {
            SequenceNumber = sequenceNumber;
            InningsNumber = inningsNumber;
            BattingTeamId = battingTeamId;
            BowlingTeamId = bowlingTeamId;
            TargetRuns = targetRuns;
        }
    }

    public readonly struct OverStartedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int InningsNumber { get; }
        public int OverNumber { get; }
        public string BowlerId { get; }
        public string StrikerId { get; }
        public string NonStrikerId { get; }

        public OverStartedEvent(
            long sequenceNumber,
            int inningsNumber,
            int overNumber,
            string bowlerId,
            string strikerId,
            string nonStrikerId)
        {
            SequenceNumber = sequenceNumber;
            InningsNumber = inningsNumber;
            OverNumber = overNumber;
            BowlerId = bowlerId;
            StrikerId = strikerId;
            NonStrikerId = nonStrikerId;
        }
    }

    public readonly struct BallResolvedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public BallEventRecord Record { get; }

        public BallResolvedEvent(long sequenceNumber, BallEventRecord record)
        {
            SequenceNumber = sequenceNumber;
            Record = record;
        }
    }

    public readonly struct ScoreUpdatedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int InningsNumber { get; }
        public int TotalRuns { get; }
        public int WicketsLost { get; }
        public int CompletedOvers { get; }
        public int BallsInCurrentOver { get; }
        public int? TargetRuns { get; }

        public ScoreUpdatedEvent(
            long sequenceNumber,
            int inningsNumber,
            int totalRuns,
            int wicketsLost,
            int completedOvers,
            int ballsInCurrentOver,
            int? targetRuns)
        {
            SequenceNumber = sequenceNumber;
            InningsNumber = inningsNumber;
            TotalRuns = totalRuns;
            WicketsLost = wicketsLost;
            CompletedOvers = completedOvers;
            BallsInCurrentOver = ballsInCurrentOver;
            TargetRuns = targetRuns;
        }
    }

    public readonly struct WicketFallenEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int InningsNumber { get; }
        public int TeamScoreAtFall { get; }
        public int WicketNumber { get; }
        public DismissalRecord Dismissal { get; }

        public WicketFallenEvent(
            long sequenceNumber,
            int inningsNumber,
            int teamScoreAtFall,
            int wicketNumber,
            DismissalRecord dismissal)
        {
            SequenceNumber = sequenceNumber;
            InningsNumber = inningsNumber;
            TeamScoreAtFall = teamScoreAtFall;
            WicketNumber = wicketNumber;
            Dismissal = dismissal;
        }
    }

    public readonly struct FreeHitStatusChangedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public bool IsFreeHitActive { get; }
        public string Reason { get; }

        public FreeHitStatusChangedEvent(long sequenceNumber, bool isFreeHitActive, string reason)
        {
            SequenceNumber = sequenceNumber;
            IsFreeHitActive = isFreeHitActive;
            Reason = reason ?? string.Empty;
        }
    }

    public readonly struct OverCompletedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int InningsNumber { get; }
        public int CompletedOverNumber { get; }
        public string BowlerId { get; }
        public int RunsConcededInOver { get; }
        public int WicketsInOver { get; }
        public bool IsMaiden { get; }

        public OverCompletedEvent(
            long sequenceNumber,
            int inningsNumber,
            int completedOverNumber,
            string bowlerId,
            int runsConcededInOver,
            int wicketsInOver,
            bool isMaiden)
        {
            SequenceNumber = sequenceNumber;
            InningsNumber = inningsNumber;
            CompletedOverNumber = completedOverNumber;
            BowlerId = bowlerId;
            RunsConcededInOver = runsConcededInOver;
            WicketsInOver = wicketsInOver;
            IsMaiden = isMaiden;
        }
    }

    public readonly struct InningsCompletedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int InningsNumber { get; }
        public string BattingTeamId { get; }
        public int TotalRuns { get; }
        public int WicketsLost { get; }
        public InningsEndReason Reason { get; }
        public bool FollowOnEligible { get; }

        public InningsCompletedEvent(
            long sequenceNumber,
            int inningsNumber,
            string battingTeamId,
            int totalRuns,
            int wicketsLost,
            InningsEndReason reason,
            bool followOnEligible)
        {
            SequenceNumber = sequenceNumber;
            InningsNumber = inningsNumber;
            BattingTeamId = battingTeamId;
            TotalRuns = totalRuns;
            WicketsLost = wicketsLost;
            Reason = reason;
            FollowOnEligible = followOnEligible;
        }
    }

    public readonly struct MatchCompletedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public MatchResult Result { get; }

        public MatchCompletedEvent(long sequenceNumber, MatchResult result)
        {
            SequenceNumber = sequenceNumber;
            Result = result;
        }
    }

    public readonly struct RuleViolationEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public string Code { get; }
        public string Message { get; }

        public RuleViolationEvent(long sequenceNumber, string code, string message)
        {
            SequenceNumber = sequenceNumber;
            Code = code;
            Message = message;
        }
    }

    public readonly struct SuperOverStartedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int SuperOverRound { get; }
        public int HalfNumber { get; }
        public string BattingTeamId { get; }
        public string BowlingTeamId { get; }
        public int? TargetRuns { get; }

        public SuperOverStartedEvent(
            long sequenceNumber,
            int superOverRound,
            int halfNumber,
            string battingTeamId,
            string bowlingTeamId,
            int? targetRuns)
        {
            SequenceNumber = sequenceNumber;
            SuperOverRound = superOverRound;
            HalfNumber = halfNumber;
            BattingTeamId = battingTeamId;
            BowlingTeamId = bowlingTeamId;
            TargetRuns = targetRuns;
        }
    }

    public readonly struct SuperOverCompletedEvent : IMatchEvent
    {
        public long SequenceNumber { get; }
        public int SuperOverRound { get; }
        public string FirstBattingTeamId { get; }
        public int FirstBattingRuns { get; }
        public string SecondBattingTeamId { get; }
        public int SecondBattingRuns { get; }
        public bool IsTied { get; }
        public string? WinnerTeamId { get; }

        public SuperOverCompletedEvent(
            long sequenceNumber,
            int superOverRound,
            string firstBattingTeamId,
            int firstBattingRuns,
            string secondBattingTeamId,
            int secondBattingRuns,
            bool isTied,
            string? winnerTeamId)
        {
            SequenceNumber = sequenceNumber;
            SuperOverRound = superOverRound;
            FirstBattingTeamId = firstBattingTeamId;
            FirstBattingRuns = firstBattingRuns;
            SecondBattingTeamId = secondBattingTeamId;
            SecondBattingRuns = secondBattingRuns;
            IsTied = isTied;
            WinnerTeamId = winnerTeamId;
        }
    }
}
