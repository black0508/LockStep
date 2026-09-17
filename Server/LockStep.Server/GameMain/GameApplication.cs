using System;
using LockStep.Framework;
using LockStep.Server.Config;
using LockStep.Server.Net;
using LockStep.Server.Rooms;

namespace LockStep.Server;

// 业务组合根：池和事件先创建，网络与房间后创建，关闭时逆序释放。
public sealed class GameApplication : IDisposable
{
    readonly World world = new World();
    readonly ServerConfig config;
    bool started;

    public Entity Root { get; }
    public ReferencePoolComponent ReferencePool { get; }
    public EventComponent Events { get; }
    public NetworkComponent Network { get; }
    public RoomComponent Room { get; }

    public GameApplication(ServerConfig config, INetworkServerHost host)
    {
        this.config = config;
        try
        {
            Root = world.CreateEntity();
            ReferencePool = Root.AddComponent<ReferencePoolComponent>();
            Events = Root.AddComponent<EventComponent>();
            Network = Root.AddComponent<NetworkComponent>();
            Network.Init(host);
            Room = Root.AddComponent<RoomComponent>();
            Room.Init(config);
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }

    public bool Start(int port)
    {
        if (world.IsDisposed)
        {
            GameLog.Error("应用已关闭，不能再次启动");
            return false;
        }
        if (started) return true;
        if (config == null || !config.IsValid)
        {
            GameLog.Error("配置无效：帧率/容量必须为正，开战人数必须在容量范围内");
            Dispose();
            return false;
        }
        if (!Network.Start(port))
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
