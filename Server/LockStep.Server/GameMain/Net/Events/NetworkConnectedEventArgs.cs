using LockStep.Framework;

namespace LockStep.Server.Net.Events;

public sealed class NetworkConnectedEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(NetworkConnectedEventArgs).GetHashCode();
    public override int Id => EventId;
    public int ConnectionId { get; private set; }

    public static NetworkConnectedEventArgs Create(ReferencePoolComponent pool, int connectionId)
    {
        var args = pool.Acquire<NetworkConnectedEventArgs>();
        args.ConnectionId = connectionId;
        return args;
    }

    public override void Clear() { ConnectionId = 0; }
}
