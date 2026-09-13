using GameMain;
using GameMain.Net;
using Lockstep.Proto;
using UnityEngine;

namespace GameMain.Room
{
    public sealed class RoomManager : ManagerBase
    {
        [SerializeField] uint roomId = 1;
        [SerializeField] string nickName = "player";

        bool isHost;
        uint localPlayerId;
        uint joinedRoomId;

        void Update()
        {
            // 开战资格由服务端裁定，被拒时会回 S2CStartReject
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GameEntry.NetworkManager.Send(MsgId.C2SStart, new C2SStart());
            }
        }

        public void OnConnected()
        {
            GameEntry.NetworkManager.Send(MsgId.C2SJoin, new C2SJoin { RoomId = roomId, NickName = nickName });
        }

        public void OnDisconnected()
        {
            if (localPlayerId != 0)
            {
                Debug.Log("退出房间 房间=" + joinedRoomId + " playerId=" + localPlayerId + " 昵称=" + nickName);
            }

            isHost = false;
            localPlayerId = 0;
            joinedRoomId = 0;
        }

        public void OnJoinAck(S2CJoinAck msg)
        {
            joinedRoomId = msg.RoomId;
            localPlayerId = msg.PlayerId;
            isHost = msg.IsHost;
            Debug.Log("加入房间成功 房间=" + msg.RoomId + " playerId=" + msg.PlayerId + " 昵称=" + nickName + " 房主=" + isHost + " 人数=" + msg.Players.Count);
        }

        public void OnJoinReject(S2CJoinReject msg)
        {
            Debug.LogWarning("加入房间失败 " + msg.Reason);
        }

        public void OnRoomUpdate(S2CRoomUpdate msg)
        {
            for (int i = 0; i < msg.Players.Count; i++)
            {
                if (msg.Players[i].PlayerId == localPlayerId)
                {
                    isHost = msg.Players[i].IsHost;
                    break;
                }
            }
        }

        public void OnMatchStart(S2CMatchStart msg)
        {
            Debug.Log("开始游戏 房间=" + joinedRoomId + " 帧率=" + msg.TickHz + " 输入延迟=" + msg.InputDelayFrames + " seed=" + msg.Seed);
        }

        public void OnStartReject(S2CStartReject msg)
        {
            Debug.LogWarning("开战失败 " + msg.Reason);
        }
    }
}
