using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Dls
{
    /// <summary>
    /// Validates rain-interruption inputs and guards official DLS calculations so they remain
    /// explicitly disabled unless a properly licensed and verified <see cref="IDlsResourceTableProvider"/>
    /// is injected and enabled by the active <see cref="CompetitionRulesProfile"/>.
    /// </summary>
    public sealed class DlsCalculator : IDlsCalculator
    {
        private readonly IDlsResourceTableProvider _resourceProvider;

        public DlsCalculator(IDlsResourceTableProvider? resourceProvider = null)
        {
            _resourceProvider = resourceProvider ?? new UnlicensedDlsResourceTableProvider();
        }

        public bool IsOperational(CompetitionRulesProfile profile)
        {
            if (profile == null || !profile.EnableDlsAdjustments)
            {
                return false;
            }

            return HasVerifiedLicensedProvider();
        }

        public DlsCalculationResult CalculateRevisedTarget(CompetitionRulesProfile profile, DlsTargetRequest request)
        {
            if (profile == null)
            {
                return new DlsCalculationResult(DlsCalculationStatus.InvalidInput, "CompetitionRulesProfile cannot be null.");
            }

            if (request == null)
            {
                return new DlsCalculationResult(DlsCalculationStatus.InvalidInput, "DlsTargetRequest cannot be null.");
            }

            string? validationError = ValidateTargetRequest(profile, request);
            if (validationError != null)
            {
                return new DlsCalculationResult(DlsCalculationStatus.InvalidInput, validationError, _resourceProvider.Metadata.TableId);
            }

            if (!profile.EnableDlsAdjustments)
            {
                return new DlsCalculationResult(
                    DlsCalculationStatus.DisabledByCompetitionProfile,
                    $"DLS adjustments are disabled for competition profile '{profile.ProfileId}'.",
                    _resourceProvider.Metadata.TableId);
            }

            if (!HasVerifiedLicensedProvider())
            {
                return new DlsCalculationResult(
                    DlsCalculationStatus.UnsupportedNoLicensedResourceTable,
                    "Official DLS calculation is disabled until a properly licensed and verified resource table is supplied.",
                    _resourceProvider.Metadata.TableId);
            }

            if (!TryComputeTotalTeamResource(request.InitialOversPerInnings, request.Team1Interruptions, profile.BallsPerOver, out double r1) ||
                !TryComputeTotalTeamResource(request.Team2RevisedMaxOvers, request.Team2Interruptions, profile.BallsPerOver, out double r2))
            {
                return new DlsCalculationResult(
                    DlsCalculationStatus.UnsupportedNoLicensedResourceTable,
                    "Resource table lookup failed for the requested match parameters.",
                    _resourceProvider.Metadata.TableId);
            }

            int revisedTarget;
            if (r2 <= r1)
            {
                double scaled = request.Team1TotalRuns * (r2 / r1);
                revisedTarget = (int)System.Math.Floor(scaled) + 1;
            }
            else
            {
                if (!_resourceProvider.TryGetCompetitionG50(profile.Format, out double g50) || g50 <= 0.0)
                {
                    return new DlsCalculationResult(
                        DlsCalculationStatus.UnsupportedNoLicensedResourceTable,
                        "Licensed provider did not supply a valid G50 constant for this format.",
                        _resourceProvider.Metadata.TableId);
                }

                double extra = g50 * ((r2 - r1) / 100.0);
                revisedTarget = (int)System.Math.Floor(request.Team1TotalRuns + extra) + 1;
            }

            return new DlsCalculationResult(
                DlsCalculationStatus.Succeeded,
                "Revised target calculated using verified licensed resource table.",
                _resourceProvider.Metadata.TableId,
                revisedTargetRuns: revisedTarget,
                parScore: null,
                team1ResourcePercentage: r1,
                team2ResourcePercentage: r2);
        }

        public DlsCalculationResult CalculateParScore(CompetitionRulesProfile profile, DlsParScoreRequest request)
        {
            if (profile == null)
            {
                return new DlsCalculationResult(DlsCalculationStatus.InvalidInput, "CompetitionRulesProfile cannot be null.");
            }

            if (request == null)
            {
                return new DlsCalculationResult(DlsCalculationStatus.InvalidInput, "DlsParScoreRequest cannot be null.");
            }

            string? validationError = ValidateParScoreRequest(profile, request);
            if (validationError != null)
            {
                return new DlsCalculationResult(DlsCalculationStatus.InvalidInput, validationError, _resourceProvider.Metadata.TableId);
            }

            if (!profile.EnableDlsAdjustments)
            {
                return new DlsCalculationResult(
                    DlsCalculationStatus.DisabledByCompetitionProfile,
                    $"DLS adjustments are disabled for competition profile '{profile.ProfileId}'.",
                    _resourceProvider.Metadata.TableId);
            }

            if (!HasVerifiedLicensedProvider())
            {
                return new DlsCalculationResult(
                    DlsCalculationStatus.UnsupportedNoLicensedResourceTable,
                    "Official DLS par score calculation is disabled until a properly licensed and verified resource table is supplied.",
                    _resourceProvider.Metadata.TableId);
            }

            if (!TryComputeTotalTeamResource(request.InitialOversPerInnings, request.Team1Interruptions, profile.BallsPerOver, out double r1) ||
                !TryComputeTotalTeamResource(request.Team2MaxOvers, request.Team2Interruptions, profile.BallsPerOver, out double r2Total))
            {
                return new DlsCalculationResult(
                    DlsCalculationStatus.UnsupportedNoLicensedResourceTable,
                    "Resource table lookup failed for the requested match parameters.",
                    _resourceProvider.Metadata.TableId);
            }

            int totalTeam2Balls = request.Team2MaxOvers * profile.BallsPerOver;
            int consumedTeam2Balls = (request.Team2OversCompleted * profile.BallsPerOver) + request.Team2BallsInCurrentOver;
            int remainingBalls = System.Math.Max(0, totalTeam2Balls - consumedTeam2Balls);
            int remainingOvers = remainingBalls / profile.BallsPerOver;
            int remainingPartialBalls = remainingBalls % profile.BallsPerOver;

            if (!_resourceProvider.TryGetRemainingResourcePercentage(remainingOvers, remainingPartialBalls, request.Team2WicketsLost, out double r2Remaining))
            {
                return new DlsCalculationResult(
                    DlsCalculationStatus.UnsupportedNoLicensedResourceTable,
                    "Resource table lookup failed for remaining Team 2 resources.",
                    _resourceProvider.Metadata.TableId);
            }

            double r2Used = System.Math.Max(0.0, r2Total - r2Remaining);
            int parScore = (int)System.Math.Floor(request.Team1TotalRuns * (r2Used / r1));

            return new DlsCalculationResult(
                DlsCalculationStatus.Succeeded,
                "Par score calculated using verified licensed resource table.",
                _resourceProvider.Metadata.TableId,
                revisedTargetRuns: null,
                parScore: parScore,
                team1ResourcePercentage: r1,
                team2ResourcePercentage: r2Used);
        }

        private bool HasVerifiedLicensedProvider()
        {
            return _resourceProvider.IsLicensedTableAvailable &&
                   _resourceProvider.Metadata != null &&
                   _resourceProvider.Metadata.IsVerifiedLicensedTable &&
                   _resourceProvider.Metadata.LicenseInfo != null &&
                   _resourceProvider.Metadata.LicenseInfo.LicenseType == ContentLicenseType.LicensedThirdParty;
        }

        private bool TryComputeTotalTeamResource(
            int startingOvers,
            IReadOnlyList<RainInterruptionRecord> interruptions,
            int ballsPerOver,
            out double totalResource)
        {
            totalResource = 0.0;
            if (!_resourceProvider.TryGetRemainingResourcePercentage(startingOvers, 0, 0, out double initialResource) ||
                initialResource <= 0.0)
            {
                return false;
            }

            double currentResource = initialResource;
            int currentMaxOvers = startingOvers;

            for (int i = 0; i < interruptions.Count; i++)
            {
                RainInterruptionRecord stop = interruptions[i];
                int totalBalls = currentMaxOvers * ballsPerOver;
                int bowledBalls = (stop.OversCompletedBeforeInterruption * ballsPerOver) + stop.BallsInOverBeforeInterruption;
                int beforeBalls = System.Math.Max(0, totalBalls - bowledBalls);
                int afterBalls = System.Math.Max(0, beforeBalls - (stop.OversLost * ballsPerOver));

                if (!_resourceProvider.TryGetRemainingResourcePercentage(beforeBalls / ballsPerOver, beforeBalls % ballsPerOver, stop.WicketsLostAtInterruption, out double resBefore) ||
                    !_resourceProvider.TryGetRemainingResourcePercentage(afterBalls / ballsPerOver, afterBalls % ballsPerOver, stop.WicketsLostAtInterruption, out double resAfter))
                {
                    return false;
                }

                currentResource -= System.Math.Max(0.0, resBefore - resAfter);
                currentMaxOvers = System.Math.Max(0, currentMaxOvers - stop.OversLost);
            }

            if (currentResource <= 0.0)
            {
                return false;
            }

            totalResource = currentResource;
            return true;
        }

        private static string? ValidateTargetRequest(CompetitionRulesProfile profile, DlsTargetRequest request)
        {
            if (request.InitialOversPerInnings < 1)
            {
                return "InitialOversPerInnings must be at least 1.";
            }

            if (request.Team1TotalRuns < 0)
            {
                return "Team1TotalRuns cannot be negative.";
            }

            if (request.Team2RevisedMaxOvers < 1 || request.Team2RevisedMaxOvers > request.InitialOversPerInnings)
            {
                return "Team2RevisedMaxOvers must be between 1 and InitialOversPerInnings.";
            }

            string? err1 = ValidateInterruptions(request.Team1Interruptions, request.InitialOversPerInnings, profile.BallsPerOver, "Team1");
            if (err1 != null)
            {
                return err1;
            }

            return ValidateInterruptions(request.Team2Interruptions, request.Team2RevisedMaxOvers, profile.BallsPerOver, "Team2");
        }

        private static string? ValidateParScoreRequest(CompetitionRulesProfile profile, DlsParScoreRequest request)
        {
            if (request.InitialOversPerInnings < 1)
            {
                return "InitialOversPerInnings must be at least 1.";
            }

            if (request.Team1TotalRuns < 0)
            {
                return "Team1TotalRuns cannot be negative.";
            }

            if (request.Team2MaxOvers < 1 || request.Team2MaxOvers > request.InitialOversPerInnings)
            {
                return "Team2MaxOvers must be between 1 and InitialOversPerInnings.";
            }

            if (request.Team2OversCompleted < 0 || request.Team2OversCompleted > request.Team2MaxOvers)
            {
                return "Team2OversCompleted is out of bounds.";
            }

            if (request.Team2BallsInCurrentOver < 0 || request.Team2BallsInCurrentOver >= profile.BallsPerOver)
            {
                return $"Team2BallsInCurrentOver must be between 0 and {profile.BallsPerOver - 1}.";
            }

            if (request.Team2OversCompleted == request.Team2MaxOvers && request.Team2BallsInCurrentOver > 0)
            {
                return "Team2BallsInCurrentOver cannot be positive when Team2OversCompleted equals Team2MaxOvers.";
            }

            if (request.Team2WicketsLost < 0 || request.Team2WicketsLost > 9)
            {
                return "Team2WicketsLost must be between 0 and 9 for an active innings par score.";
            }

            string? err1 = ValidateInterruptions(request.Team1Interruptions, request.InitialOversPerInnings, profile.BallsPerOver, "Team1");
            if (err1 != null)
            {
                return err1;
            }

            return ValidateInterruptions(request.Team2Interruptions, request.Team2MaxOvers, profile.BallsPerOver, "Team2");
        }

        private static string? ValidateInterruptions(
            IReadOnlyList<RainInterruptionRecord> interruptions,
            int maxOvers,
            int ballsPerOver,
            string teamLabel)
        {
            int remainingOvers = maxOvers;
            for (int i = 0; i < interruptions.Count; i++)
            {
                RainInterruptionRecord rec = interruptions[i];
                if (rec == null)
                {
                    return $"{teamLabel} interruption at index {i} is null.";
                }

                if (rec.InningsNumber < 1 || rec.InningsNumber > 2)
                {
                    return $"{teamLabel} interruption has invalid InningsNumber {rec.InningsNumber}.";
                }

                if (rec.OversCompletedBeforeInterruption < 0 || rec.OversCompletedBeforeInterruption > remainingOvers)
                {
                    return $"{teamLabel} interruption OversCompletedBeforeInterruption exceeds available overs.";
                }

                if (rec.BallsInOverBeforeInterruption < 0 || rec.BallsInOverBeforeInterruption >= ballsPerOver)
                {
                    return $"{teamLabel} interruption BallsInOverBeforeInterruption must be in [0, {ballsPerOver - 1}].";
                }

                if (rec.WicketsLostAtInterruption < 0 || rec.WicketsLostAtInterruption > 9)
                {
                    return $"{teamLabel} interruption WicketsLostAtInterruption must be in [0, 9].";
                }

                if (rec.OversLost < 1 || rec.OversCompletedBeforeInterruption + rec.OversLost > remainingOvers)
                {
                    return $"{teamLabel} interruption OversLost ({rec.OversLost}) is invalid for remaining overs.";
                }

                remainingOvers -= rec.OversLost;
            }

            return null;
        }
    }
}
