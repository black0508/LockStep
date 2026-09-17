using System;
using System.Collections.Generic;

namespace LockStep.Framework
{
    // 主线程上的小型 EC 容器，不认识 Unity、网络或业务组件。
    public sealed class World : IDisposable
    {
        readonly List<Entity> entities = new List<Entity>();
        readonly List<Component> updateSnapshot = new List<Component>();
        bool updating;

        public bool IsDisposed { get; private set; }

        public Entity CreateEntity()
        {
            if (IsDisposed)
            {
                GameLog.Error("Cannot create an entity in a disposed world.", nameof(World));
                return null;
            }

            Entity entity = new Entity(this);
            entities.Add(entity);
            return entity;
        }

        public void Update(float deltaTime)
        {
            if (IsDisposed) return;
            if (updating)
            {
                GameLog.Error("World.Update cannot be called recursively.", nameof(World));
                return;
            }

            // 首个回调前固定整轮名单：新增下轮生效，删除由 IsDisposed 跳过。
            foreach (Entity entity in entities)
            {
                entity.CollectUpdates(updateSnapshot);
            }

            updating = true;
            try
            {
                foreach (Component component in updateSnapshot)
                {
                    component.Update(deltaTime);
                }
            }
            finally
            {
                updateSnapshot.Clear();
                updating = false;
            }
        }

        internal void Detach(Entity entity)
        {
            entities.Remove(entity);
        }

        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            // 实体和各实体内的组件都按创建的逆序销毁。
            Entity[] snapshot = entities.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                snapshot[i].Dispose();
            }
        }
    }
}
