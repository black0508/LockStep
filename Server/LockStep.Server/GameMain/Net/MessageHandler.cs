using System;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace LockStep.Server.Net;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MessageHandlerAttribute : Attribute
{
    public MsgId Id { get; }
    public MessageHandlerAttribute(MsgId id) { Id = id; }
}

public interface IMessageHandler
{
    void Dispatch(Entity entity, int connectionId, ByteString body);
}

// 无状态 Handler。每次从接收消息的实体取得当前组件，不捕获房间或管理器。
public abstract class MessageHandler<TComponent, TMessage> : IMessageHandler
    where TComponent : Component
    where TMessage : IMessage<TMessage>, new()
{
    static readonly MessageParser<TMessage> parser = new MessageParser<TMessage>(() => new TMessage());

    public void Dispatch(Entity entity, int connectionId, ByteString body)
    {
        TComponent component = entity.GetComponent<TComponent>();
        if (component == null || component.IsDisposed)
        {
            GameLog.Warning($"消息目标组件不存在：{typeof(TComponent).Name}");
            return;
        }
        Handle(component, connectionId, parser.ParseFrom(body));
    }

    protected abstract void Handle(TComponent component, int connectionId, TMessage message);
}
