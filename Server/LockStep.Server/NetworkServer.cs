using System;
using LockStep.Server.Net;

namespace LockStep.Server
{
    public class NetworkServer : IDisposable
    {
        INetworkServerTransport transport;

        public bool IsActive
        {
            get { return transport != null && transport.IsActive; }
        }

        public void Start(int port)
        {
            transport = new KcpServerTransport();
            transport.Connected += OnConnected;
            transport.Disconnected += OnDisconnected;
            transport.OnReceivedpacket += OnReceivedPacket;
            transport.TransportError += OnTransportError;

            transport.Start(port);
            Console.WriteLine("[LockStep] listening UDP " + port);
        }

        public void Tick()
        {
            if (transport == null)
            {
                return;
            }

            transport.TickIncoming();
            transport.TickOutgoing();
        }

        public void Send(int connectionId, byte[] payload)
        {
            if (transport != null)
            {
                transport.Send(connectionId, payload);
            }
        }

        public void Dispose()
        {
            if (transport == null)
            {
                return;
            }

            transport.Connected -= OnConnected;
            transport.Disconnected -= OnDisconnected;
            transport.OnReceivedpacket -= OnReceivedPacket;
            transport.TransportError -= OnTransportError;
            transport.Dispose();
            transport = null;
        }

        void OnConnected(int connectionId)
        {
            Console.WriteLine("[LockStep] connected connection=" + connectionId);
        }

        void OnDisconnected(int connectionId)
        {
            Console.WriteLine("[LockStep] disconnected connection=" + connectionId);
        }

        void OnReceivedPacket(int connectionId, byte[] payload)
        {
            Console.WriteLine("[LockStep] recv " + payload.Length + " bytes connection=" + connectionId);
        }

        void OnTransportError(int connectionId, Exception error)
        {
            Console.WriteLine("[LockStep] transport error connection=" + connectionId + " " + error.Message);
        }
    }
}
