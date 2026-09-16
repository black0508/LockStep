using GameMain.Net;
using Lockstep.Proto;

namespace GameMain.Room.Handlers
{
    [MessageHandler(MsgId.S2CJoinAck)]
    public sealed class JoinAckHandler : MessageHandler<RoomComponent, S2CJoinAck>
    {
        protected override void Handle(RoomComponent room, S2CJoinAck message)
        {
            room.ApplyJoinAck(message);
        }
    }
}
