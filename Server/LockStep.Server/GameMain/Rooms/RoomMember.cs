namespace LockStep.Server.Rooms;

public sealed record RoomMember(int ConnectionId, uint PlayerId, string NickName);
