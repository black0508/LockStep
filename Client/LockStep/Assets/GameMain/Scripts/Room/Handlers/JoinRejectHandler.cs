using GameMain.Net;
using Lockstep.Proto;

namespace GameMain.Room.Handlers
{
    [MessageHandler(MsgId.S2CJoinReject)]
    public sealed class JoinRejectHandler : MessageHandler<RoomComponent, S2CJoinReject>
    {
        protected override void Handle(RoomComponent room, S2CJoinReject message)
        {
            room.ApplyJoinReject(message);
        }
    }
}
