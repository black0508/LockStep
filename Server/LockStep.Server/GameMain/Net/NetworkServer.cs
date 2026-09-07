using System;
using Google.Protobuf;
using kcp2k;
using Lockstep.Proto;
using LockStep.Server;

namespace LockStep.Server.Net;

public partial class NetworkServer : ManagerBase, IDisposable
{
    INetworkServerHost serverHost;

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
        Register();
    }

    public void Start(int port)
    {
        serverHost.Start(port);
        Console.WriteLine("[LockStep] 监听 UDP " + port);
    }

    public void Tick()
    {
        if (serverHost != null)
        {
            serverHost.Tick();
        }
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
        GameEntry.Unregister(this);
    }

    void OnConnected(int clientId)
    {
        Console.WriteLine("[LockStep] 连接 =" + clientId);
    }

    void OnDisconnected(int clientId)
    {
        Console.WriteLine("[LockStep] 断开连接 =" + clientId);
        GameEntry.RoomManager?.HandleDisconnect(clientId);
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
