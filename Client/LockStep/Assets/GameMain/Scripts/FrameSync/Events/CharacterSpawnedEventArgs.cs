using LockStep.Framework;

namespace GameMain.FrameSync.Events
{
    public sealed class CharacterSpawnedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(CharacterSpawnedEventArgs).GetHashCode();
        public override int Id => EventId;
        public Entity Character { get; private set; }
        public uint PlayerId { get; private set; }
        public bool IsLocal { get; private set; }

        public static CharacterSpawnedEventArgs Create(Entity character, uint playerId, bool isLocal)
        {
            var args = ReferencePool.Acquire<CharacterSpawnedEventArgs>();
            args.Character = character;
            args.PlayerId = playerId;
            args.IsLocal = isLocal;
            return args;
        }

        public override void Clear()
        {
            Character = null;
            PlayerId = 0;
            IsLocal = false;
        }
    }
}
