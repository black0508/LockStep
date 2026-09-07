using System;
using Google.Protobuf;
using kcp2k;
using Lockstep.Proto;
using LockStep.Server.Config;

namespace LockStep.Server.Net;

public partial class NetworkServer : IDisposable
{
    INetworkServerHost serverHost;
    private readonly ServerConfig config;
    public bool IsActive
    {
        get { return serverHost != null && serverHost.IsActive; }
    }

    public NetworkServer(ServerConfig config)
    {
        this.config = config;
        serverHost = new KcpServerHost();
        serverHost.Connected += OnConnected;
        serverHost.Disconnected += OnDisconnected;
        serverHost.OnReceivedpacket += OnReceivedPacket;
        serverHost.TransportError += OnHostError;
    }

    public void Start(int port)
    {
        serverHost.Start(port);
        Console.WriteLine("[LockStep] 监听 UDP " + port);
    }

    public void Tick()
    {
        if (serverHost == null)
        {
            return;
        }

        serverHost.Tick();
    }

    public void Send(int clientId, MsgId msgId, IMessage msg)
    {
        Send(clientId, MsgCodec.Encode(msgId, msg));
    }

    public void Send(int clientId, byte[] payload)
    {
        if (serverHost != null)
        {
            serverHost.Send(clientId, payload);
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

    void OnConnected(int clientId)
    {
        Console.WriteLine("[LockStep] 连接 =" + clientId);
    }

    void OnDisconnected(int clientId)
    {
        Console.WriteLine("[LockStep] 断开连接 =" + clientId);
    }

    void OnReceivedPacket(int clientId, byte[] payload)
    {
        if (!MsgCodec.TryUnpack(payload, out MsgId msgId, out ByteString body))
        {
            Console.WriteLine("[LockStep] 解包失败 connection=" + clientId);
            return;
        }

        Dispatch(clientId, msgId, body);
    }

    void OnHostError(int clientId, Exception error)
    {
        Console.WriteLine("[LockStep] 错误 connection=" + clientId + " " + error.Message);
    }
}
