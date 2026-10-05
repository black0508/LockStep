using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Lockstep.Proto;
using Framework;

namespace GameMain;

// 进程内唯一对局空间：名单、进房、广播、人齐开战。不做多房间索引。
// PlayerId 按进房顺序递增分配，名单始终按 PlayerId 升序。
public sealed class RoomComponent : Component
{
    readonly List<RoomMember> members = new List<RoomMember>();
    ServerConfig config;
    uint nextPlayerId = 1;
    bool playing;

    protected override void OnAwake()
    {
        Entity.GetComponent<EventComponent>().Subscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
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
        Entity.GetComponent<NetworkComponent>().TrySend(connectionId, MsgId.S2CJoinAck, ack);
        BroadcastRoomUpdate();
        if (members.Count < config.MinPlayersToStart) return;

        playing = true;
        var start = new S2CMatchStart { Seed = (uint)Random.Shared.Next(1, int.MaxValue) };
        FillPlayers(start.Players);
        GameLog.Info($"开战 人数={members.Count} seed={start.Seed}");
        Broadcast(MsgId.S2CMatchStart, start);
        Entity.GetComponent<FrameSyncComponent>().Start(members);
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
            // 先重置房间再断开其余连接，断开会同步回调 OnDisconnected，此时名单已空。
            List<RoomMember> dropped = new List<RoomMember>(members);
            Reset();
            GameLog.Info("对局中断，房间已重置");
            NetworkComponent network = Entity.GetComponent<NetworkComponent>();
            foreach (RoomMember droppedMember in dropped) network.Disconnect(droppedMember.ConnectionId);
            return;
        }

        members.RemoveAt(index);
        if (members.Count == 0) Reset();
        else BroadcastRoomUpdate();
    }

    void Reset()
    {
        Entity.GetComponent<FrameSyncComponent>().Stop();
        members.Clear();
        nextPlayerId = 1;
        playing = false;
    }

    void Reject(int connectionId, JoinRejectReason reason)
    {
        GameLog.Info($"进房被拒 connection={connectionId} 原因={reason}");
        Entity.GetComponent<NetworkComponent>().TrySend(connectionId, MsgId.S2CJoinReject, new S2CJoinReject { Reason = reason });
    }

    void BroadcastRoomUpdate()
    {
        var update = new S2CRoomUpdate();
        FillPlayers(update.Players);
        Broadcast(MsgId.S2CRoomUpdate, update);
    }

    void Broadcast(MsgId id, IMessage message)
    {
        NetworkComponent network = Entity.GetComponent<NetworkComponent>();
        foreach (RoomMember member in members) network.TrySend(member.ConnectionId, id, message);
    }

    void FillPlayers(RepeatedField<RoomPlayer> players)
    {
        foreach (RoomMember member in members)
            players.Add(new RoomPlayer { PlayerId = member.PlayerId, NickName = member.NickName });
    }

    protected override void OnDestroy()
    {
        Entity.GetComponent<EventComponent>().Unsubscribe(NetworkDisconnectedEventArgs.EventId, OnDisconnected);
    }
}
