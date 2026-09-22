using System;
using System.Collections.Generic;

namespace LockStep.Framework
{
    // 主线程同步事件；FireNow 接管参数，分发结束后自动清理并归还引用池。
    public sealed class EventComponent : Component
    {
        sealed class Subscription
        {
            public readonly EventHandler<GameEventArgs> Handler;
            public bool Active = true;

            public Subscription(EventHandler<GameEventArgs> handler) { Handler = handler; }
        }

        // 独立快照支持嵌套发布；Active 保证注销后再订阅也不会复活旧快照项。
        sealed class DispatchSnapshot : IReference
        {
            public readonly List<Subscription> Items = new List<Subscription>();
            public void Clear() { Items.Clear(); }
        }

        readonly Dictionary<int, List<Subscription>> handlers = new Dictionary<int, List<Subscription>>();
        // Root 上先创建、最后销毁；所属实体或 World 一开始关闭就停止通知。
        bool CanDispatch => !IsDisposed && Entity != null && !Entity.IsDisposed && !Entity.World.IsDisposed;

        public void Subscribe(int id, EventHandler<GameEventArgs> handler)
        {
            if (!CanDispatch) return;
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!handlers.TryGetValue(id, out List<Subscription> subscriptions))
            {
                subscriptions = new List<Subscription>();
                handlers.Add(id, subscriptions);
            }
            foreach (Subscription subscription in subscriptions)
            {
                if (subscription.Handler != handler) continue;
                GameLog.Warning($"Duplicate event subscription: {id}");
                return;
            }
            subscriptions.Add(new Subscription(handler));
        }

        public void Unsubscribe(int id, EventHandler<GameEventArgs> handler)
        {
            if (!handlers.TryGetValue(id, out List<Subscription> subscriptions)) return;
            for (int i = 0; i < subscriptions.Count; i++)
            {
                Subscription subscription = subscriptions[i];
                if (subscription.Handler != handler) continue;
                subscription.Active = false;
                subscriptions.RemoveAt(i);
                if (subscriptions.Count == 0) handlers.Remove(id);
                return;
            }
        }

        public void FireNow(object sender, GameEventArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            args.DispatchDepth++;
            try
            {
                Dispatch(sender, args);
            }
            finally
            {
                if (--args.DispatchDepth == 0) ReferencePool.Release(args);
            }
        }

        void Dispatch(object sender, GameEventArgs args)
        {
            if (!CanDispatch) return;
            if (!handlers.TryGetValue(args.Id, out List<Subscription> subscriptions)) return;

            // 独立快照支持嵌套发布；Active 保证注销后再订阅也不会复活旧快照项。    
            DispatchSnapshot snapshot = ReferencePool.Acquire<DispatchSnapshot>();
            try
            {
                snapshot.Items.AddRange(subscriptions);
                foreach (Subscription subscription in snapshot.Items)
                {
                    if (!CanDispatch) break;
                    if (!subscription.Active) continue;
                    subscription.Handler(sender, args);
                }
            }
            finally
            {
                ReferencePool.Release(snapshot);
            }
        }

        protected override void OnDestroy()
        {
            handlers.Clear();
        }
    }
}
