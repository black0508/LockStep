using System;
using System.Collections.Generic;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Server.Core;

namespace LockStep.Server.Net;

// MsgId 到处理器的注册表，取代传输层里硬编码的 switch
public sealed class PacketRouter
{
    readonly Dictionary<MsgId, Action<int, ByteString>> handlers = new Dictionary<MsgId, Action<int, ByteString>>();

    public void On<T>(MsgId msgId, MessageParser<T> parser, Action<int, T> handler) where T : IMessage<T>
    {
        handlers[msgId] = (connectionId, body) => handler(connectionId, parser.ParseFrom(body));
    }

    public void Route(int connectionId, MsgId msgId, ByteString body)
    {
        if (!handlers.TryGetValue(msgId, out Action<int, ByteString> handler))
        {
            Log.Warn($"未注册消息 {msgId} connection={connectionId}");
            return;
        }

        try
        {
            handler(connectionId, body);
        }
        catch (InvalidProtocolBufferException)
        {
            Log.Warn($"消息体解析失败 {msgId} connection={connectionId}");
        }
    }
}
