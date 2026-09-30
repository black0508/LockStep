//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace LockStep.Framework
{
    /// <summary>
    /// 进程级全局引用池。归还后，调用方不能再持有或使用该对象。
    /// </summary>
    public static partial class ReferencePool
    {
        private static readonly Dictionary<Type, ReferenceCollection> s_ReferenceCollections = new Dictionary<Type, ReferenceCollection>();
        private static bool m_EnableStrictCheck = false;

        /// <summary>
        /// 获取或设置是否开启重复归还检查。参数校验始终执行。
        /// </summary>
        public static bool EnableStrictCheck
        {
            get
            {
                return m_EnableStrictCheck;
            }
            set
            {
                m_EnableStrictCheck = value;
            }
        }

        /// <summary>
        /// 获取引用池的数量。
        /// </summary>
        public static int Count
        {
            get
            {
                return s_ReferenceCollections.Count;
            }
        }

        /// <summary>
        /// 获取所有引用池的信息。
        /// </summary>
        /// <returns>所有引用池的信息。</returns>
        public static ReferencePoolInfo[] GetAllReferencePoolInfos()
        {
            int index = 0;
            ReferencePoolInfo[] results = null;

            lock (s_ReferenceCollections)
            {
                results = new ReferencePoolInfo[s_ReferenceCollections.Count];
                foreach (KeyValuePair<Type, ReferenceCollection> referenceCollection in s_ReferenceCollections)
                {
                    results[index++] = new ReferencePoolInfo(referenceCollection.Key, referenceCollection.Value.UnusedReferenceCount, referenceCollection.Value.UsingReferenceCount, referenceCollection.Value.AcquireReferenceCount, referenceCollection.Value.ReleaseReferenceCount, referenceCollection.Value.AddReferenceCount, referenceCollection.Value.RemoveReferenceCount);
                }
            }

            return results;
        }

        /// <summary>
        /// 清除所有引用池及统计。应在所有借出引用归还后调用。
        /// </summary>
        public static void ClearAll()
        {
            lock (s_ReferenceCollections)
            {
                foreach (KeyValuePair<Type, ReferenceCollection> referenceCollection in s_ReferenceCollections)
                {
                    referenceCollection.Value.RemoveAll();
                }

                s_ReferenceCollections.Clear();
            }
        }

        /// <summary>
        /// 从引用池获取引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <returns>引用。</returns>
        public static T Acquire<T>() where T : class, IReference, new()
        {
            return GetReferenceCollection(typeof(T)).Acquire<T>();
        }

        /// <summary>
        /// 从引用池获取引用。
        /// </summary>
        /// <param name="referenceType">实现 IReference 且具有公共无参构造函数的非抽象封闭类类型。</param>
        /// <returns>引用；类型参数非法时记录日志并返回 null。</returns>
        public static IReference Acquire(Type referenceType)
        {
            if (!InternalCheckReferenceType(referenceType, true))
            {
                return null;
            }

            return GetReferenceCollection(referenceType).Acquire();
        }

        /// <summary>
        /// 将引用归还引用池。
        /// </summary>
        /// <param name="reference">引用。</param>
        public static void Release(IReference reference)
        {
            if (reference == null)
            {
                GameLog.Error("Reference is invalid.");
                return;
            }

            Type referenceType = reference.GetType();
            if (!InternalCheckReferenceType(referenceType))
            {
                return;
            }

            GetReferenceCollection(referenceType).Release(reference);
        }

        /// <summary>
        /// 向引用池中追加指定数量的引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">追加数量。</param>
        public static void Add<T>(int count) where T : class, IReference, new()
        {
            if (count < 0)
            {
                GameLog.Error("Reference count is invalid: " + count);
                return;
            }

            GetReferenceCollection(typeof(T)).Add<T>(count);
        }

        /// <summary>
        /// 向引用池中追加指定数量的引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">追加数量。</param>
        public static void Add(Type referenceType, int count)
        {
            if (!InternalCheckReferenceType(referenceType, true))
            {
                return;
            }

            if (count < 0)
            {
                GameLog.Error("Reference count is invalid: " + count);
                return;
            }

            GetReferenceCollection(referenceType).Add(count);
        }

        /// <summary>
        /// 从引用池中移除指定数量的引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="count">移除数量。</param>
        public static void Remove<T>(int count) where T : class, IReference
        {
            if (!InternalCheckReferenceType(typeof(T)))
            {
                return;
            }

            if (count < 0)
            {
                GameLog.Error("Reference count is invalid: " + count);
                return;
            }

            GetReferenceCollection(typeof(T)).Remove(count);
        }

        /// <summary>
        /// 从引用池中移除指定数量的引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        /// <param name="count">移除数量。</param>
        public static void Remove(Type referenceType, int count)
        {
            if (!InternalCheckReferenceType(referenceType))
            {
                return;
            }

            if (count < 0)
            {
                GameLog.Error("Reference count is invalid: " + count);
                return;
            }

            GetReferenceCollection(referenceType).Remove(count);
        }

        /// <summary>
        /// 从引用池中移除所有的引用。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        public static void RemoveAll<T>() where T : class, IReference
        {
            if (!InternalCheckReferenceType(typeof(T)))
            {
                return;
            }

            GetReferenceCollection(typeof(T)).RemoveAll();
        }

        /// <summary>
        /// 从引用池中移除所有的引用。
        /// </summary>
        /// <param name="referenceType">引用类型。</param>
        public static void RemoveAll(Type referenceType)
        {
            if (!InternalCheckReferenceType(referenceType))
            {
                return;
            }

            GetReferenceCollection(referenceType).RemoveAll();
        }

        private static bool InternalCheckReferenceType(Type referenceType, bool requireConstructor = false)
        {
            if (referenceType == null)
            {
                GameLog.Error("Reference type is invalid.");
                return false;
            }

            if (!referenceType.IsClass || referenceType.IsAbstract || referenceType.ContainsGenericParameters)
            {
                GameLog.Error("Reference type is not a closed, non-abstract class type: " + referenceType);
                return false;
            }

            if (!typeof(IReference).IsAssignableFrom(referenceType))
            {
                GameLog.Error("Reference type is invalid: " + referenceType.FullName);
                return false;
            }

            if (requireConstructor && referenceType.GetConstructor(Type.EmptyTypes) == null)
            {
                GameLog.Error("Reference type has no public parameterless constructor: " + referenceType.FullName);
                return false;
            }

            return true;
        }

        private static ReferenceCollection GetReferenceCollection(Type referenceType)
        {
            ReferenceCollection referenceCollection = null;
            lock (s_ReferenceCollections)
            {
                if (!s_ReferenceCollections.TryGetValue(referenceType, out referenceCollection))
                {
                    referenceCollection = new ReferenceCollection(referenceType);
                    s_ReferenceCollections.Add(referenceType, referenceCollection);
                }
            }

            return referenceCollection;
        }
    }
}
