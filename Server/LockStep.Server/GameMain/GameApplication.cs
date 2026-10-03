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
    readonly Entity root;

    public GameApplication(ServerConfig config, INetworkServerHost host)
    {
        root = world.CreateEntity();
        root.AddComponent<EventComponent>();
        var network = root.AddComponent<NetworkComponent>();
        network.Init(host);
        root.AddComponent<FrameSyncComponent>();
        root.AddComponent<RoomComponent>().Init(config);
    }

    public T Get<T>() where T : Component { return root.GetComponent<T>(); }
    public bool Start(int port) { return Get<NetworkComponent>().Start(port); }
    public void Update(float deltaTime) { world.Update(deltaTime); }
    public void Dispose() { world.Dispose(); }
}
