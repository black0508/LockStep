using GameMain.Net;
using Lockstep.Proto;

namespace GameMain.Room.Handlers
{
    [MessageHandler(MsgId.S2CRoomUpdate)]
    public sealed class RoomUpdateHandler : MessageHandler<RoomComponent, S2CRoomUpdate>
    {
        protected override void Handle(RoomComponent room, S2CRoomUpdate message)
        {
            room.ApplyRoomUpdate(message);
        }
    }
}
