using System;
using System.Collections.Generic;
using LockStep.Server.Net;
using LockStep.Server.Room;

namespace LockStep.Server;

public static class GameEntry
{
    static readonly Dictionary<Type, ManagerBase> managers = new Dictionary<Type, ManagerBase>();

    public static NetworkServer NetworkServer
    {
        get { return GetManager<NetworkServer>(); }
    }

    public static RoomManager RoomManager
    {
        get { return GetManager<RoomManager>(); }
    }

    public static void Register(ManagerBase manager)
    {
        managers[manager.GetType()] = manager;
    }

    public static void Unregister(ManagerBase manager)
    {
        Type type = manager.GetType();
        ManagerBase current;
        if (managers.TryGetValue(type, out current) && ReferenceEquals(current, manager))
        {
            managers.Remove(type);
        }
    }

    static T GetManager<T>() where T : ManagerBase
    {
        ManagerBase manager;
        if (managers.TryGetValue(typeof(T), out manager))
        {
            return (T)manager;
        }

        return null;
    }
}
