using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Room.Events
{
    public enum RoomLeaveReason
    {
        Cancelled,          // 开战前主动离开
        MatchQuit,          // 对局中主动离开
        JoinRejected,       // 服务端拒绝进房，原因见 RejectReason
        JoinSendFailed,     // 连接后发送进房请求时已断开
        ConnectionLost,     // 开战前连接失败或断开
        MatchDisconnected,  // 对局中连接断开
    }

    // 房间从非空闲回到空闲时发布一次，本地会话及其模拟已结束。
    public sealed class RoomLeftEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(RoomLeftEventArgs).GetHashCode();
        public override int Id => EventId;
        public RoomLeaveReason Reason { get; private set; }
        public JoinRejectReason RejectReason { get; private set; }

        public static RoomLeftEventArgs Create(RoomLeaveReason reason, JoinRejectReason rejectReason)
        {
            var args = ReferencePool.Acquire<RoomLeftEventArgs>();
            args.Reason = reason;
            args.RejectReason = rejectReason;
            return args;
        }

        public override void Clear()
        {
            Reason = RoomLeaveReason.Cancelled;
            RejectReason = JoinRejectReason.JoinRejectUnspecified;
        }
    }
}
