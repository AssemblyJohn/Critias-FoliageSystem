/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    /** Foliage tuple for holding the edit-time data and the runtime appended one. Both get drawn. */
    public struct FoliageTuple<T>
    {
        public T m_EditTime;
        public T m_RuntimeAppended;

        public FoliageTuple(T editTime)
        {
            m_EditTime = editTime;
            m_RuntimeAppended = default(T);            
        }

        public FoliageTuple(T editTime, T runtime)
        {
            m_EditTime = editTime;
            m_RuntimeAppended = runtime;
        }
    }

    public struct FoliageKeyValuePair<T, V>
    {
        public T Key;
        public V Value;

        public FoliageKeyValuePair(T key, V value)
        {
            Key = key;
            Value = value;
        }
    }

    /**
     * Foliage cell data that holds the smaller grass instances.
     */
    public class FoliageCellSubdividedDataRuntime
    {
        // Cell bounds in world space, even if we are a subdivision
        public Bounds m_Bounds;

        // Position within the local space of the bigger cell
        public FoliageCell m_Position;
        
        // Data built at load time for runtime. It is an array of all the grass types organised into a multi array of batches of 1000 foliage pieces each.
        public FoliageKeyValuePair<int, FoliageTuple<Matrix4x4[][]>>[] m_TypeHashLocationsRuntime;
    }

    /**
     * Foliage cell data that holds the larger tree foliage instances.
     */
    public class FoliageCellDataRuntime
    {        
        // Cell bounds extended
        public Bounds m_Bounds;

        // Foliage cell position with it's hash
        public FoliageCell m_Position;
        
        // Used for trees at runtime. Will be built from the above dictionary by merging the sources.
        public FoliageKeyValuePair<int, FoliageTuple<FoliageInstance[]>>[] m_TypeHashLocationsRuntime;

        // Used for grass. No need for a dict here since we will only iterate the cells
        public FoliageKeyValuePair<int, FoliageCellSubdividedDataRuntime>[] m_FoliageDataSubdivided;        
    }

    public class FoliageDataRuntime
    {
        public Dictionary<int, FoliageCellDataRuntime> m_FoliageData = new Dictionary<int, FoliageCellDataRuntime>();

        /** Slowest removal don't use often */
        public void RemoveFoliageInstance(System.Guid guid)
        {
            foreach (FoliageCellDataRuntime data in m_FoliageData.Values)
            {
                RemoveFoliageInstanceCell(0, guid, data, false);
            }
        }

        /** Slow removal don't use often */
        public void RemoveFoliageInstance(int typeHash, System.Guid guid)
        {
            foreach(FoliageCellDataRuntime data in m_FoliageData.Values)
            {
                RemoveFoliageInstanceCell(typeHash, guid, data);
            }
        }
        
        /** Fastest removal */
        public void RemoveFoliageInstance(int typeHash, System.Guid guid, Vector3 position)
        {
            FoliageCellDataRuntime cell;

            if(m_FoliageData.TryGetValue(FoliageCell.MakeHash(position), out cell))
            {
                RemoveFoliageInstanceCell(typeHash, guid, cell);
            }
        }

        /** Add foliage instance. Only works for trees at runtime.  */
        public void AddFoliageInstance(int typeHash, FoliageInstance instance)
        {
            int keyHash = FoliageCell.MakeHash(instance.m_Position);

            FoliageCellDataRuntime cell;

            if (m_FoliageData.ContainsKey(keyHash) == false)
            {
                cell = new FoliageCellDataRuntime();

                FoliageCell fCell = new FoliageCell(instance.m_Position, false);

                // Set the standard data
                cell.m_Bounds = fCell.GetBounds();
                cell.m_Position = fCell;

                // Empty subdivided data
                cell.m_FoliageDataSubdivided = new FoliageKeyValuePair<int, FoliageCellSubdividedDataRuntime>[0];

                // Empty type data
                cell.m_TypeHashLocationsRuntime = new FoliageKeyValuePair<int, FoliageTuple<FoliageInstance[]>>[0];

                // Add the data
                m_FoliageData.Add(keyHash, cell);
            }

            cell = m_FoliageData[keyHash];

            // Check if we have all the data
            int idx = System.Array.FindIndex(cell.m_TypeHashLocationsRuntime, (x) => x.Key == keyHash);

            if (idx < 0)
            {
                System.Array.Resize(ref cell.m_TypeHashLocationsRuntime, cell.m_TypeHashLocationsRuntime.Length + 1);
                idx = cell.m_TypeHashLocationsRuntime.Length - 1;

                // Add the type
                cell.m_TypeHashLocationsRuntime[idx] = new FoliageKeyValuePair<int, FoliageTuple<FoliageInstance[]>>(typeHash, new FoliageTuple<FoliageInstance[]>(new FoliageInstance[0]));
            }
            
            // We have the stuff
            System.Array.Resize(ref cell.m_TypeHashLocationsRuntime[idx].Value.m_EditTime, cell.m_TypeHashLocationsRuntime[idx].Value.m_EditTime.Length + 1);
            cell.m_TypeHashLocationsRuntime[idx].Value.m_EditTime[cell.m_TypeHashLocationsRuntime[idx].Value.m_EditTime.Length - 1] = instance;
        }
        
        /**
         * Removes the instance from the list. Costly operation that will re-build the array. Do not use often!
         */
        private void RemoveFoliageInstanceCell(int typeHash, System.Guid guid, FoliageCellDataRuntime data, bool ignoreDifferentHash = true)
        {
            for (int type = 0; type < data.m_TypeHashLocationsRuntime.Length; type++)
            {                
                if (ignoreDifferentHash && data.m_TypeHashLocationsRuntime[type].Key != typeHash)
                    continue;

                var keyValuePair = data.m_TypeHashLocationsRuntime[type];

                // Build new arrays
                FoliageTuple<FoliageInstance[]> tuple = keyValuePair.Value;

                FoliageInstance[] newInstancesEdit = null;
                FoliageInstance[] newInstancesRuntime = null;

                if (tuple.m_EditTime != null)
                    newInstancesEdit = System.Array.FindAll(tuple.m_EditTime, (x) => x.m_UniqueId != guid);

                if(tuple.m_RuntimeAppended != null)
                    newInstancesRuntime = System.Array.FindAll(tuple.m_RuntimeAppended, (x) => x.m_UniqueId != guid);                

                // Set the new value
                data.m_TypeHashLocationsRuntime[type].Value.m_EditTime = newInstancesEdit;
                data.m_TypeHashLocationsRuntime[type].Value.m_RuntimeAppended = newInstancesRuntime;
            }            
        }
    }
}
