using System;
using System.Collections.Generic;

namespace LockStep.Framework
{
    public sealed class Entity : IDisposable
    {
        readonly List<Component> components = new List<Component>();

        public World World { get; }
        public bool IsDisposed { get; private set; }

        internal Entity(World world)
        {
            World = world;
        }

        // 按具体类型保存，每种组件只创建一个实例；设置归属后才调用 Awake。
        public T AddComponent<T>() where T : Component, new()
        {
            if (IsDisposed)
            {
                World.Log.Error("Cannot add a component to a disposed entity.", nameof(Entity));
                return null;
            }
            if (GetComponent<T>() != null)
            {
                World.Log.Error("Duplicate component: " + typeof(T).Name, nameof(Entity));
                return null;
            }

            T component = null;
            try
            {
                component = new T();
                components.Add(component);
                component.Attach(this);
                return component.IsDisposed ? null : component;
            }
            catch (Exception error)
            {
                World.Log.Error("Component creation or Awake failed.", typeof(T).Name, error);
                component?.Dispose();
                return null;
            }
        }

        public T GetComponent<T>() where T : Component
        {
            foreach (Component component in components)
            {
                if (component.GetType() == typeof(T)) return (T)component;
            }
            return null;
        }

        public Component[] GetComponents()
        {
            return components.ToArray();
        }

        public bool RemoveComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component == null) return false;

            component.Dispose();
            return true;
        }

        internal void Detach(Component component)
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (!ReferenceEquals(components[i], component)) continue;

                components.RemoveAt(i);
                break;
            }
        }

        internal void CollectUpdates(List<Component> snapshot)
        {
            snapshot.AddRange(components);
        }

        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            World.Detach(this);
            // 按创建的逆序销毁，快照允许销毁回调修改实体。
            Component[] snapshot = components.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                snapshot[i].Dispose();
            }
        }
    }
}
