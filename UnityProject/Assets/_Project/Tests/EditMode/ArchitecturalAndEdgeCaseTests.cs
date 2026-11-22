using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cricket.Multiplayer.Shared.Validation;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Math;
using Cricket.Simulation.Core.Rules;
using Cricket.Simulation.Tests.TestFixtures;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class ArchitecturalAndEdgeCaseTests
    {
        [Test]
        public void CoreAndMultiplayerSharedAssemblies_HaveZeroUnityEngineReferences()
        {
            Assembly coreAssembly = typeof(CricketRulesEngine).Assembly;
            Assembly sharedAssembly = typeof(MatchCommandValidator).Assembly;

            AssertNoUnityReferences(coreAssembly);
            AssertNoUnityReferences(sharedAssembly);
        }

        [Test]
        public void CompetitionProfileConfigurability_SupportsEightBallOversFootFaultOnlyFreeHitAndLegacyCatchCross()
        {
            var customProfile = new CompetitionRulesProfile(
                profileId: "custom_8ball_legacy",
                displayName: "8-Ball Custom Competition",
                format: MatchFormat.CustomLimitedOvers,
                ballsPerOver: 8,
                maxInningsPerTeam: 1,
                maxOversPerInnings: 5,
                maxOversPerBowler: 2,
                freeHitOnAllNoBalls: false,
                freeHitOnFootFaultOnly: true,
                crossOnCatchNewBatterTakesStrike: false);

            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(customProfile);
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string opener2 = innings.NonStrikerId!;
            string batter3 = config.HomeLineup.BattingOrderPlayerIds[2];
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);

            // Waist-high No-Ball does NOT trigger Free Hit under FreeHitOnFootFaultOnly=true
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(130f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -8.5f), detectedNoBallReason: NoBallReason.AboveWaistFullToss),
                new ShotTelemetry(ShotIntent.Defensive, FootworkType.BackFoot, ContactZone.Miss),
                new FieldingAndRunningEvidence()));
            Assert.IsFalse(innings.IsFreeHitPending);

            // Front-foot No-Ball DOES trigger Free Hit
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(135f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f), detectedNoBallReason: NoBallReason.FrontFootOverstep),
                new ShotTelemetry(ShotIntent.Defensive, FootworkType.FrontFoot, ContactZone.SweetSpot),
                new FieldingAndRunningEvidence()));
            Assert.IsTrue(innings.IsFreeHitPending);

            // Consume Free Hit (legal ball 1)
            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            Assert.IsFalse(innings.IsFreeHitPending);

            // Caught with batters crossed under legacy CrossOnCatchNewBatterTakesStrike=false -> non-striker takes strike!
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(135f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0.1f, -5f)),
                new ShotTelemetry(ShotIntent.LoftedStroke, FootworkType.FrontFoot, ContactZone.OuterEdge),
                new FieldingAndRunningEvidence(
                    proposedDismissal: DismissalType.Caught,
                    primaryFielderId: config.AwayLineup.BattingOrderPlayerIds[3],
                    battersCrossedBeforeDismissal: true)));

            Assert.AreEqual(opener2, innings.StrikerId);
            Assert.AreEqual(batter3, innings.NonStrikerId);
            Assert.AreEqual(2, innings.LegalBallsInCurrentOver);

            // 6 more legal balls needed to complete an 8-ball over (balls 3..7 don't complete it, ball 8 does)
            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            Assert.AreEqual(0, innings.CompletedOvers);
            Assert.AreEqual(7, innings.LegalBallsInCurrentOver);

            engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            Assert.AreEqual(1, innings.CompletedOvers);
            Assert.AreEqual(0, innings.LegalBallsInCurrentOver);
        }

        [Test]
        public void CaughtOnFinalBallOfOver_SwapsEndsSoSurvivingNonStrikerFacesNextOver()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string opener2 = innings.NonStrikerId!;
            string batter3 = config.HomeLineup.BattingOrderPlayerIds[2];
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);

            for (int i = 0; i < 5; i++)
            {
                engine.EvaluateAndCommitDelivery(MatchTestDataFactory.DotBall());
            }

            // Ball 6: opener1 caught -> batter3 takes striker's end, then over ends and swaps ends -> opener2 faces Over 2
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(138f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f)),
                new ShotTelemetry(ShotIntent.LoftedStroke, FootworkType.FrontFoot, ContactZone.OuterEdge),
                new FieldingAndRunningEvidence(proposedDismissal: DismissalType.Caught, primaryFielderId: config.AwayLineup.BattingOrderPlayerIds[4])));

            Assert.AreEqual(1, innings.CompletedOvers);
            Assert.AreEqual(opener2, innings.StrikerId);
            Assert.AreEqual(batter3, innings.NonStrikerId);
        }

        [Test]
        public void NoBallWithByes_ChargesOnlyNoBallPenaltyToBowlerAndCountsBallFacedForStriker()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string striker = innings.StrikerId!;
            string bowler = config.AwayLineup.BattingOrderPlayerIds[10];
            engine.StartOver(bowler);

            BallEventRecord rec = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(141f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f), detectedNoBallReason: NoBallReason.FrontFootOverstep),
                new ShotTelemetry(ShotIntent.GroundStroke, FootworkType.FrontFoot, ContactZone.Miss),
                new FieldingAndRunningEvidence(completedRuns: 2)));

            Assert.AreEqual(0, rec.RunsOffBat);
            Assert.AreEqual(3, rec.ExtraRuns); // 1 No-Ball + 2 Byes
            Assert.AreEqual(3, innings.TotalRuns);
            Assert.AreEqual(1, innings.NoBalls);
            Assert.AreEqual(2, innings.Byes);
            Assert.AreEqual(1, innings.BowlerStates[bowler].RunsConceded);
            Assert.AreEqual(1, innings.BatterStates[striker].BallsFaced);
            Assert.AreEqual(0, innings.BatterStates[striker].Runs);
        }

        [Test]
        public void PlayingLineupValidation_RejectsInvalidPlayerCountDuplicatesOrMissingCaptain()
        {
            var shortList = new List<string> { "p1", "p2", "p3" };
            Assert.Throws<ArgumentException>(() => new PlayingLineup("t1", shortList, "p1", "p2"));

            var duplicateList = new List<string> { "p1", "p1", "p3", "p4", "p5", "p6", "p7", "p8", "p9", "p10", "p11" };
            Assert.Throws<ArgumentException>(() => new PlayingLineup("t1", duplicateList, "p1", "p6"));

            var validList = new List<string> { "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9", "p10", "p11" };
            Assert.Throws<ArgumentException>(() => new PlayingLineup("t1", validList, "p99", "p6"));
        }

        private static void AssertNoUnityReferences(Assembly assembly)
        {
            AssemblyName[] referenced = assembly.GetReferencedAssemblies();
            bool hasUnity = referenced.Any(r =>
                r.Name != null &&
                (r.Name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                 r.Name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase)));

            Assert.IsFalse(hasUnity, $"Assembly '{assembly.GetName().Name}' must not reference UnityEngine or UnityEditor.");
        }
    }
}
