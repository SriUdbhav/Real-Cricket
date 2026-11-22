using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Scorecard
{
    /// <summary>
    /// Pure event-sourced scorecard projector. Derives batting figures, bowling figures,
    /// extras, fall of wickets, partnerships, and over-by-over summaries exclusively from
    /// <see cref="BallEventRecord"/> sequences without reading or mutating mutable match counters.
    /// </summary>
    public sealed class ScorecardProjector : IScorecardProjector
    {
        private sealed class BatterAccumulator
        {
            public string PlayerId { get; }
            public int Runs;
            public int BallsFaced;
            public int Fours;
            public int Sixes;
            public int DotBalls;
            public bool IsOut;
            public DismissalRecord? Dismissal;

            public BatterAccumulator(string playerId)
            {
                PlayerId = playerId;
            }
        }

        private sealed class BowlerAccumulator
        {
            public string PlayerId { get; }
            public int LegalBalls;
            public int Maidens;
            public int RunsConceded;
            public int Wickets;
            public int Wides;
            public int NoBalls;
            public int DotBalls;

            public BowlerAccumulator(string playerId)
            {
                PlayerId = playerId;
            }
        }

        private sealed class OverAccumulator
        {
            public int OverNumber { get; }
            public List<string> BowlerIds { get; } = new List<string>(2);
            public List<string> BallNotations { get; } = new List<string>(8);
            public int RunsInOver;
            public int BowlerRunsInOver;
            public int WicketsInOver;
            public int LegalBalls;
            public int ExtrasInOver;
            public int CumulativeScore;
            public int CumulativeWickets;

            public OverAccumulator(int overNumber)
            {
                OverNumber = overNumber;
            }
        }

        private sealed class PartnershipAccumulator
        {
            public int WicketNumber { get; }
            public string BatterAId { get; }
            public string BatterBId { get; }
            public int TotalRuns;
            public int LegalBalls;
            public int BatterARuns;
            public int BatterBRuns;
            public int ExtrasRuns;

            public PartnershipAccumulator(int wicketNumber, string batterAId, string batterBId)
            {
                WicketNumber = wicketNumber;
                BatterAId = batterAId;
                BatterBId = batterBId;
            }
        }

        public ScorecardSnapshot ProjectMatch(MatchState matchState)
        {
            if (matchState == null)
            {
                throw new ArgumentNullException(nameof(matchState));
            }

            CompetitionRulesProfile rules = matchState.Config.Rules;
            var mainInnings = new List<InningsScorecardSnapshot>(matchState.Innings.Count);
            for (int i = 0; i < matchState.Innings.Count; i++)
            {
                InningsState inn = matchState.Innings[i];
                IReadOnlyList<string> order = matchState.Config.GetLineupForTeam(inn.BattingTeamId).BattingOrderPlayerIds;
                var input = new InningsProjectionInput(
                    inn.InningsNumber,
                    inn.BattingTeamId,
                    inn.BowlingTeamId,
                    isSuperOver: false,
                    superOverRound: 0,
                    targetRuns: inn.TargetRuns,
                    endReason: inn.EndReason,
                    battingOrderPlayerIds: order,
                    ballLog: inn.BallLog);

                mainInnings.Add(ProjectInnings(input, rules));
            }

            var superOvers = new List<InningsScorecardSnapshot>(matchState.SuperOverInnings.Count);
            for (int i = 0; i < matchState.SuperOverInnings.Count; i++)
            {
                InningsState so = matchState.SuperOverInnings[i];
                IReadOnlyList<string> order = so.NominatedBattingOrder
                    ?? matchState.Config.GetLineupForTeam(so.BattingTeamId).BattingOrderPlayerIds;

                var input = new InningsProjectionInput(
                    so.InningsNumber,
                    so.BattingTeamId,
                    so.BowlingTeamId,
                    isSuperOver: true,
                    superOverRound: so.SuperOverRound,
                    targetRuns: so.TargetRuns,
                    endReason: so.EndReason,
                    battingOrderPlayerIds: order,
                    ballLog: so.BallLog);

                superOvers.Add(ProjectInnings(input, rules));
            }

            return new ScorecardSnapshot(
                matchState.Config.MatchId,
                matchState.Result,
                mainInnings,
                superOvers);
        }

        public InningsScorecardSnapshot ProjectInnings(InningsProjectionInput input, CompetitionRulesProfile rules)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            int ballsPerOver = rules.BallsPerOver;
            int totalRuns = 0;
            int wicketsLost = 0;
            int totalLegalBalls = 0;

            int wides = 0;
            int noBalls = 0;
            int byes = 0;
            int legByes = 0;
            int penaltyRuns = 0;

            var batterOrder = new List<string>(11);
            var batters = new Dictionary<string, BatterAccumulator>(StringComparer.Ordinal);
            var bowlerOrder = new List<string>(8);
            var bowlers = new Dictionary<string, BowlerAccumulator>(StringComparer.Ordinal);
            var fallOfWickets = new List<FallOfWicketEntry>(10);
            var partnerships = new List<PartnershipEntry>(10);
            var overOrder = new List<int>(20);
            var overs = new Dictionary<int, OverAccumulator>();

            PartnershipAccumulator? activePartnership = null;

            for (int i = 0; i < input.BallLog.Count; i++)
            {
                BallEventRecord ball = input.BallLog[i];

                // Ensure striker, non-striker, and bowler are registered in order of appearance
                BatterAccumulator striker = GetOrAddBatter(batterOrder, batters, ball.StrikerId);
                GetOrAddBatter(batterOrder, batters, ball.NonStrikerId);
                BowlerAccumulator bowler = GetOrAddBowler(bowlerOrder, bowlers, ball.BowlerId);
                OverAccumulator over = GetOrAddOver(overOrder, overs, ball.OverNumber);

                if (!over.BowlerIds.Contains(ball.BowlerId))
                {
                    over.BowlerIds.Add(ball.BowlerId);
                }

                // Start or maintain partnership tracking
                if (activePartnership == null)
                {
                    activePartnership = new PartnershipAccumulator(wicketsLost + 1, ball.StrikerId, ball.NonStrikerId);
                }

                // Extras breakdown & bowler run attribution derived solely from BallEventRecord + CompetitionRulesProfile
                int bowlerRunsOnBall = ComputeBowlerRunsAndExtras(
                    ball,
                    rules,
                    ref wides,
                    ref noBalls,
                    ref byes,
                    ref legByes,
                    ref penaltyRuns);

                // Batter stats
                bool isWide = ball.PrimaryExtraType == ExtraType.Wide;
                if (!isWide)
                {
                    striker.BallsFaced++;
                    if (ball.RunsOffBat == 0)
                    {
                        striker.DotBalls++;
                    }
                }

                striker.Runs += ball.RunsOffBat;
                if (ball.IsBoundaryFour)
                {
                    striker.Fours++;
                }

                if (ball.IsBoundarySix)
                {
                    striker.Sixes++;
                }

                // Bowler stats
                bowler.RunsConceded += bowlerRunsOnBall;
                if (ball.PrimaryExtraType == ExtraType.Wide)
                {
                    bowler.Wides++;
                }
                else if (ball.PrimaryExtraType == ExtraType.NoBall)
                {
                    bowler.NoBalls++;
                }

                if (ball.IsLegalDelivery)
                {
                    totalLegalBalls++;
                    bowler.LegalBalls++;
                    over.LegalBalls++;
                    activePartnership.LegalBalls++;
                    if (bowlerRunsOnBall == 0)
                    {
                        bowler.DotBalls++;
                    }
                }

                // Totals & over stats
                int ballTotal = ball.TotalRunsScored;
                totalRuns += ballTotal;
                over.RunsInOver += ballTotal;
                over.BowlerRunsInOver += bowlerRunsOnBall;
                over.ExtrasInOver += ball.ExtraRuns + ball.PenaltyRuns;

                // Partnership contributions
                activePartnership.TotalRuns += ballTotal;
                if (ball.RunsOffBat > 0)
                {
                    if (string.Equals(ball.StrikerId, activePartnership.BatterAId, StringComparison.Ordinal))
                    {
                        activePartnership.BatterARuns += ball.RunsOffBat;
                    }
                    else
                    {
                        activePartnership.BatterBRuns += ball.RunsOffBat;
                    }
                }

                activePartnership.ExtrasRuns += ball.ExtraRuns + ball.PenaltyRuns;

                // Dismissal & Fall of Wickets
                if (ball.Dismissal != null)
                {
                    wicketsLost++;
                    over.WicketsInOver++;
                    BatterAccumulator outBatter = GetOrAddBatter(batterOrder, batters, ball.Dismissal.OutBatterId);
                    outBatter.IsOut = true;
                    outBatter.Dismissal = ball.Dismissal;

                    if (ball.Dismissal.BowlerCredited)
                    {
                        bowler.Wickets++;
                    }

                    string fowOverDisplay = FormatBallOverDisplay(ball.OverNumber, ball.LegalBallInOver, ballsPerOver);
                    fallOfWickets.Add(new FallOfWicketEntry(
                        wicketNumber: wicketsLost,
                        scoreAtFall: totalRuns,
                        overNumber: ball.OverNumber,
                        legalBallInOver: ball.LegalBallInOver,
                        overDisplay: fowOverDisplay,
                        outBatterId: ball.Dismissal.OutBatterId,
                        dismissalType: ball.Dismissal.Type,
                        bowlerId: ball.Dismissal.BowlerId,
                        fielderId: ball.Dismissal.FielderId,
                        ballSequenceId: ball.SequenceId));

                    // Close the current partnership on wicket fall
                    partnerships.Add(new PartnershipEntry(
                        activePartnership.WicketNumber,
                        activePartnership.BatterAId,
                        activePartnership.BatterBId,
                        activePartnership.TotalRuns,
                        activePartnership.LegalBalls,
                        activePartnership.BatterARuns,
                        activePartnership.BatterBRuns,
                        activePartnership.ExtrasRuns,
                        isUnbroken: false));

                    activePartnership = null;
                }

                over.CumulativeScore = totalRuns;
                over.CumulativeWickets = wicketsLost;
                over.BallNotations.Add(FormatBallNotation(ball));
            }

            // Close any unbroken final partnership
            if (activePartnership != null)
            {
                partnerships.Add(new PartnershipEntry(
                    activePartnership.WicketNumber,
                    activePartnership.BatterAId,
                    activePartnership.BatterBId,
                    activePartnership.TotalRuns,
                    activePartnership.LegalBalls,
                    activePartnership.BatterARuns,
                    activePartnership.BatterBRuns,
                    activePartnership.ExtrasRuns,
                    isUnbroken: true));
            }

            // Build over summaries and credit maidens to bowlers who completed a full zero-run over alone
            var overSummaries = new List<OverSummaryEntry>(overOrder.Count);
            for (int i = 0; i < overOrder.Count; i++)
            {
                OverAccumulator ov = overs[overOrder[i]];
                bool isMaiden = ov.LegalBalls >= ballsPerOver && ov.BowlerRunsInOver == 0;
                if (isMaiden && ov.BowlerIds.Count == 1 && bowlers.TryGetValue(ov.BowlerIds[0], out BowlerAccumulator? singleBowler))
                {
                    singleBowler.Maidens++;
                }

                overSummaries.Add(new OverSummaryEntry(
                    ov.OverNumber,
                    ov.BowlerIds,
                    ov.RunsInOver,
                    ov.WicketsInOver,
                    ov.LegalBalls,
                    ov.ExtrasInOver,
                    isMaiden,
                    ov.CumulativeScore,
                    ov.CumulativeWickets,
                    ov.BallNotations));
            }

            // Finalize batting entries and DidNotBat list
            var battingEntries = new List<BatterScorecardEntry>(batterOrder.Count);
            var battedSet = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < batterOrder.Count; i++)
            {
                BatterAccumulator b = batters[batterOrder[i]];
                battedSet.Add(b.PlayerId);
                battingEntries.Add(new BatterScorecardEntry(
                    b.PlayerId,
                    b.Runs,
                    b.BallsFaced,
                    b.Fours,
                    b.Sixes,
                    b.DotBalls,
                    b.IsOut,
                    b.Dismissal));
            }

            var didNotBat = new List<string>();
            for (int i = 0; i < input.BattingOrderPlayerIds.Count; i++)
            {
                string playerId = input.BattingOrderPlayerIds[i];
                if (!battedSet.Contains(playerId))
                {
                    didNotBat.Add(playerId);
                }
            }

            // Finalize bowling entries
            var bowlingEntries = new List<BowlerScorecardEntry>(bowlerOrder.Count);
            for (int i = 0; i < bowlerOrder.Count; i++)
            {
                BowlerAccumulator bw = bowlers[bowlerOrder[i]];
                int completedOversForBowler = bw.LegalBalls / ballsPerOver;
                int partialBallsForBowler = bw.LegalBalls % ballsPerOver;
                bowlingEntries.Add(new BowlerScorecardEntry(
                    bw.PlayerId,
                    completedOversForBowler,
                    partialBallsForBowler,
                    bw.Maidens,
                    bw.RunsConceded,
                    bw.Wickets,
                    bw.Wides,
                    bw.NoBalls,
                    bw.DotBalls,
                    ballsPerOver));
            }

            int completedOvers = totalLegalBalls / ballsPerOver;
            int legalBallsInCurrentOver = totalLegalBalls % ballsPerOver;

            return new InningsScorecardSnapshot(
                input.InningsNumber,
                input.BattingTeamId,
                input.BowlingTeamId,
                input.IsSuperOver,
                input.SuperOverRound,
                input.TargetRuns,
                input.EndReason,
                totalRuns,
                wicketsLost,
                completedOvers,
                legalBallsInCurrentOver,
                ballsPerOver,
                new ExtrasBreakdown(wides, noBalls, byes, legByes, penaltyRuns),
                battingEntries,
                didNotBat,
                bowlingEntries,
                fallOfWickets,
                partnerships,
                overSummaries);
        }

        private static int ComputeBowlerRunsAndExtras(
            BallEventRecord ball,
            CompetitionRulesProfile rules,
            ref int wides,
            ref int noBalls,
            ref int byes,
            ref int legByes,
            ref int penaltyRuns)
        {
            penaltyRuns += ball.PenaltyRuns;

            switch (ball.PrimaryExtraType)
            {
                case ExtraType.Wide:
                    wides += ball.ExtraRuns;
                    return ball.ExtraRuns;

                case ExtraType.NoBall:
                    int nbPenalty = System.Math.Min(ball.ExtraRuns, rules.NoBallRunPenalty);
                    int additionalOnNoBall = System.Math.Max(0, ball.ExtraRuns - nbPenalty);
                    noBalls += nbPenalty;
                    if (additionalOnNoBall > 0)
                    {
                        if (ball.ShotTelemetry.PrimaryContact == ContactZone.PadOnly ||
                            ball.ShotTelemetry.PrimaryContact == ContactZone.Body)
                        {
                            legByes += additionalOnNoBall;
                        }
                        else
                        {
                            byes += additionalOnNoBall;
                        }
                    }

                    return nbPenalty + ball.RunsOffBat;

                case ExtraType.Bye:
                    byes += ball.ExtraRuns;
                    return ball.RunsOffBat;

                case ExtraType.LegBye:
                    legByes += ball.ExtraRuns;
                    return ball.RunsOffBat;

                default:
                    return ball.RunsOffBat;
            }
        }

        private static string FormatBallOverDisplay(int oneBasedOverNumber, int legalBallInOver, int ballsPerOver)
        {
            if (legalBallInOver >= ballsPerOver)
            {
                return $"{oneBasedOverNumber}.0";
            }

            return $"{System.Math.Max(0, oneBasedOverNumber - 1)}.{legalBallInOver}";
        }

        private static string FormatBallNotation(BallEventRecord ball)
        {
            string wicketSuffix = ball.Dismissal != null ? "+W" : string.Empty;

            if (ball.PrimaryExtraType == ExtraType.Wide)
            {
                return ball.ExtraRuns > 1 ? $"{ball.ExtraRuns}wd{wicketSuffix}" : $"wd{wicketSuffix}";
            }

            if (ball.PrimaryExtraType == ExtraType.NoBall)
            {
                int scored = ball.RunsOffBat + System.Math.Max(0, ball.ExtraRuns - 1);
                return scored > 0 ? $"{scored}+nb{wicketSuffix}" : $"nb{wicketSuffix}";
            }

            if (ball.PrimaryExtraType == ExtraType.Bye)
            {
                return $"{ball.ExtraRuns}b{wicketSuffix}";
            }

            if (ball.PrimaryExtraType == ExtraType.LegBye)
            {
                return $"{ball.ExtraRuns}lb{wicketSuffix}";
            }

            if (ball.Dismissal != null && ball.TotalRunsScored == 0)
            {
                return "W";
            }

            return $"{ball.RunsOffBat}{wicketSuffix}";
        }

        private static BatterAccumulator GetOrAddBatter(
            List<string> order,
            Dictionary<string, BatterAccumulator> map,
            string playerId)
        {
            if (!map.TryGetValue(playerId, out BatterAccumulator? acc))
            {
                acc = new BatterAccumulator(playerId);
                map[playerId] = acc;
                order.Add(playerId);
            }

            return acc;
        }

        private static BowlerAccumulator GetOrAddBowler(
            List<string> order,
            Dictionary<string, BowlerAccumulator> map,
            string playerId)
        {
            if (!map.TryGetValue(playerId, out BowlerAccumulator? acc))
            {
                acc = new BowlerAccumulator(playerId);
                map[playerId] = acc;
                order.Add(playerId);
            }

            return acc;
        }

        private static OverAccumulator GetOrAddOver(
            List<int> order,
            Dictionary<int, OverAccumulator> map,
            int overNumber)
        {
            if (!map.TryGetValue(overNumber, out OverAccumulator? acc))
            {
                acc = new OverAccumulator(overNumber);
                map[overNumber] = acc;
                order.Add(overNumber);
            }

            return acc;
        }
    }
}
