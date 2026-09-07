using System.Collections.Generic;
using Lockstep.Proto;

namespace LockStep.Server.Room;

public sealed class RoomInfo
{
    readonly uint maxPlayers;
    readonly List<PlayerSession> sessions = new List<PlayerSession>();
    uint nextPlayerId = 1;

    public uint RoomId { get; }
    public bool Playing { get; private set; }
    public IReadOnlyList<PlayerSession> Sessions
    {
        get { return sessions; }
    }

    public bool IsEmpty
    {
        get { return sessions.Count == 0; }
    }

    public RoomInfo(uint roomId, uint maxPlayers)
    {
        RoomId = roomId;
        this.maxPlayers = maxPlayers;
    }

    public PlayerSession TryJoin(int connectionId, string nickName, out JoinRejectReason reject)
    {
        reject = JoinRejectReason.JoinRejectUnspecified;
        if (Playing)
        {
            reject = JoinRejectReason.JoinRejectPlaying;
            return null;
        }

        if (sessions.Count >= maxPlayers)
        {
            reject = JoinRejectReason.JoinRejectFull;
            return null;
        }

        PlayerSession session = new PlayerSession(connectionId, nextPlayerId, nickName, RoomId);
        nextPlayerId += 1;
        sessions.Add(session);
        return session;
    }

    public bool TryStart(int connectionId)
    {
        if (Playing || sessions.Count == 0 || sessions[0].ConnectionId != connectionId)
        {
            return false;
        }

        Playing = true;
        return true;
    }

    public void Leave(PlayerSession session)
    {
        sessions.Remove(session);
    }

    public S2CJoinAck ToJoinAck(PlayerSession self)
    {
        S2CJoinAck ack = new S2CJoinAck
        {
            RoomId = RoomId,
            PlayerId = self.PlayerId,
            IsHost = sessions[0] == self
        };
        Fill(ack.Players);
        return ack;
    }

    public S2CRoomUpdate ToRoomUpdate()
    {
        S2CRoomUpdate update = new S2CRoomUpdate { RoomId = RoomId };
        Fill(update.Players);
        return update;
    }

    void Fill(Google.Protobuf.Collections.RepeatedField<RoomPlayer> dest)
    {
        for (int i = 0; i < sessions.Count; i++)
        {
            PlayerSession session = sessions[i];
            dest.Add(new RoomPlayer
            {
                PlayerId = session.PlayerId,
                IsHost = i == 0,
                NickName = session.NickName
            });
        }
    }
}
