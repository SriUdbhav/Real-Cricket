using System.Collections.Generic;
using Cricket.Multiplayer.Shared.Protocol;
using Cricket.Multiplayer.Shared.Sync;
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
    public sealed class FieldValidationAndMultiplayerSharedTests
    {
        [Test]
        public void ValidateFieldPlacement_EnforcesPowerplayPhasesAndBehindSquareLegLimit()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            engine.InitializeMatch(MatchTestDataFactory.CreateConfig(CompetitionPresets.CreateStandardOdi()));

            // Over 5 (ODI Powerplay 1: max 2 outside circle, max 2 behind square leg)
            List<FielderPlacement> validP1 = BuildField(outsideCount: 2, behindSquareLegCount: 2);
            Assert.IsTrue(engine.ValidateFieldPlacement(validP1, oneBasedOverNumber: 5).IsValid);

            List<FielderPlacement> tooManyOutsideP1 = BuildField(outsideCount: 3, behindSquareLegCount: 1);
            FieldValidationResult p1Result = engine.ValidateFieldPlacement(tooManyOutsideP1, oneBasedOverNumber: 5);
            Assert.IsFalse(p1Result.IsValid);

            // Same 3 outside circle IS valid in Over 15 (ODI Powerplay 2: max 4 outside)
            Assert.IsTrue(engine.ValidateFieldPlacement(tooManyOutsideP1, oneBasedOverNumber: 15).IsValid);

            // 3 behind square on leg side ALWAYS invalid under standard profile
            List<FielderPlacement> tooManyLegSide = BuildField(outsideCount: 2, behindSquareLegCount: 3);
            Assert.IsFalse(engine.ValidateFieldPlacement(tooManyLegSide, oneBasedOverNumber: 20).IsValid);
        }

        [Test]
        public void MatchCommandValidator_RejectsInvalidTurnStaleSequenceAndOutOfBoundsSpeed()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig();
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string bowler = config.AwayLineup.BattingOrderPlayerIds[10];
            string wrongPlayer = config.AwayLineup.BattingOrderPlayerIds[0];
            engine.StartOver(bowler);

            var validator = new MatchCommandValidator();

            var validDelivery = new DeliveryCommitCommand(1, config.MatchId, bowler, 141f, new SimVector2(0.1f, -5.5f), 0f, 5f, 0f);
            Assert.IsTrue(validator.ValidateDeliveryCommit(validDelivery, engine.State, 0).IsValid);

            var staleDelivery = new DeliveryCommitCommand(1, config.MatchId, bowler, 141f, new SimVector2(0.1f, -5.5f), 0f, 5f, 0f);
            Assert.AreEqual("STALE_SEQUENCE", validator.ValidateDeliveryCommit(staleDelivery, engine.State, 1).ErrorCode);

            var wrongTurn = new DeliveryCommitCommand(2, config.MatchId, wrongPlayer, 141f, new SimVector2(0.1f, -5.5f), 0f, 5f, 0f);
            Assert.AreEqual("INVALID_BOWLER_TURN", validator.ValidateDeliveryCommit(wrongTurn, engine.State, 1).ErrorCode);

            var impossibleSpeed = new DeliveryCommitCommand(2, config.MatchId, bowler, 240f, new SimVector2(0.1f, -5.5f), 0f, 5f, 0f);
            Assert.AreEqual("SPEED_OUT_OF_BOUNDS", validator.ValidateDeliveryCommit(impossibleSpeed, engine.State, 1).ErrorCode);

            var impossibleReactionShot = new ShotInputCommand(3, config.MatchId, innings.StrikerId!, ShotIntent.LoftedStroke, FootworkType.FrontFoot, 45f, 10f);
            Assert.AreEqual("REACTION_WINDOW_INVALID", validator.ValidateShotInput(impossibleReactionShot, engine.State, 2).ErrorCode);

            var validShot = new ShotInputCommand(3, config.MatchId, innings.StrikerId!, ShotIntent.GroundStroke, FootworkType.FrontFoot, 35f, 220f);
            Assert.IsTrue(validator.ValidateShotInput(validShot, engine.State, 2).IsValid);
        }

        [Test]
        public void LocalLoopbackTransportAndChecksum_SupportOfflineFirstWithMultiplayerParity()
        {
            var engine = new CricketRulesEngine(new MatchEventBus());
            MatchConfig config = MatchTestDataFactory.CreateConfig();
            engine.InitializeMatch(config);
            engine.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            InningsState innings = engine.StartNextInnings();
            string bowler = config.AwayLineup.BattingOrderPlayerIds[10];
            engine.StartOver(bowler);

            var transport = new LocalLoopbackTransport();
            var validator = new MatchCommandValidator();

            IMatchCommand? receivedCmd = null;
            AuthoritativeBallSyncPacket? receivedSync = null;
            transport.CommandReceived += cmd => receivedCmd = cmd;
            transport.BallSyncReceived += pkt => receivedSync = pkt;

            var deliveryCmd = new DeliveryCommitCommand(1, config.MatchId, bowler, 137f, new SimVector2(0f, -5f), 0f, 0f, 0f);
            transport.SendCommand(deliveryCmd);
            Assert.AreSame(deliveryCmd, receivedCmd);

            BallEventRecord rec = engine.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundaryFour: true));
            ulong checksum1 = validator.ComputeBallLogChecksum(innings.BallLog);
            ulong checksum2 = validator.ComputeBallLogChecksum(innings.BallLog);
            Assert.AreEqual(checksum1, checksum2);
            Assert.AreNotEqual(0UL, checksum1);

            transport.BroadcastAuthoritativeBallSync(new AuthoritativeBallSyncPacket(config.MatchId, rec, checksum1));
            Assert.IsNotNull(receivedSync);
            Assert.AreEqual(checksum1, receivedSync!.CumulativeLogChecksum);
            Assert.AreEqual(MatchSessionMode.OfflineSinglePlayer, transport.SessionMode);
        }

        private static List<FielderPlacement> BuildField(int outsideCount, int behindSquareLegCount)
        {
            var fielders = new List<FielderPlacement>(9);
            for (int i = 0; i < 9; i++)
            {
                bool outside = i < outsideCount;
                bool behindLeg = i < behindSquareLegCount;
                fielders.Add(new FielderPlacement(
                    $"f_{i}",
                    new SimVector2(outside ? 45f : 18f, behindLeg ? -12f : 15f),
                    outside,
                    behindLeg));
            }

            return fielders;
        }
    }
}
