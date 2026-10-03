using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.FrameSync.Events
{
    // 联网时收到并执行完一帧权威帧后发布。
    public sealed class FrameReceivedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(FrameReceivedEventArgs).GetHashCode();
        public override int Id => EventId;
        public S2CFrame Frame { get; private set; }

        public static FrameReceivedEventArgs Create(S2CFrame frame)
        {
            var args = ReferencePool.Acquire<FrameReceivedEventArgs>();
            args.Frame = frame;
            return args;
        }

        public override void Clear() { Frame = null; }
    }
}
