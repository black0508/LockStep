using System;
using System.IO;
using GameMain.FrameSync;
using Lockstep.Proto;
using LockStep.Framework;

namespace GameMain.Replay
{
    public sealed class ReplayComponent : Component
    {
        public enum PlaybackStatus { None, Playing, Finished, Failed }

        FrameSyncComponent frameSync;
        ReplayFile recording;
        ReplayFile playback;
        double elapsed;

        public string DirectoryPath { get; private set; }
        public string Message { get; private set; } = "";
        public bool IsRecording => recording != null;
        public PlaybackStatus Playback { get; private set; }
        public ReplayFile.Header PlaybackInfo { get; private set; }

        protected override void OnAwake() { frameSync = Entity.GetComponent<FrameSyncComponent>(); }
        public void Init(string directory) { DirectoryPath = directory; }

        public void BeginRecording(S2CMatchStart start, uint localPlayerId)
        {
            Message = "";
            try { recording = ReplayFile.Create(DirectoryPath, start, localPlayerId); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                ReportError("无法开始录制回放", error);
            }
        }

        public void RecordFrame(S2CFrame frame)
        {
            if (recording == null) return;
            try { recording.Append(frame); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                ReplayFile failed = recording;
                recording = null;
                try { failed.Dispose(); }
                // 写入失败后关闭缓冲流也可能再次遇到同一个磁盘错误。
                catch (IOException closeError) { GameLog.Error("关闭回放文件失败", closeError); }
                ReportError("回放录制失败，本局将不保存", error);
            }
        }

        public void FinishRecording()
        {
            if (recording == null) return;
            ReplayFile finished = recording;
            recording = null;
            string savedPath;
            try
            {
                savedPath = finished.Complete();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                ReportError("回放保存失败", error);
                return;
            }
            Message = savedPath == null ? "本局尚无可保存的回放帧" : "回放已保存";
            if (savedPath != null) GameLog.Info($"回放已保存：{savedPath}，共 {finished.Info.FrameCount} 帧");
        }

        public bool BeginPlayback(string path)
        {
            StopPlayback();
            Message = "";
            string fileError;
            try
            {
                playback = ReplayFile.Open(path, out fileError);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                fileError = error.Message;
            }
            if (playback == null)
            {
                Playback = PlaybackStatus.Failed;
                ReportError("无法播放回放：" + fileError);
                return false;
            }
            PlaybackInfo = playback.Info;
            frameSync.Start(PlaybackInfo.MatchStart.Players, PlaybackInfo.LocalPlayerId,
                PlaybackInfo.MatchStart.TickHz, FrameSyncComponent.SimulationMode.Replay);
            Playback = PlaybackStatus.Playing;
            return true;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (Playback != PlaybackStatus.Playing) return;
            elapsed += deltaTime;
            double interval = 1.0 / PlaybackInfo.MatchStart.TickHz;
            while (elapsed >= interval && frameSync.AppliedFrame < PlaybackInfo.FrameCount)
            {
                S2CFrame frame;
                string fileError;
                try { frame = playback.ReadNextFrame(out fileError); }
                catch (IOException error)
                {
                    frame = null;
                    fileError = error.Message;
                }
                if (frame == null)
                {
                    StopPlayback();
                    Playback = PlaybackStatus.Failed;
                    ReportError("回放文件读取失败：" + fileError);
                    return;
                }
                frameSync.ApplyFrame(frame);
                elapsed -= interval;
            }
            if (frameSync.AppliedFrame != PlaybackInfo.FrameCount) return;
            playback.Dispose();
            playback = null;
            Playback = PlaybackStatus.Finished;
        }

        public void StopPlayback()
        {
            ReplayFile previous = playback;
            playback = null;
            previous?.Dispose();
            if (frameSync.Mode == FrameSyncComponent.SimulationMode.Replay) frameSync.Stop();
            Playback = PlaybackStatus.None;
            PlaybackInfo = null;
            elapsed = 0;
        }

        void ReportError(string message, Exception error = null)
        {
            Message = error == null ? message : message + "：" + error.Message;
            GameLog.Error(message, error);
        }

        protected override void OnDestroy()
        {
            FinishRecording();
            StopPlayback();
        }
    }
}
