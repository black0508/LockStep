using LockStep.Framework;

namespace GameMain.Character
{
    // 纯逻辑角色：整数坐标只在执行权威帧时改变，表现层只读。
    public sealed class CharacterComponent : Component
    {
        public const int CoordinateScale = 10000;
        const long MoveSpeed = 3 * CoordinateScale;
        const int DiagonalScale = 7071;

        public uint PlayerId { get; private set; }
        public long X { get; private set; }
        public long Z { get; private set; }
        // 最近一次权威帧采用的输入，未执行过帧时为 0。
        public int MoveX { get; private set; }
        public int MoveZ { get; private set; }

        public void Init(uint playerId, long x)
        {
            PlayerId = playerId;
            X = x;
        }

        public void Step(int moveX, int moveZ, uint tickHz)
        {
            MoveX = moveX;
            MoveZ = moveZ;
            int directionScale = moveX != 0 && moveZ != 0 ? DiagonalScale : CoordinateScale;
            long divisor = (long)CoordinateScale * tickHz;
            X += MoveSpeed * moveX * directionScale / divisor;
            Z += MoveSpeed * moveZ * directionScale / divisor;
        }
    }
}
