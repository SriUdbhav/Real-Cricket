using System;
using System.Collections.Generic;

namespace Cricket.Simulation.Core.DataModels
{
    /// <summary>
    /// Defines fielding circle restrictions for a range of 1-based overs (inclusive).
    /// </summary>
    [Serializable]
    public sealed class PowerplayPhaseRule
    {
        public string PhaseName { get; }
        public int StartOverInclusive { get; }
        public int EndOverInclusive { get; }
        public int MaxFieldersOutside30YardCircle { get; }

        public PowerplayPhaseRule(
            string phaseName,
            int startOverInclusive,
            int endOverInclusive,
            int maxFieldersOutside30YardCircle)
        {
            if (startOverInclusive < 1 || endOverInclusive < startOverInclusive)
            {
                throw new ArgumentException("Invalid over range for powerplay phase.");
            }

            if (maxFieldersOutside30YardCircle < 0 || maxFieldersOutside30YardCircle > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFieldersOutside30YardCircle));
            }

            PhaseName = string.IsNullOrWhiteSpace(phaseName) ? "Phase" : phaseName;
            StartOverInclusive = startOverInclusive;
            EndOverInclusive = endOverInclusive;
            MaxFieldersOutside30YardCircle = maxFieldersOutside30YardCircle;
        }

        public bool AppliesToOver(int oneBasedOverNumber)
        {
            return oneBasedOverNumber >= StartOverInclusive && oneBasedOverNumber <= EndOverInclusive;
        }
    }

    /// <summary>
    /// Data-driven competition rules profile so no match format regulations are hardcoded
    /// inside the scoring engine. Supports T20, ODI, Test, College Cups, and custom rulesets.
    /// </summary>
    [Serializable]
    public sealed class CompetitionRulesProfile
    {
        public string ProfileId { get; }
        public string DisplayName { get; }
        public MatchFormat Format { get; }
        public int BallsPerOver { get; }
        public int MaxInningsPerTeam { get; }
        public int? MaxOversPerInnings { get; }
        public int? MaxOversPerBowler { get; }
        public int MaxConsecutiveOversPerBowler { get; }
        public int WideRunPenalty { get; }
        public int NoBallRunPenalty { get; }
        public int StandardPenaltyRuns { get; }
        public bool FreeHitOnAllNoBalls { get; }
        public bool FreeHitOnFootFaultOnly { get; }
        public int BouncersAllowedPerOver { get; }
        public int MaxFieldersBehindSquareLeg { get; }
        public int DefaultMaxFieldersOutsideCircle { get; }
        public IReadOnlyList<PowerplayPhaseRule> PowerplayPhases { get; }
        public bool CrossOnCatchNewBatterTakesStrike { get; }
        public bool AllowDeclarations { get; }
        public int? FollowOnLeadThreshold { get; }
        public int? NewBallOverThreshold { get; }
        public int ReviewsPerInnings { get; }
        public bool RetainReviewOnUmpiresCall { get; }
        public bool AllowSuperOverOnTie { get; }
        public bool CountAllOutInningsAsFullQuotaForNrr { get; }
        public bool EnableDlsAdjustments { get; }

        public CompetitionRulesProfile(
            string profileId,
            string displayName,
            MatchFormat format,
            int ballsPerOver = 6,
            int maxInningsPerTeam = 1,
            int? maxOversPerInnings = 20,
            int? maxOversPerBowler = 4,
            int maxConsecutiveOversPerBowler = 1,
            int wideRunPenalty = 1,
            int noBallRunPenalty = 1,
            int standardPenaltyRuns = 5,
            bool freeHitOnAllNoBalls = true,
            bool freeHitOnFootFaultOnly = false,
            int bouncersAllowedPerOver = 1,
            int maxFieldersBehindSquareLeg = 2,
            int defaultMaxFieldersOutsideCircle = 5,
            IReadOnlyList<PowerplayPhaseRule>? powerplayPhases = null,
            bool crossOnCatchNewBatterTakesStrike = true,
            bool allowDeclarations = false,
            int? followOnLeadThreshold = null,
            int? newBallOverThreshold = null,
            int reviewsPerInnings = 2,
            bool retainReviewOnUmpiresCall = true,
            bool allowSuperOverOnTie = true,
            bool countAllOutInningsAsFullQuotaForNrr = true,
            bool enableDlsAdjustments = true)
        {
            if (ballsPerOver < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ballsPerOver));
            }

            if (maxInningsPerTeam < 1 || maxInningsPerTeam > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInningsPerTeam));
            }

            ProfileId = string.IsNullOrWhiteSpace(profileId) ? "custom" : profileId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ProfileId : displayName;
            Format = format;
            BallsPerOver = ballsPerOver;
            MaxInningsPerTeam = maxInningsPerTeam;
            MaxOversPerInnings = maxOversPerInnings;
            MaxOversPerBowler = maxOversPerBowler;
            MaxConsecutiveOversPerBowler = System.Math.Max(1, maxConsecutiveOversPerBowler);
            WideRunPenalty = System.Math.Max(0, wideRunPenalty);
            NoBallRunPenalty = System.Math.Max(0, noBallRunPenalty);
            StandardPenaltyRuns = System.Math.Max(0, standardPenaltyRuns);
            FreeHitOnAllNoBalls = freeHitOnAllNoBalls;
            FreeHitOnFootFaultOnly = freeHitOnFootFaultOnly;
            BouncersAllowedPerOver = System.Math.Max(0, bouncersAllowedPerOver);
            MaxFieldersBehindSquareLeg = System.Math.Max(0, maxFieldersBehindSquareLeg);
            DefaultMaxFieldersOutsideCircle = System.Math.Max(0, System.Math.Min(9, defaultMaxFieldersOutsideCircle));
            PowerplayPhases = powerplayPhases ?? Array.Empty<PowerplayPhaseRule>();
            CrossOnCatchNewBatterTakesStrike = crossOnCatchNewBatterTakesStrike;
            AllowDeclarations = allowDeclarations;
            FollowOnLeadThreshold = followOnLeadThreshold;
            NewBallOverThreshold = newBallOverThreshold;
            ReviewsPerInnings = System.Math.Max(0, reviewsPerInnings);
            RetainReviewOnUmpiresCall = retainReviewOnUmpiresCall;
            AllowSuperOverOnTie = allowSuperOverOnTie;
            CountAllOutInningsAsFullQuotaForNrr = countAllOutInningsAsFullQuotaForNrr;
            EnableDlsAdjustments = enableDlsAdjustments;
        }

        public int GetMaxFieldersOutsideCircleForOver(int oneBasedOverNumber)
        {
            for (int i = 0; i < PowerplayPhases.Count; i++)
            {
                if (PowerplayPhases[i].AppliesToOver(oneBasedOverNumber))
                {
                    return PowerplayPhases[i].MaxFieldersOutside30YardCircle;
                }
            }

            return DefaultMaxFieldersOutsideCircle;
        }

        public bool TriggersFreeHit(NoBallReason reason)
        {
            if (reason == NoBallReason.None)
            {
                return false;
            }

            if (FreeHitOnAllNoBalls)
            {
                return true;
            }

            return FreeHitOnFootFaultOnly && reason == NoBallReason.FrontFootOverstep;
        }
    }

    public static class CompetitionPresets
    {
        public static CompetitionRulesProfile CreateStandardT20()
        {
            return new CompetitionRulesProfile(
                profileId: "std_t20",
                displayName: "Standard Twenty20",
                format: MatchFormat.T20,
                ballsPerOver: 6,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 20,
                maxOversPerBowler: 4,
                maxConsecutiveOversPerBowler: 1,
                wideRunPenalty: 1,
                noBallRunPenalty: 1,
                standardPenaltyRuns: 5,
                freeHitOnAllNoBalls: true,
                freeHitOnFootFaultOnly: false,
                bouncersAllowedPerOver: 1,
                maxFieldersBehindSquareLeg: 2,
                defaultMaxFieldersOutsideCircle: 5,
                powerplayPhases: new[]
                {
                    new PowerplayPhaseRule("Powerplay", 1, 6, 2),
                    new PowerplayPhaseRule("Non-Powerplay", 7, 20, 5)
                },
                crossOnCatchNewBatterTakesStrike: true,
                allowDeclarations: false,
                followOnLeadThreshold: null,
                newBallOverThreshold: null,
                reviewsPerInnings: 2,
                retainReviewOnUmpiresCall: true,
                allowSuperOverOnTie: true,
                countAllOutInningsAsFullQuotaForNrr: true,
                enableDlsAdjustments: true);
        }

        public static CompetitionRulesProfile CreateStandardOdi()
        {
            return new CompetitionRulesProfile(
                profileId: "std_odi",
                displayName: "Standard One-Day (50 Overs)",
                format: MatchFormat.ODI,
                ballsPerOver: 6,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 50,
                maxOversPerBowler: 10,
                maxConsecutiveOversPerBowler: 1,
                wideRunPenalty: 1,
                noBallRunPenalty: 1,
                standardPenaltyRuns: 5,
                freeHitOnAllNoBalls: true,
                freeHitOnFootFaultOnly: false,
                bouncersAllowedPerOver: 2,
                maxFieldersBehindSquareLeg: 2,
                defaultMaxFieldersOutsideCircle: 5,
                powerplayPhases: new[]
                {
                    new PowerplayPhaseRule("Powerplay 1", 1, 10, 2),
                    new PowerplayPhaseRule("Powerplay 2", 11, 40, 4),
                    new PowerplayPhaseRule("Powerplay 3", 41, 50, 5)
                },
                crossOnCatchNewBatterTakesStrike: true,
                allowDeclarations: false,
                followOnLeadThreshold: null,
                newBallOverThreshold: null,
                reviewsPerInnings: 2,
                retainReviewOnUmpiresCall: true,
                allowSuperOverOnTie: true,
                countAllOutInningsAsFullQuotaForNrr: true,
                enableDlsAdjustments: true);
        }

        public static CompetitionRulesProfile CreateStandardTest()
        {
            return new CompetitionRulesProfile(
                profileId: "std_test_5day",
                displayName: "Standard 5-Day Test",
                format: MatchFormat.Test,
                ballsPerOver: 6,
                maxInningsPerTeam: 2,
                maxOversPerInnings: null,
                maxOversPerBowler: null,
                maxConsecutiveOversPerBowler: 1,
                wideRunPenalty: 1,
                noBallRunPenalty: 1,
                standardPenaltyRuns: 5,
                freeHitOnAllNoBalls: false,
                freeHitOnFootFaultOnly: false,
                bouncersAllowedPerOver: 2,
                maxFieldersBehindSquareLeg: 2,
                defaultMaxFieldersOutsideCircle: 9,
                powerplayPhases: Array.Empty<PowerplayPhaseRule>(),
                crossOnCatchNewBatterTakesStrike: true,
                allowDeclarations: true,
                followOnLeadThreshold: 200,
                newBallOverThreshold: 80,
                reviewsPerInnings: 3,
                retainReviewOnUmpiresCall: true,
                allowSuperOverOnTie: false,
                countAllOutInningsAsFullQuotaForNrr: false,
                enableDlsAdjustments: false);
        }
    }
}
