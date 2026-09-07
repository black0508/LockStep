using System;
using GameMain.Net;
using Google.Protobuf;
using kcp2k;
using Lockstep.Proto;
using UnityEngine;

namespace GameMain.Net
{
    public partial class NetworkManager : MonoBehaviour
    {
        [SerializeField] string host = "127.0.0.1";
        [SerializeField] int port = 7777;
        [SerializeField] uint roomId = 1;
        [SerializeField] KeyCode startKey = KeyCode.Return;

        INetworkTransport transport;
        bool isHost;

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

        public void Send(MsgId msgId, IMessage msg)
        {
            if (transport == null)
            {
                return;
            }

            transport.Send(MsgCodec.Encode(msgId, msg));
        }

        public void RequestStart()
        {
            Send(MsgId.C2SStart, new C2SStart());
        }

        void OnConnected()
        {
            Debug.Log("[LockStep] 连接成功");
            Send(MsgId.C2SJoin, new C2SJoin { RoomId = roomId });
        }

        void OnDisconnected()
        {
            isHost = false;
            Debug.Log("[LockStep] 断开连接");
        }

        void OnReceivedPacket(byte[] payload)
        {
            if (!MsgCodec.TryUnpack(payload, out MsgId msgId, out ByteString body))
            {
                Debug.LogWarning("[LockStep] 解包失败");
                return;
            }

            Dispatch(msgId, body);
        }

        void OnTransportError(Exception error)
        {
            Debug.LogError("[LockStep] 传输错误 " + error.Message);
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
}