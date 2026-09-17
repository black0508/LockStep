namespace LockStep.Server.Rooms;

public sealed class RoomMember
{
    public int ConnectionId { get; }
    public uint PlayerId { get; }
    public string NickName { get; }

    public RoomMember(int connectionId, uint playerId, string nickName)
    {
        ConnectionId = connectionId;
        PlayerId = playerId;
        NickName = nickName ?? "";
    }
}
