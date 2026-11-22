using System;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Math;
using Cricket.Simulation.Core.Rules;
using Cricket.Simulation.Tests.TestFixtures;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class CricketRulesDismissalsAndInningsTests
    {
        [Test]
        public void CaughtDismissal_Mcc2022Law18_11_IncomingBatterTakesStrikeEvenIfBattersCrossed()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string batter3 = config.HomeLineup.BattingOrderPlayerIds[2];
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);

            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(135f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0.1f, -5f)),
                new ShotTelemetry(ShotIntent.LoftedStroke, FootworkType.FrontFoot, ContactZone.OuterEdge),
                new FieldingAndRunningEvidence(
                    proposedDismissal: DismissalType.Caught,
                    primaryFielderId: config.AwayLineup.BattingOrderPlayerIds[3],
                    battersCrossedBeforeDismissal: true)));

            Assert.AreEqual(batter3, innings.StrikerId);
            Assert.AreEqual(1, innings.WicketsLost);
        }

        [Test]
        public void CompleteLimitedOversChase_EndsImmediatelyMidOverWithWinByWickets()
        {
            var twoOverRules = new CompetitionRulesProfile(
                profileId: "two_over_match",
                displayName: "2-Over Quick",
                format: MatchFormat.CustomLimitedOvers,
                ballsPerOver: 6,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 2,
                maxOversPerBowler: 1);

            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(twoOverRules);
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);

            // Innings 1: Home scores 10 runs across 2 overs
            InningsState inn1 = engine.StartNextInnings();
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[9]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            Assert.IsTrue(inn1.IsCompleted);
            Assert.AreEqual(InningsEndReason.OversCompleted, inn1.EndReason);
            Assert.AreEqual(10, inn1.TotalRuns);

            // Innings 2: Away chases 11 in 2 balls (6 + 6)
            InningsState inn2 = engine.StartNextInnings();
            Assert.AreEqual(11, inn2.TargetRuns);
            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            Assert.IsFalse(inn2.IsCompleted);

            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            Assert.IsTrue(inn2.IsCompleted);
            Assert.AreEqual(InningsEndReason.TargetReached, inn2.EndReason);
            Assert.AreEqual(MatchResultType.WinByWickets, engine.State.Result.ResultType);
            Assert.AreEqual(config.AwayTeam.TeamId, engine.State.Result.WinnerTeamId);
            Assert.AreEqual(10, engine.State.Result.Margin);
        }

        [Test]
        public void TiedLimitedOversMatch_SignalsSuperOverPendingWhenConfiguredInProfile()
        {
            var oneOverRules = new CompetitionRulesProfile(
                profileId: "one_over_tie",
                displayName: "1-Over Tie Test",
                format: MatchFormat.CustomLimitedOvers,
                ballsPerOver: 6,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 1,
                maxOversPerBowler: 1,
                allowSuperOverOnTie: true);

            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(oneOverRules);
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);

            // Innings 1: 6 runs
            engine.StartNextInnings();
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Innings 2: 6 runs -> Tie pending Super Over
            engine.StartNextInnings();
            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            Assert.AreEqual(MatchResultType.TiePendingSuperOver, engine.State.Result.ResultType);
            Assert.IsNull(engine.State.Result.WinnerTeamId);
        }

        [Test]
        public void TestProfile_SupportsDeclarationAndFollowOnAndInningsVictory()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardTest());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);

            // Innings 1: Home scores 240 (40 sixes) and declares
            InningsState inn1 = engine.StartNextInnings();
            int bowlerToggle = 0;
            for (int over = 0; over < 7; over++)
            {
                string bowler = config.AwayLineup.BattingOrderPlayerIds[10 - (bowlerToggle++ % 2)];
                engine.StartOver(bowler);
                for (int b = 0; b < 6; b++)
                {
                    engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
                    if (inn1.TotalRuns >= 240)
                    {
                        break;
                    }
                }

                if (inn1.TotalRuns >= 240)
                {
                    break;
                }
            }

            engine.DeclareCurrentInnings();
            Assert.IsTrue(inn1.IsCompleted);
            Assert.AreEqual(InningsEndReason.Declared, inn1.EndReason);

            // Innings 2: Away all out for 0 (lead = 240 >= 200 -> Follow-on eligible!)
            InningsState inn2 = engine.StartNextInnings();
            BowlTenWickets(engine, config.HomeLineup);
            Assert.IsTrue(inn2.IsCompleted);
            Assert.AreEqual(InningsEndReason.AllOut, inn2.EndReason);
            Assert.IsTrue(engine.IsFollowOnEligible());

            // Innings 3: Follow-on enforced -> Away bats again, all out for 30 -> Home wins by an innings and 210 runs!
            InningsState inn3 = engine.StartNextInnings(enforceFollowOn: true);
            Assert.AreEqual(config.AwayTeam.TeamId, inn3.BattingTeamId);
            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[10]);
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            }
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            BowlTenWickets(engine, config.HomeLineup, startOverOffset: 1);

            Assert.AreEqual(MatchResultType.WinByInningsAndRuns, engine.State.Result.ResultType);
            Assert.AreEqual(config.HomeTeam.TeamId, engine.State.Result.WinnerTeamId);
            Assert.AreEqual(210, engine.State.Result.Margin);
        }

        [Test]
        public void LimitedOversProfile_RejectsDeclaration()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            engine.StartNextInnings();

            Assert.Throws<InvalidOperationException>(() => engine.DeclareCurrentInnings());
        }

        private static void BowlTenWickets(CricketRulesEngine engine, PlayingLineup bowlingLineup, int startOverOffset = 0)
        {
            int wickets = 0;
            int overIndex = startOverOffset;
            while (wickets < 10)
            {
                string bowler = bowlingLineup.BattingOrderPlayerIds[10 - (overIndex++ % 2)];
                engine.StartOver(bowler);
                for (int b = 0; b < 6 && wickets < 10; b++)
                {
                    engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                        new DeliveryTelemetry(140f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -6f)),
                        new ShotTelemetry(ShotIntent.Defensive, FootworkType.FrontFoot, ContactZone.Miss),
                        new FieldingAndRunningEvidence(proposedDismissal: DismissalType.Bowled)));
                    wickets++;
                }
            }
        }
    }
}
