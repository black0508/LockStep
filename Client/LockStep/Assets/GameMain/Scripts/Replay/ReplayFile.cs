using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Replay
{
    // 单一修订的本地文件容器；消息主体直接采用现有 Protobuf，不参与网络协议。
    public sealed class ReplayFile : IDisposable
    {
        const uint Magic = 0x5052534C; // LSRP，小端。
        const uint Revision = 1; // 不兼容的模拟规则变更也须提升此值。
        const long FrameCountOffset = 20;

        public sealed class Header
        {
            public DateTime RecordedAtUtc;
            public uint LocalPlayerId;
            public uint FrameCount;
            public S2CMatchStart MatchStart;
            public double Duration => (double)FrameCount / MatchStart.TickHz;
        }

        public sealed class Entry
        {
            public string Path;
            public Header Info;
            public string Error;
        }

        readonly string path;
        readonly FileStream stream;
        readonly BinaryReader reader;
        readonly BinaryWriter writer;
        uint framesRead;
        public Header Info { get; private set; }

        ReplayFile(string path, bool writing)
        {
            this.path = path;
            stream = new FileStream(path, writing ? FileMode.CreateNew : FileMode.Open,
                writing ? FileAccess.Write : FileAccess.Read, FileShare.Read, 16384);
            if (writing) writer = new BinaryWriter(stream);
            else reader = new BinaryReader(stream);
        }

        public static ReplayFile Create(string directory, S2CMatchStart start, uint localPlayerId)
        {
            Directory.CreateDirectory(directory);
            DateTime now = DateTime.UtcNow;
            string path = System.IO.Path.Combine(directory, $"{now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.tmp");
            var file = new ReplayFile(path, true);
            try
            {
                file.writer.Write(Magic);
                file.writer.Write(Revision);
                file.writer.Write(now.Ticks);
                file.writer.Write(localPlayerId);
                file.writer.Write(0u);
                file.WriteMessage(start);
                file.Info = new Header { RecordedAtUtc = now, LocalPlayerId = localPlayerId, MatchStart = start.Clone() };
                return file;
            }
            finally
            {
                if (file.Info == null) file.Dispose();
            }
        }

        public static ReplayFile Open(string path, out string error)
        {
            error = null;
            var file = new ReplayFile(path, false);
            try
            {
                if (file.reader.ReadUInt32() != Magic || file.reader.ReadUInt32() != Revision)
                {
                    error = "不支持的回放文件或修订号";
                    return null;
                }
                long recordedAtTicks = file.reader.ReadInt64();
                if (recordedAtTicks < DateTime.MinValue.Ticks || recordedAtTicks > DateTime.MaxValue.Ticks)
                {
                    error = "回放录制时间无效";
                    return null;
                }
                var header = new Header
                {
                    RecordedAtUtc = new DateTime(recordedAtTicks, DateTimeKind.Utc),
                    LocalPlayerId = file.reader.ReadUInt32(),
                    FrameCount = file.reader.ReadUInt32()
                };
                byte[] bytes = file.ReadMessage(out error);
                if (bytes == null) return null;
                header.MatchStart = S2CMatchStart.Parser.ParseFrom(bytes);
                if (header.MatchStart.TickHz == 0 || header.FrameCount == 0 || header.MatchStart.Players.Count == 0)
                {
                    error = "回放开局信息或帧数无效";
                    return null;
                }
                uint previousId = 0;
                bool foundLocal = false;
                foreach (RoomPlayer player in header.MatchStart.Players)
                {
                    if (player.PlayerId <= previousId)
                    {
                        error = "回放玩家顺序无效";
                        return null;
                    }
                    previousId = player.PlayerId;
                    foundLocal |= player.PlayerId == header.LocalPlayerId;
                }
                if (!foundLocal)
                {
                    error = "回放中缺少录制玩家";
                    return null;
                }
                file.Info = header;
                return file;
            }
            finally
            {
                if (file.Info == null) file.Dispose();
            }
        }

        public void Append(S2CFrame frame)
        {
            WriteMessage(frame);
            Info.FrameCount++;
        }

        public S2CFrame ReadNextFrame(out string error)
        {
            byte[] bytes = ReadMessage(out error);
            if (bytes == null) return null;
            S2CFrame frame = S2CFrame.Parser.ParseFrom(bytes);
            if (frame.FrameId != framesRead + 1 || frame.Inputs.Count != Info.MatchStart.Players.Count)
            {
                error = "回放帧号或输入数量无效";
                return null;
            }
            for (int i = 0; i < frame.Inputs.Count; i++)
                if (frame.Inputs[i].PlayerId != Info.MatchStart.Players[i].PlayerId)
                {
                    error = "回放输入与玩家名单不匹配";
                    return null;
                }
            framesRead++;
            if (framesRead == Info.FrameCount && stream.Position != stream.Length)
            {
                error = "回放文件帧数与内容不符";
                return null;
            }
            return frame;
        }

        // 调用方先移交持有权；失败时 .tmp 仍不会成为可播放的正式文件。
        public string Complete()
        {
            try
            {
                writer.Flush();
                stream.Position = FrameCountOffset;
                writer.Write(Info.FrameCount);
                writer.Flush();
                stream.Flush(true);
            }
            finally { Dispose(); }

            if (Info.FrameCount == 0)
            {
                File.Delete(path);
                return null;
            }
            string finalPath = System.IO.Path.ChangeExtension(path, ".replay");
            File.Move(path, finalPath);
            return finalPath;
        }

        public static List<Entry> List(string directory)
        {
            var entries = new List<Entry>();
            if (!Directory.Exists(directory)) return entries;
            foreach (string path in Directory.GetFiles(directory, "*.replay"))
            {
                var entry = new Entry { Path = path };
                try
                {
                    using (ReplayFile file = Open(path, out string error))
                    {
                        entry.Info = file?.Info;
                        entry.Error = error;
                    }
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    entry.Error = error.Message;
                }
                if (entry.Info == null) GameLog.Error($"无法读取回放 {path}：{entry.Error}");
                entries.Add(entry);
            }
            entries.Sort((a, b) => (b.Info?.RecordedAtUtc ?? DateTime.MinValue)
                .CompareTo(a.Info?.RecordedAtUtc ?? DateTime.MinValue));
            return entries;
        }

        public static void Delete(string path) { File.Delete(path); }

        void WriteMessage(IMessage message)
        {
            byte[] bytes = message.ToByteArray();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        byte[] ReadMessage(out string error)
        {
            error = null;
            int length = reader.ReadInt32();
            if (length <= 0 || length > stream.Length - stream.Position)
            {
                error = "回放消息长度无效或文件已截断";
                return null;
            }
            return reader.ReadBytes(length);
        }

        public void Dispose()
        {
            // BinaryReader/Writer 不持有额外资源，直接关闭唯一的底层流。
            stream.Dispose();
        }
    }
}
