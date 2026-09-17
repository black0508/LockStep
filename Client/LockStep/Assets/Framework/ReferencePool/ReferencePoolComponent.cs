using System;
using System.Collections.Generic;

namespace LockStep.Framework
{
    // 仅主线程使用；归还后调用方不能再持有或使用对象。
    public sealed class ReferencePoolComponent : Component
    {
        readonly Dictionary<Type, Stack<IReference>> pools = new Dictionary<Type, Stack<IReference>>();

        public T Acquire<T>() where T : class, IReference, new()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(ReferencePoolComponent));
            if (pools.TryGetValue(typeof(T), out Stack<IReference> pool) && pool.Count > 0)
                return (T)pool.Pop();
            return new T();
        }

        public void Release(IReference reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            // 销毁可能发生在事件回调中，finally 归还的对象只清理，不再缓存。
            if (IsDisposed)
            {
                reference.Clear();
                return;
            }
            Type type = reference.GetType();
            if (!pools.TryGetValue(type, out Stack<IReference> pool))
            {
                pool = new Stack<IReference>();
                pools.Add(type, pool);
            }
            // 按引用判断，避免重复归还让后续两次 Acquire 拿到同一个对象。
            foreach (IReference item in pool)
            {
                if (ReferenceEquals(item, reference))
                    throw new InvalidOperationException("Reference already released: " + type.FullName);
            }
            reference.Clear();
            pool.Push(reference);
        }

        // 只丢弃空闲缓存，不影响已经借出的对象。
        public void RemoveAll() { pools.Clear(); }

        protected override void OnDestroy() { RemoveAll(); }
    }
}
