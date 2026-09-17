using System;
using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace LockStep.Server.Net;

public sealed class MessageDispatcher
{
    Dictionary<MsgId, IMessageHandler> handlers = new Dictionary<MsgId, IMessageHandler>();

    // 初始化错误交给组合根收口；注册整批成功才替换路由。
    public void RegisterAssembly(Assembly assembly)
    {
        var discovered = new Dictionary<MsgId, IMessageHandler>();
        foreach (Type type in assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<MessageHandlerAttribute>();
            bool isHandler = typeof(IMessageHandler).IsAssignableFrom(type);
            if (attribute == null && (!isHandler || type.IsAbstract || type.ContainsGenericParameters)) continue;
            if (!isHandler || !type.IsClass || type.IsAbstract || type.ContainsGenericParameters
                || attribute == null || type.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException("非法 Handler：" + type.FullName);
            }
            if (discovered.ContainsKey(attribute.Id))
                throw new InvalidOperationException("重复消息编号 " + attribute.Id + "：" + type.FullName);
            discovered.Add(attribute.Id, (IMessageHandler)Activator.CreateInstance(type));
        }
        if (discovered.Count == 0)
            throw new InvalidOperationException("程序集没有发现任何 Handler：" + assembly.GetName().Name);
        handlers = discovered;
    }

    public void Dispatch(Entity entity, int connectionId, MsgId id, ByteString body)
    {
        if (entity == null || entity.IsDisposed) return;
        if (!handlers.TryGetValue(id, out IMessageHandler handler))
        {
            GameLog.Warning($"未处理的消息：{id} connection={connectionId}");
            return;
        }
        try { handler.Dispatch(entity, connectionId, body); }
        catch (Exception error)
        {
            GameLog.Error($"消息处理失败：{id} connection={connectionId} Handler={handler.GetType().Name}", error);
        }
    }
}
