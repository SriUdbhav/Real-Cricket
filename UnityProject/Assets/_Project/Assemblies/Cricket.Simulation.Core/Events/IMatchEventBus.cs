using System;

namespace Cricket.Simulation.Core.Events
{
    /// <summary>
    /// Strongly-typed synchronous pub/sub event bus connecting independent simulation
    /// modules and downstream presentation/broadcast directors.
    /// </summary>
    public interface IMatchEventBus
    {
        /// <summary>
        /// Subscribes a handler for events of type <typeparamref name="TEvent"/>.
        /// Dispose the returned token to unsubscribe cleanly.
        /// </summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IMatchEvent;

        /// <summary>
        /// Publishes an event immediately to all active subscribers of <typeparamref name="TEvent"/>.
        /// </summary>
        void Publish<TEvent>(in TEvent matchEvent) where TEvent : struct, IMatchEvent;

        /// <summary>
        /// Returns the current number of active subscribers for <typeparamref name="TEvent"/>.
        /// </summary>
        int GetSubscriberCount<TEvent>() where TEvent : struct, IMatchEvent;

        /// <summary>
        /// Clears all subscriptions across all event types (used on match teardown).
        /// </summary>
        void ClearAll();
    }
}
