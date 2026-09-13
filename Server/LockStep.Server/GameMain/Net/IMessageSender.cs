using Google.Protobuf;
using Lockstep.Proto;

namespace LockStep.Server.Net;

// 业务层对外发包的唯一出口，让房间逻辑不依赖具体传输实现
public interface IMessageSender
{
    void Send(int connectionId, MsgId msgId, IMessage msg);
}
