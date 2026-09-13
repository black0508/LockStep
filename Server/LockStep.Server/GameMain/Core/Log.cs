using System;

namespace LockStep.Server.Core;

public static class Log
{
    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warn(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    static void Write(string level, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}][LockStep][{level}] {message}");
    }
}
