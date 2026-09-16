using Lockstep.Proto;
using LockStep.Server.Config;
using LockStep.Server.Net;
using LockStep.Server.Rooms;

namespace LockStep.Server;

// 组合根：各模块只在这里被创建并接线，下层一律不反查全局
public static class GameEntry
{
    public static NetworkServer NetworkServer { get; private set; }
    public static RoomService RoomService { get; private set; }

    public static void Initialize(ServerConfig config, int port)
    {
        NetworkServer = new NetworkServer();
        RoomService = new RoomService(config, NetworkServer);

        PacketRouter router = new PacketRouter();
        router.On(MsgId.C2SJoin, C2SJoin.Parser, RoomService.OnJoin);

        NetworkServer.UseRouter(router);
        NetworkServer.ClientDisconnected += RoomService.OnClientDisconnected;
        NetworkServer.Start(port);
    }

    public static void Tick(long nowMs)
    {
        NetworkServer.Tick();
        RoomService.Tick(nowMs);
    }

    public static void Shutdown()
    {
        if (NetworkServer != null)
        {
            NetworkServer.Dispose();
        }

        NetworkServer = null;
        RoomService = null;
    }
}
