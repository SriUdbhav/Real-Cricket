using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class MatchEventBusTests
    {
        [Test]
        public void Publish_DeliversStronglyTypedEventsOnlyToMatchingSubscribers()
        {
            var bus = new MatchEventBus();
            var receivedPhases = new List<MatchPhase>();
            int scoreUpdates = 0;

            using IDisposable sub1 = bus.Subscribe<MatchPhaseChangedEvent>(e => receivedPhases.Add(e.CurrentPhase));
            using IDisposable sub2 = bus.Subscribe<ScoreUpdatedEvent>(_ => scoreUpdates++);

            bus.Publish(new MatchPhaseChangedEvent(1, MatchPhase.Uninitialized, MatchPhase.MatchSetup, "Boot"));
            bus.Publish(new MatchPhaseChangedEvent(2, MatchPhase.MatchSetup, MatchPhase.Toss, "Toss"));

            Assert.AreEqual(2, receivedPhases.Count);
            Assert.AreEqual(MatchPhase.MatchSetup, receivedPhases[0]);
            Assert.AreEqual(MatchPhase.Toss, receivedPhases[1]);
            Assert.AreEqual(0, scoreUpdates);
        }

        [Test]
        public void DisposeSubscription_StopsReceivingFurtherEvents()
        {
            var bus = new MatchEventBus();
            int count = 0;

            IDisposable sub = bus.Subscribe<ScoreUpdatedEvent>(_ => count++);
            bus.Publish(new ScoreUpdatedEvent(1, 1, 4, 0, 0, 1, null));
            Assert.AreEqual(1, count);

            sub.Dispose();
            bus.Publish(new ScoreUpdatedEvent(2, 1, 8, 0, 0, 2, null));
            Assert.AreEqual(1, count);
            Assert.AreEqual(0, bus.GetSubscriberCount<ScoreUpdatedEvent>());
        }

        [Test]
        public void Publish_SupportsReEntrantUnsubscribeDuringCallback()
        {
            var bus = new MatchEventBus();
            int calls = 0;
            IDisposable? token = null;

            token = bus.Subscribe<RuleViolationEvent>(_ =>
            {
                calls++;
                token?.Dispose();
            });

            bus.Publish(new RuleViolationEvent(1, "TEST", "First"));
            bus.Publish(new RuleViolationEvent(2, "TEST", "Second"));

            Assert.AreEqual(1, calls);
        }
    }
}
