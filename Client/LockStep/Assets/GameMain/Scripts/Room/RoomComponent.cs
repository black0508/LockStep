using Lockstep.Proto;
using Framework;

namespace GameMain
{
    // 连接后自动进房，服务端开战后启动帧同步，断线即结束本局回到空闲。
    public sealed class RoomComponent : Component
    {
        string nickName;

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
            GameEntry.Application.Data.Phase = GamePhase.Connecting;
        }

        public void ApplyJoinAck(S2CJoinAck message)
        {
            GameData data = GameEntry.Application.Data;
            if (data.Phase != GamePhase.Joining) return;
            data.LocalPlayerId = message.PlayerId;
            data.Phase = GamePhase.Joined;
            GameLog.Info($"加入房间 playerId={data.LocalPlayerId} 人数={message.Players.Count}，等待自动开战");
        }

        public void ApplyJoinReject(S2CJoinReject message)
        {
            if (GameEntry.Application.Data.Phase != GamePhase.Joining) return;
            GameLog.Warning($"加入房间失败 {message.Reason}");
            Leave(RoomLeaveReason.JoinRejected, message.Reason);
        }

        public void ApplyRoomUpdate(S2CRoomUpdate message)
        {
            if (GameEntry.Application.Data.Phase != GamePhase.Joined) return;
            GameLog.Info($"房间成员变化 人数={message.Players.Count}");
        }

        public void ApplyMatchStart(S2CMatchStart message)
        {
            GameData data = GameEntry.Application.Data;
            if (data.Phase != GamePhase.Joined) return;
            data.Phase = GamePhase.Playing;
            data.IsOnline = true;
            GameLog.Info($"自动开战 人数={message.Players.Count} seed={message.Seed}");
            GameEntry.Application.Units.Start(message.Players);
            GameEntry.Application.FrameSync.Start();
            GameEntry.Application.Events.FireNow(this, MatchStartedEventArgs.Create(message));
        }

        void OnConnected(object sender, GameEventArgs args)
        {
            GameData data = GameEntry.Application.Data;
            if (data.Phase != GamePhase.Connecting) return;
            if (GameEntry.Application.Network.TrySend(MsgId.C2SJoin, new C2SJoin { NickName = nickName })) data.Phase = GamePhase.Joining;
            else Leave(RoomLeaveReason.JoinSendFailed, JoinRejectReason.JoinRejectUnspecified);
        }

        void OnDisconnected(object sender, GameEventArgs args)
        {
            Reset(GameEntry.Application.Data.Phase == GamePhase.Playing ? RoomLeaveReason.MatchDisconnected : RoomLeaveReason.ConnectionLost,
                JoinRejectReason.JoinRejectUnspecified);
        }

        public void Leave()
        {
            Leave(GameEntry.Application.Data.Phase == GamePhase.Playing ? RoomLeaveReason.MatchQuit : RoomLeaveReason.Cancelled,
                JoinRejectReason.JoinRejectUnspecified);
        }

        void Leave(RoomLeaveReason reason, JoinRejectReason rejectReason)
        {
            // 先置为空闲，主动断线的同步回调就不会再次清理会话。
            Reset(reason, rejectReason);
            GameEntry.Application.Network.Disconnect();
        }

        // 只停止本组件启动的联网模拟。回放会话由 ReplayComponent 结束。
        void Reset(RoomLeaveReason reason, JoinRejectReason rejectReason)
        {
            GameData data = GameEntry.Application.Data;
            if (data.Phase == GamePhase.None || data.Phase == GamePhase.Replay) return;
            if (data.Phase == GamePhase.Playing)
            {
                GameEntry.Application.FrameSync.Stop();
                GameEntry.Application.Units.Stop();
            }
            data.Clear();
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
