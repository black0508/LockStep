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
        readonly GameLog log;
        Dictionary<MsgId, IMessageHandler> handlers = new Dictionary<MsgId, IMessageHandler>();

        public MessageDispatcher(GameLog log) { this.log = log; }

        // 只在启动时扫描指定程序集。整批成功才替换路由，失败不会留下半张表。
        public bool RegisterAssembly(Assembly assembly)
        {
            var discovered = new Dictionary<MsgId, IMessageHandler>();
            try
            {
                if (assembly == null)
                {
                    log.Error("没有指定 Handler 程序集", nameof(MessageDispatcher));
                    return false;
                }
                foreach (Type type in assembly.GetTypes())
                {
                    var attribute = type.GetCustomAttribute<MessageHandlerAttribute>();
                    bool isHandler = typeof(IMessageHandler).IsAssignableFrom(type);
                    if (attribute == null && (!isHandler || type.IsAbstract || type.ContainsGenericParameters)) continue;
                    if (!isHandler || !type.IsClass || type.IsAbstract || type.ContainsGenericParameters
                        || attribute == null || type.GetConstructor(Type.EmptyTypes) == null)
                    {
                        log.Error("非法 Handler：" + type.FullName + "；需要具体类、消息标记和公共无参构造", nameof(MessageDispatcher));
                        return false;
                    }
                    if (discovered.ContainsKey(attribute.Id))
                    {
                        log.Error("重复消息编号 " + attribute.Id + "：" + type.FullName, nameof(MessageDispatcher));
                        return false;
                    }
                    discovered.Add(attribute.Id, (IMessageHandler)Activator.CreateInstance(type));
                }
                if (discovered.Count == 0)
                {
                    log.Error("程序集没有发现任何 Handler：" + assembly.GetName().Name, nameof(MessageDispatcher));
                    return false;
                }
                handlers = discovered;
                return true;
            }
            catch (Exception error)
            {
                log.Error("Handler 扫描或创建失败：" + assembly?.GetName().Name, nameof(MessageDispatcher), error);
                return false;
            }
        }

        public void Dispatch(Entity entity, MsgId id, ByteString body)
        {
            if (entity == null || entity.IsDisposed) return;
            if (!handlers.TryGetValue(id, out IMessageHandler handler))
            {
                log.Warning("未处理的消息：" + id, nameof(MessageDispatcher));
                return;
            }
            try
            {
                handler.Dispatch(entity, body);
            }
            catch (Exception error)
            {
                log.Error("消息处理失败：" + id + "，Handler=" + handler.GetType().Name, nameof(MessageDispatcher), error);
            }
        }
    }
}
