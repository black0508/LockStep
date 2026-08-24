using System;
using GameMain.Net;
using Google.Protobuf;
using Lockstep.Proto;

public class LockstepSession
{
    const uint ProtocolVersion = 1;

    public Action<string> Log;

    KcpClientTransport client;
    uint nextPingMs;
    uint lastPongMs;
    uint playerId;
    bool hasPlayerId;

    public LockstepSession(KcpClientTransport client)
    {
        this.client = client;
        this.client.Connected = OnConnected;
        this.client.Disconnected = OnDisconnected;
    }

    public void Tick()
    {
        if (client == null || !client.IsConnected)
        {
            return;
        }

        uint now = (uint)Environment.TickCount;
        byte[] payload;
        while (client.TryRecv(out payload))
        {
            Handle(payload, now);
        }

        if ((int)(now - lastPongMs) > 3000)
        {
            WriteLog("[LockStep] pong timeout");
            client.Close();
            return;
        }

        if ((int)(now - nextPingMs) >= 0)
        {
            SendPing(now);
        }
    }

    void OnConnected()
    {
        uint now = (uint)Environment.TickCount;
        lastPongMs = now;
        WriteLog("[LockStep] KCP connected");
        SendJoin();
        SendPing(now);
    }

    void OnDisconnected()
    {
        WriteLog("[LockStep] disconnected");
    }

    void Handle(byte[] payload, uint now)
    {
        Packet packet;
        try
        {
            packet = Packet.Parser.ParseFrom(payload);
        }
        catch (InvalidProtocolBufferException)
        {
            return;
        }

        if (packet.BodyCase == Packet.BodyOneofCase.Pong)
        {
            lastPongMs = now;
            WriteLog("[LockStep] RTT=" + (now - packet.Pong.ClientSendMs) + " ms");
            return;
        }

        if (packet.BodyCase == Packet.BodyOneofCase.JoinAck)
        {
            playerId = packet.JoinAck.PlayerId;
            hasPlayerId = true;
            WriteLog("[LockStep] JoinAck playerId=" + playerId);
            return;
        }

        if (packet.BodyCase == Packet.BodyOneofCase.JoinReject)
        {
            WriteLog("[LockStep] JoinReject reason=" + packet.JoinReject.Reason);
            return;
        }

        if (packet.BodyCase == Packet.BodyOneofCase.MatchStart)
        {
            MatchStart start = packet.MatchStart;
            WriteLog("[LockStep] MatchStart tickHz=" + start.TickHz
                + " delay=" + start.InputDelayFrames
                + " seed=" + start.Seed
                + " arena=" + start.ArenaHalfExtentMm
                + " playerId=" + (hasPlayerId ? playerId.ToString() : "?"));
        }
    }

    void SendJoin()
    {
        Join join = new Join();
        join.ProtocolVersion = ProtocolVersion;
        Packet packet = new Packet();
        packet.Join = join;
        client.Send(packet.ToByteArray());
    }

    void SendPing(uint now)
    {
        Ping ping = new Ping();
        ping.ClientSendMs = now;
        Packet packet = new Packet();
        packet.Ping = ping;
        client.Send(packet.ToByteArray());
        nextPingMs = now + 500;
    }

    void WriteLog(string msg)
    {
        if (Log != null)
        {
            Log(msg);
        }
    }
}
