namespace LockStep.Server.Room;

public sealed class PlayerSession
{
    public int ConnectionId { get; set; }
    public uint PlayerId { get; }
    public string NickName { get; set; }
    public uint RoomId { get; }

    public PlayerSession(int connectionId, uint playerId, string nickName, uint roomId)
    {
        ConnectionId = connectionId;
        PlayerId = playerId;
        NickName = nickName ?? "";
        RoomId = roomId;
    }
}
