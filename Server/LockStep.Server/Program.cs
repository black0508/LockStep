using System;
using System.Threading;
using LockStep.Server.Net;

namespace LockStep.Server
{
    class Program
    {
        static void Main()
        {
            KcpServerTransport server = new KcpServerTransport();
            Room room = new Room();
            server.Bind(7777);
            Console.WriteLine("[LockStep] listening UDP 7777");
            Console.WriteLine("Press Enter to exit (debugger: click Stop).");

            while (true)
            {
                if (IsEnterPressed())
                {
                    break;
                }

                server.Tick();
                room.Tick(server);
                Thread.Sleep(1);
            }
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
