using GameMain.Character;
using LockStep.Framework;

namespace GameMain.FrameSync.Events
{
    public sealed class CharacterSpawnedEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(CharacterSpawnedEventArgs).GetHashCode();
        public override int Id => EventId;
        public CharacterComponent Character { get; private set; }
        public bool IsLocal { get; private set; }

        public static CharacterSpawnedEventArgs Create(CharacterComponent character, bool isLocal)
        {
            var args = ReferencePool.Acquire<CharacterSpawnedEventArgs>();
            args.Character = character;
            args.IsLocal = isLocal;
            return args;
        }

        public override void Clear()
        {
            Character = null;
            IsLocal = false;
        }
    }
}
