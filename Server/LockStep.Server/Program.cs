using System;
using System.Threading;
using System.Diagnostics;
using LockStep.Server;
using LockStep.Server.Config;
using LockStep.Server.Net;
using LockStep.Framework;

class Program
{
    const int Port = 7777;
    const int SleepMs = 10;

    static volatile bool running = true;

    static int Main()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
        try
        {
            using var application = new GameApplication(ServerConfig.Default, new KcpServerHost());
            if (!application.Start(Port)) return 1;
            var clock = Stopwatch.StartNew();
            double previousTime = clock.Elapsed.TotalSeconds;
            while (running)
            {
                double now = clock.Elapsed.TotalSeconds;
                application.Update((float)(now - previousTime));
                previousTime = now;
                Thread.Sleep(SleepMs);
            }
            return 0;
        }
        catch (Exception error)
        {
            GameLog.Error("服务器运行失败", error);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            GameLog.Info("服务器已退出");
        }
    }

    static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        running = false;
    }
}
