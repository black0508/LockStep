using System.Collections.Generic;
using Lockstep.Proto;
using Framework;

namespace GameMain;

// 服务端按客户端标注的目标帧缓存输入、固定频率封帧，不模拟角色位置。缺失输入时沿用上一帧实际采用的方向。
public sealed class FrameSyncComponent : Component
{
    // 须与客户端 FrameSyncComponent.TickHz 一致。
    public const uint TickHz = 30;
    const double FrameInterval = 1.0 / TickHz;

    sealed class PlayerInputBuffer
    {
        public PlayerFrameInput Current;
        public readonly Dictionary<uint, C2SInput> Pending = new Dictionary<uint, C2SInput>();
    }

    readonly Dictionary<int, PlayerInputBuffer> inputs = new Dictionary<int, PlayerInputBuffer>();
    S2CFrame frame;
    double elapsed;
    
    public void Start(IReadOnlyList<RoomMember> players)
    {
        frame = new S2CFrame { FrameId = 1 };
        foreach (RoomMember player in players)
        {
            var input = new PlayerFrameInput { PlayerId = player.PlayerId };
            frame.Inputs.Add(input);
            inputs.Add(player.ConnectionId, new PlayerInputBuffer { Current = input });
        }
    }

    public void OnInput(int connectionId, C2SInput message)
    {
        // frame.FrameId 是下一待封闭帧，迟到的输入不会再被取用。
        if (!inputs.TryGetValue(connectionId, out PlayerInputBuffer input) || message.FrameId < frame.FrameId) return;
        input.Pending[message.FrameId] = message;
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (frame == null) return;
        NetworkComponent network = Entity.GetComponent<NetworkComponent>();
        elapsed += deltaTime;
        while (elapsed >= FrameInterval)
        {
            elapsed -= FrameInterval;
            foreach (PlayerInputBuffer input in inputs.Values)
            {
                if (!input.Pending.Remove(frame.FrameId, out C2SInput pending)) continue;
                input.Current.MoveX = pending.MoveX;
                input.Current.MoveZ = pending.MoveZ;
            }
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
