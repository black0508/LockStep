using System.Collections.Generic;
using Lockstep.Proto;

namespace LockStep.Server.Rooms;

public enum RoomState
{
    Lobby,
    Playing
}

// 一局对战的参数，开战瞬间定下，全员必须一致
public readonly record struct MatchSettings(uint TickHz, uint InputDelayFrames, uint Seed);

// 房间的状态与规则。不做网络 IO，也不组装协议消息，便于脱离网络单测。
public sealed class Room
{
    readonly uint maxPlayers;
    readonly uint minPlayersToStart;
    readonly List<RoomMember> members = new List<RoomMember>();
    uint nextPlayerId = 1;

    public uint RoomId { get; }
    public RoomState State { get; private set; }
    public uint HostPlayerId { get; private set; }
    public MatchSettings Match { get; private set; }

    public bool IsReadyToStart => State == RoomState.Lobby && members.Count >= minPlayersToStart;

    public IReadOnlyList<RoomMember> Members
    {
        get { return members; }
    }

    // 没有任何在线成员的房间可以回收
    public bool IsAbandoned
    {
        get
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Connected)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public Room(uint roomId, uint maxPlayers, uint minPlayersToStart)
    {
        RoomId = roomId;
        this.maxPlayers = maxPlayers;
        this.minPlayersToStart = minPlayersToStart;
        State = RoomState.Lobby;
    }

    public RoomMember Find(int connectionId)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].ConnectionId == connectionId)
            {
                return members[i];
            }
        }

        return null;
    }

    public bool IsHost(RoomMember member)
    {
        return member != null && member.PlayerId == HostPlayerId;
    }

    // 返回 JoinRejectUnspecified 表示成功，member 为新成员
    public JoinRejectReason TryJoin(int connectionId, string nickName, out RoomMember member)
    {
        member = null;
        if (State != RoomState.Lobby)
        {
            return JoinRejectReason.JoinRejectPlaying;
        }

        if (members.Count >= maxPlayers)
        {
            return JoinRejectReason.JoinRejectFull;
        }

        member = new RoomMember(connectionId, nextPlayerId, nickName);
        nextPlayerId += 1;
        members.Add(member);
        if (HostPlayerId == 0)
        {
            HostPlayerId = member.PlayerId;
        }

        return JoinRejectReason.JoinRejectUnspecified;
    }

    public void BeginMatch(MatchSettings settings)
    {
        Match = settings;
        State = RoomState.Playing;
    }

    // 连接断开。大厅内直接移出名单并迁移房主；对局中必须保住槽位和 PlayerId，
    // 否则输入编号会错位，因此只标记离线，留给后续重连接回。
    public RoomMember Detach(int connectionId)
    {
        for (int i = 0; i < members.Count; i++)
        {
            RoomMember member = members[i];
            if (member.ConnectionId != connectionId)
            {
                continue;
            }

            member.Connected = false;
            if (State == RoomState.Lobby)
            {
                members.RemoveAt(i);
                if (member.PlayerId == HostPlayerId)
                {
                    HostPlayerId = members.Count > 0 ? members[0].PlayerId : 0;
                }
            }

            return member;
        }

        return null;
    }

    public void Tick(long nowMs)
    {
        if (State != RoomState.Playing)
        {
            return;
        }

        // TODO 帧同步：按 Match.TickHz 推进逻辑帧，收集输入并广播帧数据
    }
}
