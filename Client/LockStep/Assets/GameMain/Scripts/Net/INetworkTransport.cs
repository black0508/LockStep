using System;

namespace GameMain.Net
{
    public interface INetworkTransport : IDisposable
    {
        event Action Connected;
        event Action Disconnected;
        event Action<byte[]> OnReceivedpacket;
        event Action<Exception> TransportError;

        bool IsConnected { get; }

        void Connect(string host, int port);
        void Disconnect();
        void Tick();
        void Send(byte[] payload);
    }
}
