using System;
using System.Threading;
using LockStep.Server.Net;
using LockStep.Server.Room;
using LockStep.Server.Config;

class Program
{
    static void Main()
    {
        ServerConfig config = ServerConfig.Default;
        using NetworkServer server = new NetworkServer(config);
        server.Start(7777);
        Console.WriteLine("Press Enter to exit (debugger: click Stop).");

        while (true)
        {
            server.Tick();
            Thread.Sleep(10);
        }
    }
}
