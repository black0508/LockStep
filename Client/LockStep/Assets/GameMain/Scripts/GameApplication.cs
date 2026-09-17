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

        public Entity Root { get; }
        public ReferencePoolComponent ReferencePool { get; }
        public EventComponent Events { get; }
        public NetworkComponent Network { get; }
        public RoomComponent Room { get; }

        public GameApplication(ClientConfig config, INetworkTransport transport)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            world = new World();
            try
            {
                Root = world.CreateEntity();
                ReferencePool = Root.AddComponent<ReferencePoolComponent>();
                Events = Root.AddComponent<EventComponent>();
                Network = Root.AddComponent<NetworkComponent>();
                Room = Root.AddComponent<RoomComponent>();
                Network.Init(transport);
                Room.Init(config.RoomId, config.NickName);
            }
            catch
            {
                world.Dispose();
                throw;
            }
        }

        public bool Start()
        {
            if (!Network.Connect(config.Host, config.Port))
            {
                Dispose();
                return false;
            }
            return true;
        }

        public void Update(float deltaTime) { world.Update(deltaTime); }
        public void Dispose() { world.Dispose(); }
    }
}
