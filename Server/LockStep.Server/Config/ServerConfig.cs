namespace GameMain;

public sealed class ServerConfig
{
    public uint MaxPlayersPerRoom { get; init; } = 2;
    public uint MinPlayersToStart { get; init; } = 2;
}
