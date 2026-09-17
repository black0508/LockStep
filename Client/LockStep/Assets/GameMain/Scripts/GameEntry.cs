using GameMain.Logging;
using GameMain.Net;
using LockStep.Framework;
using UnityEngine;

namespace GameMain
{
    // 场景唯一的业务 Mono：配置和 Unity 生命周期在这里进入普通 C# 运行时。
    public sealed class GameEntry : MonoBehaviour
    {
        [SerializeField] string host = "127.0.0.1";
        [SerializeField] int port = 7777;
        [SerializeField] uint roomId = 1;
        [SerializeField] string nickName = "player";

        GameApplication application;

        void OnEnable()
        {
            GameLog.Writer = UnityGameLog.Write;
            var config = new ClientConfig(host, port, roomId, nickName);
            application = new GameApplication(config, new KcpClientTransport());
            if (!application.Start()) enabled = false;
        }

        void Update()
        {
            application?.Update(Time.unscaledDeltaTime);
        }

        void OnDisable() { Shutdown(); }
        void OnDestroy() { Shutdown(); }
        void OnApplicationQuit() { Shutdown(); }

        void Shutdown()
        {
            GameApplication current = application;
            application = null;
            current?.Dispose();
        }
    }
}
