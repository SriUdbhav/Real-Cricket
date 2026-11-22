using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using Cricket.Simulation.Core.Lifecycle;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class MatchLifecycleEngineTests
    {
        [Test]
        public void FullMatchLifecycle_ExecutesAllLegalTransitionsAndPublishesEvents()
        {
            var bus = new MatchEventBus();
            var observed = new List<MatchPhase>();
            using IDisposable sub = bus.Subscribe<MatchPhaseChangedEvent>(e => observed.Add(e.CurrentPhase));

            var lifecycle = new MatchLifecycleEngine(bus);
            Assert.AreEqual(MatchPhase.Uninitialized, lifecycle.CurrentPhase);

            lifecycle.TransitionTo(MatchPhase.MatchSetup, "Init");
            lifecycle.TransitionTo(MatchPhase.PreMatchCeremony, "Walkout");
            lifecycle.TransitionTo(MatchPhase.Toss, "Coin flip");
            lifecycle.TransitionTo(MatchPhase.InningsReady, "Innings 1");
            lifecycle.TransitionTo(MatchPhase.OverSetup, "Over 1");
            lifecycle.TransitionTo(MatchPhase.DeliveryInProgress, "Ball 1");
            lifecycle.TransitionTo(MatchPhase.BallInPlay, "Bat contact");
            lifecycle.TransitionTo(MatchPhase.DecisionAndReview, "Appeal");
            lifecycle.TransitionTo(MatchPhase.OverBreak, "Over complete");
            lifecycle.TransitionTo(MatchPhase.InningsBreak, "Innings 1 complete");
            lifecycle.TransitionTo(MatchPhase.InningsReady, "Innings 2");
            lifecycle.TransitionTo(MatchPhase.OverSetup, "Chase Over 1");
            lifecycle.TransitionTo(MatchPhase.PostMatchCeremony, "Target chased");
            lifecycle.TransitionTo(MatchPhase.Completed, "Done");

            Assert.AreEqual(MatchPhase.Completed, lifecycle.CurrentPhase);
            Assert.AreEqual(14, observed.Count);
            Assert.AreEqual(MatchPhase.Completed, observed[observed.Count - 1]);
        }

        [Test]
        public void IllegalTransition_ThrowsInvalidOperationExceptionAndPreservesState()
        {
            var bus = new MatchEventBus();
            var lifecycle = new MatchLifecycleEngine(bus);
            lifecycle.TransitionTo(MatchPhase.MatchSetup);

            Assert.IsFalse(lifecycle.CanTransitionTo(MatchPhase.DeliveryInProgress));
            Assert.Throws<InvalidOperationException>(() => lifecycle.TransitionTo(MatchPhase.DeliveryInProgress));
            Assert.AreEqual(MatchPhase.MatchSetup, lifecycle.CurrentPhase);
        }
    }
}
