namespace GameMain
{
    public sealed class ClientConfig
    {
        public string Host { get; }
        public int Port { get; }
        public uint RoomId { get; }
        public string NickName { get; }

        public ClientConfig(string host, int port, uint roomId, string nickName)
        {
            Host = host;
            Port = port;
            RoomId = roomId;
            NickName = nickName ?? "";
        }
    }
}
