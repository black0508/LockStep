using Google.Protobuf;
using Lockstep.Proto;

namespace GameMain.Net
{
    public static class MsgCodec
    {
        public static byte[] Encode(MsgId msgId, IMessage msg)
        {
            Packet packet = new Packet();
            packet.Id = msgId;
            packet.Body = msg.ToByteString();
            return packet.ToByteArray();
        }

        public static bool TryUnpack(byte[] payload, out MsgId msgId, out ByteString body)
        {
            msgId = MsgId.Unspecified;
            body = ByteString.Empty;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            Packet packet;
            try
            {
                packet = Packet.Parser.ParseFrom(payload);
            }
            catch (InvalidProtocolBufferException)
            {
                return false;
            }

            msgId = packet.Id;
            body = packet.Body;
            return true;
        }

        public static T Parse<T>(ByteString body, MessageParser<T> parser) where T : IMessage<T>
        {
            return parser.ParseFrom(body);
        }
    }
}
