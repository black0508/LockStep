using System;
using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Room;
using LockStep.Framework;

namespace GameMain
{
    // 组件按依赖顺序创建（后创建的在 OnAwake 中取用前面的），全部就绪后才开始连接。
    public sealed class GameApplication : IDisposable
    {
        readonly ClientConfig config;
        readonly World world = new World();
        readonly Entity root;
        readonly NetworkComponent network;

        public GameApplication(ClientConfig config, INetworkTransport transport, IMoveInput moveInput)
        {
            this.config = config;
            try
            {
                root = world.CreateEntity();
                root.AddComponent<EventComponent>();
                network = root.AddComponent<NetworkComponent>();
                network.Init(transport);
                root.AddComponent<FrameSyncComponent>().Init(moveInput);
                root.AddComponent<RoomComponent>().Init(config.NickName);
            }
            catch
            {
                world.Dispose();
                throw;
            }
        }

        public T Get<T>() where T : Component { return root.GetComponent<T>(); }
        public bool Start() { return network.Connect(config.Host, config.Port); }
        public void Update(float deltaTime) { world.Update(deltaTime); }
        public void Dispose() { world.Dispose(); }
    }
}
