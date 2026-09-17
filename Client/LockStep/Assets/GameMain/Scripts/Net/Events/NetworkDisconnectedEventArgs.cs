using LockStep.Framework;

namespace GameMain.Net.Events
{
    public sealed class NetworkDisconnectedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(NetworkDisconnectedEventArgs).GetHashCode();
        public override int Id => EventId;
        public static NetworkDisconnectedEventArgs Create(ReferencePoolComponent pool) => pool.Acquire<NetworkDisconnectedEventArgs>();
        public override void Clear() { }
    }
}
