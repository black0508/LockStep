using Lockstep.Proto;

namespace GameMain
{
    [MessageHandler(MsgId.S2CMatchStart)]
    public sealed class MatchStartHandler : MessageHandler<RoomComponent, S2CMatchStart>
    {
        protected override void Handle(RoomComponent room, S2CMatchStart message)
        {
            room.ApplyMatchStart(message);
        }
    }
}
