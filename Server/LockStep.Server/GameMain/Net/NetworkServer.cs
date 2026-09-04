using System;
using kcp2k;
using LockStep.Server.Config;

namespace LockStep.Server.Net;

// 这个类主要是对服务器主机进行包装，并且路由各个客户端消息Handle
public class NetworkServer : IDisposable
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
        Console.WriteLine("[LockStep] 接收到 " + payload.Length + " bytes connection=" + clientId);
    }

    void OnHostError(int clientId, Exception error)
    {
        Console.WriteLine("[LockStep] 错误 connection=" + clientId + " " + error.Message);
    }
}