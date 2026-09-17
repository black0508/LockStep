using System;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Net
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class MessageHandlerAttribute : Attribute
    {
        public MsgId Id { get; }
        public MessageHandlerAttribute(MsgId id) { Id = id; }
    }

    public interface IMessageHandler
    {
        void Dispatch(Entity entity, ByteString body);
    }

    // Handler 只负责消息到组件的适配，不保存组件实例；生命周期跟随分发器。
    public abstract class MessageHandler<TComponent, TMessage> : IMessageHandler
        where TComponent : Component
        where TMessage : IMessage<TMessage>, new()
    {
        static readonly MessageParser<TMessage> parser = new MessageParser<TMessage>(() => new TMessage());

        public void Dispatch(Entity entity, ByteString body)
        {
            TComponent component = entity.GetComponent<TComponent>();
            if (component == null || component.IsDisposed)
            {
                GameLog.Warning("消息目标组件不存在：" + typeof(TComponent).Name, GetType().Name);
                return;
            }
            Handle(component, parser.ParseFrom(body));
        }

        protected abstract void Handle(TComponent component, TMessage message);
    }
}
