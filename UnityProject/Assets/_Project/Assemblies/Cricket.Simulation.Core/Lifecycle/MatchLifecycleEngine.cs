using System;
using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Events;

namespace Cricket.Simulation.Core.Lifecycle
{
    /// <summary>
    /// Deterministic Match Lifecycle Finite State Machine governing all valid transitions
    /// across pre-match ceremonies, live overs/deliveries, breaks, Super Overs, and post-match.
    /// </summary>
    public sealed class MatchLifecycleEngine : IMatchLifecycleEngine
    {
        private static readonly Dictionary<MatchPhase, HashSet<MatchPhase>> AllowedTransitions =
            new Dictionary<MatchPhase, HashSet<MatchPhase>>
            {
                [MatchPhase.Uninitialized] = new HashSet<MatchPhase>
                {
                    MatchPhase.MatchSetup
                },
                [MatchPhase.MatchSetup] = new HashSet<MatchPhase>
                {
                    MatchPhase.PreMatchCeremony,
                    MatchPhase.Toss
                },
                [MatchPhase.PreMatchCeremony] = new HashSet<MatchPhase>
                {
                    MatchPhase.Toss
                },
                [MatchPhase.Toss] = new HashSet<MatchPhase>
                {
                    MatchPhase.InningsReady
                },
                [MatchPhase.InningsReady] = new HashSet<MatchPhase>
                {
                    MatchPhase.OverSetup
                },
                [MatchPhase.OverSetup] = new HashSet<MatchPhase>
                {
                    MatchPhase.DeliveryInProgress,
                    MatchPhase.SessionOrDrinksBreak,
                    MatchPhase.InningsBreak,
                    MatchPhase.PostMatchCeremony
                },
                [MatchPhase.DeliveryInProgress] = new HashSet<MatchPhase>
                {
                    MatchPhase.BallInPlay,
                    MatchPhase.DecisionAndReview
                },
                [MatchPhase.BallInPlay] = new HashSet<MatchPhase>
                {
                    MatchPhase.DecisionAndReview,
                    MatchPhase.OverSetup,
                    MatchPhase.OverBreak,
                    MatchPhase.InningsBreak,
                    MatchPhase.SuperOverSetup,
                    MatchPhase.PostMatchCeremony
                },
                [MatchPhase.DecisionAndReview] = new HashSet<MatchPhase>
                {
                    MatchPhase.OverSetup,
                    MatchPhase.OverBreak,
                    MatchPhase.InningsBreak,
                    MatchPhase.SuperOverSetup,
                    MatchPhase.PostMatchCeremony
                },
                [MatchPhase.OverBreak] = new HashSet<MatchPhase>
                {
                    MatchPhase.OverSetup,
                    MatchPhase.SessionOrDrinksBreak,
                    MatchPhase.InningsBreak,
                    MatchPhase.SuperOverSetup,
                    MatchPhase.PostMatchCeremony
                },
                [MatchPhase.SessionOrDrinksBreak] = new HashSet<MatchPhase>
                {
                    MatchPhase.OverSetup,
                    MatchPhase.InningsBreak,
                    MatchPhase.PostMatchCeremony
                },
                [MatchPhase.InningsBreak] = new HashSet<MatchPhase>
                {
                    MatchPhase.InningsReady,
                    MatchPhase.SuperOverSetup,
                    MatchPhase.PostMatchCeremony
                },
                [MatchPhase.SuperOverSetup] = new HashSet<MatchPhase>
                {
                    MatchPhase.InningsReady
                },
                [MatchPhase.PostMatchCeremony] = new HashSet<MatchPhase>
                {
                    MatchPhase.Completed
                },
                [MatchPhase.Completed] = new HashSet<MatchPhase>()
            };

        private readonly IMatchEventBus _eventBus;
        private long _sequenceCounter;

        public MatchPhase CurrentPhase { get; private set; } = MatchPhase.Uninitialized;

        public MatchLifecycleEngine(IMatchEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public bool CanTransitionTo(MatchPhase targetPhase)
        {
            if (AllowedTransitions.TryGetValue(CurrentPhase, out HashSet<MatchPhase>? nextPhases))
            {
                return nextPhases.Contains(targetPhase);
            }

            return false;
        }

        public void TransitionTo(MatchPhase targetPhase, string reason = "")
        {
            if (!CanTransitionTo(targetPhase))
            {
                throw new InvalidOperationException(
                    $"Illegal match lifecycle transition from '{CurrentPhase}' to '{targetPhase}'.");
            }

            MatchPhase previous = CurrentPhase;
            CurrentPhase = targetPhase;
            long seq = ++_sequenceCounter;
            _eventBus.Publish(new MatchPhaseChangedEvent(seq, previous, CurrentPhase, reason));
        }

        public void Reset()
        {
            CurrentPhase = MatchPhase.Uninitialized;
            _sequenceCounter = 0;
        }
    }
}
