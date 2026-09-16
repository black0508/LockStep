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

    public class GameLog
    {
        readonly string applicationName;

        public GameLog(string applicationName = "LockStep.Client")
        {
            this.applicationName = applicationName;
        }

        public LogLevel MinimumLevel { get; set; }

        public void Info(string message, string source = null)
        {
            Emit(LogLevel.Info, message, source, null);
        }

        public void Warning(string message, string source = null)
        {
            Emit(LogLevel.Warning, message, source, null);
        }

        public void Error(string message, string source = null, Exception exception = null)
        {
            Emit(LogLevel.Error, message, source, exception);
        }

        void Emit(LogLevel level, string message, string source, Exception exception)
        {
            if (level < MinimumLevel) return;

            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            string text = "[" + timestamp + "][" + applicationName + "][" + level.ToString().ToUpperInvariant() + "]";
            if (!string.IsNullOrEmpty(source))
            {
                text += "[" + source + "]";
            }
            text += " " + message;
            if (exception != null)
            {
                text += Environment.NewLine + exception.ToString();
            }
            Write(level, text);
        }

        protected virtual void Write(LogLevel level, string message)
        {
            Console.WriteLine(message);
        }
    }
}
