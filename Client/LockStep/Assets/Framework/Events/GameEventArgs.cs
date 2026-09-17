using System;

namespace LockStep.Framework
{
    public abstract class GameEventArgs : EventArgs, IReference
    {
        // 嵌套转发同一参数时，最外层 FireNow 返回前不能归还。
        internal int DispatchDepth;

        public abstract int Id { get; }
        public abstract void Clear();
    }
}
