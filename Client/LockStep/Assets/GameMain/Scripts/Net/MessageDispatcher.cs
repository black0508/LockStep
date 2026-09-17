using System;
using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Net
{
    public sealed class MessageDispatcher
    {
        Dictionary<MsgId, IMessageHandler> handlers = new Dictionary<MsgId, IMessageHandler>();

        // 只在启动时扫描指定程序集。整批成功才替换路由，失败不会留下半张表。
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
                    throw new InvalidOperationException("非法 Handler：" + type.FullName + "；需要具体类、消息标记和公共无参构造");
                }
                discovered.Add(attribute.Id, (IMessageHandler)Activator.CreateInstance(type));
            }
            if (discovered.Count == 0)
            {
                throw new InvalidOperationException("程序集没有发现任何 Handler：" + assembly.GetName().Name);
            }
            handlers = discovered;
        }

        public void Dispatch(Entity entity, MsgId id, ByteString body)
        {
            if (entity == null || entity.IsDisposed) return;
            if (!handlers.TryGetValue(id, out IMessageHandler handler))
            {
                GameLog.Warning("未处理的消息：" + id, nameof(MessageDispatcher));
                return;
            }
            try
            {
                handler.Dispatch(entity, body);
            }
            catch (Exception error)
            {
                GameLog.Error("消息处理失败：" + id + "，Handler=" + handler.GetType().Name, nameof(MessageDispatcher), error);
            }
        }
    }
}
