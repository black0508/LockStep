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
        readonly Dictionary<MsgId, IMessageHandler> handlers = new Dictionary<MsgId, IMessageHandler>();

        // 每个分发器只在启动时注册一次；失败由组合根清理整个运行时。
        public void RegisterAssembly(Assembly assembly)
        {
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
                handlers.Add(attribute.Id, (IMessageHandler)Activator.CreateInstance(type));
            }
            if (handlers.Count == 0)
            {
                throw new InvalidOperationException("程序集没有发现任何 Handler：" + assembly.GetName().Name);
            }
        }

        public void Dispatch(Entity entity, MsgId id, ByteString body)
        {
            if (!handlers.TryGetValue(id, out IMessageHandler handler))
            {
                GameLog.Warning($"未处理的消息：{id}");
                return;
            }
            handler.Dispatch(entity, body);
        }
    }
}
