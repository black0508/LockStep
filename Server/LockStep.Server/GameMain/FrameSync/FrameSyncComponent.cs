using System.Collections.Generic;
using Lockstep.Proto;
using LockStep.Framework;
using LockStep.Server.Net;
using LockStep.Server.Rooms;

namespace LockStep.Server.FrameSync;

// 服务端只决定每帧采用的输入，不模拟角色位置。没有新输入时沿用每名玩家最近的方向。
public sealed class FrameSyncComponent : Component
{
    // connectionId -> 该玩家在 frame.Inputs 中的条目；收到输入直接改写，封帧时整条发送。
    readonly Dictionary<int, PlayerFrameInput> inputs = new Dictionary<int, PlayerFrameInput>();
    NetworkComponent network;
    S2CFrame frame;
    double frameInterval;
    double elapsed;

    protected override void OnAwake()
    {
        network = Entity.GetComponent<NetworkComponent>();
    }

    // players 须已按 PlayerId 升序，客户端按同一顺序执行帧输入。
    public void Start(IReadOnlyList<RoomMember> players, uint tickHz)
    {
        Stop();
        frame = new S2CFrame { FrameId = 1 };
        foreach (RoomMember player in players)
        {
            var input = new PlayerFrameInput { PlayerId = player.PlayerId };
            frame.Inputs.Add(input);
            inputs.Add(player.ConnectionId, input);
        }
        frameInterval = 1.0 / tickHz;
    }

    public void OnInput(int connectionId, C2SInput message)
    {
        if (!inputs.TryGetValue(connectionId, out PlayerFrameInput input)) return;
        if (message.MoveX < -1 || message.MoveX > 1 || message.MoveZ < -1 || message.MoveZ > 1)
        {
            GameLog.Warning($"非法移动方向 connection={connectionId}");
            return;
        }
        // 直接改即可，这里kcp是单线程，不会出现竞争问题，Update逻辑帧里面一定用的是下一帧的内容
        input.MoveX = message.MoveX;
        input.MoveZ = message.MoveZ;
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (frame == null) return;
        elapsed += deltaTime;
        while (elapsed >= frameInterval)
        {
            elapsed -= frameInterval;
            foreach (int connectionId in inputs.Keys)
                network.TrySend(connectionId, MsgId.S2CFrame, frame);
            frame.FrameId++;
        }
    }

    public void Stop()
    {
        frame = null;
        inputs.Clear();
        elapsed = 0;
    }
}
