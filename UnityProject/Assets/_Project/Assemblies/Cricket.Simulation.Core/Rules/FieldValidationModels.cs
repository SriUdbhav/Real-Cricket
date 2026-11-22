using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.Math;

namespace Cricket.Simulation.Core.Rules
{
    /// <summary>
    /// Position of one of the 9 movable fielders (excluding bowler and wicketkeeper)
    /// relative to the striker's popping crease origin (0, 0).
    /// Convention: +X = off side for right-hand batter, -X = leg side for right-hand batter,
    /// +Z = down pitch toward bowler, -Z = behind popping crease (square/behind square).
    /// </summary>
    [Serializable]
    public readonly struct FielderPlacement
    {
        public string PlayerId { get; }
        public SimVector2 GroundPositionMeters { get; }
        public bool IsOutside30YardCircle { get; }
        public bool IsBehindSquareOnLegSide { get; }

        public FielderPlacement(
            string playerId,
            SimVector2 groundPositionMeters,
            bool isOutside30YardCircle,
            bool isBehindSquareOnLegSide)
        {
            PlayerId = playerId ?? string.Empty;
            GroundPositionMeters = groundPositionMeters;
            IsOutside30YardCircle = isOutside30YardCircle;
            IsBehindSquareOnLegSide = isBehindSquareOnLegSide;
        }
    }

    [Serializable]
    public sealed class FieldValidationResult
    {
        public bool IsValid => Violations.Count == 0;
        public IReadOnlyList<string> Violations { get; }

        public FieldValidationResult(IReadOnlyList<string> violations)
        {
            Violations = violations ?? Array.Empty<string>();
        }

        public static FieldValidationResult Valid() => new FieldValidationResult(Array.Empty<string>());
    }
}
