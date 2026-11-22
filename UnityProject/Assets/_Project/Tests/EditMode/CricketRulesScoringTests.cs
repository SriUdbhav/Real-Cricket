using System;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Rules;
using Cricket.Simulation.Tests.TestFixtures;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class CricketRulesScoringTests
    {
        [Test]
        public void StandardScoring_TracksRunsStrikeRotationBoundariesAndOverCompletion()
        {
            var bus = new MatchEventBus();
            var engine = new CricketRulesEngine(bus);
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);

            InningsState innings = engine.StartNextInnings();
            string opener1 = config.HomeLineup.BattingOrderPlayerIds[0];
            string opener2 = config.HomeLineup.BattingOrderPlayerIds[1];
            string bowler1 = config.AwayLineup.BattingOrderPlayerIds[10];

            engine.StartOver(bowler1);

            // Ball 1: 1 run -> strike rotates to opener2
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(1));
            Assert.AreEqual(opener2, innings.StrikerId);
            Assert.AreEqual(opener1, innings.NonStrikerId);

            // Ball 2: 2 runs -> opener2 retains strike
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(2));
            Assert.AreEqual(opener2, innings.StrikerId);

            // Ball 3: Four -> opener2 retains strike
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            Assert.AreEqual(opener2, innings.StrikerId);
            Assert.AreEqual(1, innings.BatterStates[opener2].Fours);

            // Ball 4: Six -> opener2 retains strike
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            Assert.AreEqual(1, innings.BatterStates[opener2].Sixes);

            // Ball 5: 3 runs -> strike rotates to opener1
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(3));
            Assert.AreEqual(opener1, innings.StrikerId);

            // Ball 6: Dot ball -> over ends, ends swap so opener2 is striker for Over 2
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());

            Assert.AreEqual(16, innings.TotalRuns);
            Assert.AreEqual(1, innings.CompletedOvers);
            Assert.AreEqual(0, innings.LegalBallsInCurrentOver);
            Assert.AreEqual(opener2, innings.StrikerId);
            Assert.AreEqual(opener1, innings.NonStrikerId);
            Assert.AreEqual(1, innings.BatterStates[opener1].Runs);
            Assert.AreEqual(2, innings.BatterStates[opener1].BallsFaced);
            Assert.AreEqual(15, innings.BatterStates[opener2].Runs);
            Assert.AreEqual(4, innings.BatterStates[opener2].BallsFaced);
            Assert.AreEqual(16, innings.BowlerStates[bowler1].RunsConceded);
            Assert.AreEqual(0, innings.BowlerStates[bowler1].Maidens);
        }

        [Test]
        public void ShortRun_DeductsRunButPreservesPhysicalEndSwap()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig();
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string opener1 = innings.StrikerId!;
            string opener2 = innings.NonStrikerId!;
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);

            // Batters run 2, 1 called short -> only 1 run awarded, batters still finished at original ends (even physical crossings)
            BallEventRecord rec = engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(runs: 2, shortRuns: 1));
            Assert.AreEqual(1, rec.RunsOffBat);
            Assert.AreEqual(opener1, innings.StrikerId);
            Assert.AreEqual(opener2, innings.NonStrikerId);
        }

        [Test]
        public void MaidenOver_CreditsBowlerMaidenWhenZeroRunsChargedToBowler()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig();
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string bowler = config.AwayLineup.BattingOrderPlayerIds[10];
            engine.StartOver(bowler);

            for (int i = 0; i < 6; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            Assert.AreEqual(1, innings.BowlerStates[bowler].Maidens);
            Assert.AreEqual(0, innings.BowlerStates[bowler].RunsConceded);
        }

        [Test]
        public void BowlerRestrictions_RejectConsecutiveOversAndExceedingProfileQuota()
        {
            var customRules = new CompetitionRulesProfile(
                profileId: "short_4_over",
                displayName: "4 Over Blitz",
                format: MatchFormat.CustomLimitedOvers,
                ballsPerOver: 6,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 4,
                maxOversPerBowler: 1);

            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(customRules);
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            engine.StartNextInnings();

            string bowler1 = config.AwayLineup.BattingOrderPlayerIds[10];
            string bowler2 = config.AwayLineup.BattingOrderPlayerIds[9];

            engine.StartOver(bowler1);
            for (int i = 0; i < 6; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Consecutive over blocked
            Assert.Throws<InvalidOperationException>(() => engine.StartOver(bowler1));

            // Bowler 2 bowls Over 2
            engine.StartOver(bowler2);
            for (int i = 0; i < 6; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Bowler 1 now blocked by MaxOversPerBowler = 1
            Assert.Throws<InvalidOperationException>(() => engine.StartOver(bowler1));
        }

        [Test]
        public void CalculateNetRunRate_UsesFullQuotaWhenAllOutPerCompetitionProfile()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            engine.InitializeMatch(MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20()));

            // Team scored 120 all out in 15 overs (counted as 20 overs = 6.00 RPO)
            // Opponent scored 121/2 in 16 overs (7.5625 RPO) -> NRR = -1.5625
            double nrr = engine.CalculateNetRunRate(
                totalRunsScored: 120,
                legalBallsFaced: 90,
                wasAllOutInAnyInningsAdjusted: true,
                fullQuotaBallsForAdjustedInnings: 120,
                totalRunsConceded: 121,
                legalBallsBowled: 96,
                opponentAllOutAdjusted: false,
                fullQuotaBallsForOpponentAdjusted: 120);

            Assert.AreEqual(-1.5625, nrr, 0.0001);
        }
    }
}
