using System;
using System.Globalization;

namespace Framework
{
    // 默认写控制台。宿主可改成 Debug.Log / LogWarning / LogError，避免框架引用 Unity。
    public static class GameLog
    {
        public static Action<string> InfoWriter = Console.WriteLine;
        public static Action<string> WarningWriter = Console.WriteLine;
        public static Action<string> ErrorWriter = Console.Error.WriteLine;

        public static void Info(string message)
        {
            Write(InfoWriter, "INFO", message, null);
        }

        public static void Warning(string message)
        {
            Write(WarningWriter, "WARNING", message, null);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write(ErrorWriter, "ERROR", message, exception);
        }

        static void Write(Action<string> writer, string level, string message, Exception exception)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string text = $"[{timestamp}][{level}] {message}";
            if (exception != null)
            {
                text += Environment.NewLine + exception;
            }

            writer(text);
        }
    }
}
