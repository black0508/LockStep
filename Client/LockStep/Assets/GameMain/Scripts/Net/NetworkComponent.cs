using System;
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

        public bool IsConnected => !IsDisposed && transport != null && transport.IsConnected;
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
            catch (Exception error)
            {
                GameLog.Error($"连接失败 {host}:{port}", error);
                return false;
            }
        }

        public void Disconnect()
        {
            try { transport.Disconnect(); }
            catch (Exception error) { GameLog.Error("断开连接失败", error); }
        }

        public bool TrySend(MsgId id, IMessage message)
        {
            if (!IsConnected) return false;
            if (message == null)
            {
                GameLog.Error($"不能发送空消息：{id}");
                return false;
            }
            try
            {
                transport.Send(MsgCodec.Encode(id, message));
                return true;
            }
            catch (Exception error)
            {
                GameLog.Error($"发送消息失败：{id}", error);
                return false;
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            transport.Tick(); // 异常由组件生命周期边界记录。
        }

        void OnConnected()
        {
            if (IsDisposed) return;
            GameLog.Info("连接成功");
            Entity.GetComponent<EventComponent>().FireNow(this, NetworkConnectedEventArgs.Create());
        }

        void OnDisconnected()
        {
            if (IsDisposed) return;
            GameLog.Info("断开连接");
            Entity.GetComponent<EventComponent>().FireNow(this, NetworkDisconnectedEventArgs.Create());
        }

        void OnReceivedPacket(byte[] payload)
        {
            if (IsDisposed) return;
            if (!MsgCodec.TryUnpack(payload, out MsgId id, out ByteString body))
            {
                GameLog.Warning("解包失败");
                return;
            }
            dispatcher.Dispatch(Entity, id, body);
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
