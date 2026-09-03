using System;
using Google.Protobuf;

namespace LockStep.Server.Net
{
    public static class Opcode
    {
        public const ushort C2S_Ping = 1;
        public const ushort S2C_Pong = 2;
        public const ushort C2S_Join = 3;
        public const ushort S2C_JoinAck = 4;
        public const ushort S2C_JoinReject = 5;
        public const ushort S2C_MatchStart = 6;
    }

    public static class MsgCodec
    {
        public static byte[] Encode(ushort opcode, IMessage msg)
        {
            byte[] body = msg.ToByteArray();
            byte[] buf = new byte[2 + body.Length];
            buf[0] = (byte)opcode;
            buf[1] = (byte)(opcode >> 8);
            Buffer.BlockCopy(body, 0, buf, 2, body.Length);
            return buf;
        }

        public static bool TryRead(byte[] payload, out ushort opcode)
        {
            if (payload == null || payload.Length < 2)
            {
                opcode = 0;
                return false;
            }

            opcode = (ushort)(payload[0] | (payload[1] << 8));
            return true;
        }

        public static T Parse<T>(byte[] payload, MessageParser<T> parser) where T : IMessage<T>
        {
            return parser.ParseFrom(payload, 2, payload.Length - 2);
        }
    }
}
