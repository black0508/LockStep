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
        string nickName;

        public RoomStatus Status { get; private set; }
        public uint LocalPlayerId { get; private set; }
        public IReadOnlyList<RoomPlayer> Players { get; private set; } = Array.Empty<RoomPlayer>();
        public S2CMatchStart Match { get; private set; }

        public void Init(string nickName)
        {
            network = Entity.GetComponent<NetworkComponent>();
            events = Entity.GetComponent<EventComponent>();
            this.nickName = nickName ?? "";
            events.Subscribe(NetworkConnectedEventArgs.EventId, OnConnected);
            events.Subscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
        }

        void OnConnected(object sender, GameEventArgs args)
        {
            if (!ReferenceEquals(sender, network)) return;
            Reset();
            Status = RoomStatus.Joining;
            if (!network.TrySend(MsgId.C2SJoin, new C2SJoin { NickName = nickName }))
            {
                Reset();
            }
        }

        public void ApplyJoinAck(S2CJoinAck message)
        {
            if (Status != RoomStatus.Joining) return;
            LocalPlayerId = message.PlayerId;
            Players = message.Players;
            Status = RoomStatus.Joined;
            GameLog.Info($"加入房间 playerId={LocalPlayerId} 人数={Players.Count}，等待自动开战");
        }

        public void ApplyJoinReject(S2CJoinReject message)
        {
            if (Status != RoomStatus.Joining) return;
            Reset();
            GameLog.Warning($"加入房间失败 {message.Reason}");
        }

        public void ApplyRoomUpdate(S2CRoomUpdate message)
        {
            if (Status != RoomStatus.Joined) return;
            Players = message.Players;
            GameLog.Info($"房间成员变化 人数={Players.Count}");
        }

        public void ApplyMatchStart(S2CMatchStart message)
        {
            if (Status != RoomStatus.Joined) return;
            Match = message;
            Status = RoomStatus.Playing;
            GameLog.Info($"自动开战 帧率={message.TickHz} 输入延迟={message.InputDelayFrames} seed={message.Seed}");
        }

        void Reset()
        {
            Status = RoomStatus.None;
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
