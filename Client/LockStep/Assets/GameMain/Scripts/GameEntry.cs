using GameMain.Net;
using LockStep.Framework;
using UnityEngine;
using Component = LockStep.Framework.Component;

namespace GameMain
{
    // 场景唯一入口：组装运行时并驱动更新。表现层通过 Get 取运行时组件，逻辑层不使用这里。
    // 执行顺序提前，保证同场景其他脚本的 Awake/OnEnable 能取到已创建的运行时。
    [DefaultExecutionOrder(-100)]
    public sealed class GameEntry : MonoBehaviour
    {
        static GameEntry instance;

        [SerializeField] string host = "127.0.0.1";
        [SerializeField] int port = 7777;
        [SerializeField] string nickName = "player";

        GameApplication application;

        public static T Get<T>() where T : Component
        {
            return instance.application.Get<T>();
        }

        void Awake()
        {
            instance = this;
            GameLog.InfoWriter = Debug.Log;
            GameLog.WarningWriter = Debug.LogWarning;
            GameLog.ErrorWriter = Debug.LogError;
            application = new GameApplication(
                new ClientConfig(host, port, nickName), new KcpClientTransport(), new KeyboardMoveInput());
            if (!application.Start()) enabled = false;
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
