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
        if (port < 1 || port > ushort.MaxValue)
        {
            GameLog.Error($"监听端口无效：{port}");
            return false;
        }
        try
        {
            host.Start(port);
            GameLog.Info($"监听 UDP {port}");
            return true;
        }
        catch (Exception error)
        {
            GameLog.Error($"监听失败 UDP {port}", error);
            return false;
        }
    }

    // false 只表示连接已移除；编码或调用异常交给消息/生命周期边界，true 不代表对端已收到。
    public bool TrySend(int connectionId, MsgId id, IMessage message)
    {
        if (!connections.Contains(connectionId)) return false;
        var packet = new Packet { Id = id, Body = message.ToByteString() };
        host.Send(connectionId, packet.ToByteArray());
        return true;
    }

    // 会同步触发 Disconnected 事件。
    public void Disconnect(int connectionId)
    {
        if (!connections.Contains(connectionId)) return;
        host.Disconnect(connectionId);
    }

    protected override void OnUpdate(float deltaTime)
    {
        host.Tick();
    }

    void OnConnected(int connectionId)
    {
        connections.Add(connectionId);
        GameLog.Info($"连接 connection={connectionId}");
    }

    void OnDisconnected(int connectionId)
    {
        connections.Remove(connectionId);
        GameLog.Info($"断开 connection={connectionId}");
        events.FireNow(this, NetworkDisconnectedEventArgs.Create(connectionId));
    }

    void OnReceivedPacket(int connectionId, byte[] payload)
    {
        // KCP 延迟移除连接，先丢弃本轮断线后仍排队的消息。
        if (!connections.Contains(connectionId)) return;
        MsgId id = MsgId.Unspecified;
        try
        {
            Packet packet = Packet.Parser.ParseFrom(payload);
            id = packet.Id;
            dispatcher.Dispatch(Entity, connectionId, id, packet.Body);
        }
        catch (Exception error)
        {
            // 隔离当前消息，避免业务异常进入 KCP 的断线处理。
            GameLog.Error($"消息处理失败：{id} connection={connectionId}", error);
        }
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
