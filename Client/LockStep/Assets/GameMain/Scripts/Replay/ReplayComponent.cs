using System.Collections.Generic;
using Lockstep.Proto;
using Framework;

namespace GameMain
{
    // 订阅联网对局事件录制回放；播放时按帧率把文件中的帧喂给帧同步。
    public sealed class ReplayComponent : Component
    {
        // 非空表示正在录制本局。
        ReplayFile recording;
        double elapsed;

        // 非空表示已载入回放，播完后仍保留到 StopPlayback。
        public ReplayFile Playback { get; private set; }

        protected override void OnAwake()
        {
            EventComponent events = GameEntry.Application.Events;
            events.Subscribe(MatchStartedEventArgs.EventId, OnMatchStarted);
            events.Subscribe(FrameReceivedEventArgs.EventId, OnFrameReceived);
            events.Subscribe(RoomLeftEventArgs.EventId, OnRoomLeft);
        }

        void OnMatchStarted(object sender, GameEventArgs args)
        {
            var started = (MatchStartedEventArgs)args;
            recording = new ReplayFile(started.Start, GameEntry.Application.Data.LocalPlayerId);
        }

        void OnFrameReceived(object sender, GameEventArgs args)
        {
            recording?.Append(((FrameReceivedEventArgs)args).Frame);
        }

        void OnRoomLeft(object sender, GameEventArgs args)
        {
            if (recording == null) return;
            ReplayRecordResult result = recording.Info.FrameCount == 0 ? ReplayRecordResult.Empty
                : recording.Save() ? ReplayRecordResult.Saved : ReplayRecordResult.SaveFailed;
            recording = null;
            GameEntry.Application.Events.FireNow(this, RecordingEndedEventArgs.Create(result));
        }

        public bool BeginPlayback(string path)
        {
            StopPlayback();
            Playback = ReplayFile.Load(path);
            if (Playback == null) return false;
            GameData data = GameEntry.Application.Data;
            data.Phase = GamePhase.Replay;
            data.IsOnline = false;
            data.LocalPlayerId = Playback.Info.LocalPlayerId;
            GameEntry.Application.Units.Start(Playback.Info.MatchStart.Players);
            GameEntry.Application.FrameSync.Start();
            return true;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (Playback == null) return;
            FrameSyncComponent frameSync = GameEntry.Application.FrameSync;
            IReadOnlyList<S2CFrame> frames = Playback.Frames;
            // 帧号从 1 连续递增，已执行帧号即下一帧的下标。
            if (frameSync.AppliedFrame == frames.Count) return;
            elapsed += deltaTime;
            const double interval = 1.0 / FrameSyncComponent.TickHz;
            while (elapsed >= interval && frameSync.AppliedFrame < frames.Count)
            {
                frameSync.ApplyFrame(frames[(int)frameSync.AppliedFrame]);
                elapsed -= interval;
            }
        }

        // 只停止本组件启动的回放模拟。
        public void StopPlayback()
        {
            if (Playback == null) return;
            GameEntry.Application.FrameSync.Stop();
            GameEntry.Application.Units.Stop();
            Playback = null;
            elapsed = 0;
            GameEntry.Application.Data.Clear();
        }

        protected override void OnDestroy()
        {
            StopPlayback();
            EventComponent events = GameEntry.Application.Events;
            events.Unsubscribe(MatchStartedEventArgs.EventId, OnMatchStarted);
            events.Unsubscribe(FrameReceivedEventArgs.EventId, OnFrameReceived);
            events.Unsubscribe(RoomLeftEventArgs.EventId, OnRoomLeft);
        }
    }
}
