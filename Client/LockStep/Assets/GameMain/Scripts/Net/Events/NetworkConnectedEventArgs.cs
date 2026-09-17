using LockStep.Framework;

namespace GameMain.Net.Events
{
    public sealed class NetworkConnectedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(NetworkConnectedEventArgs).GetHashCode();
        public override int Id => EventId;
        public static NetworkConnectedEventArgs Create(ReferencePoolComponent pool) => pool.Acquire<NetworkConnectedEventArgs>();
        public override void Clear() { }
    }
}
