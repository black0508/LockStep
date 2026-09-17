using System;
using System.Globalization;

namespace LockStep.Framework
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public static class GameLog
    {
        public static LogLevel MinimumLevel { get; set; }

        public static Action<string> InfoWriter = Console.WriteLine;
        public static Action<string> WarningWriter = Console.WriteLine;
        public static Action<string> ErrorWriter = Console.Error.WriteLine;

        public static void Info(string message)
        {
            Write(LogLevel.Info, message, null);
        }

        public static void Warning(string message)
        {
            Write(LogLevel.Warning, message, null);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write(LogLevel.Error, message, exception);
        }

        static void Write(LogLevel level, string message, Exception exception)
        {
            if (level < MinimumLevel) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string text = $"[{timestamp}][{level.ToString().ToUpperInvariant()}] {message}";
            if (exception != null)
            {
                text += Environment.NewLine + exception;
            }

            switch (level)
            {
                case LogLevel.Error: ErrorWriter(text); break;
                case LogLevel.Warning: WarningWriter(text); break;
                default: InfoWriter(text); break;
            }
        }
    }
}
