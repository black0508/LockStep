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

        // Unity 入口设置输出函数；纯 C# 环境默认写入控制台。
        public static Action<LogLevel, string> Writer { get; set; } = (level, message) => Console.WriteLine(message);

        public static void Info(string message, string source = null)
        {
            Emit(LogLevel.Info, message, source, null);
        }

        public static void Warning(string message, string source = null)
        {
            Emit(LogLevel.Warning, message, source, null);
        }

        public static void Error(string message, string source = null, Exception exception = null)
        {
            Emit(LogLevel.Error, message, source, exception);
        }

        static void Emit(LogLevel level, string message, string source, Exception exception)
        {
            if (level < MinimumLevel) return;

            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            string text = "[" + timestamp + "][LockStep.Client][" + level.ToString().ToUpperInvariant() + "]";
            if (!string.IsNullOrEmpty(source))
            {
                text += "[" + source + "]";
            }
            text += " " + message;
            if (exception != null)
            {
                text += Environment.NewLine + exception.ToString();
            }
            Writer(level, text);
        }
    }
}
