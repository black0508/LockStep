namespace GameMain.FrameSync
{
    // 本地移动输入来源，由表现层实现并注入；每个分量为 -1、0 或 1。
    public interface IMoveInput
    {
        void Read(out int moveX, out int moveZ);
    }
}
