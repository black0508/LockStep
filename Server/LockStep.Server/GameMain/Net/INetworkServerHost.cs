using System;

namespace LockStep.Server.Net
{
    public interface INetworkServerHost : IDisposable
    {
        event Action<int> Connected;
        event Action<int> Disconnected;
        event Action<int, byte[]> OnReceivedpacket;
        event Action<int, Exception> TransportError;

        bool IsActive { get; }

        void Start(int port);
        void Stop();
        void Tick();
        void Send(int clientId, byte[] payload);
        void Disconnect(int clientId);
    }
}
