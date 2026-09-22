using GameMain.Net;
using Lockstep.Proto;

namespace GameMain.FrameSync.Handlers
{
    [MessageHandler(MsgId.S2CFrame)]
    public sealed class FrameHandler : MessageHandler<FrameSyncComponent, S2CFrame>
    {
        protected override void Handle(FrameSyncComponent frameSync, S2CFrame message)
        {
            frameSync.ReceiveFrame(message);
        }
    }
}
