using System;
using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace LockStep.Server.Net;

public sealed class MessageDispatcher
{
    readonly Dictionary<MsgId, IMessageHandler> handlers = new Dictionary<MsgId, IMessageHandler>();

    // 启动时注册当前程序集内标记过的 Handler。
    public void RegisterAssembly(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<MessageHandlerAttribute>();
            if (attribute == null) continue;
            handlers.Add(attribute.Id, (IMessageHandler)Activator.CreateInstance(type));
        }
    }

    public void Dispatch(Entity entity, int connectionId, MsgId id, ByteString body)
    {
        if (!handlers.TryGetValue(id, out IMessageHandler handler))
        {
            GameLog.Warning($"未处理的消息：{id} connection={connectionId}");
            return;
        }
        handler.Dispatch(entity, connectionId, body);
    }
}
