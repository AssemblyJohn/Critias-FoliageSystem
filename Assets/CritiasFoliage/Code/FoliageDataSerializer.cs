/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

namespace CritiasFoliage
{

    /**
     * Class that provides utilities for saving and loadin the grass from a file in the streaming assets folder.
     */
    public class FoliageDataSerializer
    {
        /**
         * Load from file the runtime version of the data
         */
        public static FoliageDataRuntime LoadFromFileRuntime(string filename)
        {            
            FoliageData data = LoadFromFileEditTime(filename);
            
            // Build the runtime data from the edit time data
            FoliageDataRuntime runtime = new FoliageDataRuntime();            

            foreach (var hashedCell in data.m_FoliageData)
            {
                FoliageCellData editCell = hashedCell.Value;
                FoliageCellDataRuntime runtimeCell = new FoliageCellDataRuntime();

                // Set the data. Note that we only need the extended bounds
                runtimeCell.m_Bounds = editCell.m_BoundsExtended;
                runtimeCell.m_Position = editCell.m_Position;

                // Build the tree instance data
                int idx = -1;
                runtimeCell.m_TypeHashLocationsRuntime = new FoliageKeyValuePair<int, FoliageTuple<FoliageInstance[]>>[editCell.m_TypeHashLocationsEditor.Count];
                foreach (var instances in editCell.m_TypeHashLocationsEditor)
                {
                    idx++;

                    List<FoliageInstance> allTreeInstances = new List<FoliageInstance>();
                    var labeledInstances = instances.Value;

                    // Build all the data from the labeled data
                    foreach (List<FoliageInstance> inst in labeledInstances.Values)
                        allTreeInstances.AddRange(inst);

                    // We will build the world matrix for trees
                    for (int i = 0; i < allTreeInstances.Count; i++)
                    {
                        FoliageInstance inst = allTreeInstances[i];
                        inst.BuildWorldMatrix();
                        allTreeInstances[i] = inst;
                    }

                    // Don't forget to trim all excess instances!
                    allTreeInstances.TrimExcess();

                    #if UNITY_EDITOR
                    if (allTreeInstances.Count == 0)
                        Debug.Assert(false, "Count 0!");
                    #endif

                    runtimeCell.m_TypeHashLocationsRuntime[idx] = new FoliageKeyValuePair<int, FoliageTuple<FoliageInstance[]>>(instances.Key, new FoliageTuple<FoliageInstance[]>(allTreeInstances.ToArray()));
                }

                // Build the grass instance data from the subdivided cells                
                List<FoliageKeyValuePair<int, FoliageCellSubdividedDataRuntime>> foliageCellDataSubdivided = new List<FoliageKeyValuePair<int, FoliageCellSubdividedDataRuntime>>(editCell.m_FoliageDataSubdivided.Count);

                foreach (var hashedSubdividedCell in editCell.m_FoliageDataSubdivided)
                {
                    FoliageCellSubdividedData editSubdividedCell = hashedSubdividedCell.Value;
                    FoliageCellSubdividedDataRuntime runtimeSubdividedCell = new FoliageCellSubdividedDataRuntime();

                    // Set the data
                    runtimeSubdividedCell.m_Bounds = editSubdividedCell.m_Bounds;
                    runtimeSubdividedCell.m_Position = editSubdividedCell.m_Position;

                    idx = -1;
                    runtimeSubdividedCell.m_TypeHashLocationsRuntime = new FoliageKeyValuePair<int, FoliageTuple<Matrix4x4[][]>>[editSubdividedCell.m_TypeHashLocationsEditor.Count];
                    foreach(var instances in editSubdividedCell.m_TypeHashLocationsEditor)
                    {
                        idx++;

                        List<FoliageInstance> allGrassInstances = new List<FoliageInstance>();
                        var labeledInstances = instances.Value;

                        foreach (List<FoliageInstance> inst in labeledInstances.Values)
                            allGrassInstances.AddRange(inst);

                        #if UNITY_EDITOR
                        if (allGrassInstances.Count == 0)
                            Debug.Assert(false, "Count 0!");
                        #endif

                        // Build the multi-array data
                        int ranges = Mathf.CeilToInt(allGrassInstances.Count / (float)FoliageGlobals.RENDER_BATCH_SIZE);
                        Matrix4x4[][] batches = new Matrix4x4[ranges][];

                        for (int i = 0; i < ranges; i++)
                        {
                            List<FoliageInstance> range = allGrassInstances.GetRange(i * FoliageGlobals.RENDER_BATCH_SIZE, 
                                i * FoliageGlobals.RENDER_BATCH_SIZE + FoliageGlobals.RENDER_BATCH_SIZE > allGrassInstances.Count 
                                    ? allGrassInstances.Count - i * FoliageGlobals.RENDER_BATCH_SIZE 
                                    : FoliageGlobals.RENDER_BATCH_SIZE);                          

                            batches[i] = range.ConvertAll<Matrix4x4>((x) => x.GetWorldTransform()).ToArray();
                        }

                        // Set the data
                        runtimeSubdividedCell.m_TypeHashLocationsRuntime[idx] = new FoliageKeyValuePair<int, FoliageTuple<Matrix4x4[][]>>(instances.Key, new FoliageTuple<Matrix4x4[][]>(batches));
                    }

                    // Add the subdivided runtime cell
                    foliageCellDataSubdivided.Add(new FoliageKeyValuePair<int, FoliageCellSubdividedDataRuntime>(hashedSubdividedCell.Key, runtimeSubdividedCell));
                }

                // Build the subdivided data
                runtimeCell.m_FoliageDataSubdivided = foliageCellDataSubdivided.ToArray();


                // Add the runtime cell
                runtime.m_FoliageData.Add(hashedCell.Key, runtimeCell);
            }

            // Good for GC
            data = null;

            return runtime;
        }

        /**
         * Loads the edited grass that was added at edit time. It can even be an empty file,
         * since we can add all the grass data that we need at runtime.
         * 
         * @param filename
         *          Filename where we should load the data from
         * @param loadRuntimeDataOnly
         *          To be used at runtime only. If this is 'true' it will not load the whole hierarchy
         *          but only the runtime use intended data. Set this to false when loading from the
         *          'FoliagePainter' that is intended for edit time use and 'false' when loading from
         *          the 'FoliageRenderer' that is intended for runtime use
         */
        public static FoliageData LoadFromFileEditTime(string filename)
        {
            string path = Path.Combine(Application.streamingAssetsPath, filename);
            Debug.Log("Loading runtime foliage from file: " + path);

            FoliageData data = new FoliageData();

            // Ensure the file exists
            if (File.Exists(path))
            {
                // Read the runtime grass from a file
                // using (BinaryReader reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(path))))
                using (BinaryReader reader = new BinaryReader(new BufferedStream(File.OpenRead(path))))
                {
                    ulong ID = reader.ReadUInt64();
                    int version = reader.ReadInt32();

                    if (ID == FoliageGlobals.DISK_IDENTIFIER)
                    {
                        Debug.Log(string.Format("Reading file with identifier: {0} and version: {1}", ID, version));
                        ReadFoliageData(reader, data);
                    }
                    else
                    {
                        Debug.LogError("Foliage data has been tampered with! Delete it!");
                    }
                }
            }
            else
            {
                Debug.LogWarning("Warning, no grass file data exists! Save the grass!");
            }

#if UNITY_EDITOR
            {
                int instances = data.GetInstanceCount();
                Debug.Log(string.Format("Read foliage data with [{0} Cells] and [{1} Instances].", data.m_FoliageData.Count, instances));
            }
#endif

            return data;
        }

        public static void SaveToFile(string filename, FoliageData data)
        {
            string path = Path.Combine(Application.streamingAssetsPath, filename);
            Debug.Log("Saving runtime grass to file: " + path);

            // Write the grass runtime to the file
            if (File.Exists(path))
                File.Delete(path);

            using (BinaryWriter writer = new BinaryWriter(new BufferedStream(File.Open(path, FileMode.OpenOrCreate))))
            {
                writer.Write(FoliageGlobals.DISK_IDENTIFIER);
                writer.Write(FoliageGlobals.DISK_VERSION);

                // Remove any empty data so that we don't have any empty cells that we're going to have to test for
                data.RemoveEmptyData();

                WriteFoliageData(writer, data);
            }

            // Just print out some information
#if UNITY_EDITOR
            {
                int instances = data.GetInstanceCount();                
                Debug.Log(string.Format("Written foliage data with [{0} Cells] and [{1} Instances].", data.m_FoliageData.Count, instances));
            }
#endif
        }
        
        private static void WriteFoliageData(BinaryWriter a, FoliageData data)
        {
            Dictionary<int, FoliageCellData> cellData = data.m_FoliageData;

            // Write the count of entries
            a.Write(cellData.Count);

            foreach (int key in cellData.Keys)
            {
                WriteFoliageCellData(a, key, cellData[key]);
            }
        }

        private static void ReadFoliageData(BinaryReader a, FoliageData data)
        {
            // Read the count of entries
            int entries = a.ReadInt32();

            for (int i = 0; i < entries; i++)
            {
                int key;
                FoliageCellData cellData;

                // Read the cell data
                ReadFoliageCellData(a, out key, out cellData);

                // Add it to the list
                data.m_FoliageData.Add(key, cellData);
            }
        }

        private static void ReadFoliageCellData(BinaryReader a, out int key, out FoliageCellData data)
        {
            // Read key
            key = a.ReadInt32();

            // Read foliage cell data
            data = new FoliageCellData();

            // Read bounds and position
            data.m_Bounds = ReadBounds(a);
            data.m_BoundsExtended = ReadBounds(a);
            data.m_Position = ReadFoliageCell(a);

            {
                // Read inner cell data
                int entries = a.ReadInt32();
                for (int i = 0; i < entries; i++)
                {
                    int dataKey = a.ReadInt32();

                    // Add the type
                    data.m_TypeHashLocationsEditor.Add(dataKey, new Dictionary<string, List<FoliageInstance>>());

                    // Read the foliage instances
                    int entriesLabeled = a.ReadInt32();
                    for (int j = 0; j < entriesLabeled; j++)
                    {
                        string labeledKey = a.ReadString();
                        List<FoliageInstance> instances = ReadListFoliageInstance(a, true);

                        data.m_TypeHashLocationsEditor[dataKey].Add(labeledKey, instances);
                    }
                }
            }

            {
                // Read the subdivided data
                int subdivided = a.ReadInt32();
                for (int j = 0; j < subdivided; j++)
                {
                    int subdivKey;
                    FoliageCellSubdividedData subdivData;

                    ReadFoliageCellDataSubdivided(a, out subdivKey, out subdivData);
                    data.m_FoliageDataSubdivided.Add(subdivKey, subdivData);
                }
            }
        }

        private static void WriteFoliageCellData(BinaryWriter a, int key, FoliageCellData data)
        {
            // Write key
            a.Write(key);

            // Write foliage cell data

            // Write bounds and position
            WriteBounds(a, data.m_Bounds);
            WriteBounds(a, data.m_BoundsExtended);
            WriteFoliageCell(a, data.m_Position);

            {
                // Write inner cell data
                a.Write(data.m_TypeHashLocationsEditor.Count);
                foreach (int dataKey in data.m_TypeHashLocationsEditor.Keys)
                {
                    // Write data
                    a.Write(dataKey);
                    var labeled = data.m_TypeHashLocationsEditor[dataKey];

                    // Write foliage instances
                    a.Write(labeled.Count);
                    foreach (string dataKeyLabel in labeled.Keys)
                    {
                        a.Write(dataKeyLabel);
                        WriteListFoliageInstance(a, labeled[dataKeyLabel], true, true);
                    }
                }
            }

            {
                // Write subdivided data
                a.Write(data.m_FoliageDataSubdivided.Count);
                foreach (int subdivKey in data.m_FoliageDataSubdivided.Keys)
                {
                    WriteFoliageCellDataSubdivided(a, subdivKey, data.m_FoliageDataSubdivided[subdivKey]);
                }
            }
        }

        private static void ReadFoliageCellDataSubdivided(BinaryReader a, out int key, out FoliageCellSubdividedData data)
        {
            // Read the key
            key = a.ReadInt32();

            // Create new data
            data = new FoliageCellSubdividedData();

            data.m_Bounds = ReadBounds(a);
            data.m_Position = ReadFoliageCell(a);

            int entries = a.ReadInt32();

            for(int i = 0; i < entries; i++)
            {
                int dataKey = a.ReadInt32();

                // Add the data with the key
                data.m_TypeHashLocationsEditor.Add(dataKey, new Dictionary<string, List<FoliageInstance>>());

                // Read the content
                int entriesLabeled = a.ReadInt32();
                for(int j = 0; j < entriesLabeled; j++)
                {
                    string dataKeyLabel = a.ReadString();
                    List<FoliageInstance> instances = ReadListFoliageInstance(a, false);

                    data.m_TypeHashLocationsEditor[dataKey].Add(dataKeyLabel, instances);
                }
            }            
        }        

        private static void WriteFoliageCellDataSubdivided(BinaryWriter a, int key, FoliageCellSubdividedData data)
        {
            // Write key
            a.Write(key);

            WriteBounds(a, data.m_Bounds);
            WriteFoliageCell(a, data.m_Position);

            // Write inner cell data
            a.Write(data.m_TypeHashLocationsEditor.Count);

            foreach(int dataKey in data.m_TypeHashLocationsEditor.Keys)
            {
                a.Write(dataKey);
                var labeled = data.m_TypeHashLocationsEditor[dataKey];

                a.Write(labeled.Count);
                foreach(string dataKeyLabel in labeled.Keys)
                {
                    a.Write(dataKeyLabel);
                    WriteListFoliageInstance(a, labeled[dataKeyLabel], false, true);
                }
            }
        }

        public static List<FoliageInstance> ReadListFoliageInstance(BinaryReader a, bool tree)
        {
            int count = a.ReadInt32();

            List<FoliageInstance> list = new List<FoliageInstance>(count);

            for (int i = 0; i < count; i++)
                list.Add(ReadFoliageInstance(a, tree));

            return list;
        }

        public static void WriteListFoliageInstance(BinaryWriter a, List<FoliageInstance> list, bool tree, bool shuffle)
        {
            a.Write(list.Count);

            // Shuffle the list if we save so that when we use the density we won't have problems with the lastly set list    
            if(shuffle)
                FoliageUtilities.Shuffle(list);            

            for (int i = 0; i < list.Count; i++)
                WriteFoliageInstance(a, list[i], tree);
        }
        
        private static void WriteFoliageInstance(BinaryWriter a, FoliageInstance i, bool tree)
        {
            if(tree)
            {
                WriteBounds(a, i.m_Bounds);
                WriteVector3(a, i.m_Position);
                WriteQuaternion(a, i.m_Rotation);
                WriteVector3(a, i.m_Scale);
                WriteGuid(a, i.m_UniqueId);
            }
            else
            {
                WriteVector3(a, i.m_Position);
                WriteQuaternion(a, i.m_Rotation);
                WriteVector3(a, i.m_Scale);
            }
        }

        private static FoliageInstance ReadFoliageInstance(BinaryReader a, bool tree)
        {
            FoliageInstance instance = new FoliageInstance();

            if(tree)
            {
                instance.m_Bounds = ReadBounds(a);
                instance.m_Position = ReadVector3(a);
                instance.m_Rotation = ReadQuaternion(a);
                instance.m_Scale = ReadVector3(a);
                instance.m_UniqueId = ReadGuid(a);
            }
            else
            {
                instance.m_Position = ReadVector3(a);
                instance.m_Rotation = ReadQuaternion(a);
                instance.m_Scale = ReadVector3(a);
            }

            return instance;
        }

        private static System.Guid ReadGuid(BinaryReader a)
        {
            System.Guid g;

            byte ct = a.ReadByte();
            g = new System.Guid(a.ReadBytes(ct));

            return g;
        }

        private static void WriteGuid(BinaryWriter a, System.Guid g)
        {
            byte[] guid = g.ToByteArray();

            a.Write((byte)guid.Length);
            a.Write(guid);
        }

        private static void WriteMatrix4x4(BinaryWriter a, Matrix4x4 m)
        {
            a.Write(m.m00);
            a.Write(m.m01);
            a.Write(m.m02);
            a.Write(m.m03);

            a.Write(m.m10);
            a.Write(m.m11);
            a.Write(m.m12);
            a.Write(m.m13);

            a.Write(m.m20);
            a.Write(m.m21);
            a.Write(m.m22);
            a.Write(m.m23);

            a.Write(m.m30);
            a.Write(m.m31);
            a.Write(m.m32);
            a.Write(m.m33);
        }        

        private static Matrix4x4 ReadMatrix4x4(BinaryReader a)
        {
            Matrix4x4 m;

            m.m00 = a.ReadSingle();
            m.m01 = a.ReadSingle();
            m.m02 = a.ReadSingle();
            m.m03 = a.ReadSingle();

            m.m10 = a.ReadSingle();
            m.m11 = a.ReadSingle();
            m.m12 = a.ReadSingle();
            m.m13 = a.ReadSingle();

            m.m20 = a.ReadSingle();
            m.m21 = a.ReadSingle();
            m.m22 = a.ReadSingle();
            m.m23 = a.ReadSingle();

            m.m30 = a.ReadSingle();
            m.m31 = a.ReadSingle();
            m.m32 = a.ReadSingle();
            m.m33 = a.ReadSingle();

            return m;
        }

        private static void WriteQuaternion(BinaryWriter a, Quaternion q)
        {
            a.Write(q.x);
            a.Write(q.y);
            a.Write(q.z);
            a.Write(q.w);
        }
        
        private static Quaternion ReadQuaternion(BinaryReader a)
        {
            Quaternion q;

            q.x = a.ReadSingle();
            q.y = a.ReadSingle();
            q.z = a.ReadSingle();
            q.w = a.ReadSingle();

            return q;
        }

        private static void WriteVector2(BinaryWriter a, Vector2 v)
        {
            a.Write(v.x);
            a.Write(v.y);
        }

        private static void ReadVector2(BinaryReader a, out Vector2 v)
        {
            v.x = a.ReadSingle();
            v.y = a.ReadSingle();
        }

        private static Vector2 ReadVector2(BinaryReader a)
        {
            Vector2 v;

            v.x = a.ReadSingle();
            v.y = a.ReadSingle();

            return v;
        }

        private static void WriteVector3(BinaryWriter a, Vector3 v)
        {
            a.Write(v.x);
            a.Write(v.y);
            a.Write(v.z);
        }

        private static void ReadVector3(BinaryReader a, out Vector3 v)
        {
            v.x = a.ReadSingle();
            v.y = a.ReadSingle();
            v.z = a.ReadSingle();
        }

        private static Vector3 ReadVector3(BinaryReader a)
        {
            Vector3 v;

            v.x = a.ReadSingle();
            v.y = a.ReadSingle();
            v.z = a.ReadSingle();

            return v;
        }

        private static void WriteBounds(BinaryWriter a, Bounds b)
        {
            WriteVector3(a, b.center);
            WriteVector3(a, b.size);
        }

        private static void ReadBounds(BinaryReader a, out Bounds b)
        {
            b = new Bounds(ReadVector3(a), ReadVector3(a));
        }

        private static Bounds ReadBounds(BinaryReader a)
        {
            return new Bounds(ReadVector3(a), ReadVector3(a));
        }

        private static void WriteFoliageCell(BinaryWriter a, FoliageCell fc)
        {
            a.Write(fc.x);
            a.Write(fc.y);
            a.Write(fc.z);
        }

        private static void ReadFoliageCell(BinaryReader a, out FoliageCell fc)
        {
            fc.x = a.ReadInt32();
            fc.y = a.ReadInt32();
            fc.z = a.ReadInt32();
        }

        private static FoliageCell ReadFoliageCell(BinaryReader a)
        {
            FoliageCell fc;

            fc.x = a.ReadInt32();
            fc.y = a.ReadInt32();
            fc.z = a.ReadInt32();

            return fc;
        }
    }
}