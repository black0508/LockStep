using System;
using LockStep.Server.Net;

namespace LockStep.Server.Config;

public sealed class ServerConfig
{
    public uint TickRate {get; init;} = 30; // 每秒多少逻辑帧
    public static ServerConfig Default => new ServerConfig();
}