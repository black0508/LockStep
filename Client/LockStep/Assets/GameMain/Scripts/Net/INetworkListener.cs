namespace GameMain.Net
{
    // 连接通知发给 NetworkComponent 所属实体上当前存活的组件，无需订阅或注销。
    public interface INetworkListener
    {
        void OnConnected();
        void OnDisconnected();
    }
}
