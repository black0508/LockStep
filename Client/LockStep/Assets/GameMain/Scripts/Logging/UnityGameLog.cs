using LockStep.Framework;
using UnityEngine;

namespace GameMain.Logging
{
    // 时间、级别与来源已经由框架格式化，适配器只负责输出。
    public static class UnityGameLog
    {
        public static void Write(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Error: Debug.LogError(message); break;
                case LogLevel.Warning: Debug.LogWarning(message); break;
                default: Debug.Log(message); break;
            }
        }
    }
}
