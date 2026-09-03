using System;

namespace LockStep.Server.Net
{
    public interface INetworkServerTransport : IDisposable
    {
        event Action<int> Connected;
        event Action<int> Disconnected;
        event Action<int, byte[]> OnReceivedpacket;
        event Action<int, Exception> TransportError;

        bool IsActive { get; }

        void Start(int port);
        void Stop();
        void TickIncoming();
        void TickOutgoing();
        void Send(int connectionId, byte[] payload);
        void Disconnect(int connectionId);
    }
}
