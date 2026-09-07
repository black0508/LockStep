using System;
using kcp2k;

namespace GameMain.Net
{
    public class KcpClientTransport : INetworkTransport
    {
        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> OnReceivedpacket;
        public event Action<Exception> TransportError;

        KcpClient client;

        public bool IsConnected
        {
            get { return client != null && client.connected; }
        }

        public void Connect(string host, int port)
        {
            Disconnect();

            var config = new KcpConfig(
                DualMode: false,
                NoDelay: true,
                Interval: 10,
                FastResend: 2,
                CongestionWindow: false
            );

            client = new KcpClient(OnConnected, OnData, OnDisconnected, OnError, config);
            client.Connect(host, (ushort)port);
        }

        public void Tick()
        {
            if (client != null)
            {
                client.Tick();
            }
        }

        public void Send(byte[] payload)
        {
            if (!IsConnected)
            {
                return;
            }

            client.Send(new ArraySegment<byte>(payload), KcpChannel.Reliable);
        }

        public void Disconnect()
        {
            if (client == null)
            {
                return;
            }

            client.Disconnect();
            client = null;
        }

        public void Dispose()
        {
            Disconnect();
        }

        void OnConnected()
        {
            Connected?.Invoke();
        }

        void OnDisconnected()
        {
            Disconnected?.Invoke();
        }

        void OnData(ArraySegment<byte> data, KcpChannel channel)
        {
            byte[] payload = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, payload, 0, data.Count);
            OnReceivedpacket?.Invoke(payload);
        }

        void OnError(ErrorCode error, string message)
        {
            Log.Error("[LockStep] kcp 错误 " + error + " " + message);
            TransportError?.Invoke(new Exception("kcp 错误 " + error + " - " + message));
        }
    }
}
