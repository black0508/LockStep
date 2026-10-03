using LockStep.Framework;
using UnityEngine;

namespace GameMain
{
    // 场景唯一入口：传入配置并驱动应用生命周期。全局组件通过 Application 的属性读取。
    // 执行顺序提前，保证同场景其他脚本的 Awake/OnEnable 能取到已创建的运行时。
    [DefaultExecutionOrder(-100)]
    public sealed class GameEntry : MonoBehaviour
    {
        static GameEntry instance;

        [SerializeField] string host = "127.0.0.1";
        [SerializeField] int port = 7777;
        [SerializeField] string nickName = "player";

        GameApplication application;

        public static GameApplication Application => instance == null ? null : instance.application;

        void Awake()
        {
            instance = this;
            GameLog.InfoWriter = Debug.Log;
            GameLog.WarningWriter = Debug.LogWarning;
            GameLog.ErrorWriter = Debug.LogError;
            application = new GameApplication(host, port, nickName);
            application.Init();
        }

        void Update()
        {
            application.Update(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            application?.Dispose();
            if (instance == this) instance = null;
        }
    }
}
