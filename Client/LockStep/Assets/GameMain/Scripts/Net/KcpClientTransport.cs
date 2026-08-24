using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using KCP;

namespace GameMain.Net
{
    public class KcpClientTransport
    {
        const int Head = 5;
        const int Mtu = 470;

        public Action Connected;
        public Action Disconnected;

        Socket socket;
        Kcp kcp;
        uint channelId;
        bool connected;
        uint nextRequestMs;
        byte[] recvBuf = new byte[2048];
        Queue<byte[]> recvQueue = new Queue<byte[]>();

        public bool IsConnected
        {
            get { return connected; }
        }

        public void Connect(string host, int port)
        {
            channelId = (uint)new Random().Next();
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            socket.Connect(new IPEndPoint(IPAddress.Parse(host), port));
            CreateKcp();
            SendHead(KcpHeader.RequestConnection);
            nextRequestMs = (uint)Environment.TickCount + 300;
        }

        public void Tick()
        {
            if (socket == null)
            {
                return;
            }

            uint now = (uint)Environment.TickCount;
            RecvUdp();
            if (socket == null || kcp == null)
            {
                return;
            }

            if (!connected && (int)(now - nextRequestMs) >= 0)
            {
                SendHead(KcpHeader.RequestConnection);
                nextRequestMs = now + 300;
            }

            kcp.Update(now);
            Pull();
        }

        public void Send(byte[] payload)
        {
            if (connected)
            {
                kcp.Send(payload);
            }
        }

        public bool TryRecv(out byte[] payload)
        {
            if (recvQueue.Count == 0)
            {
                payload = null;
                return false;
            }

            payload = recvQueue.Dequeue();
            return true;
        }

        public void Close()
        {
            Drop(true);
        }

        void Drop(bool sendDisconnect)
        {
            if (socket == null && kcp == null)
            {
                return;
            }

            bool wasConnected = connected;
            if (sendDisconnect && socket != null)
            {
                try
                {
                    SendHead(KcpHeader.Disconnect);
                }
                catch (SocketException)
                {
                }
            }

            if (socket != null)
            {
                socket.Close();
                socket = null;
            }

            connected = false;
            if (kcp != null)
            {
                kcp.Dispose();
                kcp = null;
            }

            if (wasConnected && Disconnected != null)
            {
                Disconnected();
            }
        }

        void RecvUdp()
        {
            while (socket.Poll(0, SelectMode.SelectRead))
            {
                int n;
                try
                {
                    n = socket.Receive(recvBuf);
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
                if (conv != channelId)
                {
                    continue;
                }

                if (header == KcpHeader.ReceiveData)
                {
                    if (!connected)
                    {
                        continue;
                    }

                    byte[] pack = new byte[n - Head];
                    Array.Copy(recvBuf, Head, pack, 0, pack.Length);
                    kcp.Input(pack);
                    continue;
                }

                if (header == KcpHeader.WaitConfirmConnection)
                {
                    SendHead(KcpHeader.ConfirmConnection);
                }
                else if (header == KcpHeader.ConfirmConnection)
                {
                    if (!connected)
                    {
                        connected = true;
                        if (Connected != null)
                        {
                            Connected();
                        }
                    }
                }
                else if (header == KcpHeader.RepeatChannelId)
                {
                    channelId = (uint)new Random().Next();
                    CreateKcp();
                    SendHead(KcpHeader.RequestConnection);
                }
                else if (header == KcpHeader.Disconnect)
                {
                    Drop(false);
                    return;
                }
            }
        }

        void Pull()
        {
            while (true)
            {
                int size = kcp.PeekSize();
                if (size < 0)
                {
                    return;
                }

                byte[] msg = new byte[size];
                kcp.Receive(msg);
                recvQueue.Enqueue(msg);
            }
        }

        void CreateKcp()
        {
            if (kcp != null)
            {
                kcp.Dispose();
            }

            kcp = new Kcp(channelId, OnOutput, Head);
            kcp.SetNoDelay(1, 5, 2, 1);
            kcp.SetWindowSize(256, 256);
            kcp.SetMtu(Mtu);
            kcp.SetMinrto(30);
        }

        void OnOutput(byte[] buffer, int length)
        {
            if (socket == null)
            {
                return;
            }

            buffer[0] = (byte)KcpHeader.ReceiveData;
            WriteU32(buffer, 1, channelId);
            socket.Send(buffer, 0, length + Head, SocketFlags.None);
        }

        void SendHead(KcpHeader header)
        {
            byte[] buf = new byte[Head];
            buf[0] = (byte)header;
            WriteU32(buf, 1, channelId);
            socket.Send(buf);
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
