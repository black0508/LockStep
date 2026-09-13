namespace LockStep.Server.Rooms;

public sealed class RoomMember
{
    // kcp 分配的连接编号，重连时由房间改写
    public int ConnectionId { get; internal set; }
    // 房间内分配的唯一玩家 Id，进房后不再变化
    public uint PlayerId { get; }
    public string NickName { get; }
    public bool Connected { get; internal set; }

    public RoomMember(int connectionId, uint playerId, string nickName)
    {
        ConnectionId = connectionId;
        PlayerId = playerId;
        NickName = nickName ?? "";
        Connected = true;
    }
}
