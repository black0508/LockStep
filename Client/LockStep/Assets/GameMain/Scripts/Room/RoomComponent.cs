using System;
using System.Collections.Generic;
using GameMain.Net;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Room
{
    public enum RoomStatus { None, Joining, Joined, Playing }

    // 连接后自动进房，人数到齐后等待服务端开战；不负责输入或帧同步推进。
    public sealed class RoomComponent : Component, INetworkListener
    {
        NetworkComponent network;
        uint targetRoomId;
        string nickName;

        public RoomStatus Status { get; private set; }
        public uint RoomId { get; private set; }
        public uint LocalPlayerId { get; private set; }
        public IReadOnlyList<RoomPlayer> Players { get; private set; } = Array.Empty<RoomPlayer>();
        public S2CMatchStart Match { get; private set; }

        public bool Init(uint roomId, string nickName)
        {
            if (IsDisposed || Entity == null || network != null)
            {
                Log.Error("房间组件不可用或已经初始化", nameof(RoomComponent));
                return false;
            }
            network = Entity.GetComponent<NetworkComponent>();
            if (network == null)
            {
                Log.Error("房间需要同实体上的 NetworkComponent", nameof(RoomComponent));
                return false;
            }
            targetRoomId = roomId;
            this.nickName = nickName ?? "";
            return true;
        }

        public void OnConnected()
        {
            if (network == null) return;
            Reset();
            Status = RoomStatus.Joining;
            if (!network.TrySend(MsgId.C2SJoin, new C2SJoin { RoomId = targetRoomId, NickName = nickName }))
            {
                Reset();
            }
        }

        public void ApplyJoinAck(S2CJoinAck message)
        {
            if (Status != RoomStatus.Joining || message.RoomId != targetRoomId) return;
            RoomId = message.RoomId;
            LocalPlayerId = message.PlayerId;
            Players = message.Players;
            Status = RoomStatus.Joined;
            Log.Info("加入房间 房间=" + RoomId + " playerId=" + LocalPlayerId + " 人数=" + Players.Count + "，等待自动开战", nameof(RoomComponent));
        }

        public void ApplyJoinReject(S2CJoinReject message)
        {
            if (Status != RoomStatus.Joining) return;
            Reset();
            Log.Warning("加入房间失败 " + message.Reason, nameof(RoomComponent));
        }

        public void ApplyRoomUpdate(S2CRoomUpdate message)
        {
            if (Status != RoomStatus.Joined || message.RoomId != RoomId) return;
            Players = message.Players;
            Log.Info("房间成员变化 房间=" + RoomId + " 人数=" + Players.Count, nameof(RoomComponent));
        }

        public void ApplyMatchStart(S2CMatchStart message)
        {
            if (Status != RoomStatus.Joined) return;
            Match = message;
            Status = RoomStatus.Playing;
            Log.Info("自动开战 房间=" + RoomId + " 帧率=" + message.TickHz + " 输入延迟=" + message.InputDelayFrames + " seed=" + message.Seed, nameof(RoomComponent));
        }

        void Reset()
        {
            Status = RoomStatus.None;
            RoomId = 0;
            LocalPlayerId = 0;
            Players = Array.Empty<RoomPlayer>();
            Match = null;
        }

        public void OnDisconnected() { Reset(); }
        protected override void OnDestroy() { Reset(); }
    }
}
