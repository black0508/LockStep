using Lockstep.Proto;

namespace GameMain;

[MessageHandler(MsgId.C2SInput)]
public sealed class InputHandler : MessageHandler<FrameSyncComponent, C2SInput>
{
    protected override void Handle(FrameSyncComponent frameSync, int connectionId, C2SInput message)
    {
        frameSync.OnInput(connectionId, message);
    }
}
