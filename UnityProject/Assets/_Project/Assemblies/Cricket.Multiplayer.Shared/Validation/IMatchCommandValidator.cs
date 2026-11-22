using System.Collections.Generic;
using Cricket.Multiplayer.Shared.Protocol;
using Cricket.Simulation.Core.DataModels;

namespace Cricket.Multiplayer.Shared.Validation
{
    public readonly struct CommandValidationResult
    {
        public bool IsValid { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        private CommandValidationResult(bool isValid, string errorCode, string errorMessage)
        {
            IsValid = isValid;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public static CommandValidationResult Valid() => new CommandValidationResult(true, string.Empty, string.Empty);

        public static CommandValidationResult Reject(string code, string message) =>
            new CommandValidationResult(false, code, message);
    }

    public interface IMatchCommandValidator
    {
        CommandValidationResult ValidateDeliveryCommit(DeliveryCommitCommand command, MatchState state, long lastAcceptedSequence);
        CommandValidationResult ValidateShotInput(ShotInputCommand command, MatchState state, long lastAcceptedSequence);
        ulong ComputeBallLogChecksum(IReadOnlyList<BallEventRecord> ballLog);
    }
}
