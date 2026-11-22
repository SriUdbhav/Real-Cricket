using System.Linq;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Lifecycle;
using Cricket.Simulation.Core.Math;
using Cricket.Simulation.Core.PresentationContracts;
using Cricket.Simulation.Core.Rules;
using Cricket.Simulation.Tests.TestFixtures;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class BroadcastDirectorTests
    {
        [Test]
        public void BroadcastDirector_EmitsCoordinatedCuesFromGameplayEventsWithoutMutatingRules()
        {
            var bus = new MatchEventBus();
            using var director = new BroadcastMatchDirector(bus);
            var lifecycle = new MatchLifecycleEngine(bus);
            var rules = new CricketRulesEngine(bus);

            MatchConfig config = MatchTestDataFactory.CreateConfig();
            rules.InitializeMatch(config);
            lifecycle.TransitionTo(MatchPhase.MatchSetup);
            lifecycle.TransitionTo(MatchPhase.PreMatchCeremony);
            lifecycle.TransitionTo(MatchPhase.Toss);

            rules.RecordToss(config.HomeTeam.TeamId, TossDecision.BatFirst);
            rules.StartNextInnings();
            rules.StartOver(config.AwayLineup.BattingOrderPlayerIds[10]);

            // Hit a six
            rules.EvaluateAndCommitDelivery(MatchTestDataFactory.RunsOffBat(0, boundarySix: true));

            // Take a wicket
            rules.EvaluateAndCommitDelivery(new DeliveryResolutionInput(
                new DeliveryTelemetry(142f, new SimVector3(0.2f, 2f, 8.84f), new SimVector2(0f, -6f)),
                new ShotTelemetry(ShotIntent.GroundStroke, FootworkType.FrontFoot, ContactZone.Miss),
                new FieldingAndRunningEvidence(proposedDismissal: DismissalType.Bowled)));

            Assert.IsTrue(director.EmittedCueHistory.Any(c => c.CueKey == "Ceremony.PreMatchIntro"));
            Assert.IsTrue(director.EmittedCueHistory.Any(c => c.CueKey == "Hud.TossBanner"));
            Assert.IsTrue(director.EmittedCueHistory.Any(c => c.CueKey == "Crowd.RoarSix" && c.Priority == BroadcastPriority.ImmediateInterrupt));
            Assert.IsTrue(director.EmittedCueHistory.Any(c => c.CueKey == "Replay.BoundarySix"));
            Assert.IsTrue(director.EmittedCueHistory.Any(c => c.CueKey == "Cam.WicketCelebration" && c.Category == BroadcastCueCategory.CameraCut));
            Assert.IsTrue(director.EmittedCueHistory.Any(c => c.CueKey == "Replay.WicketSlowMo"));
        }
    }
}
