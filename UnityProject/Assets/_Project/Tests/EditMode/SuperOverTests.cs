using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Math;
using Cricket.Simulation.Core.Rules;
using Cricket.Simulation.Tests.TestFixtures;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class SuperOverTests
    {
        [Test]
        public void SuperOver_FullFlow_TwoWicketAllOutAndMidOverChaseDeterminesWinnerWithoutMutatingMainInnings()
        {
            var bus = new MatchEventBus();
            var startedEvents = new List<SuperOverStartedEvent>();
            var completedEvents = new List<SuperOverCompletedEvent>();
            using IDisposable s1 = bus.Subscribe<SuperOverStartedEvent>(e => startedEvents.Add(e));
            using IDisposable s2 = bus.Subscribe<SuperOverCompletedEvent>(e => completedEvents.Add(e));

            var engine = CreateTiedOneOverMatch(bus, out MatchConfig config);
            Assert.AreEqual(MatchResultType.TiePendingSuperOver, engine.State.Result.ResultType);

            // Regular StartNextInnings must reject while TiePendingSuperOver
            Assert.Throws<InvalidOperationException>(() => engine.StartNextInnings());

            // Super Over Half 1: Team that batted 2nd in main match (Away) bats first
            IReadOnlyList<string> awayBatters = new[]
            {
                config.AwayLineup.BattingOrderPlayerIds[0],
                config.AwayLineup.BattingOrderPlayerIds[1],
                config.AwayLineup.BattingOrderPlayerIds[2]
            };
            InningsState so1 = engine.StartSuperOverInnings(awayBatters);

            Assert.IsTrue(so1.IsSuperOver);
            Assert.AreEqual(1, so1.SuperOverRound);
            Assert.AreEqual(2, so1.MaxWickets);
            Assert.AreEqual(config.AwayTeam.TeamId, so1.BattingTeamId);
            Assert.AreEqual(awayBatters[0], so1.StrikerId);
            Assert.AreEqual(awayBatters[1], so1.NonStrikerId);

            // Declarations disallowed during Super Over
            Assert.Throws<InvalidOperationException>(() => engine.DeclareCurrentInnings());

            string homeBowler = config.HomeLineup.BattingOrderPlayerIds[10];
            engine.StartOver(homeBowler);

            // Ball 1: 6 runs; Ball 2: 1 run (strike rotates to awayBatters[1]);
            // Ball 3: Wicket 1 (awayBatters[1] bowled -> awayBatters[2] comes in);
            // Ball 4: Wicket 2 (awayBatters[2] bowled -> All Out at 2 wickets!)
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(1));
            Assert.AreEqual(awayBatters[1], so1.StrikerId);

            engine.EvaluateAndCommitDelivery(BowledDelivery());
            Assert.AreEqual(1, so1.WicketsLost);
            Assert.AreEqual(awayBatters[2], so1.StrikerId);
            Assert.IsFalse(so1.IsCompleted);

            engine.EvaluateAndCommitDelivery(BowledDelivery());
            Assert.AreEqual(2, so1.WicketsLost);
            Assert.IsTrue(so1.IsCompleted);
            Assert.AreEqual(InningsEndReason.AllOut, so1.EndReason);
            Assert.AreEqual(7, so1.TotalRuns);
            Assert.AreEqual(MatchResultType.TiePendingSuperOver, engine.State.Result.ResultType);

            // Super Over Half 2: Home chases 8
            IReadOnlyList<string> homeBatters = new[]
            {
                config.HomeLineup.BattingOrderPlayerIds[0],
                config.HomeLineup.BattingOrderPlayerIds[1],
                config.HomeLineup.BattingOrderPlayerIds[2]
            };
            InningsState so2 = engine.StartSuperOverInnings(homeBatters);
            Assert.AreEqual(config.HomeTeam.TeamId, so2.BattingTeamId);
            Assert.AreEqual(8, so2.TargetRuns);

            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);
            // Wide (+1), 4, 4 -> 9 runs in 2 legal balls -> immediate Super Over win!
            engine.EvaluateAndCommitDelivery(WideDelivery());
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            Assert.IsFalse(so2.IsCompleted);

            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            Assert.IsTrue(so2.IsCompleted);
            Assert.AreEqual(InningsEndReason.TargetReached, so2.EndReason);
            Assert.AreEqual(9, so2.TotalRuns);

            Assert.AreEqual(MatchResultType.WinBySuperOver, engine.State.Result.ResultType);
            Assert.AreEqual(config.HomeTeam.TeamId, engine.State.Result.WinnerTeamId);
            Assert.AreEqual(2, engine.State.Result.Margin); // 2 wickets remaining in Super Over

            // Main match innings remain untouched at 6 runs each
            Assert.AreEqual(2, engine.State.Innings.Count);
            Assert.AreEqual(6, engine.State.Innings[0].TotalRuns);
            Assert.AreEqual(6, engine.State.Innings[1].TotalRuns);
            Assert.AreEqual(2, engine.State.SuperOverInnings.Count);
            Assert.AreEqual(2, startedEvents.Count);
            Assert.AreEqual(1, completedEvents.Count);
            Assert.AreEqual(config.HomeTeam.TeamId, completedEvents[0].WinnerTeamId);
        }

        [Test]
        public void RepeatedSuperOvers_ReversesBattingOrderEnforcesPlayerExclusionsAndResolvesWinnerInRoundTwo()
        {
            var bus = new MatchEventBus();
            var engine = CreateTiedOneOverMatch(bus, out MatchConfig config, allowRepeatedSuperOvers: true);

            // --- Super Over Round 1 (Away bats first, Home bats second, both score 6 -> Tied!) ---
            string awayB1 = config.AwayLineup.BattingOrderPlayerIds[0];
            string awayB2 = config.AwayLineup.BattingOrderPlayerIds[1];
            string awayB3 = config.AwayLineup.BattingOrderPlayerIds[2];
            string homeBowl1 = config.HomeLineup.BattingOrderPlayerIds[10];

            engine.StartSuperOverInnings(new[] { awayB1, awayB2, awayB3 });
            engine.StartOver(homeBowl1);
            // AwayB1 hits 6 then gets bowled, remaining 4 balls dots -> 6/1
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            engine.EvaluateAndCommitDelivery(BowledDelivery());
            for (int i = 0; i < 4; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            string homeB1 = config.HomeLineup.BattingOrderPlayerIds[0];
            string homeB2 = config.HomeLineup.BattingOrderPlayerIds[1];
            string homeB3 = config.HomeLineup.BattingOrderPlayerIds[2];
            string awayBowl1 = config.AwayLineup.BattingOrderPlayerIds[10];

            engine.StartSuperOverInnings(new[] { homeB1, homeB2, homeB3 });
            engine.StartOver(awayBowl1);
            // Home scores 6/0 across 6 balls -> Super Over 1 TIED!
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            Assert.AreEqual(MatchResultType.TiePendingSuperOver, engine.State.Result.ResultType);

            // --- Super Over Round 2: Team batting 2nd in SO1 (Home) bats first! ---
            // Home batters were NOT dismissed in SO1 so they remain eligible.
            InningsState so2Half1 = engine.StartSuperOverInnings(new[] { homeB1, homeB2, homeB3 });
            Assert.AreEqual(2, so2Half1.SuperOverRound);
            Assert.AreEqual(config.HomeTeam.TeamId, so2Half1.BattingTeamId);

            // Away's SO1 bowler MUST be rejected in SO2!
            Assert.Throws<InvalidOperationException>(() => engine.StartOver(awayBowl1));

            // Fresh bowler allowed
            string awayBowl2 = config.AwayLineup.BattingOrderPlayerIds[9];
            engine.StartOver(awayBowl2);
            // Home scores 12/0
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 4; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Away's dismissed SO1 batter (awayB1) MUST be rejected in SO2!
            Assert.Throws<InvalidOperationException>(() =>
                engine.StartSuperOverInnings(new[] { awayB1, awayB2, awayB3 }));

            // Nominating eligible batters (awayB2 was not out, awayB3 not out, awayB4 fresh) succeeds
            string awayB4 = config.AwayLineup.BattingOrderPlayerIds[3];
            InningsState so2Half2 = engine.StartSuperOverInnings(new[] { awayB2, awayB3, awayB4 });
            Assert.AreEqual(13, so2Half2.TargetRuns);

            // Home's SO1 bowler MUST be rejected in SO2
            Assert.Throws<InvalidOperationException>(() => engine.StartOver(homeBowl1));

            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[9]);
            // Away scores 4 runs across the over -> Home wins Super Over 2 by 8 runs!
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            Assert.AreEqual(MatchResultType.WinBySuperOver, engine.State.Result.ResultType);
            Assert.AreEqual(config.HomeTeam.TeamId, engine.State.Result.WinnerTeamId);
            Assert.AreEqual(8, engine.State.Result.Margin);
            Assert.AreEqual(4, engine.State.SuperOverInnings.Count);
        }

        [Test]
        public void SingleSuperOverProfile_EndsAsTieWhenSuperOverTiesAndBlocksSecondRound()
        {
            var bus = new MatchEventBus();
            var engine = CreateTiedOneOverMatch(bus, out MatchConfig config, allowRepeatedSuperOvers: false);

            engine.StartSuperOverInnings(new[]
            {
                config.AwayLineup.BattingOrderPlayerIds[0],
                config.AwayLineup.BattingOrderPlayerIds[1],
                config.AwayLineup.BattingOrderPlayerIds[2]
            });
            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[10]);
            for (int i = 0; i < 6; i++)
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
            for (int i = 0; i < 6; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            Assert.AreEqual(MatchResultType.Tie, engine.State.Result.ResultType);
            Assert.IsNull(engine.State.Result.WinnerTeamId);

            // Attempting to start a 2nd Super Over must fail
            Assert.Throws<InvalidOperationException>(() => engine.StartSuperOverInnings(new[]
            {
                config.HomeLineup.BattingOrderPlayerIds[3],
                config.HomeLineup.BattingOrderPlayerIds[4],
                config.HomeLineup.BattingOrderPlayerIds[5]
            }));
        }

        [Test]
        public void SuperOverValidation_RejectsWrongBatterCountDuplicatesAndNonLineupPlayers()
        {
            var bus = new MatchEventBus();
            var engine = CreateTiedOneOverMatch(bus, out MatchConfig config);

            string p1 = config.AwayLineup.BattingOrderPlayerIds[0];
            string p2 = config.AwayLineup.BattingOrderPlayerIds[1];
            string opponentPlayer = config.HomeLineup.BattingOrderPlayerIds[0];

            // Only 2 batters instead of 3
            Assert.Throws<ArgumentException>(() => engine.StartSuperOverInnings(new[] { p1, p2 }));
            // Duplicate batters
            Assert.Throws<ArgumentException>(() => engine.StartSuperOverInnings(new[] { p1, p1, p2 }));
            // Opponent player not in Away XI
            Assert.Throws<InvalidOperationException>(() => engine.StartSuperOverInnings(new[] { p1, p2, opponentPlayer }));
        }

        private static CricketRulesEngine CreateTiedOneOverMatch(
            IMatchEventBus bus,
            out MatchConfig config,
            bool allowRepeatedSuperOvers = true)
        {
            var rules = new CompetitionRulesProfile(
                profileId: "one_over_so",
                displayName: "1-Over Super Over Test",
                format: MatchFormat.CustomLimitedOvers,
                ballsPerOver: 6,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 1,
                maxOversPerBowler: 1,
                allowSuperOverOnTie: true,
                allowRepeatedSuperOvers: allowRepeatedSuperOvers);

            var engine = new CricketRulesEngine(bus);
            config = MatchTestDataFactory.CreateConfig(rules);
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);

            // Innings 1: Home scores 6
            engine.StartNextInnings();
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Innings 2: Away scores 6 -> TiePendingSuperOver
            engine.StartNextInnings();
            engine.StartOver(config.HomeLineup.BattingOrderPlayerIds[10]);
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            return engine;
        }

        private static DeliveryResolutionInput BowledDelivery()
        {
            return new DeliveryResolutionInput(
                new DeliveryTelemetry(141f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -6f)),
                new ShotTelemetry(ShotIntent.LoftedStroke, FootworkType.FrontFoot, ContactZone.Miss),
                new FieldingAndRunningEvidence(proposedDismissal: DismissalType.Bowled));
        }

        private static DeliveryResolutionInput WideDelivery()
        {
            return new DeliveryResolutionInput(
                new DeliveryTelemetry(138f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(1.4f, -4f), isWideByCreaseLine: true),
                new ShotTelemetry(ShotIntent.Leave, FootworkType.Neutral, ContactZone.Miss, shotOffered: false),
                new FieldingAndRunningEvidence());
        }
    }
}
