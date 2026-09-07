using Google.Protobuf;
using Lockstep.Proto;
using UnityEngine;

namespace GameMain.Net
{
    public partial class NetworkManager : MonoBehaviour
    {
        void Dispatch(MsgId msgId, ByteString body)
        {
            switch (msgId)
            {
                case MsgId.S2CHello:
                    OnS2CHello(MsgCodec.Parse(body, S2CHello.Parser));
                    break;
                case MsgId.S2CJoinAck:
                    OnS2CJoinAck(MsgCodec.Parse(body, S2CJoinAck.Parser));
                    break;
                case MsgId.S2CJoinReject:
                    OnS2CJoinReject(MsgCodec.Parse(body, S2CJoinReject.Parser));
                    break;
                case MsgId.S2CRoomUpdate:
                    OnS2CRoomUpdate(MsgCodec.Parse(body, S2CRoomUpdate.Parser));
                    break;
                case MsgId.S2CMatchStart:
                    OnS2CMatchStart(MsgCodec.Parse(body, S2CMatchStart.Parser));
                    break;
                default:
                    Debug.LogWarning("[LockStep] 未知消息 " + msgId);
                    break;
            }
        }

        void OnS2CHello(S2CHello msg)
        {
            Debug.Log("[LockStep] S2C_Hello " + msg.Text);
        }

        void OnS2CJoinAck(S2CJoinAck msg)
        {
            isHost = msg.IsHost;
            Debug.Log("[LockStep] JoinAck room=" + msg.RoomId + " player=" + msg.PlayerId + " host=" + msg.IsHost + " count=" + msg.Players.Count);
        }

        void OnS2CJoinReject(S2CJoinReject msg)
        {
            Debug.LogWarning("[LockStep] JoinReject " + msg.Reason);
        }

        void OnS2CRoomUpdate(S2CRoomUpdate msg)
        {
            for (int i = 0; i < msg.Players.Count; i++)
            {
                if (msg.Players[i].IsHost)
                {
                    isHost = msg.Players[i].PlayerId == LocalPlayerId(msg);
                    break;
                }
            }

            Debug.Log("[LockStep] RoomUpdate room=" + msg.RoomId + " count=" + msg.Players.Count + " host=" + isHost);
        }

        void OnS2CMatchStart(S2CMatchStart msg)
        {
            Debug.Log("[LockStep] MatchStart tick=" + msg.TickHz + " delay=" + msg.InputDelayFrames + " seed=" + msg.Seed);
        }

        static uint LocalPlayerId(S2CRoomUpdate msg)
        {
            return 0;
        }
    }
}
