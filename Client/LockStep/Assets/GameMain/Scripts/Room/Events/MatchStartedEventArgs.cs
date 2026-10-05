using Lockstep.Proto;
using Framework;

namespace GameMain
{
    // 联网对局的模拟已启动后发布。
    public sealed class MatchStartedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(MatchStartedEventArgs).GetHashCode();
        public override int Id => EventId;
        public S2CMatchStart Start { get; private set; }

        public static MatchStartedEventArgs Create(S2CMatchStart start)
        {
            var args = ReferencePool.Acquire<MatchStartedEventArgs>();
            args.Start = start;
            return args;
        }

        public override void Clear()
        {
            Start = null;
        }
    }
}
