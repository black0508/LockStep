using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Lockstep.Proto;
using LockStep.Framework;
using LockStep.Server.Config;
using LockStep.Server.Net;
using LockStep.Server.Net.Events;

namespace LockStep.Server.Rooms;

public enum RoomState
{
    Lobby,
    Playing
}

public readonly record struct MatchSettings(uint TickHz, uint InputDelayFrames, uint Seed);

// 进程内唯一对局空间：名单、进房、广播、人齐开战。不做多房间索引。
public sealed class RoomComponent : Component
{
    ServerConfig config;
    NetworkComponent network;
    EventComponent events;
    readonly List<RoomMember> members = new List<RoomMember>();
    uint nextPlayerId = 1;

    public RoomState State { get; private set; }
    public MatchSettings Match { get; private set; }

    public IReadOnlyList<RoomMember> Members
    {
        get { return members; }
    }

    public void Init(ServerConfig config)
    {
        if (IsDisposed || Entity == null || network != null)
            throw new InvalidOperationException("房间组件不可用或已经初始化");
        this.config = config;
        network = Entity.GetComponent<NetworkComponent>()
            ?? throw new InvalidOperationException("房间组件需要 NetworkComponent");
        events = Entity.GetComponent<EventComponent>()
            ?? throw new InvalidOperationException("房间组件需要 EventComponent");
        State = RoomState.Lobby;
        events.Subscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
    }

    public void OnJoin(int connectionId, C2SJoin msg)
    {
        if (IsDisposed || !network.IsConnected(connectionId)) return;
        if (Find(connectionId) != null)
        {
            GameLog.Warning($"重复进房 connection={connectionId}");
            return;
        }

        if (State == RoomState.Playing)
        {
            RejectJoin(connectionId, JoinRejectReason.JoinRejectPlaying);
            return;
        }

        if (members.Count >= config.MaxPlayersPerRoom)
        {
            RejectJoin(connectionId, JoinRejectReason.JoinRejectFull);
            return;
        }

        RoomMember member = new RoomMember(connectionId, nextPlayerId, msg.NickName);
        nextPlayerId += 1;
        members.Add(member);
        GameLog.Info($"进房 playerId={member.PlayerId} 昵称={member.NickName} connection={connectionId} 人数={members.Count}");
        network.TrySend(connectionId, MsgId.S2CJoinAck, ToJoinAck(member));
        Broadcast(MsgId.S2CRoomUpdate, ToRoomUpdate(), connectionId);
        TryStart();
    }

    void TryStart()
    {
        if (State != RoomState.Lobby || members.Count < config.MinPlayersToStart) return;
        Match = new MatchSettings(config.TickRate, config.InputDelayFrames, NextSeed());
        State = RoomState.Playing;
        GameLog.Info($"开战 人数={members.Count} seed={Match.Seed}");
        Broadcast(MsgId.S2CMatchStart, ToMatchStart());
    }

    void OnDisconnected(object sender, GameEventArgs args)
    {
        if (!ReferenceEquals(sender, network)) return;
        int connectionId = ((NetworkDisconnectedEventArgs)args).ConnectionId;
        int index = IndexOf(connectionId);
        if (index < 0) return;

        RoomMember member = members[index];
        GameLog.Info($"退房 playerId={member.PlayerId} 昵称={member.NickName} connection={connectionId} 剩余={members.Count - 1}");

        if (State == RoomState.Playing)
        {
            AbortMatch(connectionId);
            return;
        }

        members.RemoveAt(index);
        if (members.Count == 0)
        {
            Reset();
            return;
        }

        Broadcast(MsgId.S2CRoomUpdate, ToRoomUpdate());
    }

    // 对局中有人掉线则本局作废：清空房间并断开其余连接，客户端随断线回到空闲。
    void AbortMatch(int disconnectedId)
    {
        var toDrop = new List<int>();
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].ConnectionId != disconnectedId)
            {
                toDrop.Add(members[i].ConnectionId);
            }
        }

        Reset();
        GameLog.Info("对局中断，房间已重置");
        for (int i = 0; i < toDrop.Count; i++)
        {
            network.Disconnect(toDrop[i]);
        }
    }

    void Reset()
    {
        members.Clear();
        nextPlayerId = 1;
        State = RoomState.Lobby;
        Match = default;
    }

    RoomMember Find(int connectionId)
    {
        int index = IndexOf(connectionId);
        return index < 0 ? null : members[index];
    }

    int IndexOf(int connectionId)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].ConnectionId == connectionId)
            {
                return i;
            }
        }

        return -1;
    }

    void RejectJoin(int connectionId, JoinRejectReason reason)
    {
        GameLog.Info($"进房被拒 connection={connectionId} 原因={reason}");
        network.TrySend(connectionId, MsgId.S2CJoinReject, new S2CJoinReject { Reason = reason });
    }

    void Broadcast(MsgId msgId, IMessage msg, int? exceptConnectionId = null)
    {
        // Send 可能同步引发断线事件，从而改名单；不能遍历可变的原名单。
        var snapshot = new List<RoomMember>(members);
        for (int i = 0; i < snapshot.Count; i++)
        {
            RoomMember member = snapshot[i];
            if (member.ConnectionId != exceptConnectionId)
            {
                network.TrySend(member.ConnectionId, msgId, msg);
            }
        }
    }

    S2CJoinAck ToJoinAck(RoomMember self)
    {
        S2CJoinAck ack = new S2CJoinAck { PlayerId = self.PlayerId };
        FillPlayers(ack.Players);
        return ack;
    }

    S2CRoomUpdate ToRoomUpdate()
    {
        S2CRoomUpdate update = new S2CRoomUpdate();
        FillPlayers(update.Players);
        return update;
    }

    S2CMatchStart ToMatchStart()
    {
        return new S2CMatchStart
        {
            TickHz = Match.TickHz,
            InputDelayFrames = Match.InputDelayFrames,
            Seed = Match.Seed
        };
    }

    void FillPlayers(RepeatedField<RoomPlayer> dest)
    {
        for (int i = 0; i < members.Count; i++)
        {
            RoomMember member = members[i];
            dest.Add(new RoomPlayer { PlayerId = member.PlayerId, NickName = member.NickName });
        }
    }

    static uint NextSeed()
    {
        return (uint)Random.Shared.Next(1, int.MaxValue);
    }

    protected override void OnDestroy()
    {
        events?.Unsubscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
        Reset();
        events = null;
        network = null;
        config = null;
    }
}
