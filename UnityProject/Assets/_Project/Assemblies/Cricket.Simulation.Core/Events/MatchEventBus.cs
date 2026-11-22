using System;
using System.Collections.Generic;

namespace Cricket.Simulation.Core.Events
{
    /// <summary>
    /// Thread-safe, re-entrancy-safe strongly-typed event bus with zero-boxing dispatch
    /// per event type channel.
    /// </summary>
    public sealed class MatchEventBus : IMatchEventBus
    {
        private interface IChannel
        {
            void Clear();
        }

        private sealed class Channel<TEvent> : IChannel where TEvent : struct, IMatchEvent
        {
            private readonly object _gate = new object();
            private readonly List<Action<TEvent>> _handlers = new List<Action<TEvent>>(8);

            public IDisposable Add(Action<TEvent> handler)
            {
                lock (_gate)
                {
                    _handlers.Add(handler);
                }

                return new Subscription<TEvent>(this, handler);
            }

            public void Remove(Action<TEvent> handler)
            {
                lock (_gate)
                {
                    _handlers.Remove(handler);
                }
            }

            public void Dispatch(in TEvent evt)
            {
                Action<TEvent>[] snapshot;
                lock (_gate)
                {
                    if (_handlers.Count == 0)
                    {
                        return;
                    }

                    snapshot = _handlers.ToArray();
                }

                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i].Invoke(evt);
                }
            }

            public int Count
            {
                get
                {
                    lock (_gate)
                    {
                        return _handlers.Count;
                    }
                }
            }

            public void Clear()
            {
                lock (_gate)
                {
                    _handlers.Clear();
                }
            }
        }

        private sealed class Subscription<TEvent> : IDisposable where TEvent : struct, IMatchEvent
        {
            private Channel<TEvent>? _channel;
            private Action<TEvent>? _handler;

            public Subscription(Channel<TEvent> channel, Action<TEvent> handler)
            {
                _channel = channel;
                _handler = handler;
            }

            public void Dispose()
            {
                Channel<TEvent>? channel = _channel;
                Action<TEvent>? handler = _handler;
                if (channel != null && handler != null)
                {
                    channel.Remove(handler);
                }

                _channel = null;
                _handler = null;
            }
        }

        private readonly object _registryGate = new object();
        private readonly Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>(16);

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IMatchEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Channel<TEvent> channel = GetOrCreateChannel<TEvent>();
            return channel.Add(handler);
        }

        public void Publish<TEvent>(in TEvent matchEvent) where TEvent : struct, IMatchEvent
        {
            Channel<TEvent>? channel = TryGetChannel<TEvent>();
            if (channel != null)
            {
                channel.Dispatch(in matchEvent);
            }
        }

        public int GetSubscriberCount<TEvent>() where TEvent : struct, IMatchEvent
        {
            Channel<TEvent>? channel = TryGetChannel<TEvent>();
            return channel != null ? channel.Count : 0;
        }

        public void ClearAll()
        {
            lock (_registryGate)
            {
                foreach (IChannel channel in _channels.Values)
                {
                    channel.Clear();
                }

                _channels.Clear();
            }
        }

        private Channel<TEvent> GetOrCreateChannel<TEvent>() where TEvent : struct, IMatchEvent
        {
            lock (_registryGate)
            {
                Type key = typeof(TEvent);
                if (!_channels.TryGetValue(key, out IChannel? existing))
                {
                    var created = new Channel<TEvent>();
                    _channels[key] = created;
                    return created;
                }

                return (Channel<TEvent>)existing;
            }
        }

        private Channel<TEvent>? TryGetChannel<TEvent>() where TEvent : struct, IMatchEvent
        {
            lock (_registryGate)
            {
                if (_channels.TryGetValue(typeof(TEvent), out IChannel? existing))
                {
                    return (Channel<TEvent>)existing;
                }

                return null;
            }
        }
    }
}
