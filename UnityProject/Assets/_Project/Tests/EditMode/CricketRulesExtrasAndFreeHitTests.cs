using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Math;
using Cricket.Simulation.Core.Rules;
using Cricket.Simulation.Tests.TestFixtures;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class CricketRulesExtrasAndFreeHitTests
    {
        [Test]
        public void NoBallTriggersFreeHit_WideCarriesFreeHitOver_AndBowledIsBlockedOnFreeHit()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string bowler = config.AwayLineup.BattingOrderPlayerIds[10];
            engine.StartOver(bowler);

            // 1. Front-foot No-Ball hit for 4 -> 5 runs total, 0 legal balls, Free Hit active
            BallEventRecord nb = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(142f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0.1f, -5f), detectedNoBallReason: NoBallReason.FrontFootOverstep),
                new ShotTelemetry(ShotIntent.GroundStroke, FootworkType.FrontFoot, ContactZone.SweetSpot, 110f, 5f),
                new FieldingAndRunningEvidence(reachedBoundaryOnBounce: true)));

            Assert.IsFalse(nb.IsLegalDelivery);
            Assert.AreEqual(ExtraType.NoBall, nb.PrimaryExtraType);
            Assert.AreEqual(4, nb.RunsOffBat);
            Assert.AreEqual(1, nb.ExtraRuns);
            Assert.AreEqual(0, innings.LegalBallsInCurrentOver);
            Assert.IsTrue(innings.IsFreeHitPending);

            // 2. Wide bowled while Free Hit pending -> Free Hit MUST carry over!
            BallEventRecord wide = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(140f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(1.4f, -4f), isWideByCreaseLine: true),
                new ShotTelemetry(ShotIntent.Leave, FootworkType.Neutral, ContactZone.Miss, shotOffered: false),
                new FieldingAndRunningEvidence()));

            Assert.IsFalse(wide.IsLegalDelivery);
            Assert.AreEqual(ExtraType.Wide, wide.PrimaryExtraType);
            Assert.IsTrue(wide.WasFreeHit);
            Assert.IsTrue(innings.IsFreeHitPending);
            Assert.AreEqual(0, innings.LegalBallsInCurrentOver);

            // 3. Legal Free Hit delivery hits stumps -> Bowled MUST be rejected, Free Hit cleared
            BallEventRecord fh = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(144f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -6f)),
                new ShotTelemetry(ShotIntent.LoftedStroke, FootworkType.FrontFoot, ContactZone.Miss),
                new FieldingAndRunningEvidence(proposedDismissal: DismissalType.Bowled)));

            Assert.IsTrue(fh.IsLegalDelivery);
            Assert.IsTrue(fh.WasFreeHit);
            Assert.IsNull(fh.Dismissal);
            Assert.AreEqual(0, innings.WicketsLost);
            Assert.IsFalse(innings.IsFreeHitPending);
            Assert.AreEqual(1, innings.LegalBallsInCurrentOver);
        }

        [Test]
        public void RunOutIsAllowedOnFreeHit_AndNotCreditedToBowler()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string bowler = config.AwayLineup.BattingOrderPlayerIds[10];
            string nonStriker = innings.NonStrikerId!;
            engine.StartOver(bowler);

            // Trigger Free Hit via waist-high full toss No-Ball
            engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(136f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -8.5f), detectedNoBallReason: NoBallReason.AboveWaistFullToss),
                new ShotTelemetry(ShotIntent.Defensive, FootworkType.BackFoot, ContactZone.Miss),
                new FieldingAndRunningEvidence()));

            Assert.IsTrue(innings.IsFreeHitPending);

            // Non-striker run out on Free Hit
            BallEventRecord rec = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(136f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f)),
                new ShotTelemetry(ShotIntent.GroundStroke, FootworkType.FrontFoot, ContactZone.SweetSpot),
                new FieldingAndRunningEvidence(
                    completedRuns: 0,
                    proposedDismissal: DismissalType.RunOut,
                    dismissedBatterId: nonStriker,
                    primaryFielderId: config.AwayLineup.BattingOrderPlayerIds[2])));

            Assert.IsNotNull(rec.Dismissal);
            Assert.AreEqual(DismissalType.RunOut, rec.Dismissal!.Type);
            Assert.AreEqual(nonStriker, rec.Dismissal.OutBatterId);
            Assert.IsFalse(rec.Dismissal.BowlerCredited);
            Assert.AreEqual(1, innings.WicketsLost);
            Assert.AreEqual(0, innings.BowlerStates[bowler].Wickets);
        }

        [Test]
        public void StumpingIsValidOnWideBall_AndCreditedToBowler()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string bowler = config.AwayLineup.BattingOrderPlayerIds[7];
            string keeper = config.AwayLineup.WicketKeeperId;
            engine.StartOver(bowler);

            BallEventRecord rec = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(88f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(1.3f, -5f), isWideByCreaseLine: true),
                new ShotTelemetry(ShotIntent.LoftedStroke, FootworkType.AdvanceDownPitch, ContactZone.Miss),
                new FieldingAndRunningEvidence(proposedDismissal: DismissalType.Stumped, primaryFielderId: keeper)));

            Assert.IsFalse(rec.IsLegalDelivery);
            Assert.AreEqual(ExtraType.Wide, rec.PrimaryExtraType);
            Assert.IsNotNull(rec.Dismissal);
            Assert.AreEqual(DismissalType.Stumped, rec.Dismissal!.Type);
            Assert.IsTrue(rec.Dismissal.BowlerCredited);
            Assert.AreEqual(1, innings.BowlerStates[bowler].Wickets);
        }

        [Test]
        public void ExcessiveBouncersInOver_BecomesNoBallAccordingToCompetitionProfile()
        {
            // T20 allows 1 bouncer per over; 2nd bouncer is a No-Ball
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardT20());
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            engine.StartNextInnings();
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);

            var bouncerInput = new DeliveryResolutionInput(
                new DeliveryTelemetry(146f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -1f), isBouncerAboveShoulder: true),
                new ShotTelemetry(ShotIntent.DuckOrEvade, FootworkType.BackFoot, ContactZone.Miss, shotOffered: false),
                new FieldingAndRunningEvidence());

            BallEventRecord first = engine.EvaluateAndCommitDelivery(bouncerInput);
            BallEventRecord second = engine.EvaluateAndCommitDelivery(bouncerInput);

            Assert.IsTrue(first.IsLegalDelivery);
            Assert.IsFalse(second.IsLegalDelivery);
            Assert.AreEqual(NoBallReason.ExcessiveBouncersInOver, second.NoBallReason);
            Assert.IsTrue(engine.State.CurrentInnings!.IsFreeHitPending);
        }

        [Test]
        public void MccLaw19_8_Overthrows_IncludesCompletedPlusInProgressIfCrossedAtThrowInstantPlusFour()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig();
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            engine.StartNextInnings();
            engine.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);

            // Completed 1 run, crossed for 2nd at instant of throw, overthrow reaches boundary -> 1 + 1 + 4 = 6 runs!
            BallEventRecord crossedAtThrow = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(132f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0.1f, -5f)),
                new ShotTelemetry(ShotIntent.GroundStroke, FootworkType.FrontFoot, ContactZone.SweetSpot),
                new FieldingAndRunningEvidence(
                    completedRuns: 1,
                    crossedAtInstantOfThrow: true,
                    overthrowReachedBoundary: true)));

            Assert.AreEqual(6, crossedAtThrow.RunsOffBat);

            // Completed 1 run, NOT crossed at instant of throw, overthrow reaches boundary -> 1 + 0 + 4 = 5 runs!
            BallEventRecord notCrossedAtThrow = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(132f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0.1f, -5f)),
                new ShotTelemetry(ShotIntent.GroundStroke, FootworkType.FrontFoot, ContactZone.SweetSpot),
                new FieldingAndRunningEvidence(
                    completedRuns: 1,
                    crossedAtInstantOfThrow: false,
                    overthrowReachedBoundary: true)));

            Assert.AreEqual(5, notCrossedAtThrow.RunsOffBat);
        }

        [Test]
        public void LegByesAndPenaltyRuns_NotChargedToBowlerOrBatter()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig();
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string striker = innings.StrikerId!;
            string bowler = config.AwayLineup.BattingOrderPlayerIds[10];
            engine.StartOver(bowler);

            BallEventRecord rec = engine.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(138f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -5f)),
                new ShotTelemetry(ShotIntent.Defensive, FootworkType.FrontFoot, ContactZone.PadOnly),
                new FieldingAndRunningEvidence(completedRuns: 2, penaltyAwardedToBattingSide: true)));

            Assert.AreEqual(ExtraType.LegBye, rec.PrimaryExtraType);
            Assert.AreEqual(0, rec.RunsOffBat);
            Assert.AreEqual(2, rec.ExtraRuns);
            Assert.AreEqual(5, rec.PenaltyRuns);
            Assert.AreEqual(7, innings.TotalRuns);
            Assert.AreEqual(0, innings.BatterStates[striker].Runs);
            Assert.AreEqual(0, innings.BowlerStates[bowler].RunsConceded);
        }
    }
}
