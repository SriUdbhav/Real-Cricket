using System.Linq;
using Cricket.Multiplayer.Shared.Validation;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Math;
using Cricket.Simulation.Core.Rules;
using Cricket.Simulation.Core.Scorecard;
using Cricket.Simulation.Tests.TestFixtures;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class ScorecardProjectionTests
    {
        [Test]
        public void ProjectMatch_RebuildsCompleteScorecardStrictlyFromBallEventHistoryWithoutMutatingMatchState()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);

            InningsState innings = engine.StartNextInnings();
            string p1 = config.HomeLineup.BattingOrderPlayerIds[0];
            string p2 = config.HomeLineup.BattingOrderPlayerIds[1];
            string p3 = config.HomeLineup.BattingOrderPlayerIds[2];
            string b1 = config.AwayLineup.BattingOrderPlayerIds[10];
            string b2 = config.AwayLineup.BattingOrderPlayerIds[9];

            // --- Over 1 by b1 ---
            engine.StartOver(b1);
            // 1.1: p1 hits 4
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            // 1.2: Wide (+1)
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(138f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(1.4f, -4f), isWideByCreaseLine: true),
                new ShotTelemetry(ShotIntent.Leave, FootworkType.Neutral, ContactZone.Miss, shotOffered: false),
                new FieldingAndRunningEvidence()));
            // 1.2 (re-bowled): p1 hits 1 -> strike to p2
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(1));
            // 1.3: Non-striker (p1) run out on 0 runs off ball; p3 comes in (p2 remains striker)
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(140f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f)),
                new ShotTelemetry(ShotIntent.Defensive, FootworkType.FrontFoot, ContactZone.SweetSpot),
                new FieldingAndRunningEvidence(
                    completedRuns: 0,
                    proposedDismissal: DismissalType.RunOut,
                    dismissedBatterId: p1,
                    primaryFielderId: config.AwayLineup.BattingOrderPlayerIds[2])));
            // 1.4: No-Ball + 2 byes (1 NB charged to bowler, 2 byes NOT charged to bowler)
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(142f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f), detectedNoBallReason: NoBallReason.FrontFootOverstep),
                new ShotTelemetry(ShotIntent.GroundStroke, FootworkType.FrontFoot, ContactZone.Miss),
                new FieldingAndRunningEvidence(completedRuns: 2)));
            // 1.4 (Free Hit): p2 hits 6
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            // 1.5: p2 caught -> p3 is non-striker, p4 comes in as striker (MCC 2022 Law 18.11)
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(139f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0.1f, -5f)),
                new ShotTelemetry(ShotIntent.LoftedStroke, FootworkType.FrontFoot, ContactZone.OuterEdge),
                new FieldingAndRunningEvidence(proposedDismissal: DismissalType.Caught, primaryFielderId: config.AwayLineup.BattingOrderPlayerIds[3])));
            // 1.6: Dot ball -> Over 1 ends
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());

            // --- Over 2 by b2: Maiden over containing 1 leg-bye (0 runs charged to bowler -> still a maiden!) ---
            engine.StartOver(b2);
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(134f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f)),
                new ShotTelemetry(ShotIntent.Defensive, FootworkType.FrontFoot, ContactZone.PadOnly),
                new FieldingAndRunningEvidence(completedRuns: 1)));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Capture checksum before projection to prove zero mutation
            var validator = new MatchCommandValidator();
            ulong checksumBefore = validator.ComputeBallLogChecksum(innings.BallLog);
            int totalBefore = innings.TotalRuns;
            int wicketsBefore = innings.WicketsLost;

            var projector = new ScorecardProjector();
            ScorecardSnapshot snapshot = projector.ProjectMatch(engine.State);

            // Also rebuild strictly from raw BallLog via ProjectInnings without MatchState
            var rawInput = new InningsProjectionInput(
                innings.InningsNumber,
                innings.BattingTeamId,
                innings.BowlingTeamId,
                isSuperOver: false,
                superOverRound: 0,
                targetRuns: innings.TargetRuns,
                endReason: innings.EndReason,
                battingOrderPlayerIds: config.HomeLineup.BattingOrderPlayerIds,
                ballLog: innings.BallLog.ToList());
            InningsScorecardSnapshot rebuilt = projector.ProjectInnings(rawInput, config.Rules);

            // Official match state untouched
            Assert.AreEqual(checksumBefore, validator.ComputeBallLogChecksum(innings.BallLog));
            Assert.AreEqual(totalBefore, innings.TotalRuns);
            Assert.AreEqual(wicketsBefore, innings.WicketsLost);

            // Innings totals match
            InningsScorecardSnapshot card = snapshot.InningsScorecards[0];
            Assert.AreEqual(16, card.TotalRuns); // 4 + 1wd + 1 + 0 + (1nb+2b) + 6 + 0 + 0 + 1lb
            Assert.AreEqual(2, card.WicketsLost);
            Assert.AreEqual("2", card.OversDisplay);
            Assert.AreEqual(card.TotalRuns, rebuilt.TotalRuns);
            Assert.AreEqual(card.WicketsLost, rebuilt.WicketsLost);

            // Extras breakdown
            Assert.AreEqual(1, card.Extras.Wides);
            Assert.AreEqual(1, card.Extras.NoBalls);
            Assert.AreEqual(2, card.Extras.Byes);
            Assert.AreEqual(1, card.Extras.LegByes);
            Assert.AreEqual(0, card.Extras.PenaltyRuns);
            Assert.AreEqual(5, card.Extras.Total);

            // Batting entries & DidNotBat
            BatterScorecardEntry p1Card = card.BattingEntries.Single(x => x.PlayerId == p1);
            Assert.AreEqual(5, p1Card.Runs);
            Assert.AreEqual(2, p1Card.BallsFaced); // Wide does not count as ball faced
            Assert.AreEqual(1, p1Card.Fours);
            Assert.IsTrue(p1Card.IsOut);
            Assert.AreEqual(DismissalType.RunOut, p1Card.Dismissal!.Type);

            BatterScorecardEntry p2Card = card.BattingEntries.Single(x => x.PlayerId == p2);
            Assert.AreEqual(6, p2Card.Runs);
            Assert.AreEqual(4, p2Card.BallsFaced); // Run-out ball + No-Ball + Free Hit + Caught ball
            Assert.AreEqual(1, p2Card.Sixes);
            Assert.IsTrue(p2Card.IsOut);

            Assert.AreEqual(7, card.DidNotBatPlayerIds.Count); // 4 players appeared (p1..p4), 7 DNB

            // Bowling figures
            BowlerScorecardEntry b1Card = card.BowlingEntries.Single(x => x.PlayerId == b1);
            Assert.AreEqual("1", b1Card.OversDisplay);
            Assert.AreEqual(13, b1Card.RunsConceded); // 4 + 1wd + 1 + 1nb + 6 (2 byes NOT charged to bowler!)
            Assert.AreEqual(1, b1Card.Wickets);       // Caught credited; RunOut NOT credited
            Assert.AreEqual(1, b1Card.Wides);
            Assert.AreEqual(1, b1Card.NoBalls);
            Assert.AreEqual(0, b1Card.Maidens);

            BowlerScorecardEntry b2Card = card.BowlingEntries.Single(x => x.PlayerId == b2);
            Assert.AreEqual("1", b2Card.OversDisplay);
            Assert.AreEqual(0, b2Card.RunsConceded);  // Leg-bye not charged to bowler
            Assert.AreEqual(1, b2Card.Maidens);

            // Fall of Wickets
            Assert.AreEqual(2, card.FallOfWickets.Count);
            Assert.AreEqual(1, card.FallOfWickets[0].WicketNumber);
            Assert.AreEqual(6, card.FallOfWickets[0].ScoreAtFall);
            Assert.AreEqual("0.3", card.FallOfWickets[0].OverDisplay);
            Assert.AreEqual(p1, card.FallOfWickets[0].OutBatterId);

            Assert.AreEqual(2, card.FallOfWickets[1].WicketNumber);
            Assert.AreEqual(15, card.FallOfWickets[1].ScoreAtFall);
            Assert.AreEqual("0.5", card.FallOfWickets[1].OverDisplay);
            Assert.AreEqual(p2, card.FallOfWickets[1].OutBatterId);

            // Partnerships (2 broken + 1 unbroken)
            Assert.AreEqual(3, card.Partnerships.Count);
            Assert.AreEqual(6, card.Partnerships[0].TotalRuns);
            Assert.IsFalse(card.Partnerships[0].IsUnbroken);
            Assert.AreEqual(9, card.Partnerships[1].TotalRuns); // 1nb + 2b + 6
            Assert.IsFalse(card.Partnerships[1].IsUnbroken);
            Assert.AreEqual(1, card.Partnerships[2].TotalRuns); // 1 leg-bye
            Assert.IsTrue(card.Partnerships[2].IsUnbroken);

            // Over-by-over summaries
            Assert.AreEqual(2, card.OverSummaries.Count);
            Assert.AreEqual(15, card.OverSummaries[0].RunsInOver);
            Assert.AreEqual(2, card.OverSummaries[0].WicketsInOver);
            Assert.IsFalse(card.OverSummaries[0].IsMaiden);
            Assert.AreEqual(1, card.OverSummaries[1].RunsInOver);
            Assert.IsTrue(card.OverSummaries[1].IsMaiden);
            Assert.AreEqual(16, card.OverSummaries[1].CumulativeScore);
            Assert.AreEqual(2, card.OverSummaries[1].CumulativeWickets);
        }

        [Test]
        public void ProjectInnings_HandlesEmptyBallLogCleanly()
        {
            MatchConfig config = MatchTestDataFactory.CreateConfig();
            var input = new InningsProjectionInput(
                1,
                config.HomeTeam.TeamId,
                config.AwayTeam.TeamId,
                isSuperOver: false,
                superOverRound: 0,
                targetRuns: null,
                endReason: InningsEndReason.None,
                battingOrderPlayerIds: config.HomeLineup.BattingOrderPlayerIds,
                ballLog: System.Array.Empty<BallEventRecord>());

            var projector = new ScorecardProjector();
            InningsScorecardSnapshot card = projector.ProjectInnings(input, config.Rules);

            Assert.AreEqual(0, card.TotalRuns);
            Assert.AreEqual(0, card.WicketsLost);
            Assert.AreEqual("0", card.OversDisplay);
            Assert.AreEqual(0, card.BattingEntries.Count);
            Assert.AreEqual(11, card.DidNotBatPlayerIds.Count);
            Assert.AreEqual(0, card.BowlingEntries.Count);
            Assert.AreEqual(0, card.FallOfWickets.Count);
            Assert.AreEqual(0, card.Partnerships.Count);
            Assert.AreEqual(0, card.OverSummaries.Count);
        }

        [Test]
        public void ProjectMatch_SeparatesSuperOverScorecardsFromMainMatchInnings()
        {
            var rules = new CompetitionRulesProfile(
                profileId: "one_over_card",
                displayName: "1-Over Card Test",
                format: MatchFormat.CustomLimitedOvers,
                ballsPerOver: 6,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 1,
                maxOversPerBowler: 1,
                allowSuperOverOnTie: true);

            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(rules);
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);

            // Tied main match (4 each)
            engine.StartNextInnings();
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            engine.StartNextInnings();
            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Super Over Half 1 (Away scores 6) & Half 2 (Home chases 7 via 4 + 4)
            engine.StartSuperOverInnings(new[]
            {
                config.AwayLineup.BattingOrderPlayerIds[0],
                config.AwayLineup.BattingOrderPlayerIds[1],
                config.AwayLineup.BattingOrderPlayerIds[2]
            });
            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            engine.StartSuperOverInnings(new[]
            {
                config.HomeLineup.BattingOrderPlayerIds[0],
                config.HomeLineup.BattingOrderPlayerIds[1],
                config.HomeLineup.BattingOrderPlayerIds[2]
            });
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));

            var projector = new ScorecardProjector();
            ScorecardSnapshot snapshot = projector.ProjectMatch(engine.State);

            Assert.AreEqual(2, snapshot.InningsScorecards.Count);
            Assert.AreEqual(4, snapshot.InningsScorecards[0].TotalRuns);
            Assert.AreEqual(4, snapshot.InningsScorecards[1].TotalRuns);

            Assert.AreEqual(2, snapshot.SuperOverScorecards.Count);
            Assert.IsTrue(snapshot.SuperOverScorecards[0].IsSuperOver);
            Assert.AreEqual(1, snapshot.SuperOverScorecards[0].SuperOverRound);
            Assert.AreEqual(6, snapshot.SuperOverScorecards[0].TotalRuns);
            Assert.AreEqual(8, snapshot.SuperOverScorecards[1].TotalRuns);
            Assert.AreEqual("0.2", snapshot.SuperOverScorecards[1].OversDisplay);
            Assert.AreEqual(MatchResultType.WinBySuperOver, snapshot.Result.ResultType);
        }
    }
}
