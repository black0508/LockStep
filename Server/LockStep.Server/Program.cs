using System;
using System.Threading;
using LockStep.Server;
using LockStep.Server.Config;
using LockStep.Server.Core;

class Program
{
    const int Port = 7777;
    const int SleepMs = 10;

    static volatile bool running = true;

    static void Main()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
        GameEntry.Initialize(ServerConfig.Default, Port);

        while (running)
        {
            GameEntry.Tick(Environment.TickCount64);
            Thread.Sleep(SleepMs);
        }

        GameEntry.Shutdown();
        Log.Info("服务器已退出");
    }

    static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        running = false;
    }
}
