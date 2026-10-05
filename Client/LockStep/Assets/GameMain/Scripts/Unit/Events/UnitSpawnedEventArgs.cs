using Framework;

namespace GameMain
{
    public sealed class UnitSpawnedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(UnitSpawnedEventArgs).GetHashCode();
        public override int Id => EventId;
        public Unit Unit { get; private set; }

        public static UnitSpawnedEventArgs Create(Unit unit)
        {
            var args = ReferencePool.Acquire<UnitSpawnedEventArgs>();
            args.Unit = unit;
            return args;
        }

        public override void Clear()
        {
            Unit = null;
        }
    }
}
