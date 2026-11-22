using System;
using System.Collections.Generic;
using Cricket.Multiplayer.Shared.Protocol;
using Cricket.Simulation.Core.DataModels;

namespace Cricket.Multiplayer.Shared.Validation
{
    /// <summary>
    /// Shared anti-cheat and structural input validator executed identically in offline local validation
    /// and on the authoritative multiplayer server.
    /// </summary>
    public sealed class MatchCommandValidator : IMatchCommandValidator
    {
        private const float MinLegalSpeedKmh = 55f;
        private const float MaxLegalSpeedKmh = 165f;
        private const float MaxLegalSpinRpm = 4000f;
        private const float MinHumanReactionMs = 80f;
        private const float MaxShotWindowMs = 2500f;

        public CommandValidationResult ValidateDeliveryCommit(
            DeliveryCommitCommand command,
            MatchState state,
            long lastAcceptedSequence)
        {
            if (command == null || state == null)
            {
                return CommandValidationResult.Reject("NULL_INPUT", "Command or match state is null.");
            }

            if (!string.Equals(command.MatchId, state.Config.MatchId, StringComparison.Ordinal))
            {
                return CommandValidationResult.Reject("MATCH_ID_MISMATCH", "Command MatchId does not match active match.");
            }

            if (command.CommandSequence <= lastAcceptedSequence)
            {
                return CommandValidationResult.Reject("STALE_SEQUENCE", "Command sequence must strictly increase.");
            }

            InningsState? innings = state.CurrentInnings;
            if (innings == null || innings.IsCompleted)
            {
                return CommandValidationResult.Reject("NO_ACTIVE_INNINGS", "No active innings for delivery.");
            }

            if (!string.Equals(command.SenderPlayerId, innings.CurrentBowlerId, StringComparison.Ordinal))
            {
                return CommandValidationResult.Reject("INVALID_BOWLER_TURN", "Sender is not the active bowler.");
            }

            if (command.TargetSpeedKmh < MinLegalSpeedKmh || command.TargetSpeedKmh > MaxLegalSpeedKmh)
            {
                return CommandValidationResult.Reject("SPEED_OUT_OF_BOUNDS", $"Speed {command.TargetSpeedKmh:F1} km/h exceeds legal physical bounds.");
            }

            if (System.Math.Abs(command.SpinRpm) > MaxLegalSpinRpm)
            {
                return CommandValidationResult.Reject("SPIN_OUT_OF_BOUNDS", $"Spin RPM {command.SpinRpm:F0} exceeds physical bounds.");
            }

            return CommandValidationResult.Valid();
        }

        public CommandValidationResult ValidateShotInput(
            ShotInputCommand command,
            MatchState state,
            long lastAcceptedSequence)
        {
            if (command == null || state == null)
            {
                return CommandValidationResult.Reject("NULL_INPUT", "Command or match state is null.");
            }

            if (!string.Equals(command.MatchId, state.Config.MatchId, StringComparison.Ordinal))
            {
                return CommandValidationResult.Reject("MATCH_ID_MISMATCH", "Command MatchId does not match active match.");
            }

            if (command.CommandSequence <= lastAcceptedSequence)
            {
                return CommandValidationResult.Reject("STALE_SEQUENCE", "Command sequence must strictly increase.");
            }

            InningsState? innings = state.CurrentInnings;
            if (innings == null || innings.IsCompleted)
            {
                return CommandValidationResult.Reject("NO_ACTIVE_INNINGS", "No active innings for shot input.");
            }

            if (!string.Equals(command.SenderPlayerId, innings.StrikerId, StringComparison.Ordinal))
            {
                return CommandValidationResult.Reject("INVALID_BATTER_TURN", "Sender is not the active striker.");
            }

            if (command.ReactionTimeFromReleaseMs < MinHumanReactionMs || command.ReactionTimeFromReleaseMs > MaxShotWindowMs)
            {
                return CommandValidationResult.Reject("REACTION_WINDOW_INVALID", $"Reaction timestamp {command.ReactionTimeFromReleaseMs:F1}ms outside plausible bounds.");
            }

            return CommandValidationResult.Valid();
        }

        /// <summary>
        /// Computes a stable 64-bit FNV-1a checksum over the official immutable BallEventRecord log
        /// (pure integer fields only — independent of platform floating-point representation).
        /// </summary>
        public ulong ComputeBallLogChecksum(IReadOnlyList<BallEventRecord> ballLog)
        {
            const ulong fnvOffsetBasis = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;

            ulong hash = fnvOffsetBasis;
            if (ballLog == null)
            {
                return hash;
            }

            for (int i = 0; i < ballLog.Count; i++)
            {
                BallEventRecord r = ballLog[i];
                hash = Mix(hash, (ulong)r.SequenceId, fnvPrime);
                hash = Mix(hash, (ulong)r.InningsNumber, fnvPrime);
                hash = Mix(hash, (ulong)r.OverNumber, fnvPrime);
                hash = Mix(hash, (ulong)r.LegalBallInOver, fnvPrime);
                hash = Mix(hash, (ulong)r.RunsOffBat, fnvPrime);
                hash = Mix(hash, (ulong)r.ExtraRuns, fnvPrime);
                hash = Mix(hash, (ulong)r.PenaltyRuns, fnvPrime);
                hash = Mix(hash, (ulong)r.PrimaryExtraType, fnvPrime);
                hash = Mix(hash, r.IsLegalDelivery ? 1UL : 0UL, fnvPrime);
                hash = Mix(hash, r.Dismissal != null ? (ulong)r.Dismissal.Type : 0UL, fnvPrime);
            }

            return hash;
        }

        private static ulong Mix(ulong current, ulong value, ulong prime)
        {
            current ^= value;
            current *= prime;
            return current;
        }
    }
}
