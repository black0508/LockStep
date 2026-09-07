using System;
using System.Collections.Generic;
using Lockstep.Proto;
using LockStep.Server;
using LockStep.Server.Config;
using LockStep.Server.Net;

namespace LockStep.Server.Room;

public sealed class RoomManager : ManagerBase
{
    readonly ServerConfig config;
    readonly Dictionary<uint, RoomInfo> rooms = new Dictionary<uint, RoomInfo>();
    readonly Dictionary<int, PlayerSession> sessions = new Dictionary<int, PlayerSession>();

    public RoomManager(ServerConfig config)
    {
        this.config = config;
        Register();
    }

    public void HandleJoin(int connectionId, C2SJoin msg)
    {
        if (sessions.ContainsKey(connectionId))
        {
            Reject(connectionId, JoinRejectReason.JoinRejectAlreadyInRoom);
            return;
        }

        RoomInfo room;
        if (!rooms.TryGetValue(msg.RoomId, out room))
        {
            room = new RoomInfo(msg.RoomId, config.MaxPlayersPerRoom);
            rooms[msg.RoomId] = room;
        }

        PlayerSession session = room.TryJoin(connectionId, msg.NickName, out JoinRejectReason reject);
        if (session == null)
        {
            if (room.IsEmpty)
            {
                rooms.Remove(msg.RoomId);
            }

            Reject(connectionId, reject);
            return;
        }

        sessions[connectionId] = session;
        Console.WriteLine("收到加入房间 房间=" + session.RoomId + " playerId=" + session.PlayerId + " 昵称=" + session.NickName + " 连接=" + connectionId + " 人数=" + room.Sessions.Count);
        Send(connectionId, MsgId.S2CJoinAck, room.ToJoinAck(session));
        Broadcast(room, MsgId.S2CRoomUpdate, room.ToRoomUpdate(), connectionId);
    }

    public void HandleStart(int connectionId)
    {
        PlayerSession session;
        if (!sessions.TryGetValue(connectionId, out session))
        {
            return;
        }

        RoomInfo room = rooms[session.RoomId];
        if (!room.TryStart(connectionId))
        {
            return;
        }

        Console.WriteLine("收到开始游戏 房间=" + session.RoomId + " 房主playerId=" + session.PlayerId + " 昵称=" + session.NickName + " 人数=" + room.Sessions.Count);
        Broadcast(room, MsgId.S2CMatchStart, new S2CMatchStart
        {
            TickHz = config.TickRate,
            InputDelayFrames = config.InputDelayFrames,
            Seed = config.Seed
        });
    }

    public void HandleDisconnect(int connectionId)
    {
        PlayerSession session;
        if (!sessions.TryGetValue(connectionId, out session))
        {
            return;
        }

        sessions.Remove(connectionId);
        RoomInfo room = rooms[session.RoomId];
        room.Leave(session);
        Console.WriteLine("收到退出房间 房间=" + session.RoomId + " playerId=" + session.PlayerId + " 昵称=" + session.NickName + " 连接=" + connectionId + " 剩余=" + room.Sessions.Count);

        if (room.IsEmpty)
        {
            rooms.Remove(session.RoomId);
            return;
        }

        Broadcast(room, MsgId.S2CRoomUpdate, room.ToRoomUpdate());
    }

    void Reject(int connectionId, JoinRejectReason reason)
    {
        Send(connectionId, MsgId.S2CJoinReject, new S2CJoinReject { Reason = reason });
    }

    static void Send(int connectionId, MsgId msgId, Google.Protobuf.IMessage msg)
    {
        NetworkServer network = GameEntry.NetworkServer;
        if (network == null)
        {
            return;
        }

        network.Send(connectionId, msgId, msg);
    }

    static void Broadcast(RoomInfo room, MsgId msgId, Google.Protobuf.IMessage msg, int exceptConnectionId = -1)
    {
        IReadOnlyList<PlayerSession> list = room.Sessions;
        for (int i = 0; i < list.Count; i++)
        {
            int id = list[i].ConnectionId;
            if (id != exceptConnectionId)
            {
                Send(id, msgId, msg);
            }
        }
    }
}
