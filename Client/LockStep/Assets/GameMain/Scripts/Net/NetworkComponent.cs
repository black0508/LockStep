using System;
using System.Net.Sockets;
using GameMain.Net.Events;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Net
{
    public sealed class NetworkComponent : Component
    {
        INetworkTransport transport;
        MessageDispatcher dispatcher;

        public bool IsConnected => transport.IsConnected;
        public uint RttMilliseconds => transport.RttMilliseconds;

        // 从此处接管 transport，销毁组件时统一释放。
        public void Init(INetworkTransport value)
        {
            transport = value;
            dispatcher = new MessageDispatcher();
            dispatcher.RegisterAssembly(typeof(GameApplication).Assembly);
            transport.Connected += OnConnected;
            transport.Disconnected += OnDisconnected;
            transport.ReceivedPacket += OnReceivedPacket;
            transport.TransportError += OnTransportError;
        }

        public bool Connect(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > ushort.MaxValue)
            {
                GameLog.Error("连接失败：地址/端口无效");
                return false;
            }
            try
            {
                GameLog.Info($"正在连接 {host}:{port}");
                transport.Connect(host, port);
                return true;
            }
            catch (SocketException error)
            {
                GameLog.Error($"连接失败 {host}:{port}", error);
                return false;
            }
        }

        public void Disconnect()
        {
            transport.Disconnect();
        }

        // false 只表示当前未连接，true 不代表对端已收到。
        public bool TrySend(MsgId id, IMessage message)
        {
            if (!IsConnected) return false;
            var packet = new Packet { Id = id, Body = message.ToByteString() };
            transport.Send(packet.ToByteArray());
            return true;
        }

        protected override void OnUpdate(float deltaTime)
        {
            transport.Tick();
        }

        void OnConnected()
        {
            GameLog.Info("连接成功");
            GameEntry.Application.Events.FireNow(this, NetworkConnectedEventArgs.Create());
        }

        void OnDisconnected()
        {
            GameLog.Info("断开连接");
            GameEntry.Application.Events.FireNow(this, NetworkDisconnectedEventArgs.Create());
        }

        void OnReceivedPacket(byte[] payload)
        {
            MsgId id = MsgId.Unspecified;
            try
            {
                Packet packet = Packet.Parser.ParseFrom(payload);
                id = packet.Id;
                dispatcher.Dispatch(Entity, id, packet.Body);
            }
            catch (InvalidProtocolBufferException error)
            {
                GameLog.Error($"消息解析失败：{id}", error);
            }
        }

        void OnTransportError(Exception error)
        {
            GameLog.Error("传输错误", error);
        }

        protected override void OnDestroy()
        {
            if (transport == null) return;
            transport.Connected -= OnConnected;
            transport.Disconnected -= OnDisconnected;
            transport.ReceivedPacket -= OnReceivedPacket;
            transport.TransportError -= OnTransportError;
            transport.Dispose();
        }
    }
}
