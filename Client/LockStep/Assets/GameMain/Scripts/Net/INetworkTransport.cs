using System;

namespace GameMain.Net
{
    public interface INetworkTransport : IDisposable
    {
        event Action Connected;
        event Action Disconnected;
        event Action<byte[]> ReceivedPacket;
        event Action<Exception> TransportError;

        bool IsConnected { get; }
        uint RttMilliseconds { get; }

        void Connect(string host, int port);
        void Disconnect();
        void Tick();
        void Send(byte[] payload);
    }
}
