using System;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Server.Net;

namespace LockStep.Server
{
    public class Room
    {
        const uint ProtocolVersion = 1;
        const uint RejectVersion = 1;
        const uint RejectRoomFull = 2;
        const uint RejectAlreadyStarted = 3;

        KcpServerPeer[] seats = new KcpServerPeer[2];
        bool started;

        public void Tick(KcpServerTransport server)
        {
            Reap(server);
            for (int i = 0; i < server.Peers.Count; i++)
            {
                KcpServerPeer peer = server.Peers[i];
                byte[] payload;
                while (peer.TryRecv(out payload))
                {
                    Handle(peer, payload);
                }
            }
        }

        void Reap(KcpServerTransport server)
        {
            for (int i = 0; i < seats.Length; i++)
            {
                KcpServerPeer seat = seats[i];
                if (seat == null)
                {
                    continue;
                }

                if (FindPeer(server, seat.ChannelId) != null)
                {
                    continue;
                }

                if (started)
                {
                    Console.WriteLine("[LockStep] disconnect after match player=" + i + " channel=" + seat.ChannelId);
                }
                else
                {
                    Console.WriteLine("[LockStep] leave seat player=" + i + " channel=" + seat.ChannelId);
                    seats[i] = null;
                }
            }
        }

        void Handle(KcpServerPeer peer, byte[] payload)
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

            if (packet.BodyCase == Packet.BodyOneofCase.Ping)
            {
                Pong pong = new Pong();
                pong.ClientSendMs = packet.Ping.ClientSendMs;
                Packet reply = new Packet();
                reply.Pong = pong;
                peer.Send(reply.ToByteArray());
                return;
            }

            if (packet.BodyCase == Packet.BodyOneofCase.Join)
            {
                OnJoin(peer, packet.Join.ProtocolVersion);
            }
        }

        void OnJoin(KcpServerPeer peer, uint version)
        {
            int existing = SeatOf(peer);
            if (existing >= 0)
            {
                SendJoinAck(peer, (uint)existing);
                return;
            }

            if (version != ProtocolVersion)
            {
                SendJoinReject(peer, RejectVersion);
                return;
            }

            if (started)
            {
                SendJoinReject(peer, RejectAlreadyStarted);
                return;
            }

            int seat = FreeSeat();
            if (seat < 0)
            {
                SendJoinReject(peer, RejectRoomFull);
                return;
            }

            seats[seat] = peer;
            Console.WriteLine("[LockStep] JoinAck player=" + seat + " channel=" + peer.ChannelId);
            SendJoinAck(peer, (uint)seat);
            MaybeStart();
        }

        void MaybeStart()
        {
            if (started || seats[0] == null || seats[1] == null)
            {
                return;
            }

            started = true;
            MatchStart start = new MatchStart();
            start.TickHz = 20;
            start.InputDelayFrames = 2;
            start.Seed = 0;
            start.ArenaHalfExtentMm = 4000;
            Packet packet = new Packet();
            packet.MatchStart = start;
            byte[] bytes = packet.ToByteArray();
            seats[0].Send(bytes);
            seats[1].Send(bytes);
            Console.WriteLine("[LockStep] MatchStart tickHz=20 delay=2 seed=0 arena=4000");
        }

        int SeatOf(KcpServerPeer peer)
        {
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] == peer)
                {
                    return i;
                }
            }

            return -1;
        }

        int FreeSeat()
        {
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        static KcpServerPeer FindPeer(KcpServerTransport server, uint channelId)
        {
            for (int i = 0; i < server.Peers.Count; i++)
            {
                if (server.Peers[i].ChannelId == channelId)
                {
                    return server.Peers[i];
                }
            }

            return null;
        }

        static void SendJoinAck(KcpServerPeer peer, uint playerId)
        {
            JoinAck ack = new JoinAck();
            ack.PlayerId = playerId;
            Packet packet = new Packet();
            packet.JoinAck = ack;
            peer.Send(packet.ToByteArray());
        }

        static void SendJoinReject(KcpServerPeer peer, uint reason)
        {
            JoinReject reject = new JoinReject();
            reject.Reason = reason;
            Packet packet = new Packet();
            packet.JoinReject = reject;
            peer.Send(packet.ToByteArray());
            Console.WriteLine("[LockStep] JoinReject reason=" + reason + " channel=" + peer.ChannelId);
        }
    }
}
