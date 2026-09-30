using System;
using LockStep.Framework;
using LockStep.Server.Config;
using LockStep.Server.FrameSync;
using LockStep.Server.Net;
using LockStep.Server.Rooms;

namespace LockStep.Server;

// 业务组合根：组件按依赖顺序创建（后创建的在 OnAwake 中取用前面的），关闭时逆序释放。
public sealed class GameApplication : IDisposable
{
    readonly World world = new World();
    readonly NetworkComponent network;

    public GameApplication(ServerConfig config, INetworkServerHost host)
    {
        Entity root = world.CreateEntity();
        root.AddComponent<EventComponent>();
        network = root.AddComponent<NetworkComponent>();
        network.Init(host);
        root.AddComponent<FrameSyncComponent>();
        root.AddComponent<RoomComponent>().Init(config);
    }

    public bool Start(int port) { return network.Start(port); }
    public void Update(float deltaTime) { world.Update(deltaTime); }
    public void Dispose() { world.Dispose(); }
}
