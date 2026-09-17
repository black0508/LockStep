using System;
using System.Collections.Generic;
using GameMain.Net;
using GameMain.Net.Events;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Room
{
    public enum RoomStatus { None, Joining, Joined, Playing }

    // 连接后自动进房，人数到齐后等待服务端开战；不负责输入或帧同步推进。
    public sealed class RoomComponent : Component
    {
        NetworkComponent network;
        EventComponent events;
        uint targetRoomId;
        string nickName;

        public RoomStatus Status { get; private set; }
        public uint RoomId { get; private set; }
        public uint LocalPlayerId { get; private set; }
        public IReadOnlyList<RoomPlayer> Players { get; private set; } = Array.Empty<RoomPlayer>();
        public S2CMatchStart Match { get; private set; }

        public void Init(uint roomId, string nickName)
        {
            network = Entity.GetComponent<NetworkComponent>();
            events = Entity.GetComponent<EventComponent>();
            targetRoomId = roomId;
            this.nickName = nickName ?? "";
            events.Subscribe(NetworkConnectedEventArgs.EventId, OnConnected);
            events.Subscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
        }

        void OnConnected(object sender, GameEventArgs args)
        {
            if (!ReferenceEquals(sender, network)) return;
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
            GameLog.Info("加入房间 房间=" + RoomId + " playerId=" + LocalPlayerId + " 人数=" + Players.Count + "，等待自动开战", nameof(RoomComponent));
        }

        public void ApplyJoinReject(S2CJoinReject message)
        {
            if (Status != RoomStatus.Joining) return;
            Reset();
            GameLog.Warning("加入房间失败 " + message.Reason, nameof(RoomComponent));
        }

        public void ApplyRoomUpdate(S2CRoomUpdate message)
        {
            if (Status != RoomStatus.Joined || message.RoomId != RoomId) return;
            Players = message.Players;
            GameLog.Info("房间成员变化 房间=" + RoomId + " 人数=" + Players.Count, nameof(RoomComponent));
        }

        public void ApplyMatchStart(S2CMatchStart message)
        {
            if (Status != RoomStatus.Joined) return;
            Match = message;
            Status = RoomStatus.Playing;
            GameLog.Info("自动开战 房间=" + RoomId + " 帧率=" + message.TickHz + " 输入延迟=" + message.InputDelayFrames + " seed=" + message.Seed, nameof(RoomComponent));
        }

        void Reset()
        {
            Status = RoomStatus.None;
            RoomId = 0;
            LocalPlayerId = 0;
            Players = Array.Empty<RoomPlayer>();
            Match = null;
        }

        void OnDisconnected(object sender, GameEventArgs args)
        {
            if (!ReferenceEquals(sender, network)) return;
            Reset();
        }

        protected override void OnDestroy()
        {
            events?.Unsubscribe(NetworkConnectedEventArgs.EventId, OnConnected);
            events?.Unsubscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
            Reset();
            events = null;
            network = null;
        }
    }
}
