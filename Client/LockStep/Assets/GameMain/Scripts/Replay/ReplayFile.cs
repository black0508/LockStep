using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Lockstep.Proto;
using Framework;

namespace GameMain
{
    // 一局回放：录制时逐帧追加后整体保存，播放时整体读入。只负责读写成败，原因写日志；内容由本程序写入，读取时不校验对局规则。
    public sealed class ReplayFile
    {
        const uint Magic = 0x5052534C; // LSRP，小端。
        const uint Revision = 2; // 不兼容的模拟规则变更也须提升此值。
        const int HeaderSize = 24;

        static string DirectoryPath => System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Replays");

        public sealed class Header
        {
            public DateTime RecordedAtUtc;
            public uint LocalPlayerId;
            public uint FrameCount;
            public S2CMatchStart MatchStart;
            public double Duration => (double)FrameCount / FrameSyncComponent.TickHz;
        }

        // Info 为 null 表示该文件无法读取。
        public sealed class Entry
        {
            public string Path;
            public Header Info;
        }

        readonly List<S2CFrame> frames;
        public Header Info { get; }
        public IReadOnlyList<S2CFrame> Frames => frames;

        ReplayFile(Header info, List<S2CFrame> frames)
        {
            Info = info;
            this.frames = frames;
        }

        // 开始录制一局。
        public ReplayFile(S2CMatchStart start, uint localPlayerId)
            : this(new Header { RecordedAtUtc = DateTime.UtcNow, LocalPlayerId = localPlayerId, MatchStart = start }, new List<S2CFrame>())
        {
        }

        public void Append(S2CFrame frame)
        {
            frames.Add(frame);
            Info.FrameCount++;
        }

        public bool Save()
        {
            var buffer = new MemoryStream();
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write(Magic);
                writer.Write(Revision);
                writer.Write(Info.RecordedAtUtc.Ticks);
                writer.Write(Info.LocalPlayerId);
                writer.Write(Info.FrameCount);
                WriteMessage(writer, Info.MatchStart);
                foreach (S2CFrame frame in frames) WriteMessage(writer, frame);
            }

            string directory = DirectoryPath;
            string name = System.IO.Path.Combine(directory, $"{Info.RecordedAtUtc:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}");
            string finalPath = name + ".replay";
            // 文件 API 以异常报告失败；保存失败只放弃本局回放。先写 .tmp 再改名，半截文件不会进入列表。
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(name + ".tmp", buffer.ToArray());
                File.Move(name + ".tmp", finalPath);
            }
            catch (IOException exception)
            {
                GameLog.Error("回放保存失败 " + finalPath, exception);
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                GameLog.Error("回放保存失败 " + finalPath, exception);
                return false;
            }
            GameLog.Info($"回放已保存：{finalPath}，共 {Info.FrameCount} 帧");
            return true;
        }

        // 读取失败时返回 null。
        public static ReplayFile Load(string path)
        {
            byte[] bytes;
            // 文件 API 以异常报告失败；列表与播放只需跳过这个文件。
            try { bytes = File.ReadAllBytes(path); }
            catch (IOException exception)
            {
                GameLog.Error("读取回放失败 " + path, exception);
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                GameLog.Error("读取回放失败 " + path, exception);
                return null;
            }

            if (bytes.Length < HeaderSize)
            {
                GameLog.Error("回放文件已截断 " + path);
                return null;
            }
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                if (reader.ReadUInt32() != Magic || reader.ReadUInt32() != Revision)
                {
                    GameLog.Error("不支持的回放文件或修订号 " + path);
                    return null;
                }
                var header = new Header
                {
                    RecordedAtUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                    LocalPlayerId = reader.ReadUInt32(),
                    FrameCount = reader.ReadUInt32(),
                    MatchStart = ReadMessage(reader, S2CMatchStart.Parser, path)
                };
                if (header.MatchStart == null) return null;

                var frames = new List<S2CFrame>();
                while (reader.BaseStream.Position < bytes.Length)
                {
                    S2CFrame frame = ReadMessage(reader, S2CFrame.Parser, path);
                    if (frame == null) return null;
                    frames.Add(frame);
                }
                return new ReplayFile(header, frames);
            }
        }

        // 目录读取失败时返回 null。
        public static List<Entry> List()
        {
            string directory = DirectoryPath;
            var entries = new List<Entry>();
            if (!Directory.Exists(directory)) return entries;
            string[] paths;
            // 文件 API 以异常报告失败；列表界面需要继续可用。
            try { paths = Directory.GetFiles(directory, "*.replay"); }
            catch (IOException exception)
            {
                GameLog.Error("无法读取回放目录 " + directory, exception);
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                GameLog.Error("无法读取回放目录 " + directory, exception);
                return null;
            }
            foreach (string path in paths)
                entries.Add(new Entry { Path = path, Info = Load(path)?.Info });
            entries.Sort((a, b) => (b.Info?.RecordedAtUtc ?? DateTime.MinValue)
                .CompareTo(a.Info?.RecordedAtUtc ?? DateTime.MinValue));
            return entries;
        }

        public static bool Delete(string path)
        {
            // 文件 API 以异常报告失败；删除失败时保留列表现状。
            try { File.Delete(path); }
            catch (IOException exception)
            {
                GameLog.Error("删除回放失败 " + path, exception);
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                GameLog.Error("删除回放失败 " + path, exception);
                return false;
            }
            return true;
        }

        static void WriteMessage(BinaryWriter writer, IMessage message)
        {
            byte[] bytes = message.ToByteArray();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        // 读取 int32 长度前缀的消息，截断或解析失败时返回 null。
        static T ReadMessage<T>(BinaryReader reader, MessageParser<T> parser, string path) where T : class, IMessage<T>
        {
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(int))
            {
                GameLog.Error("回放文件已截断 " + path);
                return null;
            }
            int length = reader.ReadInt32();
            if (length <= 0 || length > stream.Length - stream.Position)
            {
                GameLog.Error("回放消息长度无效或文件已截断 " + path);
                return null;
            }
            byte[] bytes = reader.ReadBytes(length);
            // Protobuf 以异常报告损坏数据。
            try { return parser.ParseFrom(bytes); }
            catch (InvalidProtocolBufferException exception)
            {
                GameLog.Error("回放消息解析失败 " + path, exception);
                return null;
            }
        }
    }
}
