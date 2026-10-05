namespace GameMain
{
    public enum GamePhase { None, Connecting, Joining, Joined, Playing, Replay }

    // 当前会话。房间和回放写入，其余位置只读。
    public sealed class GameData
    {
        public GamePhase Phase { get; internal set; }
        public bool IsOnline { get; internal set; }
        public uint LocalPlayerId { get; internal set; }

        internal void Clear()
        {
            Phase = GamePhase.None;
            IsOnline = false;
            LocalPlayerId = 0;
        }
    }
}
