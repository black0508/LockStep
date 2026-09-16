using System;

namespace LockStep.Framework
{
    // 组件保存状态与行为，生命周期由所属实体和 World 驱动。
    public abstract class Component : IDisposable
    {
        public Entity Entity { get; private set; }
        public bool IsDisposed { get; private set; }
        protected GameLog Log { get; private set; } = new GameLog();

        internal void Attach(Entity entity)
        {
            Entity = entity;
            Log = entity.World.Log;
            OnAwake();
        }

        internal void Update(float deltaTime)
        {
            if (IsDisposed) return;

            try
            {
                OnUpdate(deltaTime);
            }
            catch (Exception error)
            {
                Log.Error("Component update failed.", GetType().Name, error);
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            Entity?.Detach(this);
            try
            {
                OnDestroy();
            }
            catch (Exception error)
            {
                Log.Error("Component destruction failed.", GetType().Name, error);
            }
            finally
            {
                Entity = null;
            }
        }

        protected virtual void OnAwake() { }
        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnDestroy() { }
    }
}
