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

    protected override void OnAwake()
    {
        events = Entity.GetComponent<EventComponent>();
    }

    // 从此处接管 host，销毁组件时统一释放。
    public void Init(INetworkServerHost value)
    {
        host = value;
        dispatcher = new MessageDispatcher();
        dispatcher.RegisterAssembly(typeof(GameApplication).Assembly);
        host.Connected += OnConnected;
        host.Disconnected += OnDisconnected;
        host.ReceivedPacket += OnReceivedPacket;
        host.TransportError += OnTransportError;
    }

    public bool Start(int port)
    {
        try
        {
            host.Start(port);
        }
        catch (Exception error)
        {
            GameLog.Error($"监听失败 UDP {port}", error);
            return false;
        }
        if (!host.IsActive)
        {
            GameLog.Error($"监听失败 UDP {port}");
            return false;
        }
        GameLog.Info($"监听 UDP {port}");
        return true;
    }

    public bool TrySend(int connectionId, MsgId id, IMessage message)
    {
        if (!connections.Contains(connectionId)) return false;
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

    // 会同步触发 Disconnected 事件。
    public void Disconnect(int connectionId)
    {
        if (!connections.Contains(connectionId)) return;
        try { host.Disconnect(connectionId); }
        catch (Exception error)
        {
            GameLog.Error($"断开失败 connection={connectionId}", error);
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        host.Tick();
    }

    void OnConnected(int connectionId)
    {
        if (IsDisposed) return;
        connections.Add(connectionId);
        GameLog.Info($"连接 connection={connectionId}");
        events.FireNow(this, NetworkConnectedEventArgs.Create(connectionId));
    }

    void OnDisconnected(int connectionId)
    {
        if (IsDisposed) return;
        connections.Remove(connectionId);
        GameLog.Info($"断开 connection={connectionId}");
        events.FireNow(this, NetworkDisconnectedEventArgs.Create(connectionId));
    }

    void OnReceivedPacket(int connectionId, byte[] payload)
    {
        if (!connections.Contains(connectionId)) return;
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
