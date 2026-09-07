using System;
using Google.Protobuf;
using Lockstep.Proto;

namespace LockStep.Server.Net;

public partial class NetworkServer
{
    void Dispatch(int clientId, MsgId msgId, ByteString body)
    {
        switch (msgId)
        {
            case MsgId.C2SHello:
                OnC2SHello(clientId, MsgCodec.Parse(body, C2SHello.Parser));
                break;
            default:
                Console.WriteLine("[LockStep] 未知消息 " + msgId + " connection=" + clientId);
                break;
        }
    }

    void OnC2SHello(int clientId, C2SHello msg)
    {
        Console.WriteLine("[LockStep] C2S_Hello connection=" + clientId + " " + msg.Text);
        Send(clientId, MsgId.S2CHello, new S2CHello { Text = "echo " + msg.Text });
    }
}
