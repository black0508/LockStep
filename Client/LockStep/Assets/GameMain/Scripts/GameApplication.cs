using System;
using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Room;
using GameMain.Replay;
using LockStep.Framework;

namespace GameMain
{
    // 运行时在菜单、联网和回放之间复用；连接由 UI 明确发起。全局组件随根实体存活，以属性公开。
    public sealed class GameApplication : IDisposable
    {
        readonly ClientConfig config;
        readonly World world = new World();

        public Entity Root { get; }
        public EventComponent Events { get; private set; }
        public NetworkComponent Network { get; private set; }
        public InputComponent Input { get; private set; }
        public FrameSyncComponent FrameSync { get; private set; }
        public ReplayComponent Replay { get; private set; }
        public RoomComponent Room { get; private set; }

        public GameApplication(string host, int port, string nickName)
        {
            config = new ClientConfig(host, port, nickName);
            Root = world.CreateEntity();
        }

        // 须在 GameEntry.Application 已指向本实例后调用，组件 Awake 会读取这些属性。
        public void Init()
        {
            Events = Root.AddComponent<EventComponent>();
            Network = Root.AddComponent<NetworkComponent>();
            Network.Init(new KcpClientTransport());
            Input = Root.AddComponent<InputComponent>();
            FrameSync = Root.AddComponent<FrameSyncComponent>();
            Replay = Root.AddComponent<ReplayComponent>();
            Room = Root.AddComponent<RoomComponent>();
            Room.Init(config.NickName);
        }

        public bool StartOnline()
        {
            if (Room.Phase != RoomComponent.Status.None || Replay.Playback != null)
                return false;
            // 旧 transport 可能同步发布断线（例如上次 DNS 失败），须在新进房状态之前清理。
            Network.Disconnect();
            Room.BeginJoin();
            if (Network.Connect(config.Host, config.Port)) return true;
            Room.Leave();
            return false;
        }

        public void ExitOnline() { Room.Leave(); }

        public bool StartReplay(string path)
        {
            if (Room.Phase != RoomComponent.Status.None || Replay.Playback != null)
                return false;
            // 同步取消旧 transport（包括连接中的会话），之后才创建回放角色。
            Network.Disconnect();
            return Replay.BeginPlayback(path);
        }

        public void ExitReplay() { Replay.StopPlayback(); }
        public void Update(float deltaTime) { world.Update(deltaTime); }
        public void Dispose()
        {
            if (world.IsDisposed) return;
            try
            {
                Replay.StopPlayback();
                Room.Leave();
            }
            finally { world.Dispose(); }
        }
    }
}
