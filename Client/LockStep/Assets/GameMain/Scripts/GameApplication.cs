using System;
using GameMain.Net;
using GameMain.Room;
using LockStep.Framework;

namespace GameMain
{
    // 先创建/初始化组件和自动路由，再开始连接。
    public sealed class GameApplication : IDisposable
    {
        readonly ClientConfig config;
        readonly World world;
        readonly bool ready;
        bool started;

        public Entity Root { get; }
        public NetworkComponent Network { get; }
        public RoomComponent Room { get; }

        public GameApplication(ClientConfig config, INetworkTransport transport, GameLog log)
        {
            this.config = config;
            world = new World(log);
            Root = world.CreateEntity();
            Network = Root.AddComponent<NetworkComponent>();
            Room = Root.AddComponent<RoomComponent>();
            ready = Network != null && Room != null && Network.Init(transport)
                && config != null && Room.Init(config.RoomId, config.NickName);
        }

        public bool Start()
        {
            if (world.IsDisposed)
            {
                world.Log.Error("应用已经关闭，不能再次启动", nameof(GameApplication));
                return false;
            }
            if (started) return true;
            if (!ready)
            {
                world.Log.Error("启动失败：配置无效或必要组件初始化失败", nameof(GameApplication));
                Dispose();
                return false;
            }
            if (!Network.Connect(config.Host, config.Port))
            {
                Dispose();
                return false;
            }
            started = true;
            return true;
        }

        public void Update(float deltaTime) { world.Update(deltaTime); }
        public void Dispose() { world.Dispose(); }
    }
}
