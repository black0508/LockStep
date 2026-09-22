using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Lockstep.Proto;
using LockStep.Framework;
using LockStep.Server.Config;
using LockStep.Server.FrameSync;
using LockStep.Server.Net;
using LockStep.Server.Net.Events;

namespace LockStep.Server.Rooms;

// 进程内唯一对局空间：名单、进房、广播、人齐开战。不做多房间索引。
// PlayerId 按进房顺序递增分配，名单始终按 PlayerId 升序。
public sealed class RoomComponent : Component
{
    readonly List<RoomMember> members = new List<RoomMember>();
    ServerConfig config;
    NetworkComponent network;
    EventComponent events;
    FrameSyncComponent frameSync;
    uint nextPlayerId = 1;
    bool playing;

    protected override void OnAwake()
    {
        network = Entity.GetComponent<NetworkComponent>();
        events = Entity.GetComponent<EventComponent>();
        frameSync = Entity.GetComponent<FrameSyncComponent>();
        events.Subscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
    }

    public void Init(ServerConfig value)
    {
        config = value;
    }

    public void OnJoin(int connectionId, C2SJoin message)
    {
        if (members.FindIndex(m => m.ConnectionId == connectionId) >= 0)
        {
            GameLog.Warning($"重复进房 connection={connectionId}");
            return;
        }
        if (playing)
        {
            Reject(connectionId, JoinRejectReason.JoinRejectPlaying);
            return;
        }
        if (members.Count >= config.MaxPlayersPerRoom)
        {
            Reject(connectionId, JoinRejectReason.JoinRejectFull);
            return;
        }

        var member = new RoomMember(connectionId, nextPlayerId++, message.NickName);
        members.Add(member);
        GameLog.Info($"进房 playerId={member.PlayerId} 昵称={member.NickName} connection={connectionId} 人数={members.Count}");
        var ack = new S2CJoinAck { PlayerId = member.PlayerId };
        FillPlayers(ack.Players);
        network.TrySend(connectionId, MsgId.S2CJoinAck, ack);
        BroadcastRoomUpdate();
        if (members.Count >= config.MinPlayersToStart) StartMatch();
    }

    void StartMatch()
    {
        playing = true;
        var start = new S2CMatchStart { TickHz = config.TickRate, Seed = (uint)Random.Shared.Next(1, int.MaxValue) };
        FillPlayers(start.Players);
        GameLog.Info($"开战 人数={members.Count} seed={start.Seed}");
        Broadcast(MsgId.S2CMatchStart, start);
        frameSync.Start(members, config.TickRate);
    }

    void OnDisconnected(object sender, GameEventArgs args)
    {
        int connectionId = ((NetworkDisconnectedEventArgs)args).ConnectionId;
        int index = members.FindIndex(m => m.ConnectionId == connectionId);
        if (index < 0) return;

        RoomMember member = members[index];
        GameLog.Info($"退房 playerId={member.PlayerId} 昵称={member.NickName} connection={connectionId} 剩余={members.Count - 1}");
        if (playing)
        {
            AbortMatch();
            return;
        }

        members.RemoveAt(index);
        if (members.Count == 0) Reset();
        else BroadcastRoomUpdate();
    }

    // 对局中有人掉线则本局作废：先重置房间再断开其余连接，断开会同步回调 OnDisconnected，此时名单已空。
    void AbortMatch()
    {
        List<RoomMember> dropped = new List<RoomMember>(members);
        Reset();
        GameLog.Info("对局中断，房间已重置");
        foreach (RoomMember member in dropped) network.Disconnect(member.ConnectionId);
    }

    void Reset()
    {
        frameSync.Stop();
        members.Clear();
        nextPlayerId = 1;
        playing = false;
    }

    void Reject(int connectionId, JoinRejectReason reason)
    {
        GameLog.Info($"进房被拒 connection={connectionId} 原因={reason}");
        network.TrySend(connectionId, MsgId.S2CJoinReject, new S2CJoinReject { Reason = reason });
    }

    void BroadcastRoomUpdate()
    {
        var update = new S2CRoomUpdate();
        FillPlayers(update.Players);
        Broadcast(MsgId.S2CRoomUpdate, update);
    }

    void Broadcast(MsgId id, IMessage message)
    {
        foreach (RoomMember member in members) network.TrySend(member.ConnectionId, id, message);
    }

    void FillPlayers(RepeatedField<RoomPlayer> players)
    {
        foreach (RoomMember member in members)
            players.Add(new RoomPlayer { PlayerId = member.PlayerId, NickName = member.NickName });
    }

    protected override void OnDestroy()
    {
        events.Unsubscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
    }
}
