using LockStep.Server.Config;

namespace LockStep.Server.Room;

public sealed class BattleRoom
{
    private readonly ServerConfig config;

    public BattleRoom(ServerConfig config)
    {
        this.config = config;
    }
}