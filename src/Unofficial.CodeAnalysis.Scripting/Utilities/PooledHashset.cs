﻿using System.Diagnostics;

namespace Unofficial.CodeAnalysis.Scripting.Utilities
{
    // HashSet that can be recycled via an object pool
    // NOTE: these HashSets always have the default comparer.
    internal sealed partial class PooledHashSet<T> : HashSet<T>
    {
        private readonly ObjectPool<PooledHashSet<T>> _pool;

        private PooledHashSet(ObjectPool<PooledHashSet<T>> pool, IEqualityComparer<T> equalityComparer) :
            base(equalityComparer)
        {
            _pool = pool;
        }

        public void Free()
        {
            this.Clear();
            _pool?.Free(this);
        }

        // global pool
        private static readonly ObjectPool<PooledHashSet<T>> s_poolInstance = CreatePool(EqualityComparer<T>.Default);

        // if someone needs to create a pool;
        public static ObjectPool<PooledHashSet<T>> CreatePool(IEqualityComparer<T> equalityComparer)
        {
            ObjectPool<PooledHashSet<T>>? pool = null;
            pool = new ObjectPool<PooledHashSet<T>>(() => new PooledHashSet<T>(pool!, equalityComparer), 128);
            return pool;
        }

        public static PooledHashSet<T> GetInstance()
        {
            var instance = s_poolInstance.Allocate();
            Debug.Assert(instance.Count == 0);
            return instance;
        }
    }
}
