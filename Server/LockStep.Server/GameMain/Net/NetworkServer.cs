using System;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Server.Core;

namespace LockStep.Server.Net;

public sealed class NetworkServer : IMessageSender, IDisposable
{
    INetworkServerHost serverHost;
    PacketRouter router;

    public event Action<int> ClientConnected;
    public event Action<int> ClientDisconnected;

    public bool IsActive
    {
        get { return serverHost != null && serverHost.IsActive; }
    }

    public NetworkServer()
    {
        serverHost = new KcpServerHost();
        serverHost.Connected += OnConnected;
        serverHost.Disconnected += OnDisconnected;
        serverHost.OnReceivedpacket += OnReceivedPacket;
        serverHost.TransportError += OnHostError;
    }

    public void UseRouter(PacketRouter value)
    {
        router = value;
    }

    public void Start(int port)
    {
        serverHost.Start(port);
        Log.Info($"监听 UDP {port}");
    }

    public void Tick()
    {
        if (serverHost != null)
        {
            serverHost.Tick();
        }
    }

    public void Send(int connectionId, MsgId msgId, IMessage msg)
    {
        Send(connectionId, MsgCodec.Encode(msgId, msg));
    }

    public void Send(int connectionId, byte[] payload)
    {
        if (serverHost != null)
        {
            serverHost.Send(connectionId, payload);
        }
    }

    public void Disconnect(int connectionId)
    {
        if (serverHost != null)
        {
            serverHost.Disconnect(connectionId);
        }
    }

    public void Dispose()
    {
        if (serverHost == null)
        {
            return;
        }

        serverHost.Connected -= OnConnected;
        serverHost.Disconnected -= OnDisconnected;
        serverHost.OnReceivedpacket -= OnReceivedPacket;
        serverHost.TransportError -= OnHostError;
        serverHost.Dispose();
        serverHost = null;
    }

    void OnConnected(int connectionId)
    {
        Log.Info($"连接 connection={connectionId}");
        ClientConnected?.Invoke(connectionId);
    }

    void OnDisconnected(int connectionId)
    {
        Log.Info($"断开 connection={connectionId}");
        ClientDisconnected?.Invoke(connectionId);
    }

    void OnReceivedPacket(int connectionId, byte[] payload)
    {
        if (!MsgCodec.TryUnpack(payload, out MsgId msgId, out ByteString body))
        {
            Log.Warn($"解包失败 connection={connectionId}");
            return;
        }

        if (router == null)
        {
            Log.Warn($"路由未就绪，丢弃 {msgId} connection={connectionId}");
            return;
        }

        router.Route(connectionId, msgId, body);
    }

    void OnHostError(int connectionId, Exception error)
    {
        Log.Error($"传输错误 connection={connectionId} {error.Message}");
    }
}
