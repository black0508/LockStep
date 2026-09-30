using System;
using System.IO;
using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Room;
using GameMain.Replay;
using LockStep.Framework;

namespace GameMain
{
    // 运行时在菜单、联网和回放之间复用；连接由 UI 明确发起。
    public sealed class GameApplication : IDisposable
    {
        readonly ClientConfig config;
        readonly World world = new World();
        readonly Entity root;
        readonly NetworkComponent network;
        readonly FrameSyncComponent frameSync;
        readonly ReplayComponent replay;
        readonly RoomComponent room;

        public GameApplication(string host, int port, string nickName)
        {
            config = new ClientConfig(host, port, nickName);
            root = world.CreateEntity();
            root.AddComponent<EventComponent>();
            network = root.AddComponent<NetworkComponent>();
            network.Init(new KcpClientTransport());
            frameSync = root.AddComponent<FrameSyncComponent>();
            replay = root.AddComponent<ReplayComponent>();
            replay.Init(Path.Combine(UnityEngine.Application.persistentDataPath, "Replays"));
            frameSync.Init(new KeyboardMoveInput(), replay);
            room = root.AddComponent<RoomComponent>();
            room.Init(config.NickName);
        }

        public T Get<T>() where T : Component { return root.GetComponent<T>(); }
        public bool StartOnline()
        {
            if (room.Phase != RoomComponent.Status.None || frameSync.Mode != FrameSyncComponent.SimulationMode.None)
                return false;
            // 旧 transport 可能同步发布断线（例如上次 DNS 失败），须在新进房状态之前清理。
            network.Disconnect();
            room.BeginJoin();
            if (network.Connect(config.Host, config.Port)) return true;
            room.Leave();
            return false;
        }

        public void ExitOnline() { room.Leave(); }

        public bool StartReplay(string path)
        {
            if (room.Phase != RoomComponent.Status.None || frameSync.Mode != FrameSyncComponent.SimulationMode.None)
                return false;
            // 同步取消旧 transport（包括连接中的会话），之后才创建回放角色。
            network.Disconnect();
            return replay.BeginPlayback(path);
        }

        public void ExitReplay() { replay.StopPlayback(); }
        public void Update(float deltaTime) { world.Update(deltaTime); }
        public void Dispose()
        {
            if (world.IsDisposed) return;
            try
            {
                replay.FinishRecording();
                replay.StopPlayback();
                room.Leave();
            }
            finally { world.Dispose(); }
        }
    }
}
