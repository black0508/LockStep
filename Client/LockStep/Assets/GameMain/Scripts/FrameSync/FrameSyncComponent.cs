using System.Collections.Generic;
using GameMain.Character;
using GameMain.FrameSync.Events;
using GameMain.Net;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.FrameSync
{
    // 收到服务端权威帧即执行一步，执行后采样本地输入上报，供服务端后续封帧使用。不引用 Unity，也不知道表现层。
    public sealed class FrameSyncComponent : Component
    {
        // 按 PlayerId 升序，与帧内输入顺序一致。
        readonly List<CharacterComponent> characters = new List<CharacterComponent>();
        NetworkComponent network;
        EventComponent events;
        IMoveInput moveInput;
        uint tickHz;
        uint lastFrameId;
        bool running;

        // 测试用数据
        public uint FrameId => lastFrameId;
        public uint TickHz => tickHz;
        public IReadOnlyList<CharacterComponent> Characters => characters;

        // 测试用数据
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
            Stop();
            this.tickHz = tickHz;
            running = true;
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
            if (!running || frame.FrameId <= lastFrameId) return;
            bool valid = frame.FrameId == lastFrameId + 1 && frame.Inputs.Count == characters.Count;
            for (int i = 0; valid && i < characters.Count; i++)
                valid = frame.Inputs[i].PlayerId == characters[i].PlayerId;
            if (!valid)
            {
                GameLog.Error($"权威帧无效：期望帧={lastFrameId + 1} 收到帧={frame.FrameId}");
                Stop();
                network.Disconnect();
                return;
            }

            lastFrameId = frame.FrameId;
            for (int i = 0; i < characters.Count; i++)
                characters[i].Step(frame.Inputs[i].MoveX, frame.Inputs[i].MoveZ, tickHz);
            moveInput.Read(out int moveX, out int moveZ);
            network.TrySend(MsgId.C2SInput, new C2SInput { MoveX = moveX, MoveZ = moveZ });
        }

        // 销毁角色实体时，挂在上面的表现组件随之销毁。
        public void Stop()
        {
            running = false;
            foreach (CharacterComponent character in characters) character.Entity.Dispose();
            characters.Clear();
            lastFrameId = 0;
            tickHz = 0;
        }
    }
}
