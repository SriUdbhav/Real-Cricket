using System;
using Cricket.Simulation.Core.Math;

namespace Cricket.Simulation.Core.DataModels
{
    /// <summary>
    /// Approximate physical delivery observations recorded by the bowling/physics layer.
    /// Does NOT directly mutate score; passed as evidence into ICricketRulesEngine.
    /// </summary>
    [Serializable]
    public sealed class DeliveryTelemetry
    {
        public float ReleaseSpeedKmh { get; }
        public SimVector3 ReleasePosition { get; }
        public SimVector2 PitchBouncePosition { get; }
        public float SpinRpm { get; }
        public float SeamAngleDegrees { get; }
        public bool IsBouncerAboveShoulder { get; }
        public bool IsWideByCreaseLine { get; }
        public NoBallReason DetectedNoBallReason { get; }

        public DeliveryTelemetry(
            float releaseSpeedKmh,
            SimVector3 releasePosition,
            SimVector2 pitchBouncePosition,
            float spinRpm = 0f,
            float seamAngleDegrees = 0f,
            bool isBouncerAboveShoulder = false,
            bool isWideByCreaseLine = false,
            NoBallReason detectedNoBallReason = NoBallReason.None)
        {
            ReleaseSpeedKmh = releaseSpeedKmh;
            ReleasePosition = releasePosition;
            PitchBouncePosition = pitchBouncePosition;
            SpinRpm = spinRpm;
            SeamAngleDegrees = seamAngleDegrees;
            IsBouncerAboveShoulder = isBouncerAboveShoulder;
            IsWideByCreaseLine = isWideByCreaseLine;
            DetectedNoBallReason = detectedNoBallReason;
        }
    }

    /// <summary>
    /// Approximate batting contact observations produced by the batting/physics layer.
    /// </summary>
    [Serializable]
    public sealed class ShotTelemetry
    {
        public ShotIntent Intent { get; }
        public FootworkType Footwork { get; }
        public ContactZone PrimaryContact { get; }
        public float ExitSpeedKmh { get; }
        public float LaunchAngleDegrees { get; }
        public SimVector2 GroundLandingOrStopPosition { get; }
        public bool ShotOffered { get; }

        public ShotTelemetry(
            ShotIntent intent,
            FootworkType footwork,
            ContactZone primaryContact,
            float exitSpeedKmh = 0f,
            float launchAngleDegrees = 0f,
            SimVector2 groundLandingOrStopPosition = default,
            bool shotOffered = true)
        {
            Intent = intent;
            Footwork = footwork;
            PrimaryContact = primaryContact;
            ExitSpeedKmh = exitSpeedKmh;
            LaunchAngleDegrees = launchAngleDegrees;
            GroundLandingOrStopPosition = groundLandingOrStopPosition;
            ShotOffered = shotOffered;
        }

        public bool MadeBatOrGloveContact =>
            PrimaryContact == ContactZone.SweetSpot ||
            PrimaryContact == ContactZone.InnerEdge ||
            PrimaryContact == ContactZone.OuterEdge ||
            PrimaryContact == ContactZone.Toe ||
            PrimaryContact == ContactZone.SpliceOrHandle ||
            PrimaryContact == ContactZone.Glove;
    }

    /// <summary>
    /// Structured physical/umpire evidence from the ball-in-play phase submitted to ICricketRulesEngine.
    /// </summary>
    [Serializable]
    public sealed class FieldingAndRunningEvidence
    {
        public int CompletedRuns { get; }
        public int ShortRunsDetected { get; }
        public bool CrossedOnIncompleteRun { get; }
        public bool CrossedAtInstantOfThrow { get; }
        public bool ReachedBoundaryOnBounce { get; }
        public bool ClearedBoundaryOnFull { get; }
        public bool OverthrowReachedBoundary { get; }
        public bool PenaltyAwardedToBattingSide { get; }
        public DismissalType ProposedDismissal { get; }
        public string? DismissedBatterId { get; }
        public string? PrimaryFielderId { get; }
        public bool BattersCrossedBeforeDismissal { get; }

        public FieldingAndRunningEvidence(
            int completedRuns = 0,
            int shortRunsDetected = 0,
            bool crossedOnIncompleteRun = false,
            bool crossedAtInstantOfThrow = false,
            bool reachedBoundaryOnBounce = false,
            bool clearedBoundaryOnFull = false,
            bool overthrowReachedBoundary = false,
            bool penaltyAwardedToBattingSide = false,
            DismissalType proposedDismissal = DismissalType.None,
            string? dismissedBatterId = null,
            string? primaryFielderId = null,
            bool battersCrossedBeforeDismissal = false)
        {
            CompletedRuns = System.Math.Max(0, completedRuns);
            ShortRunsDetected = System.Math.Max(0, shortRunsDetected);
            CrossedOnIncompleteRun = crossedOnIncompleteRun;
            CrossedAtInstantOfThrow = crossedAtInstantOfThrow;
            ReachedBoundaryOnBounce = reachedBoundaryOnBounce;
            ClearedBoundaryOnFull = clearedBoundaryOnFull;
            OverthrowReachedBoundary = overthrowReachedBoundary;
            PenaltyAwardedToBattingSide = penaltyAwardedToBattingSide;
            ProposedDismissal = proposedDismissal;
            DismissedBatterId = dismissedBatterId;
            PrimaryFielderId = primaryFielderId;
            BattersCrossedBeforeDismissal = battersCrossedBeforeDismissal;
        }
    }

    /// <summary>
    /// Complete input evidence bundle submitted to ICricketRulesEngine once a delivery becomes dead.
    /// </summary>
    [Serializable]
    public sealed class DeliveryResolutionInput
    {
        public DeliveryTelemetry Delivery { get; }
        public ShotTelemetry Shot { get; }
        public FieldingAndRunningEvidence Fielding { get; }

        public DeliveryResolutionInput(
            DeliveryTelemetry delivery,
            ShotTelemetry shot,
            FieldingAndRunningEvidence fielding)
        {
            Delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
            Shot = shot ?? throw new ArgumentNullException(nameof(shot));
            Fielding = fielding ?? throw new ArgumentNullException(nameof(fielding));
        }
    }

    /// <summary>
    /// Official validated dismissal record produced exclusively by ICricketRulesEngine.
    /// </summary>
    [Serializable]
    public sealed class DismissalRecord
    {
        public DismissalType Type { get; }
        public string OutBatterId { get; }
        public string BowlerId { get; }
        public string? FielderId { get; }
        public bool BowlerCredited { get; }

        public DismissalRecord(
            DismissalType type,
            string outBatterId,
            string bowlerId,
            string? fielderId,
            bool bowlerCredited)
        {
            Type = type;
            OutBatterId = outBatterId;
            BowlerId = bowlerId;
            FielderId = fielderId;
            BowlerCredited = bowlerCredited;
        }
    }

    /// <summary>
    /// Immutable, event-sourced official record of a resolved delivery.
    /// Scorecards, analytics, replays, and multiplayer checksums derive from this log.
    /// </summary>
    [Serializable]
    public sealed class BallEventRecord
    {
        public long SequenceId { get; }
        public int InningsNumber { get; }
        public int OverNumber { get; }
        public int LegalBallInOver { get; }
        public string StrikerId { get; }
        public string NonStrikerId { get; }
        public string BowlerId { get; }
        public bool WasFreeHit { get; }
        public bool IsLegalDelivery { get; }
        public int RunsOffBat { get; }
        public ExtraType PrimaryExtraType { get; }
        public NoBallReason NoBallReason { get; }
        public int ExtraRuns { get; }
        public int PenaltyRuns { get; }
        public int TotalRunsScored => RunsOffBat + ExtraRuns + PenaltyRuns;
        public bool IsBoundaryFour { get; }
        public bool IsBoundarySix { get; }
        public DismissalRecord? Dismissal { get; }
        public DeliveryTelemetry DeliveryTelemetry { get; }
        public ShotTelemetry ShotTelemetry { get; }

        public BallEventRecord(
            long sequenceId,
            int inningsNumber,
            int overNumber,
            int legalBallInOver,
            string strikerId,
            string nonStrikerId,
            string bowlerId,
            bool wasFreeHit,
            bool isLegalDelivery,
            int runsOffBat,
            ExtraType primaryExtraType,
            NoBallReason noBallReason,
            int extraRuns,
            int penaltyRuns,
            bool isBoundaryFour,
            bool isBoundarySix,
            DismissalRecord? dismissal,
            DeliveryTelemetry deliveryTelemetry,
            ShotTelemetry shotTelemetry)
        {
            SequenceId = sequenceId;
            InningsNumber = inningsNumber;
            OverNumber = overNumber;
            LegalBallInOver = legalBallInOver;
            StrikerId = strikerId;
            NonStrikerId = nonStrikerId;
            BowlerId = bowlerId;
            WasFreeHit = wasFreeHit;
            IsLegalDelivery = isLegalDelivery;
            RunsOffBat = runsOffBat;
            PrimaryExtraType = primaryExtraType;
            NoBallReason = noBallReason;
            ExtraRuns = extraRuns;
            PenaltyRuns = penaltyRuns;
            IsBoundaryFour = isBoundaryFour;
            IsBoundarySix = isBoundarySix;
            Dismissal = dismissal;
            DeliveryTelemetry = deliveryTelemetry;
            ShotTelemetry = shotTelemetry;
        }
    }
}
