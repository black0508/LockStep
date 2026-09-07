using System;
using GameMain;
using Google.Protobuf;
using Lockstep.Proto;
using UnityEngine;

namespace GameMain.Net
{
    public partial class NetworkManager : ManagerBase
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

        void OnConnected()
        {
            Debug.Log("[LockStep] 连接成功");
            GameEntry.RoomManager.OnConnected();
        }

        void OnDisconnected()
        {
            Debug.Log("[LockStep] 断开连接");
            GameEntry.RoomManager.OnDisconnected();
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

        protected override void OnDestroy()
        {
            StopClient();
            base.OnDestroy();
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
