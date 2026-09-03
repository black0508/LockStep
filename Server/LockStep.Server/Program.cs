using System;
using System.Threading;

namespace LockStep.Server
{
    class Program
    {
        static void Main()
        {
            NetworkServer server = new NetworkServer();
            server.Start(7777);
            Console.WriteLine("Press Enter to exit (debugger: click Stop).");

            while (true)
            {
                if (IsEnterPressed())
                {
                    break;
                }

                server.Tick();
                Thread.Sleep(1);
            }

            server.Dispose();
        }

        static bool IsEnterPressed()
        {
            try
            {
                if (Console.IsInputRedirected)
                {
                    return false;
                }

                return Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
