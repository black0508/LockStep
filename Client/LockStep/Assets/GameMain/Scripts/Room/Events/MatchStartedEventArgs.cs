using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Room.Events
{
    // 联网对局的模拟已启动后发布。
    public sealed class MatchStartedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(MatchStartedEventArgs).GetHashCode();
        public override int Id => EventId;
        public S2CMatchStart Start { get; private set; }
        public uint LocalPlayerId { get; private set; }

        public static MatchStartedEventArgs Create(S2CMatchStart start, uint localPlayerId)
        {
            var args = ReferencePool.Acquire<MatchStartedEventArgs>();
            args.Start = start;
            args.LocalPlayerId = localPlayerId;
            return args;
        }

        public override void Clear()
        {
            Start = null;
            LocalPlayerId = 0;
        }
    }
}
