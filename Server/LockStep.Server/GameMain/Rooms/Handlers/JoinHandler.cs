using Lockstep.Proto;

namespace GameMain;

[MessageHandler(MsgId.C2SJoin)]
public sealed class JoinHandler : MessageHandler<RoomComponent, C2SJoin>
{
    protected override void Handle(RoomComponent room, int connectionId, C2SJoin message)
    {
        room.OnJoin(connectionId, message);
    }
}
