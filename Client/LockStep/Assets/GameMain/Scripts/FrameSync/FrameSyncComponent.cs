using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameMain.Character;
using GameMain.FrameSync.Events;
using GameMain.Net;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.FrameSync
{
    // 本地固定步长时钟领先服务端提交带帧号的输入；角色只在收到权威帧时执行。不引用 Unity，也不知道表现层。
    public sealed class FrameSyncComponent : Component
    {
        const int BufferFrames = 1;
        // 须明显小于 TickHz，保证变速后的频率为正。
        const int MaxAheadFrames = 10;

        // 按 PlayerId 升序，与帧内输入顺序一致。
        readonly List<CharacterComponent> characters = new List<CharacterComponent>();
        // 单调时间，不受 Unity timeScale 影响。
        readonly Stopwatch sinceReceived = new Stopwatch();
        NetworkComponent network;
        EventComponent events;
        IMoveInput moveInput;
        bool running;
        double elapsed;

        public uint TickHz { get; private set; }
        public uint InputFrame { get; private set; }
        public uint AppliedFrame { get; private set; }
        public IReadOnlyList<CharacterComponent> Characters => characters;

        protected override void OnAwake()
        {
            network = Entity.GetComponent<NetworkComponent>();
            events = Entity.GetComponent<EventComponent>();
        }

        public void Init(IMoveInput value)
        {
            moveInput = value;
        }

        // players 为服务端下发的参战名单，已按 PlayerId 升序。
        public void Start(IReadOnlyList<RoomPlayer> players, uint localPlayerId, uint tickHz)
        {
            TickHz = tickHz;
            running = true;
            sinceReceived.Restart();
            for (int i = 0; i < players.Count; i++)
            {
                var character = Entity.World.CreateEntity().AddComponent<CharacterComponent>();
                // 沿 X 轴间隔 2 个单位、以原点为中心摆放。
                character.Init(players[i].PlayerId, (2L * i - (players.Count - 1)) * CharacterComponent.CoordinateScale);
                characters.Add(character);
                events.FireNow(this, CharacterSpawnedEventArgs.Create(character, character.PlayerId == localPlayerId));
            }
        }

        public void ReceiveFrame(S2CFrame frame)
        {
            for (int i = 0; i < characters.Count; i++)
                characters[i].Step(frame.Inputs[i].MoveX, frame.Inputs[i].MoveZ, TickHz);
            AppliedFrame = frame.FrameId;
            sinceReceived.Restart();
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!running) return;

            int halfRttFrames = (int)Math.Ceiling(network.RttMilliseconds * TickHz / 2000.0);
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
            moveInput.Read(out int moveX, out int moveZ);
            network.TrySend(MsgId.C2SInput, new C2SInput { FrameId = InputFrame, MoveX = moveX, MoveZ = moveZ });
        }

        // 销毁角色实体时，挂在上面的表现组件随之销毁。
        public void Stop()
        {
            running = false;
            foreach (CharacterComponent character in characters) character.Entity.Dispose();
            characters.Clear();
            sinceReceived.Reset();
            TickHz = 0;
            InputFrame = 0;
            AppliedFrame = 0;
            elapsed = 0;
        }
    }
}
