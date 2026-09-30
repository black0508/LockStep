using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Net.Events;
using GameMain.Replay;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Room
{
    // 连接后自动进房，服务端开战后启动帧同步，断线即结束本局回到空闲。
    public sealed class RoomComponent : Component
    {
        public enum Status { None, Connecting, Joining, Joined, Playing }

        NetworkComponent network;
        EventComponent events;
        FrameSyncComponent frameSync;
        ReplayComponent replay;
        string nickName;

        public Status Phase { get; private set; }
        public uint LocalPlayerId { get; private set; }
        public string Message { get; private set; } = "";

        protected override void OnAwake()
        {
            network = Entity.GetComponent<NetworkComponent>();
            events = Entity.GetComponent<EventComponent>();
            frameSync = Entity.GetComponent<FrameSyncComponent>();
            replay = Entity.GetComponent<ReplayComponent>();
            events.Subscribe(NetworkConnectedEventArgs.EventId, OnConnected);
            events.Subscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
        }

        public void Init(string value)
        {
            nickName = value;
        }

        public void BeginJoin()
        {
            Message = "";
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
            Message = $"加入房间失败：{message.Reason}";
            Leave();
            GameLog.Warning($"加入房间失败 {message.Reason}");
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
            GameLog.Info($"自动开战 人数={message.Players.Count} 帧率={message.TickHz} seed={message.Seed}");
            frameSync.Start(message.Players, LocalPlayerId, message.TickHz, FrameSyncComponent.SimulationMode.Online);
            replay.BeginRecording(message, LocalPlayerId);
        }

        void OnConnected(object sender, GameEventArgs args)
        {
            if (Phase != Status.Connecting) return;
            if (network.TrySend(MsgId.C2SJoin, new C2SJoin { NickName = nickName })) Phase = Status.Joining;
            else
            {
                Message = "进房失败：连接已断开";
                Leave();
            }
        }

        void OnDisconnected(object sender, GameEventArgs args)
        {
            if (Phase == Status.None) return;
            Message = Phase == Status.Playing ? "连接已断开，对局结束" : "连接失败或已断开";
            Reset();
        }

        public void Leave()
        {
            // 先置为空闲，主动断线的同步回调就不会再次清理会话。
            Reset();
            network.Disconnect();
        }

        void Reset()
        {
            replay.FinishRecording();
            if (frameSync.Mode == FrameSyncComponent.SimulationMode.Online) frameSync.Stop();
            Phase = Status.None;
            LocalPlayerId = 0;
        }

        protected override void OnDestroy()
        {
            events.Unsubscribe(NetworkConnectedEventArgs.EventId, OnConnected);
            events.Unsubscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
        }
    }
}
