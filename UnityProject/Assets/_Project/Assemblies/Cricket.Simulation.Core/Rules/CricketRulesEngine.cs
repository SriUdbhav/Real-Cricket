using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;

namespace Cricket.Simulation.Core.Rules
{
    /// <summary>
    /// Complete competition-profile-driven cricket scoring and MCC Laws rules engine.
    /// Separates official rule adjudication from approximate physical telemetry and visuals.
    /// </summary>
    public sealed class CricketRulesEngine : ICricketRulesEngine
    {
        private const int WicketsRequiredForAllOut = 10;

        private readonly IMatchEventBus _eventBus;
        private MatchState? _state;
        private long _eventSequence;
        private long _ballSequence;

        public MatchState State => _state ?? throw new InvalidOperationException("Match has not been initialized.");

        public CricketRulesEngine(IMatchEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public void InitializeMatch(MatchConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _state = new MatchState(config);
            _eventSequence = 0;
            _ballSequence = 0;
        }

        public void RecordToss(string tossWinnerTeamId, TossDecision decision)
        {
            MatchState state = State;
            if (state.TossWinnerTeamId != null)
            {
                throw new InvalidOperationException("Toss has already been recorded for this match.");
            }

            string opponentTeamId = state.Config.GetOpponentTeamId(tossWinnerTeamId);
            state.TossWinnerTeamId = tossWinnerTeamId;
            state.TossDecision = decision;

            if (decision == TossDecision.BatFirst)
            {
                state.BattingFirstTeamId = tossWinnerTeamId;
                state.BowlingFirstTeamId = opponentTeamId;
            }
            else
            {
                state.BattingFirstTeamId = opponentTeamId;
                state.BowlingFirstTeamId = tossWinnerTeamId;
            }

            _eventBus.Publish(new TossCompletedEvent(
                NextEventSeq(),
                tossWinnerTeamId,
                decision,
                state.BattingFirstTeamId,
                state.BowlingFirstTeamId));
        }

        public InningsState StartNextInnings(bool enforceFollowOn = false)
        {
            MatchState state = State;
            if (state.BattingFirstTeamId == null || state.BowlingFirstTeamId == null)
            {
                throw new InvalidOperationException("Cannot start an innings before the toss is recorded.");
            }

            if (state.Result.ResultType != MatchResultType.InProgress &&
                state.Result.ResultType != MatchResultType.TiePendingSuperOver)
            {
                throw new InvalidOperationException("Match is already completed.");
            }

            if (state.CurrentInnings != null && !state.CurrentInnings.IsCompleted)
            {
                throw new InvalidOperationException("Cannot start next innings while current innings is still active.");
            }

            CompetitionRulesProfile rules = state.Config.Rules;
            int nextInningsNumber = state.Innings.Count + 1;
            int maxTotalInnings = rules.MaxInningsPerTeam * 2;
            if (nextInningsNumber > maxTotalInnings)
            {
                throw new InvalidOperationException($"Competition profile allows at most {maxTotalInnings} scheduled innings.");
            }

            string battingTeamId;
            string bowlingTeamId;
            int? targetRuns = null;

            if (nextInningsNumber == 1)
            {
                battingTeamId = state.BattingFirstTeamId;
                bowlingTeamId = state.BowlingFirstTeamId;
            }
            else if (nextInningsNumber == 2)
            {
                battingTeamId = state.BowlingFirstTeamId;
                bowlingTeamId = state.BattingFirstTeamId;
                if (rules.MaxInningsPerTeam == 1)
                {
                    targetRuns = state.Innings[0].TotalRuns + 1;
                }
            }
            else if (nextInningsNumber == 3)
            {
                if (enforceFollowOn)
                {
                    if (!IsFollowOnEligible())
                    {
                        throw new InvalidOperationException("Follow-on is not eligible under current match state or competition profile.");
                    }

                    battingTeamId = state.Innings[1].BattingTeamId;
                    bowlingTeamId = state.Innings[1].BowlingTeamId;
                }
                else
                {
                    battingTeamId = state.Innings[0].BattingTeamId;
                    bowlingTeamId = state.Innings[0].BowlingTeamId;
                }
            }
            else
            {
                // 4th innings of a multi-innings match
                InningsState third = state.Innings[2];
                battingTeamId = third.BowlingTeamId;
                bowlingTeamId = third.BattingTeamId;

                int bowlingSideTotal = GetTeamCumulativeRuns(state, bowlingTeamId);
                int battingSidePriorTotal = GetTeamCumulativeRuns(state, battingTeamId);
                int requiredToLead = (bowlingSideTotal - battingSidePriorTotal) + 1;
                targetRuns = System.Math.Max(1, requiredToLead);
            }

            PlayingLineup battingLineup = state.Config.GetLineupForTeam(battingTeamId);
            var innings = new InningsState(nextInningsNumber, battingTeamId, bowlingTeamId, targetRuns)
            {
                StrikerId = battingLineup.BattingOrderPlayerIds[0],
                NonStrikerId = battingLineup.BattingOrderPlayerIds[1],
                NextBatterOrderIndex = 2
            };

            EnsureBatterState(innings, innings.StrikerId);
            EnsureBatterState(innings, innings.NonStrikerId);

            state.Innings.Add(innings);
            state.CurrentInningsIndex = state.Innings.Count - 1;

            _eventBus.Publish(new InningsStartedEvent(
                NextEventSeq(),
                innings.InningsNumber,
                innings.BattingTeamId,
                innings.BowlingTeamId,
                innings.TargetRuns));

            return innings;
        }

        public void StartOver(string bowlerId)
        {
            MatchState state = State;
            InningsState innings = RequireActiveInnings();
            CompetitionRulesProfile rules = state.Config.Rules;

            if (innings.CurrentBowlerId != null && innings.LegalBallsInCurrentOver > 0)
            {
                throw new InvalidOperationException("Cannot start a new over while an over is already in progress.");
            }

            PlayingLineup bowlingLineup = state.Config.GetLineupForTeam(innings.BowlingTeamId);
            if (!bowlingLineup.ContainsPlayer(bowlerId))
            {
                PublishRuleViolation("BOWLER_NOT_IN_XI", $"Bowler '{bowlerId}' is not in the bowling team's Playing XI.");
                throw new InvalidOperationException($"Bowler '{bowlerId}' is not in the bowling team's Playing XI.");
            }

            if (rules.MaxConsecutiveOversPerBowler == 1 &&
                string.Equals(innings.PreviousOverBowlerId, bowlerId, StringComparison.Ordinal))
            {
                PublishRuleViolation("CONSECUTIVE_OVER_DISALLOWED", $"Bowler '{bowlerId}' cannot bowl consecutive overs.");
                throw new InvalidOperationException($"Bowler '{bowlerId}' cannot bowl consecutive overs.");
            }

            BowlerSpellState spell = EnsureBowlerState(innings, bowlerId);
            if (rules.MaxOversPerBowler.HasValue && spell.CompletedOvers >= rules.MaxOversPerBowler.Value)
            {
                PublishRuleViolation("BOWLER_QUOTA_EXCEEDED", $"Bowler '{bowlerId}' has completed their maximum quota of {rules.MaxOversPerBowler.Value} overs.");
                throw new InvalidOperationException($"Bowler '{bowlerId}' has already bowled the maximum {rules.MaxOversPerBowler.Value} overs.");
            }

            innings.CurrentBowlerId = bowlerId;
            innings.LegalBallsInCurrentOver = 0;
            innings.BouncersInCurrentOver = 0;
            innings.RunsConcededInCurrentOverByBowler = 0;
            innings.WicketsInCurrentOver = 0;

            _eventBus.Publish(new OverStartedEvent(
                NextEventSeq(),
                innings.InningsNumber,
                innings.CompletedOvers + 1,
                bowlerId,
                innings.StrikerId ?? string.Empty,
                innings.NonStrikerId ?? string.Empty));
        }

        public BallEventRecord EvaluateAndCommitDelivery(DeliveryResolutionInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            MatchState state = State;
            InningsState innings = RequireActiveInnings();
            CompetitionRulesProfile rules = state.Config.Rules;

            if (string.IsNullOrEmpty(innings.CurrentBowlerId))
            {
                throw new InvalidOperationException("No active bowler selected. Call StartOver before evaluating a delivery.");
            }

            if (string.IsNullOrEmpty(innings.StrikerId) || string.IsNullOrEmpty(innings.NonStrikerId))
            {
                throw new InvalidOperationException("Active striker and non-striker must be present.");
            }

            string strikerId = innings.StrikerId;
            string nonStrikerId = innings.NonStrikerId;
            string bowlerId = innings.CurrentBowlerId;
            bool wasFreeHit = innings.IsFreeHitPending;

            BatterInningsState strikerState = EnsureBatterState(innings, strikerId);
            BowlerSpellState bowlerState = EnsureBowlerState(innings, bowlerId);

            // 1. Determine effective No-Ball and Wide status under CompetitionRulesProfile
            NoBallReason noBallReason = input.Delivery.DetectedNoBallReason;
            if (noBallReason == NoBallReason.None && input.Delivery.IsBouncerAboveShoulder)
            {
                if (innings.BouncersInCurrentOver >= rules.BouncersAllowedPerOver)
                {
                    noBallReason = NoBallReason.ExcessiveBouncersInOver;
                }
                else
                {
                    innings.BouncersInCurrentOver++;
                }
            }

            bool isNoBall = noBallReason != NoBallReason.None;
            // A ball cannot be both a No-Ball and a Wide; nor a Wide if the batter hit it with bat/glove.
            bool isWide = !isNoBall && input.Delivery.IsWideByCreaseLine && !input.Shot.MadeBatOrGloveContact;
            bool isLegalDelivery = !isNoBall && !isWide;

            // 2. Resolve physical running, short runs, boundaries, and MCC Law 19.8 overthrows
            int netCompletedRuns = System.Math.Max(0, input.Fielding.CompletedRuns - input.Fielding.ShortRunsDetected);
            int runningOrBoundaryRuns;
            bool isBoundaryFour = false;
            bool isBoundarySix = false;

            if (input.Fielding.ClearedBoundaryOnFull && input.Shot.MadeBatOrGloveContact)
            {
                runningOrBoundaryRuns = 6;
                isBoundarySix = true;
            }
            else if (input.Fielding.OverthrowReachedBoundary)
            {
                // MCC Law 19.8: completed runs + run in progress if crossed at instant of throw + 4 boundary runs
                int inProgressRun = input.Fielding.CrossedAtInstantOfThrow ? 1 : 0;
                runningOrBoundaryRuns = netCompletedRuns + inProgressRun + 4;
            }
            else if (input.Fielding.ReachedBoundaryOnBounce)
            {
                // If ball reached boundary cleanly (not via overthrow), boundary 4 replaces running runs
                runningOrBoundaryRuns = 4;
                isBoundaryFour = input.Shot.MadeBatOrGloveContact;
            }
            else
            {
                runningOrBoundaryRuns = netCompletedRuns;
            }

            // 3. Allocate runs to Bat vs Extras vs Bowler
            int runsOffBat = 0;
            ExtraType primaryExtra = ExtraType.None;
            int extraRuns = 0;
            int bowlerRunsCharged = 0;

            if (isWide)
            {
                primaryExtra = ExtraType.Wide;
                extraRuns = rules.WideRunPenalty + runningOrBoundaryRuns;
                innings.Wides += extraRuns;
                bowlerState.Wides++;
                bowlerRunsCharged = extraRuns;
            }
            else if (isNoBall)
            {
                primaryExtra = ExtraType.NoBall;
                bowlerState.NoBalls++;
                innings.NoBalls += rules.NoBallRunPenalty;

                if (input.Shot.MadeBatOrGloveContact)
                {
                    runsOffBat = runningOrBoundaryRuns;
                    extraRuns = rules.NoBallRunPenalty;
                    bowlerRunsCharged = rules.NoBallRunPenalty + runsOffBat;
                }
                else
                {
                    // On a No-Ball without bat contact, additional runs are byes/leg-byes and NOT charged to bowler
                    extraRuns = rules.NoBallRunPenalty + runningOrBoundaryRuns;
                    bowlerRunsCharged = rules.NoBallRunPenalty;
                    if (runningOrBoundaryRuns > 0)
                    {
                        if (input.Shot.PrimaryContact == ContactZone.PadOnly || input.Shot.PrimaryContact == ContactZone.Body)
                        {
                            innings.LegByes += runningOrBoundaryRuns;
                        }
                        else
                        {
                            innings.Byes += runningOrBoundaryRuns;
                        }
                    }
                }
            }
            else
            {
                // Legal delivery
                if (input.Shot.MadeBatOrGloveContact)
                {
                    runsOffBat = runningOrBoundaryRuns;
                    bowlerRunsCharged = runsOffBat;
                }
                else if (runningOrBoundaryRuns > 0)
                {
                    if (input.Shot.PrimaryContact == ContactZone.PadOnly || input.Shot.PrimaryContact == ContactZone.Body)
                    {
                        primaryExtra = ExtraType.LegBye;
                        extraRuns = runningOrBoundaryRuns;
                        innings.LegByes += extraRuns;
                    }
                    else
                    {
                        primaryExtra = ExtraType.Bye;
                        extraRuns = runningOrBoundaryRuns;
                        innings.Byes += extraRuns;
                    }
                }
            }

            int penaltyRuns = input.Fielding.PenaltyAwardedToBattingSide ? rules.StandardPenaltyRuns : 0;
            if (penaltyRuns > 0)
            {
                innings.PenaltyRuns += penaltyRuns;
                if (primaryExtra == ExtraType.None)
                {
                    primaryExtra = ExtraType.Penalty;
                }
            }

            // 4. Validate and adjudicate dismissal under MCC Laws + Free Hit rules
            DismissalRecord? officialDismissal = AdjudicateDismissal(
                input.Fielding,
                strikerId,
                nonStrikerId,
                bowlerId,
                isWide,
                isNoBall,
                wasFreeHit);

            // 5. Commit stats to batter, bowler, and innings
            // Note: No-Balls count as balls faced by the striker; Wides do NOT.
            if (!isWide)
            {
                strikerState.BallsFaced++;
            }

            strikerState.Runs += runsOffBat;
            if (isBoundaryFour)
            {
                strikerState.Fours++;
            }

            if (isBoundarySix)
            {
                strikerState.Sixes++;
            }

            int totalRunsOnBall = runsOffBat + extraRuns + penaltyRuns;
            innings.TotalRuns += totalRunsOnBall;
            innings.TotalExtras += extraRuns + penaltyRuns;

            bowlerState.RunsConceded += bowlerRunsCharged;
            innings.RunsConcededInCurrentOverByBowler += bowlerRunsCharged;

            if (isLegalDelivery)
            {
                innings.LegalBallsInCurrentOver++;
                bowlerState.LegalBallsInCurrentOver++;
            }

            if (officialDismissal != null)
            {
                innings.WicketsLost++;
                innings.WicketsInCurrentOver++;
                BatterInningsState outBatterState = EnsureBatterState(innings, officialDismissal.OutBatterId);
                outBatterState.IsOut = true;
                outBatterState.Dismissal = officialDismissal;

                if (officialDismissal.BowlerCredited)
                {
                    bowlerState.Wickets++;
                }
            }

            // 6. Update Free Hit state
            bool previousFreeHit = innings.IsFreeHitPending;
            if (isNoBall && rules.TriggersFreeHit(noBallReason))
            {
                innings.IsFreeHitPending = true;
            }
            else if (isLegalDelivery && wasFreeHit)
            {
                innings.IsFreeHitPending = false;
            }
            // Note: If wasFreeHit && isWide, Free Hit carries over unchanged!

            if (previousFreeHit != innings.IsFreeHitPending)
            {
                _eventBus.Publish(new FreeHitStatusChangedEvent(
                    NextEventSeq(),
                    innings.IsFreeHitPending,
                    innings.IsFreeHitPending ? $"Triggered by {noBallReason}" : "Consumed by legal delivery"));
            }

            // 7. Create immutable event-sourced BallEventRecord
            var record = new BallEventRecord(
                sequenceId: ++_ballSequence,
                inningsNumber: innings.InningsNumber,
                overNumber: innings.CompletedOvers + 1,
                legalBallInOver: innings.LegalBallsInCurrentOver,
                strikerId: strikerId,
                nonStrikerId: nonStrikerId,
                bowlerId: bowlerId,
                wasFreeHit: wasFreeHit,
                isLegalDelivery: isLegalDelivery,
                runsOffBat: runsOffBat,
                primaryExtraType: primaryExtra,
                noBallReason: noBallReason,
                extraRuns: extraRuns,
                penaltyRuns: penaltyRuns,
                isBoundaryFour: isBoundaryFour,
                isBoundarySix: isBoundarySix,
                dismissal: officialDismissal,
                deliveryTelemetry: input.Delivery,
                shotTelemetry: input.Shot);

            innings.BallLog.Add(record);

            // 8. Apply strike rotation & bring in next batter (before over-end swap)
            ApplyStrikeAndBatterRotation(state, innings, input.Fielding, officialDismissal, isBoundaryFour || isBoundarySix);

            // 9. Publish ball, wicket, and score events
            _eventBus.Publish(new BallResolvedEvent(NextEventSeq(), record));

            if (officialDismissal != null)
            {
                _eventBus.Publish(new WicketFallenEvent(
                    NextEventSeq(),
                    innings.InningsNumber,
                    innings.TotalRuns,
                    innings.WicketsLost,
                    officialDismissal));
            }

            // 10. Check immediate mid-over innings completion (Target reached or All Out)
            bool inningsEnded = TryCompleteInningsIfConditionMet(state, innings);

            // 11. Handle Over completion if legal ball quota reached and innings still active
            if (!inningsEnded && innings.LegalBallsInCurrentOver >= rules.BallsPerOver)
            {
                int completedOverNumber = innings.CompletedOvers + 1;
                bool isMaiden = innings.RunsConcededInCurrentOverByBowler == 0;

                innings.CompletedOvers++;
                innings.LegalBallsInCurrentOver = 0;

                bowlerState.CompletedOvers++;
                bowlerState.LegalBallsInCurrentOver = 0;
                if (isMaiden)
                {
                    bowlerState.Maidens++;
                }

                // Swap ends at the completion of the over
                SwapEnds(innings);

                innings.PreviousOverBowlerId = bowlerId;
                innings.CurrentBowlerId = null;

                _eventBus.Publish(new OverCompletedEvent(
                    NextEventSeq(),
                    innings.InningsNumber,
                    completedOverNumber,
                    bowlerId,
                    innings.RunsConcededInCurrentOverByBowler,
                    innings.WicketsInCurrentOver,
                    isMaiden));

                TryCompleteInningsIfConditionMet(state, innings);
            }

            _eventBus.Publish(new ScoreUpdatedEvent(
                NextEventSeq(),
                innings.InningsNumber,
                innings.TotalRuns,
                innings.WicketsLost,
                innings.CompletedOvers,
                innings.LegalBallsInCurrentOver,
                innings.TargetRuns));

            return record;
        }

        public void DeclareCurrentInnings()
        {
            MatchState state = State;
            InningsState innings = RequireActiveInnings();

            if (!state.Config.Rules.AllowDeclarations)
            {
                PublishRuleViolation("DECLARATION_DISALLOWED", "Declarations are not permitted in this competition profile.");
                throw new InvalidOperationException("Declarations are not permitted in this competition profile.");
            }

            CompleteInnings(state, innings, InningsEndReason.Declared);
        }

        public bool IsFollowOnEligible()
        {
            MatchState state = State;
            int? threshold = state.Config.Rules.FollowOnLeadThreshold;
            if (!threshold.HasValue || state.Innings.Count != 2)
            {
                return false;
            }

            InningsState first = state.Innings[0];
            InningsState second = state.Innings[1];
            if (!first.IsCompleted || !second.IsCompleted)
            {
                return false;
            }

            int lead = first.TotalRuns - second.TotalRuns;
            return lead >= threshold.Value;
        }

        public FieldValidationResult ValidateFieldPlacement(
            IReadOnlyList<FielderPlacement> movableFielders,
            int oneBasedOverNumber)
        {
            MatchState state = State;
            CompetitionRulesProfile rules = state.Config.Rules;
            var violations = new List<string>();

            if (movableFielders == null || movableFielders.Count != 9)
            {
                violations.Add($"Exactly 9 movable fielders (excluding bowler and wicketkeeper) must be placed. Found {movableFielders?.Count ?? 0}.");
                return new FieldValidationResult(violations);
            }

            int outsideCircleCount = 0;
            int behindSquareLegCount = 0;

            for (int i = 0; i < movableFielders.Count; i++)
            {
                if (movableFielders[i].IsOutside30YardCircle)
                {
                    outsideCircleCount++;
                }

                if (movableFielders[i].IsBehindSquareOnLegSide)
                {
                    behindSquareLegCount++;
                }
            }

            int maxOutside = rules.GetMaxFieldersOutsideCircleForOver(oneBasedOverNumber);
            if (outsideCircleCount > maxOutside)
            {
                violations.Add($"Over {oneBasedOverNumber} allows at most {maxOutside} fielders outside the 30-yard circle; {outsideCircleCount} placed.");
            }

            if (behindSquareLegCount > rules.MaxFieldersBehindSquareLeg)
            {
                violations.Add($"At most {rules.MaxFieldersBehindSquareLeg} fielders are permitted behind square on the leg side; {behindSquareLegCount} placed.");
            }

            return new FieldValidationResult(violations);
        }

        public double CalculateNetRunRate(
            int totalRunsScored,
            int legalBallsFaced,
            bool wasAllOutInAnyInningsAdjusted,
            int fullQuotaBallsForAdjustedInnings,
            int totalRunsConceded,
            int legalBallsBowled,
            bool opponentAllOutAdjusted,
            int fullQuotaBallsForOpponentAdjusted)
        {
            CompetitionRulesProfile rules = State.Config.Rules;
            int ballsPerOver = rules.BallsPerOver;

            int effectiveBallsFaced = (rules.CountAllOutInningsAsFullQuotaForNrr && wasAllOutInAnyInningsAdjusted)
                ? fullQuotaBallsForAdjustedInnings
                : legalBallsFaced;

            int effectiveBallsBowled = (rules.CountAllOutInningsAsFullQuotaForNrr && opponentAllOutAdjusted)
                ? fullQuotaBallsForOpponentAdjusted
                : legalBallsBowled;

            double oversFaced = effectiveBallsFaced > 0 ? effectiveBallsFaced / (double)ballsPerOver : 0.0;
            double oversBowled = effectiveBallsBowled > 0 ? effectiveBallsBowled / (double)ballsPerOver : 0.0;

            double runRateFor = oversFaced > 0.0 ? totalRunsScored / oversFaced : 0.0;
            double runRateAgainst = oversBowled > 0.0 ? totalRunsConceded / oversBowled : 0.0;

            return runRateFor - runRateAgainst;
        }

        private DismissalRecord? AdjudicateDismissal(
            FieldingAndRunningEvidence fielding,
            string strikerId,
            string nonStrikerId,
            string bowlerId,
            bool isWide,
            bool isNoBall,
            bool wasFreeHit)
        {
            DismissalType proposed = fielding.ProposedDismissal;
            if (proposed == DismissalType.None)
            {
                return null;
            }

            // Free Hit and No-Ball dismissal restrictions (MCC Law 21.18 + ICC Playing Conditions)
            if (wasFreeHit || isNoBall)
            {
                bool allowedOnNoBallOrFreeHit =
                    proposed == DismissalType.RunOut ||
                    proposed == DismissalType.HitBallTwice ||
                    proposed == DismissalType.ObstructingTheField;

                if (!allowedOnNoBallOrFreeHit)
                {
                    return null;
                }
            }

            // Wide dismissal restrictions (MCC Law 22.9)
            if (isWide)
            {
                bool allowedOnWide =
                    proposed == DismissalType.RunOut ||
                    proposed == DismissalType.Stumped ||
                    proposed == DismissalType.HitWicket ||
                    proposed == DismissalType.ObstructingTheField;

                if (!allowedOnWide)
                {
                    return null;
                }
            }

            string outBatterId = strikerId;
            if (proposed == DismissalType.RunOut && !string.IsNullOrEmpty(fielding.DismissedBatterId))
            {
                if (!string.Equals(fielding.DismissedBatterId, strikerId, StringComparison.Ordinal) &&
                    !string.Equals(fielding.DismissedBatterId, nonStrikerId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Dismissed batter '{fielding.DismissedBatterId}' is not currently at the crease.");
                }

                outBatterId = fielding.DismissedBatterId;
            }

            bool bowlerCredited =
                proposed == DismissalType.Bowled ||
                proposed == DismissalType.Caught ||
                proposed == DismissalType.LegBeforeWicket ||
                proposed == DismissalType.Stumped ||
                proposed == DismissalType.HitWicket;

            return new DismissalRecord(
                proposed,
                outBatterId,
                bowlerId,
                fielding.PrimaryFielderId,
                bowlerCredited);
        }

        private void ApplyStrikeAndBatterRotation(
            MatchState state,
            InningsState innings,
            FieldingAndRunningEvidence fielding,
            DismissalRecord? dismissal,
            bool struckBoundary)
        {
            CompetitionRulesProfile rules = state.Config.Rules;

            if (dismissal == null)
            {
                if (!struckBoundary)
                {
                    int physicalCrossings = fielding.CompletedRuns + (fielding.OverthrowReachedBoundary && fielding.CrossedAtInstantOfThrow ? 1 : 0);
                    if (physicalCrossings % 2 == 1)
                    {
                        SwapEnds(innings);
                    }
                }

                return;
            }

            string? incomingBatterId = FetchNextBatterId(state, innings);
            if (incomingBatterId != null)
            {
                EnsureBatterState(innings, incomingBatterId);
            }

            if (dismissal.Type == DismissalType.Caught)
            {
                if (rules.CrossOnCatchNewBatterTakesStrike)
                {
                    // MCC 2022 Law 18.11: Incoming batter takes strike regardless of crossing
                    innings.StrikerId = incomingBatterId;
                }
                else
                {
                    if (fielding.BattersCrossedBeforeDismissal)
                    {
                        innings.StrikerId = innings.NonStrikerId;
                        innings.NonStrikerId = incomingBatterId;
                    }
                    else
                    {
                        innings.StrikerId = incomingBatterId;
                    }
                }

                return;
            }

            if (dismissal.Type == DismissalType.RunOut)
            {
                string survivingBatterId = string.Equals(dismissal.OutBatterId, innings.StrikerId, StringComparison.Ordinal)
                    ? (innings.NonStrikerId ?? string.Empty)
                    : (innings.StrikerId ?? string.Empty);

                int totalCrossings = fielding.CompletedRuns + (fielding.CrossedOnIncompleteRun || fielding.BattersCrossedBeforeDismissal ? 1 : 0);
                bool strikerWasOut = string.Equals(dismissal.OutBatterId, innings.StrikerId, StringComparison.Ordinal);

                // Determine where surviving batter ended up based on total crossings
                bool survivingAtStrikerEnd = strikerWasOut
                    ? (totalCrossings % 2 == 1)
                    : (totalCrossings % 2 == 0);

                if (survivingAtStrikerEnd)
                {
                    innings.StrikerId = survivingBatterId;
                    innings.NonStrikerId = incomingBatterId;
                }
                else
                {
                    innings.StrikerId = incomingBatterId;
                    innings.NonStrikerId = survivingBatterId;
                }

                return;
            }

            // Bowled, LBW, Stumped, HitWicket, etc.: incoming batter takes striker's end
            innings.StrikerId = incomingBatterId;
        }

        private bool TryCompleteInningsIfConditionMet(MatchState state, InningsState innings)
        {
            if (innings.IsCompleted)
            {
                return true;
            }

            if (innings.TargetRuns.HasValue && innings.TotalRuns >= innings.TargetRuns.Value)
            {
                CompleteInnings(state, innings, InningsEndReason.TargetReached);
                return true;
            }

            if (innings.WicketsLost >= WicketsRequiredForAllOut)
            {
                CompleteInnings(state, innings, InningsEndReason.AllOut);
                return true;
            }

            int? maxOvers = state.Config.Rules.MaxOversPerInnings;
            if (maxOvers.HasValue && innings.CompletedOvers >= maxOvers.Value)
            {
                CompleteInnings(state, innings, InningsEndReason.OversCompleted);
                return true;
            }

            return false;
        }

        private void CompleteInnings(MatchState state, InningsState innings, InningsEndReason reason)
        {
            innings.IsCompleted = true;
            innings.EndReason = reason;
            innings.CurrentBowlerId = null;

            bool followOnEligible = IsFollowOnEligible();

            _eventBus.Publish(new InningsCompletedEvent(
                NextEventSeq(),
                innings.InningsNumber,
                innings.BattingTeamId,
                innings.TotalRuns,
                innings.WicketsLost,
                reason,
                followOnEligible));

            EvaluateMatchCompletion(state);
        }

        private void EvaluateMatchCompletion(MatchState state)
        {
            CompetitionRulesProfile rules = state.Config.Rules;

            // Limited overs (1 innings per team)
            if (rules.MaxInningsPerTeam == 1 && state.Innings.Count == 2 && state.Innings[1].IsCompleted)
            {
                InningsState first = state.Innings[0];
                InningsState second = state.Innings[1];

                if (second.TotalRuns > first.TotalRuns)
                {
                    int wicketsRemaining = WicketsRequiredForAllOut - second.WicketsLost;
                    CommitMatchResult(state, new MatchResult(
                        MatchResultType.WinByWickets,
                        second.BattingTeamId,
                        wicketsRemaining,
                        $"{second.BattingTeamId} won by {wicketsRemaining} wicket(s)"));
                }
                else if (first.TotalRuns > second.TotalRuns)
                {
                    int runMargin = first.TotalRuns - second.TotalRuns;
                    CommitMatchResult(state, new MatchResult(
                        MatchResultType.WinByRuns,
                        first.BattingTeamId,
                        runMargin,
                        $"{first.BattingTeamId} won by {runMargin} run(s)"));
                }
                else
                {
                    MatchResultType tieType = rules.AllowSuperOverOnTie
                        ? MatchResultType.TiePendingSuperOver
                        : MatchResultType.Tie;

                    CommitMatchResult(state, new MatchResult(
                        tieType,
                        null,
                        0,
                        rules.AllowSuperOverOnTie ? "Match tied - Super Over required" : "Match tied"));
                }

                return;
            }

            // Multi-innings (Test cricket: 2 innings per team)
            if (rules.MaxInningsPerTeam == 2)
            {
                if (state.Innings.Count == 3 && state.Innings[2].IsCompleted)
                {
                    // Check innings victory (e.g., team following-on fails to surpass opponent's single innings)
                    string thirdBattingTeam = state.Innings[2].BattingTeamId;
                    string thirdBowlingTeam = state.Innings[2].BowlingTeamId;
                    int thirdTeamInningsCount = CountTeamInnings(state, thirdBattingTeam);
                    if (thirdTeamInningsCount == 2)
                    {
                        int thirdTeamTotal = GetTeamCumulativeRuns(state, thirdBattingTeam);
                        int opponentTotal = GetTeamCumulativeRuns(state, thirdBowlingTeam);
                        if (thirdTeamTotal < opponentTotal)
                        {
                            int margin = opponentTotal - thirdTeamTotal;
                            CommitMatchResult(state, new MatchResult(
                                MatchResultType.WinByInningsAndRuns,
                                thirdBowlingTeam,
                                margin,
                                $"{thirdBowlingTeam} won by an innings and {margin} run(s)"));
                        }
                    }
                }
                else if (state.Innings.Count == 4 && state.Innings[3].IsCompleted)
                {
                    InningsState fourth = state.Innings[3];
                    int chasingTotal = GetTeamCumulativeRuns(state, fourth.BattingTeamId);
                    int defendingTotal = GetTeamCumulativeRuns(state, fourth.BowlingTeamId);

                    if (chasingTotal > defendingTotal)
                    {
                        int wicketsRemaining = WicketsRequiredForAllOut - fourth.WicketsLost;
                        CommitMatchResult(state, new MatchResult(
                            MatchResultType.WinByWickets,
                            fourth.BattingTeamId,
                            wicketsRemaining,
                            $"{fourth.BattingTeamId} won by {wicketsRemaining} wicket(s)"));
                    }
                    else if (defendingTotal > chasingTotal)
                    {
                        int runMargin = defendingTotal - chasingTotal;
                        CommitMatchResult(state, new MatchResult(
                            MatchResultType.WinByRuns,
                            fourth.BowlingTeamId,
                            runMargin,
                            $"{fourth.BowlingTeamId} won by {runMargin} run(s)"));
                    }
                    else
                    {
                        CommitMatchResult(state, new MatchResult(MatchResultType.Tie, null, 0, "Match tied"));
                    }
                }
            }
        }

        private void CommitMatchResult(MatchState state, MatchResult result)
        {
            state.Result = result;
            _eventBus.Publish(new MatchCompletedEvent(NextEventSeq(), result));
        }

        private InningsState RequireActiveInnings()
        {
            InningsState? current = State.CurrentInnings;
            if (current == null || current.IsCompleted)
            {
                throw new InvalidOperationException("There is no active innings in progress.");
            }

            return current;
        }

        private static string? FetchNextBatterId(MatchState state, InningsState innings)
        {
            PlayingLineup lineup = state.Config.GetLineupForTeam(innings.BattingTeamId);
            if (innings.NextBatterOrderIndex >= lineup.BattingOrderPlayerIds.Count)
            {
                return null;
            }

            string nextId = lineup.BattingOrderPlayerIds[innings.NextBatterOrderIndex];
            innings.NextBatterOrderIndex++;
            return nextId;
        }

        private static void SwapEnds(InningsState innings)
        {
            string? temp = innings.StrikerId;
            innings.StrikerId = innings.NonStrikerId;
            innings.NonStrikerId = temp;
        }

        private static BatterInningsState EnsureBatterState(InningsState innings, string playerId)
        {
            if (!innings.BatterStates.TryGetValue(playerId, out BatterInningsState? batter))
            {
                batter = new BatterInningsState(playerId);
                innings.BatterStates[playerId] = batter;
            }

            return batter;
        }

        private static BowlerSpellState EnsureBowlerState(InningsState innings, string playerId)
        {
            if (!innings.BowlerStates.TryGetValue(playerId, out BowlerSpellState? bowler))
            {
                bowler = new BowlerSpellState(playerId);
                innings.BowlerStates[playerId] = bowler;
            }

            return bowler;
        }

        private static int GetTeamCumulativeRuns(MatchState state, string teamId)
        {
            int sum = 0;
            for (int i = 0; i < state.Innings.Count; i++)
            {
                if (string.Equals(state.Innings[i].BattingTeamId, teamId, StringComparison.Ordinal))
                {
                    sum += state.Innings[i].TotalRuns;
                }
            }

            return sum;
        }

        private static int CountTeamInnings(MatchState state, string teamId)
        {
            int count = 0;
            for (int i = 0; i < state.Innings.Count; i++)
            {
                if (string.Equals(state.Innings[i].BattingTeamId, teamId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private void PublishRuleViolation(string code, string message)
        {
            _eventBus.Publish(new RuleViolationEvent(NextEventSeq(), code, message));
        }

        private long NextEventSeq() => ++_eventSequence;
    }
}
