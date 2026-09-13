using System.Collections.Generic;
using Google.Protobuf.Collections;
using Lockstep.Proto;

namespace LockStep.Server.Rooms;

// 房间到下行协议消息的转换集中在这里，房间模型本身不碰 protobuf
public static class RoomMessages
{
    public static S2CJoinAck ToJoinAck(Room room, RoomMember self)
    {
        S2CJoinAck ack = new S2CJoinAck
        {
            RoomId = room.RoomId,
            PlayerId = self.PlayerId,
            IsHost = room.IsHost(self)
        };
        FillPlayers(room, ack.Players);
        return ack;
    }

    public static S2CRoomUpdate ToRoomUpdate(Room room)
    {
        S2CRoomUpdate update = new S2CRoomUpdate { RoomId = room.RoomId };
        FillPlayers(room, update.Players);
        return update;
    }

    public static S2CMatchStart ToMatchStart(Room room)
    {
        return new S2CMatchStart
        {
            TickHz = room.Match.TickHz,
            InputDelayFrames = room.Match.InputDelayFrames,
            Seed = room.Match.Seed
        };
    }

    static void FillPlayers(Room room, RepeatedField<RoomPlayer> dest)
    {
        IReadOnlyList<RoomMember> members = room.Members;
        for (int i = 0; i < members.Count; i++)
        {
            RoomMember member = members[i];
            dest.Add(new RoomPlayer
            {
                PlayerId = member.PlayerId,
                IsHost = room.IsHost(member),
                NickName = member.NickName
            });
        }
    }
}
