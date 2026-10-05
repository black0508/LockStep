using Framework;

namespace GameMain
{
    // 一名玩家：整数坐标只在执行权威帧时改变，表现层只读。实体只用来挂表现组件。
    public sealed class Unit
    {
        public const int CoordinateScale = 10000;
        const long MoveSpeed = 3 * CoordinateScale;
        const int DiagonalScale = 7071;

        public uint PlayerId { get; }
        public bool IsLocal { get; }
        public long X { get; private set; }
        public long Z { get; private set; }
        internal Entity Entity { get; }

        internal Unit(World world, uint playerId, bool isLocal, long x)
        {
            PlayerId = playerId;
            IsLocal = isLocal;
            X = x;
            Entity = world.CreateEntity();
        }

        public void Step(int moveX, int moveZ)
        {
            int directionScale = moveX != 0 && moveZ != 0 ? DiagonalScale : CoordinateScale;
            long divisor = (long)CoordinateScale * FrameSyncComponent.TickHz;
            X += MoveSpeed * moveX * directionScale / divisor;
            Z += MoveSpeed * moveZ * directionScale / divisor;
        }

        internal void Dispose()
        {
            Entity.Dispose();
        }
    }
}
