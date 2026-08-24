using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using KCP;

namespace LockStep.Server.Net
{
    public class KcpServerPeer
    {
        public uint ChannelId;
        public IPEndPoint Remote;
        public uint LastHeardMs;
        public Queue<byte[]> RecvQueue = new Queue<byte[]>();

        internal Kcp Kcp;

        public void Send(byte[] payload)
        {
            Kcp.Send(payload);
        }

        public bool TryRecv(out byte[] payload)
        {
            if (RecvQueue.Count == 0)
            {
                payload = null;
                return false;
            }

            payload = RecvQueue.Dequeue();
            return true;
        }

        internal void Pull()
        {
            while (true)
            {
                int size = Kcp.PeekSize();
                if (size < 0)
                {
                    return;
                }

                byte[] msg = new byte[size];
                Kcp.Receive(msg);
                RecvQueue.Enqueue(msg);
            }
        }
    }

    public class KcpServerTransport
    {
        const int Head = 5;
        const int Mtu = 470;

        Socket socket;
        byte[] recvBuf = new byte[2048];
        EndPoint recvFrom = new IPEndPoint(IPAddress.Any, 0);
        Dictionary<uint, IPEndPoint> pending = new Dictionary<uint, IPEndPoint>();
        public List<KcpServerPeer> Peers = new List<KcpServerPeer>();

        public void Bind(int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public void Tick()
        {
            if (socket == null)
            {
                return;
            }

            RecvUdp();
            uint now = (uint)Environment.TickCount;
            for (int i = Peers.Count - 1; i >= 0; i--)
            {
                KcpServerPeer peer = Peers[i];
                peer.Kcp.Update(now);
                peer.Pull();
                if ((int)(now - peer.LastHeardMs) > 3000)
                {
                    Console.WriteLine("[LockStep] timeout channel=" + peer.ChannelId);
                    SendHead(KcpHeader.Disconnect, peer.ChannelId, peer.Remote);
                    peer.Kcp.Dispose();
                    Peers.RemoveAt(i);
                }
            }
        }

        void RecvUdp()
        {
            while (socket.Poll(0, SelectMode.SelectRead))
            {
                int n;
                try
                {
                    n = socket.ReceiveFrom(recvBuf, ref recvFrom);
                }
                catch (SocketException)
                {
                    return;
                }

                if (n < Head)
                {
                    continue;
                }

                KcpHeader header = (KcpHeader)recvBuf[0];
                uint conv = ReadU32(recvBuf, 1);
                IPEndPoint remote = (IPEndPoint)recvFrom;

                if (header == KcpHeader.RequestConnection)
                {
                    if (FindPeer(conv) != null)
                    {
                        SendHead(KcpHeader.ConfirmConnection, conv, remote);
                        continue;
                    }

                    pending[conv] = new IPEndPoint(remote.Address, remote.Port);
                    SendHead(KcpHeader.WaitConfirmConnection, conv, remote);
                    continue;
                }

                if (header == KcpHeader.ConfirmConnection)
                {
                    if (FindPeer(conv) != null)
                    {
                        SendHead(KcpHeader.ConfirmConnection, conv, remote);
                        continue;
                    }

                    if (!pending.ContainsKey(conv))
                    {
                        continue;
                    }

                    pending.Remove(conv);
                    KcpServerPeer peer = AddPeer(conv, new IPEndPoint(remote.Address, remote.Port));
                    SendHead(KcpHeader.ConfirmConnection, conv, remote);
                    Console.WriteLine("[LockStep] handshake ok channel=" + conv);
                    continue;
                }

                KcpServerPeer live = FindPeer(conv);
                if (live == null)
                {
                    continue;
                }

                live.LastHeardMs = (uint)Environment.TickCount;
                if (header == KcpHeader.ReceiveData)
                {
                    byte[] pack = new byte[n - Head];
                    Array.Copy(recvBuf, Head, pack, 0, pack.Length);
                    live.Kcp.Input(pack);
                }
                else if (header == KcpHeader.Disconnect)
                {
                    Console.WriteLine("[LockStep] disconnect channel=" + conv);
                    Peers.Remove(live);
                    live.Kcp.Dispose();
                }
            }
        }

        KcpServerPeer AddPeer(uint conv, IPEndPoint remote)
        {
            KcpServerPeer peer = new KcpServerPeer();
            peer.ChannelId = conv;
            peer.Remote = remote;
            peer.Kcp = new Kcp(conv, (buffer, length) =>
            {
                if (socket == null)
                {
                    return;
                }

                buffer[0] = (byte)KcpHeader.ReceiveData;
                WriteU32(buffer, 1, conv);
                socket.SendTo(buffer, 0, length + Head, SocketFlags.None, remote);
            }, Head);
            peer.Kcp.SetNoDelay(1, 5, 2, 1);
            peer.Kcp.SetWindowSize(256, 256);
            peer.Kcp.SetMtu(Mtu);
            peer.Kcp.SetMinrto(30);
            peer.LastHeardMs = (uint)Environment.TickCount;
            Peers.Add(peer);
            return peer;
        }

        KcpServerPeer FindPeer(uint conv)
        {
            for (int i = 0; i < Peers.Count; i++)
            {
                if (Peers[i].ChannelId == conv)
                {
                    return Peers[i];
                }
            }

            return null;
        }

        void SendHead(KcpHeader header, uint conv, EndPoint remote)
        {
            byte[] buf = new byte[Head];
            buf[0] = (byte)header;
            WriteU32(buf, 1, conv);
            socket.SendTo(buf, remote);
        }

        static void WriteU32(byte[] buf, int offset, uint v)
        {
            buf[offset] = (byte)v;
            buf[offset + 1] = (byte)(v >> 8);
            buf[offset + 2] = (byte)(v >> 16);
            buf[offset + 3] = (byte)(v >> 24);
        }

        static uint ReadU32(byte[] buf, int offset)
        {
            return (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
        }
    }
}
