using System;
using System.Threading;
using LockStep.Server;
using LockStep.Server.Net;
using LockStep.Server.Room;
using LockStep.Server.Config;

class Program
{
    static void Main()
    {
        new RoomManager(ServerConfig.Default);
        using NetworkServer server = new NetworkServer();
        if (GameEntry.RoomManager == null || GameEntry.NetworkServer == null)
        {
            throw new InvalidOperationException("GameEntry 未就绪");
        }

        server.Start(7777);

        while (true)
        {
            server.Tick();
            Thread.Sleep(10);
        }
    }
}
