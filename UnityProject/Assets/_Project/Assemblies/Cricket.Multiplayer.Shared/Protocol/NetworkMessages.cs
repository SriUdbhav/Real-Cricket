using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Math;
using Cricket.Simulation.Core.Rules;

namespace Cricket.Multiplayer.Shared.Protocol
{
    public enum MatchSessionMode
    {
        OfflineSinglePlayer = 0,
        AuthoritativeOnline = 1,
        SpectatorDelayed = 2
    }

    public enum MatchCommandType
    {
        SetFieldPlacement = 1,
        CommitDelivery = 2,
        SubmitShotInput = 3,
        SubmitRunningCall = 4,
        RequestDrsReview = 5,
        DeclareInnings = 6
    }

    /// <summary>
    /// Base interface for transport-agnostic gameplay commands shared by offline local play
    /// and future authoritative multiplayer servers.
    /// </summary>
    public interface IMatchCommand
    {
        long CommandSequence { get; }
        string MatchId { get; }
        string SenderPlayerId { get; }
        MatchCommandType CommandType { get; }
    }

    [Serializable]
    public sealed class SetFieldPlacementCommand : IMatchCommand
    {
        public long CommandSequence { get; }
        public string MatchId { get; }
        public string SenderPlayerId { get; }
        public MatchCommandType CommandType => MatchCommandType.SetFieldPlacement;
        public int OneBasedOverNumber { get; }
        public IReadOnlyList<FielderPlacement> Placements { get; }

        public SetFieldPlacementCommand(
            long commandSequence,
            string matchId,
            string senderPlayerId,
            int oneBasedOverNumber,
            IReadOnlyList<FielderPlacement> placements)
        {
            CommandSequence = commandSequence;
            MatchId = matchId ?? string.Empty;
            SenderPlayerId = senderPlayerId ?? string.Empty;
            OneBasedOverNumber = oneBasedOverNumber;
            Placements = placements ?? Array.Empty<FielderPlacement>();
        }
    }

    /// <summary>
    /// Raw bowler release parameters submitted by the bowling client (or local player/AI).
    /// Clients never submit outcomes; only bounded physical intent parameters.
    /// </summary>
    [Serializable]
    public sealed class DeliveryCommitCommand : IMatchCommand
    {
        public long CommandSequence { get; }
        public string MatchId { get; }
        public string SenderPlayerId { get; }
        public MatchCommandType CommandType => MatchCommandType.CommitDelivery;
        public float TargetSpeedKmh { get; }
        public SimVector2 TargetPitchCoordinate { get; }
        public float ReleaseTimingOffsetMs { get; }
        public float SeamAngleDegrees { get; }
        public float SpinRpm { get; }

        public DeliveryCommitCommand(
            long commandSequence,
            string matchId,
            string senderPlayerId,
            float targetSpeedKmh,
            SimVector2 targetPitchCoordinate,
            float releaseTimingOffsetMs,
            float seamAngleDegrees,
            float spinRpm)
        {
            CommandSequence = commandSequence;
            MatchId = matchId ?? string.Empty;
            SenderPlayerId = senderPlayerId ?? string.Empty;
            TargetSpeedKmh = targetSpeedKmh;
            TargetPitchCoordinate = targetPitchCoordinate;
            ReleaseTimingOffsetMs = releaseTimingOffsetMs;
            SeamAngleDegrees = seamAngleDegrees;
            SpinRpm = spinRpm;
        }
    }

    /// <summary>
    /// Raw batter swing input captured locally with millisecond reaction telemetry
    /// for server validation and replay without trusting client-claimed runs.
    /// </summary>
    [Serializable]
    public sealed class ShotInputCommand : IMatchCommand
    {
        public long CommandSequence { get; }
        public string MatchId { get; }
        public string SenderPlayerId { get; }
        public MatchCommandType CommandType => MatchCommandType.SubmitShotInput;
        public ShotIntent Intent { get; }
        public FootworkType Footwork { get; }
        public float AimAzimuthDegrees { get; }
        public float ReactionTimeFromReleaseMs { get; }

        public ShotInputCommand(
            long commandSequence,
            string matchId,
            string senderPlayerId,
            ShotIntent intent,
            FootworkType footwork,
            float aimAzimuthDegrees,
            float reactionTimeFromReleaseMs)
        {
            CommandSequence = commandSequence;
            MatchId = matchId ?? string.Empty;
            SenderPlayerId = senderPlayerId ?? string.Empty;
            Intent = intent;
            Footwork = footwork;
            AimAzimuthDegrees = aimAzimuthDegrees;
            ReactionTimeFromReleaseMs = reactionTimeFromReleaseMs;
        }
    }

    /// <summary>
    /// Canonical ball resolution broadcast emitted by the match authority (local engine in offline mode,
    /// dedicated .NET server in online mode) containing the official BallEventRecord and state checksum.
    /// </summary>
    [Serializable]
    public sealed class AuthoritativeBallSyncPacket
    {
        public string MatchId { get; }
        public BallEventRecord OfficialRecord { get; }
        public ulong CumulativeLogChecksum { get; }

        public AuthoritativeBallSyncPacket(
            string matchId,
            BallEventRecord officialRecord,
            ulong cumulativeLogChecksum)
        {
            MatchId = matchId ?? string.Empty;
            OfficialRecord = officialRecord ?? throw new ArgumentNullException(nameof(officialRecord));
            CumulativeLogChecksum = cumulativeLogChecksum;
        }
    }
}
