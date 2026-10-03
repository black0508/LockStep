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
    // 本地固定步长时钟领先服务端提交带帧号的输入；角色只在收到权威帧时执行。全局组件从 GameEntry.Application 读取。
    public sealed class FrameSyncComponent : Component
    {
        // 须与服务端 FrameSyncComponent.TickHz 一致。
        public const uint TickHz = 30;
        const int BufferFrames = 1;
        // 须明显小于 TickHz，保证变速后的频率为正。
        const int MaxAheadFrames = 10;

        // PlayerId 到本局角色实体的唯一映射；操作对象由帧输入中的 PlayerId 决定。
        readonly Dictionary<uint, Entity> characters = new Dictionary<uint, Entity>();
        // 单调时间，不受 Unity timeScale 影响。
        readonly Stopwatch sinceReceived = new Stopwatch();
        // 区分是单机回放还是联网对局
        bool online;
        uint localPlayerId;
        double elapsed;

        public uint InputFrame { get; private set; }
        public uint AppliedFrame { get; private set; }
        public IReadOnlyDictionary<uint, Entity> Characters => characters;

        // players 为服务端下发的参战名单，已按 PlayerId 升序。
        public void Start(IReadOnlyList<RoomPlayer> players, uint localId, bool isOnline)
        {
            Stop();
            online = isOnline;
            localPlayerId = localId;
            if (online) sinceReceived.Restart();
            for (int i = 0; i < players.Count; i++)
            {
                uint playerId = players[i].PlayerId;
                Entity character = Entity.World.CreateEntity();
                // 沿 X 轴间隔 2 个单位、以原点为中心摆放。
                character.AddComponent<CharacterComponent>().Init((2L * i - (players.Count - 1)) * CharacterComponent.CoordinateScale);
                if (online && playerId == localPlayerId)
                    character.AddComponent<PlayerControllerComponent>().Init(GameEntry.Application.Input);
                characters.Add(playerId, character);
                GameEntry.Application.Events.FireNow(this,
                    CharacterSpawnedEventArgs.Create(character, playerId, playerId == localPlayerId));
            }
        }

        public void ReceiveFrame(S2CFrame frame)
        {
            if (!online) return;
            ApplyFrame(frame);
            sinceReceived.Restart();
            GameEntry.Application.Events.FireNow(this, FrameReceivedEventArgs.Create(frame));
        }

        // 联网权威帧与外部喂入的帧共用：由帧输入决定操作对象，按 PlayerId 定位角色。
        public void ApplyFrame(S2CFrame frame)
        {
            foreach (PlayerFrameInput input in frame.Inputs)
            {
                Entity character = characters[input.PlayerId];
                character.GetComponent<CharacterComponent>().Step(input.MoveX, input.MoveZ);
            }
            AppliedFrame = frame.FrameId;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!online) return;

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
            PlayerControllerComponent controller = characters[localPlayerId].GetComponent<PlayerControllerComponent>();
            GameEntry.Application.Network.TrySend(MsgId.C2SInput,
                new C2SInput { FrameId = InputFrame, MoveX = controller.MoveX, MoveZ = controller.MoveZ });
        }

        // 销毁角色实体时，挂在上面的表现组件随之销毁。
        public void Stop()
        {
            online = false;
            localPlayerId = 0;
            foreach (Entity character in characters.Values) character.Dispose();
            characters.Clear();
            sinceReceived.Reset();
            InputFrame = 0;
            AppliedFrame = 0;
            elapsed = 0;
        }
    }
}
