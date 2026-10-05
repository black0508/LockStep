using System;
using System.Diagnostics;
using Lockstep.Proto;
using Framework;

namespace GameMain
{
    // 本地固定步长时钟领先服务端提交带帧号的输入。位移由单位在执行权威帧时完成。全局组件从 GameEntry.Application 读取。
    public sealed class FrameSyncComponent : Component
    {
        // 须与服务端 FrameSyncComponent.TickHz 一致。
        public const uint TickHz = 30;
        const int BufferFrames = 1;
        // 须明显小于 TickHz，保证变速后的频率为正。
        const int MaxAheadFrames = 10;

        // 单调时间，不受 Unity timeScale 影响。
        readonly Stopwatch sinceReceived = new Stopwatch();
        double elapsed;

        public uint InputFrame { get; private set; }
        public uint AppliedFrame { get; private set; }

        // 只重置时钟。调用前须写好 GameData，玩家由 UnitComponent 生成。
        public void Start()
        {
            Stop();
            if (GameEntry.Application.Data.IsOnline) sinceReceived.Restart();
        }

        public void ReceiveFrame(S2CFrame frame)
        {
            if (!GameEntry.Application.Data.IsOnline) return;
            ApplyFrame(frame);
            sinceReceived.Restart();
            GameEntry.Application.Events.FireNow(this, FrameReceivedEventArgs.Create(frame));
        }

        // 联网权威帧与外部喂入的帧共用：按 PlayerId 取出单位并执行输入，再推进已执行帧号。
        public void ApplyFrame(S2CFrame frame)
        {
            UnitComponent units = GameEntry.Application.Units;
            foreach (PlayerFrameInput input in frame.Inputs)
            {
                if (!units.TryGet(input.PlayerId, out Unit unit))
                {
                    GameLog.Error($"帧输入找不到玩家 playerId={input.PlayerId} frame={frame.FrameId}");
                    continue;
                }
                // TODO: 后续有其他的需要解析消息类型，这里只直接处理move
                unit.Step(input.MoveX, input.MoveZ);
            }
            AppliedFrame = frame.FrameId;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!GameEntry.Application.Data.IsOnline) return;

            int halfRttFrames = (int)Math.Ceiling(GameEntry.Application.Network.RttMilliseconds * TickHz / 2000.0);
            // 可能收到是99帧，RTT是1帧，所以估算服务器100帧，但这个结果只在接收一瞬间是对的，可能Update中也会慢，估算的就不准确
            // 所以这里也计算出从接收到帧到Update有多久时间从而估算服务器帧
            uint serverFrame = AppliedFrame + (uint)halfRttFrames + (uint)(sinceReceived.Elapsed.TotalSeconds * TickHz);
            int targetAhead = Math.Min(halfRttFrames + BufferFrames, MaxAheadFrames);
            // 落后于服务端（开局或卡顿）时，已封闭的帧不再补输入，直接从服务端当前帧追到目标领先量。
            if (InputFrame <= serverFrame)
            {
                InputFrame = serverFrame;
                for (int i = 0; i < targetAhead; i++) Tick();
            }

            int ahead = (int)(InputFrame - serverFrame);
            double interval = 1.0 / (TickHz + targetAhead - ahead);
            elapsed += deltaTime;
            for (; elapsed >= interval && ahead < MaxAheadFrames; ahead++)
            {
                elapsed -= interval;
                Tick();
            }
            // RTT 骤降或单帧耗时过长时领先量到上限，停止推进并丢弃累计时间。
            // TODO: 做断线重连处理应该
            if (ahead >= MaxAheadFrames) elapsed = 0;
        }

        void Tick()
        {
            InputFrame++;
            InputComponent input = GameEntry.Application.Input;
            GameEntry.Application.Network.TrySend(MsgId.C2SInput,
                new C2SInput { FrameId = InputFrame, MoveX = input.MoveX, MoveZ = input.MoveZ });
        }

        public void Stop()
        {
            sinceReceived.Reset();
            InputFrame = 0;
            AppliedFrame = 0;
            elapsed = 0;
        }
    }
}
