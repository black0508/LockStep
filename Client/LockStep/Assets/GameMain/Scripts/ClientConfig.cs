namespace GameMain
{
    public sealed class ClientConfig
    {
        public string Host { get; }
        public int Port { get; }
        public string NickName { get; }

        public ClientConfig(string host, int port, string nickName)
        {
            Host = host;
            Port = port;
            NickName = nickName ?? "";
        }
    }
}
