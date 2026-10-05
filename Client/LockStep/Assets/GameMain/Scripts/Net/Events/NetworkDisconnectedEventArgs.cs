using Framework;

namespace GameMain
{
    public sealed class NetworkDisconnectedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(NetworkDisconnectedEventArgs).GetHashCode();
        public override int Id => EventId;
        public static NetworkDisconnectedEventArgs Create() => ReferencePool.Acquire<NetworkDisconnectedEventArgs>();
        public override void Clear() { }
    }
}
