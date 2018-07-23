/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{       
    /**
     * Foliage cell data that holds the smaller grass instances.
     */
    public class FoliageCellSubdividedData
    {
        // Cell bounds in world space, even if we are a subdivision
        public Bounds m_Bounds;

        // Position within the local space of the bigger cell
        public FoliageCell m_Position;

        // Grass data, organised in the same manned as the trees. Grass supports labels too.
        public Dictionary<int, Dictionary<string, List<FoliageInstance>>> m_TypeHashLocationsEditor = new Dictionary<int, Dictionary<string, List<FoliageInstance>>>();
    }
    
    /**
     * Foliage cell data that holds the larger tree foliage instances.
     */
    public class FoliageCellData
    {
        // Cell bounds in world space
        public Bounds m_Bounds;

        // Cell bounds extended to encapsulate all bigger tree AABB's
        public Bounds m_BoundsExtended;

        // Foliage cell position with it's hash
        public FoliageCell m_Position;

        /**
         * Used for trees at edit time. It supports a 'label' that allows us to detect the source from which the trees came into the system.
         * That will be the case for trees/grass that were added from the terrain or from any extra objects. We have it so that we can quickly 
         * delete the ones that we don't need from the external sources. Like deleting all the trees from the terrain "Terrain_Main" but keeping 
         * our hand-painted grass.
         */
        public Dictionary<int, Dictionary<string, List<FoliageInstance>>> m_TypeHashLocationsEditor = new Dictionary<int, Dictionary<string, List<FoliageInstance>>>();

        // Used for grass. The hash data is generated basd on the local-space of the bounds and not world space.
        public Dictionary<int, FoliageCellSubdividedData> m_FoliageDataSubdivided = new Dictionary<int, FoliageCellSubdividedData>();
    }
    
    /**
     * Foliage data and the operations that are taken on it. All operations have
     * a few variants:
     * 
     * 1. DataOperation
     * 2. DataOperationSubdiv
     * 
     * 1. Data operation that is taken normally at edit-time
     * 2. Data operation that is taken normally at edit-time but for an subdivided cell. That is the case for grass
     * 
     * Each operation type has to be handled separately
     */
    public class FoliageData
    {
        // Foliage data
        public Dictionary<int, FoliageCellData> m_FoliageData = new Dictionary<int, FoliageCellData>();
        
        /**
         * Removes all the foliage instances of the type from the data.
         */
        public void RemoveType(int typeHash)
        {
            bool anyRemoved = false;
            int cellsPurged = 0;
            
            foreach(FoliageCellData data in m_FoliageData.Values)
            {
                if(data.m_TypeHashLocationsEditor.Remove(typeHash))
                {
                    anyRemoved = true;
                    cellsPurged++;
                }

                foreach(FoliageCellSubdividedData subdivData in data.m_FoliageDataSubdivided.Values)
                {
                    if(subdivData.m_TypeHashLocationsEditor.Remove(typeHash))
                    {
                        anyRemoved = true;
                        cellsPurged++;
                    }
                }
            }

            if (anyRemoved)
            {
                FoliageLog.i("Removed type instance from: " + cellsPurged + " cells.");
                RemoveEmptyData();

                // Recalc the bounds
                RecalculateBoundsAfterRemove();
            }
        }

        /**
         * Rebuild the hierarchy that contains the type. Must be used when we make a type that was
         * a tree into a grass (therefore having to use the subdivided cells) or vice-versa.
         * 
         * Will preserve labeling data.
         */
        public void RebuildType(int typeHash, bool subdivided)
        {            
            Dictionary<string, List<FoliageInstance>> instances = new Dictionary<string, List<FoliageInstance>>();
            
            foreach (FoliageCellData data in m_FoliageData.Values)
            {
                if(data.m_TypeHashLocationsEditor.ContainsKey(typeHash))
                {
                    var labeledData = data.m_TypeHashLocationsEditor[typeHash];

                    // Get all the data from the cells
                    foreach(var labeled in labeledData)
                    {
                        string label = labeled.Key;

                        if (instances.ContainsKey(label) == false)
                            instances.Add(label, new List<FoliageInstance>());

                        instances[label].AddRange(labeled.Value);
                    }
                }                

                foreach (FoliageCellSubdividedData subdivData in data.m_FoliageDataSubdivided.Values)
                {
                    if (subdivData.m_TypeHashLocationsEditor.ContainsKey(typeHash))
                    {
                        var labeledData = subdivData.m_TypeHashLocationsEditor[typeHash];

                        // Get all the data from the subdivided cells
                        foreach(var labeled in labeledData)
                        {
                            string label = labeled.Key;

                            if (instances.ContainsKey(label) == false)
                                instances.Add(label, new List<FoliageInstance>());

                            instances[label].AddRange(labeled.Value);
                        }                        
                    }
                }
            }

            if(instances.Count > 0)
            {
                // Clear the old added data
                RemoveType(typeHash);

                int count = 0;

                foreach(var labeledInstances in instances)
                {
                    string label = labeledInstances.Key;
                    List<FoliageInstance> inst = labeledInstances.Value;

                    count += inst.Count;

                    // Add back the instances                    
                    AddInstances(typeHash, inst, subdivided, label);
                }

                FoliageLog.i("Relocated: " + count + " instanced.");
            }
        }

        /**
         * Remove the foliage with the marked GUID. Not supported for grass since
         * we are not having that data for it.
         */
        public void RemoveInstanceGuid(int typeHash, Vector3 position, System.Guid guid)
        {
            FoliageCellData cell;

            if (m_FoliageData.TryGetValue(FoliageCell.MakeHash(position), out cell) == false)
                return;

            bool anyRemoved = false;

            // Look through the cell
            if (cell.m_TypeHashLocationsEditor.ContainsKey(typeHash))
            {
                var labeled = cell.m_TypeHashLocationsEditor[typeHash];

                foreach (var instances in labeled.Values)
                {
                    for (int i = 0; i < instances.Count; i++)
                    {
                        if (instances[i].m_UniqueId == guid)
                        {
                            instances.RemoveAt(i);
                            anyRemoved = true;

                            // Yea, I know. Don't use goto... However we want to break all the iterations
                            goto finished;
                        }
                    }
                } // Foreach labeled finished
                finished:;
            }

            // If we removed everything from a cell then clear it from the list completely
            RemoveEmptyTypeDataCell(cell);
            if (IsCellEmpty(cell))
                m_FoliageData.Remove(cell.GetHashCode());                

            if (anyRemoved)
            {
                RecalculateBoundsAfterRemove();
            }
        }

        /** 
         * Removes foliage instances.
         * 
         * @return True if we removed anything
         */
        public bool RemoveInstances(int typeHash, Vector3 position, float radius = 0.3f /*30cm default position delta */)
        {
            Vector3 min = position - new Vector3(radius, radius, radius);
            Vector3 max = position + new Vector3(radius, radius, radius);

            bool anyRemoved = false;
            bool anyGrassRemoved = false;

            float x, y, z;
            float distanceDelta = radius * radius;

            // Remove all the foliage that overlaps the sphere
            FoliageCell.IterateMinMax(min, max, false, (int hash) => 
            {
                if (m_FoliageData.ContainsKey(hash) == false)
                    return;

                FoliageCellData cell = m_FoliageData[hash];

                // Remove the types from the cell
                if (cell.m_TypeHashLocationsEditor.ContainsKey(typeHash))
                {
                    var labeledData = cell.m_TypeHashLocationsEditor[typeHash];

                    foreach (var instances in labeledData.Values)
                    {
                        for (int i = instances.Count - 1; i >= 0; i--)
                        {
                            x = instances[i].m_Position.x - position.x;
                            y = instances[i].m_Position.y - position.y;
                            z = instances[i].m_Position.z - position.z;

                            // If we are at a distance smaller than the threshold then remove the instance
                            if ((x * x + y * y + z * z) < distanceDelta)
                            {
                                instances.RemoveAt(i);

                                // We removed from the cell, we have to recalculate the extended bounds
                                anyRemoved = true;
                            }
                        }
                    }
                }

                // Remove the types from the subdivided cells

                // Iterate subdivisions
                Vector3 minLocal = GetLocalInCell(min, cell);
                Vector3 maxLocal = GetLocalInCell(max, cell);

                FoliageCell.IterateMinMax(minLocal, maxLocal, true, (int hashLocal) =>
                {
                    FoliageCellSubdividedData cellSubdivided;

                    if (cell.m_FoliageDataSubdivided.TryGetValue(hashLocal, out cellSubdivided) == false)
                        return;

                    // Count all the grass foliage that overlaps the sphere
                    if (cellSubdivided.m_TypeHashLocationsEditor.ContainsKey(typeHash))
                    {
                        var data = cellSubdivided.m_TypeHashLocationsEditor[typeHash];

                        foreach (var instances in data.Values)
                        {
                            for (int i = instances.Count - 1; i >= 0; i--)
                            {
                                x = instances[i].m_Position.x - position.x;
                                y = instances[i].m_Position.y - position.y;
                                z = instances[i].m_Position.z - position.z;

                                // If we are at a distance smaller than the threshold then remove the instance
                                if ((x * x + y * y + z * z) < distanceDelta)
                                {
                                    instances.RemoveAt(i);

                                    // Just for grass removal...
                                    anyGrassRemoved = true;
                                }
                            }
                        }
                    }

                    RemoveEmptyTypeDataCellSubdivided(cellSubdivided);
                    if (IsSubCellEmpty(cellSubdivided))
                        cell.m_FoliageDataSubdivided.Remove(hashLocal);
                });

                // If we removed everything from a cell then clear it from the list completely
                RemoveEmptyTypeDataCell(cell);
                if (IsCellEmpty(cell))
                    m_FoliageData.Remove(hash);
            });

            if(anyRemoved)
            {
                RecalculateBoundsAfterRemove();
            }

            return (anyRemoved || anyGrassRemoved);
        }
        
        public Dictionary<int, List<FoliageInstance>> CollectLabeledInstances(string label)
        {
            Dictionary<int, List<FoliageInstance>> data = new Dictionary<int, List<FoliageInstance>>();

            foreach (var cell in m_FoliageData.Values)
            {
                // Collect all the labeled data
                foreach (var typedData in cell.m_TypeHashLocationsEditor)
                {
                    List<FoliageInstance> instances;

                    if (typedData.Value.TryGetValue(label, out instances))
                    {
                        if (instances.Count > 0)
                        {
                            if (data.ContainsKey(typedData.Key) == false)
                                data.Add(typedData.Key, new List<FoliageInstance>());

                            data[typedData.Key].AddRange(instances);
                        }
                    }
                }

                // Remove all the labeled data from the typed subdivided data
                foreach (var cellSubdiv in cell.m_FoliageDataSubdivided.Values)
                {
                    foreach (var typedData in cellSubdiv.m_TypeHashLocationsEditor)
                    {
                        List<FoliageInstance> instances;

                        if(typedData.Value.TryGetValue(label, out instances))
                        {
                            if(instances.Count > 0)
                            {
                                if (data.ContainsKey(typedData.Key) == false)
                                    data.Add(typedData.Key, new List<FoliageInstance>());

                                data[typedData.Key].AddRange(instances);
                            }
                        }
                    }
                }
            }

            return data;
        }

        /**
         * Removes all the instances marked with the specified label
         */
        public void RemoveInstancesLabeled(string label)
        {
            bool anyRemoved = false;

            foreach(var cell in m_FoliageData.Values)
            {
                // Remove all the labeled data from the typed data
                foreach (var typedData in cell.m_TypeHashLocationsEditor.Values)
                {
                    if (typedData.Remove(label))
                        anyRemoved = true;
                }

                // Remove all the labeled data from the typed subdivided data
                foreach(var cellSubdiv in cell.m_FoliageDataSubdivided.Values)
                {
                    foreach (var typedData in cellSubdiv.m_TypeHashLocationsEditor.Values)
                    {
                        if (typedData.Remove(label))
                            anyRemoved = true;
                    }
                }
            }

            if (anyRemoved)
            {
                RemoveEmptyData();

                // Recalculate extended bounds
                RecalculateBoundsAfterRemove();
            }
        }

        /**
         * Add a new foliage instance to the underlaying data.
         * 
         * The painter should decide if our data is added to the subdivisions or if it is a tree type and must not be added to the subdivision.
         */
        public void AddInstance(int typeHash, FoliageInstance instance, bool subdivision, string label = FoliageGlobals.LABEL_PAINTED)
        {            
            int hash = FoliageCell.MakeHash(instance.m_Position);

            if(m_FoliageData.ContainsKey(hash) == false)
            {
                FoliageCellData data = new FoliageCellData();
                data.m_Position = new FoliageCell();
                data.m_Position.Set(instance.m_Position);

                data.m_Bounds = data.m_Position.GetBounds();
                data.m_BoundsExtended = data.m_Bounds;

                // Add the foliage cell
                m_FoliageData.Add(hash, data);
            }

            FoliageCellData cellData = m_FoliageData[hash];

            if (subdivision == false)
            {
                if (cellData.m_TypeHashLocationsEditor.ContainsKey(typeHash) == false)
                    cellData.m_TypeHashLocationsEditor.Add(typeHash, new Dictionary<string, List<FoliageInstance>>());

                var labeled = cellData.m_TypeHashLocationsEditor[typeHash];

                if (labeled.ContainsKey(label) == false)
                    labeled.Add(label, new List<FoliageInstance>());
                
                // Add the foliage data
                labeled[label].Add(instance);

                // Make the extended bounds larger. Encapsulate anything that might make the bounds larger. Only applied to trees
                m_FoliageData[hash].m_BoundsExtended.Encapsulate(instance.m_Bounds);
            }
            else
            {
                var foliageSubdividedData = cellData.m_FoliageDataSubdivided;

                Vector3 localPosition = GetLocalInCell(instance.m_Position, cellData);

                int hashSubdivided = FoliageCell.MakeHashSubdivided(localPosition);

                if(foliageSubdividedData.ContainsKey(hashSubdivided) == false)
                {
                    FoliageCellSubdividedData data = new FoliageCellSubdividedData();
                    data.m_Position = new FoliageCell();
                    data.m_Position.SetSubdivided(localPosition);

                    // Get bounds in world space
                    data.m_Bounds = data.m_Position.GetBoundsSubdivided();
                    data.m_Bounds.center = GetWorldInCell(data.m_Bounds.center, cellData);

                    foliageSubdividedData.Add(hashSubdivided, data);
                }

                FoliageCellSubdividedData cellDataSubdivided = foliageSubdividedData[hashSubdivided];

                if (cellDataSubdivided.m_TypeHashLocationsEditor.ContainsKey(typeHash) == false)
                    cellDataSubdivided.m_TypeHashLocationsEditor.Add(typeHash, new Dictionary<string, List<FoliageInstance>>());

                var labeled = cellDataSubdivided.m_TypeHashLocationsEditor[typeHash];

                if (labeled.ContainsKey(label) == false)
                    labeled.Add(label, new List<FoliageInstance>());

                labeled[label].Add(instance);
            }
        }
        
        public void AddInstances(int typeHash, List<FoliageInstance> instances, bool subdivision, string label = FoliageGlobals.LABEL_PAINTED)
        {
            for(int i = 0; i < instances.Count; i++)
            {                
                AddInstance(typeHash, instances[i], subdivision, label);
            }
        }


        /**
         * Removes any empty data from the list. Called before disk writing so that we're sure that
         * we are not going to test for any empty cells at runtime.
         */
        public void RemoveEmptyData()
        {            
            HashSet<int> emptyCells = null;
            HashSet<int> emptyCellsSubdiv = null;

            // Iterate cells
            foreach (var cell in m_FoliageData)
            {
                RemoveEmptyTypeDataCell(cell.Value);
                if (IsCellEmpty(cell.Value))
                {
                    if (emptyCells == null)
                        emptyCells = new HashSet<int>();

                    emptyCells.Add(cell.Key);
                }

                if (emptyCellsSubdiv != null)
                    emptyCellsSubdiv.Clear();

                // Iterate sub-cells too
                foreach (var cellSubdiv in cell.Value.m_FoliageDataSubdivided)
                {
                    RemoveEmptyTypeDataCellSubdivided(cellSubdiv.Value);
                    if (IsSubCellEmpty(cellSubdiv.Value))
                    {
                        if(emptyCellsSubdiv == null)
                            emptyCellsSubdiv = new HashSet<int>();

                        emptyCellsSubdiv.Add(cellSubdiv.Key);
                    }
                }

                if(emptyCellsSubdiv != null && emptyCellsSubdiv.Count > 0)
                {
                    foreach (int key in emptyCellsSubdiv)
                    {
                        cell.Value.m_FoliageDataSubdivided.Remove(key);
                    }
                }
            }
            
            // Remove the empty cells
            if (emptyCells != null)
            {
                foreach (int key in emptyCells)
                    m_FoliageData.Remove(key);
            }

            FoliageLog.i("Removed: " + (emptyCells != null ? emptyCells.Count : 0) + " empty cells and: " + (emptyCellsSubdiv != null ? emptyCellsSubdiv.Count : 0) + " empty subdivided cells.");
        }

        /**
         * Get all the instances withing the given radius of the provided location.
         * 
         * @param subdivision
         *          If we should get the instance count for subdivisions (grass) or for big cells.
         */
        public int GetInstanceCountLocation(Vector3 position, float radius, bool subdivision)
        {
            int count = 0;
            
            Vector3 min = position - new Vector3(radius, radius, radius);
            Vector3 max = position + new Vector3(radius, radius, radius);

            float distanceDelta = radius * radius;
            float x, y, z;

            // Iterate divisions
            FoliageCell.IterateMinMax(min, max, false, (int hash) =>
            {
                FoliageCellData cell;

                if (m_FoliageData.TryGetValue(hash, out cell) == false)
                    return;

                if (subdivision == false)
                {
                    // Count all the tree foliage that overlaps the sphere
                    foreach (var data in cell.m_TypeHashLocationsEditor.Values)
                    {
                        foreach (var instances in data.Values)
                        {
                            for (int i = 0; i < instances.Count; i++)
                            {
                                x = instances[i].m_Position.x - position.x;
                                y = instances[i].m_Position.y - position.y;
                                z = instances[i].m_Position.z - position.z;

                                if ((x * x + y * y + z * z) < distanceDelta)
                                {
                                    count++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Iterate subdivisions
                    Vector3 minLocal = GetLocalInCell(min, cell);
                    Vector3 maxLocal = GetLocalInCell(max, cell);

                    FoliageCell.IterateMinMax(minLocal, maxLocal, true, (int hashLocal) =>
                    {
                        FoliageCellSubdividedData cellSubdivided;

                        if (cell.m_FoliageDataSubdivided.TryGetValue(hashLocal, out cellSubdivided) == false)
                            return;

                    // Count all the grass foliage that overlaps the sphere
                    foreach (var data in cellSubdivided.m_TypeHashLocationsEditor.Values)
                        {
                            foreach (var instances in data.Values)
                            {
                                for (int i = 0; i < instances.Count; i++)
                                {
                                    x = instances[i].m_Position.x - position.x;
                                    y = instances[i].m_Position.y - position.y;
                                    z = instances[i].m_Position.z - position.z;

                                    if ((x * x + y * y + z * z) < distanceDelta)
                                    {
                                        count++;
                                    }
                                }
                            }
                        }
                    });
                }
            });

            return count;
        }

        /** Get all instance counts */
        public int GetInstanceCount()
        {
            int count = 0;

            foreach (FoliageCellData data in m_FoliageData.Values)
            {
                // Look through all the types
                foreach(int typeHash in data.m_TypeHashLocationsEditor.Keys)
                {
                    var labeledData = data.m_TypeHashLocationsEditor[typeHash];

                    foreach (var typeData in labeledData.Values)
                    {
                        count += typeData.Count;
                    }
                }

                // Look through the subdivisions too
                foreach (FoliageCellSubdividedData dataSubdivided in data.m_FoliageDataSubdivided.Values)
                {
                    // Look through all the types
                    foreach(int typeHash in dataSubdivided.m_TypeHashLocationsEditor.Keys)
                    {
                        var labeledData = dataSubdivided.m_TypeHashLocationsEditor[typeHash];

                        foreach (var typeDataSubdivided in labeledData.Values)
                        {
                            count += typeDataSubdivided.Count;
                        }
                    }
                }
            }

            return count;
        }

        /**
         * Get all the instance count for a given type. Will search through the whole hierarchy including 
         * the subdivided data.
         */
        public int GetInstanceCount(int typeHash)
        {
            int count = 0;

            foreach(FoliageCellData data in m_FoliageData.Values)
            {       
                // Look through all the types
                if(data.m_TypeHashLocationsEditor.ContainsKey(typeHash))
                {
                    var labeledData = data.m_TypeHashLocationsEditor[typeHash];

                    foreach (var typeData in labeledData.Values)
                    {
                        count += typeData.Count;
                    }
                }
                
                // Look through the subdivisions too
                foreach (FoliageCellSubdividedData dataSubdivided in data.m_FoliageDataSubdivided.Values)
                {
                    // Look through all the types
                    if(dataSubdivided.m_TypeHashLocationsEditor.ContainsKey(typeHash))
                    {
                        var labeledData = dataSubdivided.m_TypeHashLocationsEditor[typeHash];

                        foreach (var typeDataSubdivided in labeledData.Values)
                        {
                            count += typeDataSubdivided.Count;
                        }
                    }
                }
            }

            return count;
        }
        
        public HashSet<int> GetFoliageHashes()
        {
            HashSet<int> tempHashes = null;

            if (m_FoliageData.Count > 0)
            {
                foreach (FoliageCellData cell in m_FoliageData.Values)
                {
                    // Get the tree instances hashes
                    if(cell.m_TypeHashLocationsEditor.Count > 0)
                    {
                        if (tempHashes == null)
                            tempHashes = new HashSet<int>();

                        tempHashes.UnionWith(cell.m_TypeHashLocationsEditor.Keys);
                    }

                    // Get the grass instances hashes
                    foreach (FoliageCellSubdividedData cellSubdivided in cell.m_FoliageDataSubdivided.Values)
                    {
                        if(cellSubdivided.m_TypeHashLocationsEditor.Count > 0)
                        {
                            if (tempHashes == null)
                                tempHashes = new HashSet<int>();

                            tempHashes.UnionWith(cellSubdivided.m_TypeHashLocationsEditor.Keys);
                        }                        
                    }
                }
            }

            return tempHashes;
        }

        public HashSet<string> GetFoliageLabels()
        {
            HashSet<string> tempLabels = null;
            
            foreach (var cell in m_FoliageData.Values)
            {
                // Look through the cells
                foreach (var typedData in cell.m_TypeHashLocationsEditor.Values)
                {
                    if (tempLabels == null)
                        tempLabels = new HashSet<string>();

                    tempLabels.UnionWith(typedData.Keys);
                }
                
                // Look through the subdivisions
                foreach (var cellSubdiv in cell.m_FoliageDataSubdivided.Values)
                {
                    foreach (var typedData in cellSubdiv.m_TypeHashLocationsEditor.Values)
                    {
                        if (tempLabels == null)
                            tempLabels = new HashSet<string>();

                        tempLabels.UnionWith(typedData.Keys);
                    }
                }
            }
            
            return tempLabels;
        }

        private void RemoveEmptyTypeDataCell(FoliageCellData data)
        {
            if (data.m_TypeHashLocationsEditor.Count > 0)
            {
                HashSet<int> removeTypes = null;

                // Remove labeled data
                foreach (var pair in data.m_TypeHashLocationsEditor)
                {
                    Dictionary<string, List<FoliageInstance>> labeled = pair.Value;
                    HashSet<string> removeLabels = null;

                    // Iterate through the label data
                    foreach (var pairLabeled in labeled)
                    {
                        if (pairLabeled.Value.Count <= 0)
                        {
                            if (removeLabels == null)
                                removeLabels = new HashSet<string>();

                            removeLabels.Add(pairLabeled.Key);
                        }
                    }

                    // Labels to remove
                    if (removeLabels != null)
                    {
                        foreach (string label in removeLabels)
                            labeled.Remove(label);
                    }

                    // If we ended up with an empty list of data
                    if (labeled.Count <= 0)
                    {
                        if (removeTypes == null)
                            removeTypes = new HashSet<int>();

                        removeTypes.Add(pair.Key);
                    }
                }

                if (removeTypes != null)
                {
                    foreach (int type in removeTypes)
                        data.m_TypeHashLocationsEditor.Remove(type);
                }
            }
        }

        private void RemoveEmptyTypeDataCellSubdivided(FoliageCellSubdividedData data)
        {
            if (data.m_TypeHashLocationsEditor.Count > 0)
            {
                HashSet<int> removeTypes = null;

                // Remove labeled data
                foreach (var pair in data.m_TypeHashLocationsEditor)
                {
                    Dictionary<string, List<FoliageInstance>> labeled = pair.Value;
                    HashSet<string> removeLabels = null;

                    // Iterate through the label data
                    foreach (var pairLabeled in labeled)
                    {
                        if (pairLabeled.Value.Count <= 0)
                        {
                            if (removeLabels == null)
                                removeLabels = new HashSet<string>();

                            removeLabels.Add(pairLabeled.Key);
                        }
                    }

                    // Labels to remove
                    if (removeLabels != null)
                    {
                        foreach (string label in removeLabels)
                            labeled.Remove(label);
                    }

                    // If we ended up with an empty list of data
                    if (labeled.Count <= 0)
                    {
                        if (removeTypes == null)
                            removeTypes = new HashSet<int>();

                        removeTypes.Add(pair.Key);
                    }
                }

                if (removeTypes != null)
                {
                    foreach (int type in removeTypes)
                        data.m_TypeHashLocationsEditor.Remove(type);
                }
            }
        }

        private bool IsSubCellEmpty(FoliageCellSubdividedData cell)
        {
            foreach (var data in cell.m_TypeHashLocationsEditor.Values)
            {
                foreach (var instances in data.Values)
                {
                    // If we have a count return that we are not empty
                    if (instances.Count > 0)
                        return false;
                }
            }

            return true;
        }

        private bool IsCellEmpty(FoliageCellData cell)
        {       
            // Iterate subdivisions
            foreach(FoliageCellSubdividedData cellSubdiv in cell.m_FoliageDataSubdivided.Values)
            {
                if (IsSubCellEmpty(cellSubdiv) == false)
                    return false;
            }

            // Iterate divisions
            foreach (var data in cell.m_TypeHashLocationsEditor.Values)
            {
                foreach (var instances in data.Values)
                {
                    // If we have a count return that we are not empty
                    if (instances.Count > 0)
                        return false;
                }
            }

            return true;
        }

        /**
         * Recalculate the extended bounds after an removal. Try not to call it
         * too often since it is a costly operation. Make sure that we did remove
         * anything before calling it.
         */
        private void RecalculateBoundsAfterRemove()
        {
            foreach(FoliageCellData cell in m_FoliageData.Values)
            {
                cell.m_BoundsExtended = cell.m_Bounds;

                foreach(var labeled in cell.m_TypeHashLocationsEditor.Values)
                {
                    foreach(List<FoliageInstance> instances in labeled.Values)
                    {
                        // Encapsulare all the trees bounds
                        for (int i = 0; i < instances.Count; i++)
                            cell.m_BoundsExtended.Encapsulate(instances[i].m_Bounds);
                    }
                }
            }
        }

        /** Get a position in local space inside a cell. */
        private Vector3 GetLocalInCell(Vector3 worldPosition, FoliageCellData cell)
        {
            return worldPosition - cell.m_Bounds.min;
        }

        /** Get a position in world space that is inside a cell. */
        private Vector3 GetWorldInCell(Vector3 localPosition, FoliageCellData cell)
        {
            return localPosition + cell.m_Bounds.min;
        }
    }
}
