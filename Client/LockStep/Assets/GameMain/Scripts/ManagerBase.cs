using UnityEngine;

namespace GameMain
{
    public abstract class ManagerBase : MonoBehaviour
    {
        protected virtual void Awake()
        {
            GameEntry.Register(this);
        }

        protected virtual void OnDestroy()
        {
            GameEntry.Unregister(this);
        }
    }
}
