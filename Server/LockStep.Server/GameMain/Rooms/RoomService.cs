using System;
using System.Collections.Generic;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Server.Config;
using LockStep.Server.Core;
using LockStep.Server.Net;

namespace LockStep.Server.Rooms;

// 房间的编排层：维护连接到房间的索引、房间生命周期，并负责所有下行广播
public sealed class RoomService
{
    readonly ServerConfig config;
    readonly IMessageSender sender;
    readonly Dictionary<uint, Room> rooms = new Dictionary<uint, Room>();
    readonly Dictionary<int, Room> connectionToRoom = new Dictionary<int, Room>();
    readonly List<uint> recycleBuffer = new List<uint>();

    public RoomService(ServerConfig config, IMessageSender sender)
    {
        this.config = config;
        this.sender = sender;
    }

    public void OnJoin(int connectionId, C2SJoin msg)
    {
        if (connectionToRoom.ContainsKey(connectionId))
        {
            RejectJoin(connectionId, JoinRejectReason.JoinRejectAlreadyInRoom);
            return;
        }

        if (!rooms.TryGetValue(msg.RoomId, out Room room))
        {
            room = new Room(msg.RoomId, config.MaxPlayersPerRoom, config.MinPlayersToStart);
            rooms[msg.RoomId] = room;
        }

        JoinRejectReason reject = room.TryJoin(connectionId, msg.NickName, out RoomMember member);
        if (reject != JoinRejectReason.JoinRejectUnspecified)
        {
            RejectJoin(connectionId, reject);
            return;
        }

        connectionToRoom[connectionId] = room;
        Log.Info($"进房 房间={room.RoomId} playerId={member.PlayerId} 昵称={member.NickName} connection={connectionId} 人数={room.Members.Count}");
        sender.Send(connectionId, MsgId.S2CJoinAck, RoomMessages.ToJoinAck(room, member));
        Broadcast(room, MsgId.S2CRoomUpdate, RoomMessages.ToRoomUpdate(room), connectionId);
    }

    public void OnStart(int connectionId)
    {
        if (!connectionToRoom.TryGetValue(connectionId, out Room room))
        {
            RejectStart(connectionId, StartRejectReason.StartRejectNotInRoom);
            return;
        }

        StartRejectReason reject = room.TryStart(connectionId);
        if (reject != StartRejectReason.StartRejectUnspecified)
        {
            RejectStart(connectionId, reject);
            return;
        }

        room.BeginMatch(new MatchSettings(config.TickRate, config.InputDelayFrames, NextSeed()));
        Log.Info($"开战 房间={room.RoomId} 房主playerId={room.HostPlayerId} 人数={room.Members.Count} seed={room.Match.Seed}");
        Broadcast(room, MsgId.S2CMatchStart, RoomMessages.ToMatchStart(room));
    }

    public void OnClientDisconnected(int connectionId)
    {
        if (!connectionToRoom.TryGetValue(connectionId, out Room room))
        {
            return;
        }

        connectionToRoom.Remove(connectionId);
        RoomMember member = room.Detach(connectionId);
        if (member == null)
        {
            Log.Error($"索引与房间名单不一致 房间={room.RoomId} connection={connectionId}");
            return;
        }

        Log.Info($"退房 房间={room.RoomId} playerId={member.PlayerId} 昵称={member.NickName} connection={connectionId} 剩余={room.Members.Count}");

        // 对局中名单和房主都不能动，否则客户端会在局内改写房主身份
        if (room.State == RoomState.Lobby && !room.IsAbandoned)
        {
            Broadcast(room, MsgId.S2CRoomUpdate, RoomMessages.ToRoomUpdate(room));
        }
    }

    public void Tick(long nowMs)
    {
        foreach (KeyValuePair<uint, Room> pair in rooms)
        {
            if (pair.Value.IsAbandoned)
            {
                recycleBuffer.Add(pair.Key);
                continue;
            }

            pair.Value.Tick(nowMs);
        }

        // 空房回收只在这里做，各消息处理里不再各自清理
        for (int i = 0; i < recycleBuffer.Count; i++)
        {
            rooms.Remove(recycleBuffer[i]);
            Log.Info($"房间关闭 房间={recycleBuffer[i]}");
        }

        recycleBuffer.Clear();
    }

    void RejectJoin(int connectionId, JoinRejectReason reason)
    {
        Log.Info($"进房被拒 connection={connectionId} 原因={reason}");
        sender.Send(connectionId, MsgId.S2CJoinReject, new S2CJoinReject { Reason = reason });
    }

    void RejectStart(int connectionId, StartRejectReason reason)
    {
        Log.Info($"开战被拒 connection={connectionId} 原因={reason}");
        sender.Send(connectionId, MsgId.S2CStartReject, new S2CStartReject { Reason = reason });
    }

    void Broadcast(Room room, MsgId msgId, IMessage msg, int exceptConnectionId = -1)
    {
        IReadOnlyList<RoomMember> members = room.Members;
        for (int i = 0; i < members.Count; i++)
        {
            RoomMember member = members[i];
            if (member.Connected && member.ConnectionId != exceptConnectionId)
            {
                sender.Send(member.ConnectionId, msgId, msg);
            }
        }
    }

    static uint NextSeed()
    {
        return (uint)Random.Shared.Next(1, int.MaxValue);
    }
}
