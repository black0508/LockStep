using System;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Server;

namespace LockStep.Server.Net;

public partial class NetworkServer
{
    void Dispatch(int clientId, MsgId msgId, ByteString body)
    {
        switch (msgId)
        {
            case MsgId.C2SJoin:
                GameEntry.RoomManager?.HandleJoin(clientId, MsgCodec.Parse(body, C2SJoin.Parser));
                break;
            case MsgId.C2SStart:
                GameEntry.RoomManager?.HandleStart(clientId);
                break;
            default:
                Console.WriteLine("[LockStep] 未知消息 " + msgId + " connection=" + clientId);
                break;
        }
    }
}
