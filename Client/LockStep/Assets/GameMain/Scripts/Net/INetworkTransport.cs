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

        // NetworkComponent 校验地址和端口；连接结果通过事件通知。
        void Connect(string host, int port);
        void Disconnect();
        void Tick();
        // NetworkComponent 确认已连接后调用。
        void Send(byte[] payload);
    }
}
