using LockStep.Framework;

namespace GameMain.Replay.Events
{
    public enum ReplayRecordResult { Saved, Empty, SaveFailed }

    // 一局联网对局的录制结束后发布。
    public sealed class RecordingEndedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(RecordingEndedEventArgs).GetHashCode();
        public override int Id => EventId;
        public ReplayRecordResult Result { get; private set; }

        public static RecordingEndedEventArgs Create(ReplayRecordResult result)
        {
            var args = ReferencePool.Acquire<RecordingEndedEventArgs>();
            args.Result = result;
            return args;
        }

        public override void Clear() { Result = ReplayRecordResult.Saved; }
    }
}
