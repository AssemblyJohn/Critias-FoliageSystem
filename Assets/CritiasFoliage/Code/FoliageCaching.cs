using System;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    public class FoliageCache<TKey, TValue>
    {
        protected Dictionary<TKey, TValue> m_CachedData = new Dictionary<TKey, TValue>();
        protected Queue<TKey> m_CachedQueue = new Queue<TKey>();

        protected readonly int m_MaximumValues;
        protected readonly int m_EvictionCount;

        public FoliageCache(int maxValues = 100, int evictionCount = 10)
        {
            m_MaximumValues = maxValues;
            m_EvictionCount = evictionCount;
        }

        public TValue this[TKey key]
        {
            get { return m_CachedData[key]; }
            set { m_CachedData[key] = value; }
        }

        public int Count
        {
            get { return m_CachedData.Count; }
        }

        public bool ContainsKey(TKey key)
        {
            return m_CachedData.ContainsKey(key);
        }
    }

    public class FoliageDisposableCache<TKey, TValue> : FoliageCache<TKey, TValue>, IDisposable where TValue : IDisposable
    {
        public FoliageDisposableCache(int maxValues = 100, int evictionCount = 10) : base(maxValues, evictionCount) { }

        public void Add(TKey key, TValue value)
        {
            // If the cache count + 1 (the next element we're going to add) reached our threshold evict the first items that got in
            int cachedQueueCount = m_CachedQueue.Count;
            
            if (cachedQueueCount + 1 > m_MaximumValues)
            {
                int evictCount = m_EvictionCount <= cachedQueueCount ? m_EvictionCount : cachedQueueCount;

                for (int evict = 0; evict < evictCount; evict++)
                {
                    TKey tempKey = m_CachedQueue.Dequeue();

                    // Dispose the data and remove it
                    m_CachedData[tempKey].Dispose();
                    m_CachedData.Remove(tempKey);
                }
            }

            // Add the new datadata
            m_CachedData.Add(key, value);
            m_CachedQueue.Enqueue(key);
        }

        public void Dispose()
        {
            // Dispose all the data
            if (m_CachedData.Count > 0)
            {
                foreach (TValue val in m_CachedData.Values)
                    val.Dispose();

                m_CachedData.Clear();
                m_CachedQueue.Clear();
            }
        }
    }
}