using System;

namespace LockStep.Server.Config;

public sealed class ServerConfig
{
    public uint TickRate { get; init; } = 30;
    public uint MaxPlayersPerRoom { get; init; } = 2;
    public uint InputDelayFrames { get; init; } = 2;
    public uint Seed { get; init; } = 1;

    public static ServerConfig Default => new ServerConfig();
}
