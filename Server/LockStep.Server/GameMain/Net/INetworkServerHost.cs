using System;

namespace LockStep.Server.Net
{
    public interface INetworkServerHost : IDisposable
    {
        event Action<int> Connected;
        event Action<int> Disconnected;
        event Action<int, byte[]> ReceivedPacket;
        event Action<int, Exception> TransportError;

        bool IsActive { get; }

        // NetworkComponent 校验端口；正常返回表示已监听，启动失败抛出异常。
        void Start(int port);
        void Stop();
        void Tick();
        // NetworkComponent 确认连接有效后调用 Send / Disconnect。
        void Send(int clientId, byte[] payload);
        void Disconnect(int clientId);
    }
}
