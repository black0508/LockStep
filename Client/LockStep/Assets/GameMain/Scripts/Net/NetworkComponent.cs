using System;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Net
{
    public sealed class NetworkComponent : Component
    {
        INetworkTransport transport;
        MessageDispatcher dispatcher;
        bool initialized;

        public bool IsConnected => !IsDisposed && transport != null && transport.IsConnected;

        // 从此处接管 transport，销毁组件时统一释放。
        public bool Init(INetworkTransport value)
        {
            if (IsDisposed || Entity == null || transport != null || value == null)
            {
                Log.Error("网络组件初始化失败：组件不可用、已初始化或 transport 为空", nameof(NetworkComponent));
                return false;
            }
            transport = value;
            dispatcher = new MessageDispatcher(Log);
            if (!dispatcher.RegisterAssembly(typeof(GameApplication).Assembly)) return false;
            transport.Connected += OnConnected;
            transport.Disconnected += OnDisconnected;
            transport.ReceivedPacket += OnReceivedPacket;
            transport.TransportError += OnTransportError;
            initialized = true;
            return true;
        }

        public bool Connect(string host, int port)
        {
            if (IsDisposed || !initialized || string.IsNullOrWhiteSpace(host) || port < 1 || port > ushort.MaxValue)
            {
                Log.Error("连接失败：网络组件未就绪或地址/端口无效", nameof(NetworkComponent));
                return false;
            }
            try
            {
                Log.Info("正在连接 " + host + ":" + port, nameof(NetworkComponent));
                transport.Connect(host, port);
                return true;
            }
            catch (Exception error)
            {
                Log.Error("连接失败 " + host + ":" + port, nameof(NetworkComponent), error);
                return false;
            }
        }

        public void Disconnect()
        {
            if (IsDisposed || !initialized) return;
            try { transport.Disconnect(); }
            catch (Exception error) { Log.Error("断开连接失败", nameof(NetworkComponent), error); }
        }

        public bool TrySend(MsgId id, IMessage message)
        {
            if (!IsConnected) return false;
            if (message == null)
            {
                Log.Error("不能发送空消息：" + id, nameof(NetworkComponent));
                return false;
            }
            try
            {
                transport.Send(MsgCodec.Encode(id, message));
                return true;
            }
            catch (Exception error)
            {
                Log.Error("发送消息失败：" + id, nameof(NetworkComponent), error);
                return false;
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (initialized) transport.Tick(); // 异常由组件生命周期边界记录。
        }

        void OnConnected()
        {
            Log.Info("连接成功", nameof(NetworkComponent));
            // TODO: 重构成事件系统抛出，而不是这样遍历所有通知
            NotifyConnection(true);
        }

        void OnDisconnected()
        {
            Log.Info("断开连接", nameof(NetworkComponent));
            NotifyConnection(false);
        }

        void NotifyConnection(bool connected)
        {
            if (IsDisposed) return;
            foreach (Component component in Entity.GetComponents())
            {
                if (IsDisposed) break;
                if (component.IsDisposed || !(component is INetworkListener listener)) continue;
                try
                {
                    if (connected) listener.OnConnected();
                    else listener.OnDisconnected();
                }
                catch (Exception error)
                {
                    Log.Error("连接通知失败：" + (connected ? "OnConnected" : "OnDisconnected"), component.GetType().Name, error);
                }
            }
        }

        void OnReceivedPacket(byte[] payload)
        {
            if (IsDisposed) return;
            if (!MsgCodec.TryUnpack(payload, out MsgId id, out ByteString body))
            {
                Log.Warning("解包失败", nameof(NetworkComponent));
                return;
            }
            dispatcher.Dispatch(Entity, id, body);
        }

        void OnTransportError(Exception error)
        {
            Log.Error("传输错误", nameof(NetworkComponent), error);
        }

        protected override void OnDestroy()
        {
            if (transport == null) return;
            transport.Connected -= OnConnected;
            transport.Disconnected -= OnDisconnected;
            transport.ReceivedPacket -= OnReceivedPacket;
            transport.TransportError -= OnTransportError;
            initialized = false;
            transport.Dispose();
        }
    }
}
