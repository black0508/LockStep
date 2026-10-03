using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Net.Events;
using GameMain.Room.Events;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Room
{
    // 连接后自动进房，服务端开战后启动帧同步，断线即结束本局回到空闲。
    public sealed class RoomComponent : Component
    {
        public enum Status { None, Connecting, Joining, Joined, Playing }

        string nickName;

        public Status Phase { get; private set; }
        public uint LocalPlayerId { get; private set; }

        protected override void OnAwake()
        {
            EventComponent events = GameEntry.Application.Events;
            events.Subscribe(NetworkConnectedEventArgs.EventId, OnConnected);
            events.Subscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
        }

        public void Init(string value)
        {
            nickName = value;
        }

        public void BeginJoin()
        {
            Phase = Status.Connecting;
        }

        public void ApplyJoinAck(S2CJoinAck message)
        {
            if (Phase != Status.Joining) return;
            LocalPlayerId = message.PlayerId;
            Phase = Status.Joined;
            GameLog.Info($"加入房间 playerId={LocalPlayerId} 人数={message.Players.Count}，等待自动开战");
        }

        public void ApplyJoinReject(S2CJoinReject message)
        {
            if (Phase != Status.Joining) return;
            GameLog.Warning($"加入房间失败 {message.Reason}");
            Leave(RoomLeaveReason.JoinRejected, message.Reason);
        }

        public void ApplyRoomUpdate(S2CRoomUpdate message)
        {
            if (Phase != Status.Joined) return;
            GameLog.Info($"房间成员变化 人数={message.Players.Count}");
        }

        public void ApplyMatchStart(S2CMatchStart message)
        {
            if (Phase != Status.Joined) return;
            Phase = Status.Playing;
            GameLog.Info($"自动开战 人数={message.Players.Count} seed={message.Seed}");
            GameEntry.Application.FrameSync.Start(message.Players, LocalPlayerId, true);
            GameEntry.Application.Events.FireNow(this, MatchStartedEventArgs.Create(message, LocalPlayerId));
        }

        void OnConnected(object sender, GameEventArgs args)
        {
            if (Phase != Status.Connecting) return;
            if (GameEntry.Application.Network.TrySend(MsgId.C2SJoin, new C2SJoin { NickName = nickName })) Phase = Status.Joining;
            else Leave(RoomLeaveReason.JoinSendFailed, JoinRejectReason.JoinRejectUnspecified);
        }

        void OnDisconnected(object sender, GameEventArgs args)
        {
            Reset(Phase == Status.Playing ? RoomLeaveReason.MatchDisconnected : RoomLeaveReason.ConnectionLost,
                JoinRejectReason.JoinRejectUnspecified);
        }

        public void Leave()
        {
            Leave(Phase == Status.Playing ? RoomLeaveReason.MatchQuit : RoomLeaveReason.Cancelled,
                JoinRejectReason.JoinRejectUnspecified);
        }

        void Leave(RoomLeaveReason reason, JoinRejectReason rejectReason)
        {
            // 先置为空闲，主动断线的同步回调就不会再次清理会话。
            Reset(reason, rejectReason);
            GameEntry.Application.Network.Disconnect();
        }

        // 只停止本组件启动的联网模拟。
        void Reset(RoomLeaveReason reason, JoinRejectReason rejectReason)
        {
            if (Phase == Status.None) return;
            if (Phase == Status.Playing) GameEntry.Application.FrameSync.Stop();
            Phase = Status.None;
            LocalPlayerId = 0;
            GameEntry.Application.Events.FireNow(this, RoomLeftEventArgs.Create(reason, rejectReason));
        }

        protected override void OnDestroy()
        {
            EventComponent events = GameEntry.Application.Events;
            events.Unsubscribe(NetworkConnectedEventArgs.EventId, OnConnected);
            events.Unsubscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
        }
    }
}
