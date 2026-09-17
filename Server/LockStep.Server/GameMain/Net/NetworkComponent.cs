using System;
using System.Collections.Generic;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;
using LockStep.Server.Net.Events;

namespace LockStep.Server.Net;

public sealed class NetworkComponent : Component
{
    readonly HashSet<int> connections = new HashSet<int>();
    INetworkServerHost host;
    MessageDispatcher dispatcher;
    EventComponent events;
    ReferencePoolComponent pool;

    public bool IsActive => !IsDisposed && host != null && host.IsActive;

    public void Init(INetworkServerHost value)
    {
        if (IsDisposed || Entity == null || host != null)
            throw new InvalidOperationException("网络组件不可用或已经初始化");
        host = value ?? throw new ArgumentNullException(nameof(value));
        events = Entity.GetComponent<EventComponent>();
        pool = Entity.GetComponent<ReferencePoolComponent>();
        if (events == null || pool == null)
            throw new InvalidOperationException("网络组件需要同实体上的事件和引用池组件");
        dispatcher = new MessageDispatcher();
        dispatcher.RegisterAssembly(typeof(GameApplication).Assembly);
        host.Connected += OnConnected;
        host.Disconnected += OnDisconnected;
        host.ReceivedPacket += OnReceivedPacket;
        host.TransportError += OnTransportError;
    }

    public bool Start(int port)
    {
        if (IsDisposed || host == null || port < 1 || port > ushort.MaxValue)
        {
            GameLog.Error("监听失败：网络组件未就绪或端口无效");
            return false;
        }
        if (IsActive) return true;
        try
        {
            host.Start(port);
            if (!IsActive)
            {
                GameLog.Error("传输层未进入监听状态");
                return false;
            }
            GameLog.Info($"监听 UDP {port}");
            return true;
        }
        catch (Exception error)
        {
            GameLog.Error($"监听失败 UDP {port}", error);
            return false;
        }
    }

    public bool IsConnected(int connectionId) => IsActive && connections.Contains(connectionId);

    public bool TrySend(int connectionId, MsgId id, IMessage message)
    {
        if (!IsConnected(connectionId)) return false;
        if (message == null)
        {
            GameLog.Error($"不能发送空消息：{id}");
            return false;
        }
        try
        {
            host.Send(connectionId, MsgCodec.Encode(id, message));
            return true;
        }
        catch (Exception error)
        {
            GameLog.Error($"发送失败：{id} connection={connectionId}", error);
            return false;
        }
    }

    public void Disconnect(int connectionId)
    {
        if (!IsConnected(connectionId)) return;
        try { host.Disconnect(connectionId); }
        catch (Exception error)
        {
            GameLog.Error($"断开失败 connection={connectionId}", error);
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (IsActive) host.Tick();
    }

    void OnConnected(int connectionId)
    {
        if (IsDisposed) return;
        connections.Add(connectionId);
        GameLog.Info($"连接 connection={connectionId}");
        events.FireNow(this, NetworkConnectedEventArgs.Create(pool, connectionId));
    }

    void OnDisconnected(int connectionId)
    {
        if (IsDisposed) return;
        connections.Remove(connectionId);
        GameLog.Info($"断开 connection={connectionId}");
        events.FireNow(this, NetworkDisconnectedEventArgs.Create(pool, connectionId));
    }

    void OnReceivedPacket(int connectionId, byte[] payload)
    {
        if (!IsConnected(connectionId)) return;
        if (!MsgCodec.TryUnpack(payload, out MsgId id, out ByteString body))
        {
            GameLog.Warning($"解包失败 connection={connectionId}");
            return;
        }
        dispatcher.Dispatch(Entity, connectionId, id, body);
    }

    void OnTransportError(int connectionId, Exception error)
    {
        GameLog.Error($"传输错误 connection={connectionId}", error);
    }

    protected override void OnDestroy()
    {
        connections.Clear();
        if (host == null) return;
        host.Connected -= OnConnected;
        host.Disconnected -= OnDisconnected;
        host.ReceivedPacket -= OnReceivedPacket;
        host.TransportError -= OnTransportError;
        host.Dispose();
    }
}
