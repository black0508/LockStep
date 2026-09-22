using System;
using System.Collections.Generic;

namespace LockStep.Framework
{
    // 全局引用池，仅主线程使用；归还后调用方不能再持有或使用对象。
    public static class ReferencePool
    {
        static readonly Dictionary<Type, Stack<IReference>> pools = new Dictionary<Type, Stack<IReference>>();

        public static T Acquire<T>() where T : class, IReference, new()
        {
            if (pools.TryGetValue(typeof(T), out Stack<IReference> pool) && pool.Count > 0)
                return (T)pool.Pop();
            return new T();
        }

        public static void Release(IReference reference)
        {
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
    }
}
