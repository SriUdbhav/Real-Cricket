using System;
using Cricket.Multiplayer.Shared.Protocol;

namespace Cricket.Multiplayer.Shared.Sync
{
    /// <summary>
    /// Transport abstraction allowing offline single-player and online authoritative multiplayer
    /// to share the exact same command dispatch and sync callback pipeline.
    /// </summary>
    public interface IMatchTransport
    {
        MatchSessionMode SessionMode { get; }
        event Action<IMatchCommand>? CommandReceived;
        event Action<AuthoritativeBallSyncPacket>? BallSyncReceived;

        void SendCommand(IMatchCommand command);
        void BroadcastAuthoritativeBallSync(AuthoritativeBallSyncPacket packet);
    }

    /// <summary>
    /// Zero-latency in-process transport used for Offline Single-Player mode while maintaining
    /// full compatibility with the multiplayer command/sync pipeline.
    /// </summary>
    public sealed class LocalLoopbackTransport : IMatchTransport
    {
        public MatchSessionMode SessionMode => MatchSessionMode.OfflineSinglePlayer;

        public event Action<IMatchCommand>? CommandReceived;
        public event Action<AuthoritativeBallSyncPacket>? BallSyncReceived;

        public void SendCommand(IMatchCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            CommandReceived?.Invoke(command);
        }

        public void BroadcastAuthoritativeBallSync(AuthoritativeBallSyncPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            BallSyncReceived?.Invoke(packet);
        }
    }
}
