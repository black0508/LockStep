using GameMain;
using Google.Protobuf;
using Lockstep.Proto;
using UnityEngine;

namespace GameMain.Net
{
    public partial class NetworkManager
    {
        void Dispatch(MsgId msgId, ByteString body)
        {
            switch (msgId)
            {
                case MsgId.S2CJoinAck:
                    GameEntry.RoomManager.OnJoinAck(MsgCodec.Parse(body, S2CJoinAck.Parser));
                    break;
                case MsgId.S2CJoinReject:
                    GameEntry.RoomManager.OnJoinReject(MsgCodec.Parse(body, S2CJoinReject.Parser));
                    break;
                case MsgId.S2CRoomUpdate:
                    GameEntry.RoomManager.OnRoomUpdate(MsgCodec.Parse(body, S2CRoomUpdate.Parser));
                    break;
                case MsgId.S2CMatchStart:
                    GameEntry.RoomManager.OnMatchStart(MsgCodec.Parse(body, S2CMatchStart.Parser));
                    break;
                default:
                    Debug.LogWarning("[LockStep] 未知消息 " + msgId);
                    break;
            }
        }
    }
}
