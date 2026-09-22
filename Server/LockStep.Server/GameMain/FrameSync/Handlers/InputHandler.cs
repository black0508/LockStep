using Lockstep.Proto;
using LockStep.Server.Net;

namespace LockStep.Server.FrameSync.Handlers;

[MessageHandler(MsgId.C2SInput)]
public sealed class InputHandler : MessageHandler<FrameSyncComponent, C2SInput>
{
    protected override void Handle(FrameSyncComponent frameSync, int connectionId, C2SInput message)
    {
        frameSync.OnInput(connectionId, message);
    }
}
