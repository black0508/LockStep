using System;
using GameMain.Net;
using kcp2k;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    [SerializeField] string host = "127.0.0.1";
    [SerializeField] int port = 7777;

    INetworkTransport transport;

    public bool IsConnected
    {
        get { return transport != null && transport.IsConnected; }
    }

    void Start()
    {
        Log.Info = Debug.Log;
        Log.Warning = Debug.LogWarning;
        Log.Error = Debug.LogError;

        transport = new KcpClientTransport();
        transport.Connected += OnConnected;
        transport.Disconnected += OnDisconnected;
        transport.OnReceivedpacket += OnReceivedPacket;
        transport.TransportError += OnTransportError;

        Debug.Log("[LockStep] connecting " + host + ":" + port);
        transport.Connect(host, port);
    }

    void Update()
    {
        if (transport != null)
        {
            transport.Tick();
        }
    }

    public void Send(byte[] payload)
    {
        if (transport != null)
        {
            transport.Send(payload);
        }
    }

    void OnConnected()
    {
        Debug.Log("[LockStep] connected");
    }

    void OnDisconnected()
    {
        Debug.Log("[LockStep] disconnected");
    }

    void OnReceivedPacket(byte[] payload)
    {
        Debug.Log("[LockStep] recv " + payload.Length + " bytes");
    }

    void OnTransportError(Exception error)
    {
        Debug.LogError("[LockStep] transport error " + error.Message);
    }

    void OnDisable()
    {
        StopClient();
    }

    void OnApplicationQuit()
    {
        StopClient();
    }

    void OnDestroy()
    {
        StopClient();
    }

    void StopClient()
    {
        if (transport == null)
        {
            return;
        }

        transport.Connected -= OnConnected;
        transport.Disconnected -= OnDisconnected;
        transport.OnReceivedpacket -= OnReceivedPacket;
        transport.TransportError -= OnTransportError;
        transport.Dispose();
        transport = null;
    }
}
