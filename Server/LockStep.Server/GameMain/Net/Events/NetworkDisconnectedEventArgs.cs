using LockStep.Framework;

namespace LockStep.Server.Net.Events;

public sealed class NetworkDisconnectedEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(NetworkDisconnectedEventArgs).GetHashCode();
    public override int Id => EventId;
    public int ConnectionId { get; private set; }

    public static NetworkDisconnectedEventArgs Create(ReferencePoolComponent pool, int connectionId)
    {
        var args = pool.Acquire<NetworkDisconnectedEventArgs>();
        args.ConnectionId = connectionId;
        return args;
    }

    public override void Clear() { ConnectionId = 0; }
}
