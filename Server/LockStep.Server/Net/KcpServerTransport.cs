using System;
using System.Runtime.InteropServices;
using kcp2k;

namespace LockStep.Server.Net
{
    // Windows 下 UDP 收到 ICMP Port Unreachable（客户端退出、端口没人听）时，
    // 下一次 ReceiveFrom 会抛 SocketException 10054。kcp2k 只在 DualMode 的
    // IPv6 分支禁用了这个行为，IPv4 分支没有，所以这里补上。
    class Ipv4KcpServer : KcpServer
    {
        public Ipv4KcpServer(Action<int> onConnected,
                             Action<int, ArraySegment<byte>, KcpChannel> onData,
                             Action<int> onDisconnected,
                             Action<int, ErrorCode, string> onError,
                             KcpConfig config)
            : base(onConnected, onData, onDisconnected, onError, config)
        {
        }

        public override void Start(ushort port)
        {
            base.Start(port);

            if (socket == null || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const uint IOC_IN = 0x80000000U;
            const uint IOC_VENDOR = 0x18000000U;
            const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));
            socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0x00 }, null);
        }
    }

    public class KcpServerTransport : INetworkServerTransport
    {
        public event Action<int> Connected;
        public event Action<int> Disconnected;
        public event Action<int, byte[]> OnReceivedpacket;
        public event Action<int, Exception> TransportError;

        KcpServer server;

        public bool IsActive
        {
            get { return server != null && server.IsActive(); }
        }

        public void Start(int port)
        {
            Stop();

            var config = new KcpConfig(
                DualMode: false,
                NoDelay: true,
                Interval: 10,
                FastResend: 2,
                CongestionWindow: false
            );

            server = new Ipv4KcpServer(OnConnected, OnData, OnDisconnected, OnError, config);
            server.Start((ushort)port);
        }

        public void TickIncoming()
        {
            if (server != null)
            {
                server.TickIncoming();
            }
        }

        public void TickOutgoing()
        {
            if (server != null)
            {
                server.TickOutgoing();
            }
        }

        public void Send(int connectionId, byte[] payload)
        {
            if (server != null)
            {
                server.Send(connectionId, new ArraySegment<byte>(payload), KcpChannel.Reliable);
            }
        }

        public void Disconnect(int connectionId)
        {
            if (server != null)
            {
                server.Disconnect(connectionId);
            }
        }

        public void Stop()
        {
            if (server == null)
            {
                return;
            }

            server.Stop();
            server = null;
        }

        public void Dispose()
        {
            Stop();
        }

        void OnConnected(int connectionId)
        {
            Connected?.Invoke(connectionId);
        }

        void OnDisconnected(int connectionId)
        {
            Disconnected?.Invoke(connectionId);
        }

        void OnData(int connectionId, ArraySegment<byte> data, KcpChannel channel)
        {
            byte[] payload = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, payload, 0, data.Count);
            OnReceivedpacket?.Invoke(connectionId, payload);
        }

        void OnError(int connectionId, ErrorCode error, string message)
        {
            TransportError?.Invoke(connectionId, new Exception("kcp error " + error + " - " + message));
        }
    }
}
