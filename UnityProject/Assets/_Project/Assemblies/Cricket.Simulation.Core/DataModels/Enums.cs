namespace Cricket.Simulation.Core.DataModels
{
    public enum MatchFormat
    {
        T20 = 0,
        ODI = 1,
        Test = 2,
        CustomLimitedOvers = 3
    }

    public enum PlayerRole
    {
        SpecialistBatter = 0,
        FastBowler = 1,
        SpinBowler = 2,
        BattingAllRounder = 3,
        BowlingAllRounder = 4,
        WicketKeeperBatter = 5
    }

    public enum Handedness
    {
        Right = 0,
        Left = 1
    }

    public enum BowlingStyle
    {
        None = 0,
        RightArmFast = 1,
        RightArmFastMedium = 2,
        RightArmMedium = 3,
        LeftArmFast = 4,
        LeftArmFastMedium = 5,
        LeftArmMedium = 6,
        RightArmOffBreak = 7,
        RightArmLegBreak = 8,
        LeftArmOrthodox = 9,
        LeftArmWristSpin = 10
    }

    public enum TossDecision
    {
        BatFirst = 0,
        BowlFirst = 1
    }

    public enum ExtraType
    {
        None = 0,
        Wide = 1,
        NoBall = 2,
        Bye = 3,
        LegBye = 4,
        Penalty = 5
    }

    public enum NoBallReason
    {
        None = 0,
        FrontFootOverstep = 1,
        BackFootReturnCrease = 2,
        AboveWaistFullToss = 3,
        ExcessiveBouncersInOver = 4,
        IllegalFieldPlacement = 5,
        MoreThanTwoBouncesBeforeCrease = 6
    }

    public enum DismissalType
    {
        None = 0,
        Bowled = 1,
        Caught = 2,
        LegBeforeWicket = 3,
        RunOut = 4,
        Stumped = 5,
        HitWicket = 6,
        HitBallTwice = 7,
        ObstructingTheField = 8,
        TimedOut = 9
    }

    public enum ContactZone
    {
        Miss = 0,
        SweetSpot = 1,
        InnerEdge = 2,
        OuterEdge = 3,
        Toe = 4,
        SpliceOrHandle = 5,
        PadOnly = 6,
        Glove = 7,
        Body = 8
    }

    public enum ShotIntent
    {
        Leave = 0,
        Defensive = 1,
        GroundStroke = 2,
        LoftedStroke = 3,
        SweepOrReverse = 4,
        ScoopOrRamp = 5,
        DuckOrEvade = 6
    }

    public enum FootworkType
    {
        Neutral = 0,
        FrontFoot = 1,
        BackFoot = 2,
        AdvanceDownPitch = 3,
        DeepInCrease = 4
    }

    public enum MatchPhase
    {
        Uninitialized = 0,
        MatchSetup = 1,
        PreMatchCeremony = 2,
        Toss = 3,
        InningsReady = 4,
        OverSetup = 5,
        DeliveryInProgress = 6,
        BallInPlay = 7,
        DecisionAndReview = 8,
        OverBreak = 9,
        SessionOrDrinksBreak = 10,
        InningsBreak = 11,
        SuperOverSetup = 12,
        PostMatchCeremony = 13,
        Completed = 14
    }

    public enum InningsEndReason
    {
        None = 0,
        AllOut = 1,
        OversCompleted = 2,
        TargetReached = 3,
        Declared = 4,
        Forfeited = 5,
        MatchAbandoned = 6
    }

    public enum MatchResultType
    {
        InProgress = 0,
        WinByRuns = 1,
        WinByWickets = 2,
        WinByInningsAndRuns = 3,
        Tie = 4,
        TiePendingSuperOver = 5,
        Draw = 6,
        NoResult = 7,
        WinBySuperOver = 8
    }

    public enum ContentLicenseType
    {
        OriginalStudioCreation = 0,
        LicensedThirdParty = 1,
        UserGeneratedLocal = 2
    }
}
