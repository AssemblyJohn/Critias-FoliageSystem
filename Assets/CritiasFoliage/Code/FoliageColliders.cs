/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    [System.Serializable]
    public class FoliageCollisionSettings
    {
        [Tooltip("Defaults to 'Camera.main.transform'")]
        public Transform m_WatchedTransform;

        [Tooltip("How many meters around the watched transform we are going to add colliders")]
        [Range(0, 100)]
        public float m_CollisionDistance = 7;

        [Tooltip("After how many walked meters we are going to refresh the colliders")]
        [Range(0, 50)]
        public float m_CollisionRefreshDistance = 5;

        // Layer used for the tree colliders
        [Tooltip("What layer we are going to use for the GameObject colliders")]
        public string m_UsedLayer = "Default";
    }

    public class FoliageColliders : MonoBehaviour
    {
        // Foliage collision settings
        public FoliageCollisionSettings m_Settings;

        private Vector3 m_LastPosition;        
        private GameObject m_ColliderHolder;

        private FoliageDataRuntime m_FoliageData;
        private Dictionary<int, FoliageType> m_FoliageTypes = new Dictionary<int, FoliageType>();        

        void Start()
        {
            if (!m_Settings.m_WatchedTransform) m_Settings.m_WatchedTransform = Camera.main.transform;            

            m_LastPosition = m_Settings.m_WatchedTransform.position;
            m_ColliderHolder = new GameObject("FoliageSystemColliderHolder");
        }

        public void InitCollider(FoliageDataRuntime dataToRender, List<FoliageType> foliageTypes)
        {
            // Set the render data
            m_FoliageData = dataToRender;

            // Init the types
            m_FoliageTypes.Clear();
            foreach (FoliageType type in foliageTypes)             
                m_FoliageTypes.Add(type.m_Hash, type);
        }

        private Dictionary<int, CollisionCache> m_Cache = new Dictionary<int, CollisionCache>();

        // Issued colliders
        private int m_DataIssuedActiveColliders;

        private Vector3 m_CameraPosTemp;
        private FoliageCell currentCell;
        private int m_Layer;

        private void Update()
        {
            if (!m_Settings.m_WatchedTransform)
                return;

            m_CameraPosTemp = m_Settings.m_WatchedTransform.position;

            float x = m_CameraPosTemp.x - m_LastPosition.x;
            float y = m_CameraPosTemp.y - m_LastPosition.y;
            float z = m_CameraPosTemp.z - m_LastPosition.z;

            float distWalked = x * x + y * y + z * z;

            // If we didn't walked enough, return
            if (distWalked > m_Settings.m_CollisionRefreshDistance * m_Settings.m_CollisionRefreshDistance)
            {
                // Update last position
                m_LastPosition = m_CameraPosTemp;

                m_Layer = LayerMask.NameToLayer(m_Settings.m_UsedLayer);

                // Reset counter
                m_DataIssuedActiveColliders = 0;

                // Reset all the cache's data
                foreach (CollisionCache cache in m_Cache.Values)
                {
                    if (cache != null) cache.Reset();
                }

                // Set the current cell
                currentCell.Set(m_LastPosition);

                // Refresh eveything        
                float collDistSqr = m_Settings.m_CollisionDistance * m_Settings.m_CollisionDistance;

                // Iterate cells
                FoliageCell.IterateNeighboring(currentCell, 1, (int hash) =>
                {
                    FoliageCellDataRuntime data;

                    if (m_FoliageData.m_FoliageData.TryGetValue(hash, out data))
                    {
                        // If it is within distance and in the frustum
                        float distanceSqr = data.m_Bounds.SqrDistance(m_LastPosition);

                        if (distanceSqr <= collDistSqr)
                            ProcessCell(data, collDistSqr);
                    }
                });              
            }

#if UNITY_EDITOR
            if(Time.frameCount % 300 == 0)
            {
                FoliageLog.i("Issued colliders: " + m_DataIssuedActiveColliders);
            }
#endif
        }
        
        private void ProcessCell(FoliageCellDataRuntime cell, float colliderDistSqr)
        {
            for (int foliageType = 0; foliageType < cell.m_TypeHashLocationsRuntime.Length; foliageType++)
            {
				int foliageTypeKey = cell.m_TypeHashLocationsRuntime[foliageType].Key;
                FoliageType type = m_FoliageTypes[foliageTypeKey];

                if (type.m_EnableCollision == false)
                    continue;

                var batches = cell.m_TypeHashLocationsRuntime[foliageType].Value.m_EditTime;
                int hash = type.m_Hash;

                float x, y, z;
                float dist;

                for(int i = 0; i < batches.Length; i++)
                {
                    Vector3 pos = batches[i].m_Position;

                    x = pos.x - m_LastPosition.x;
                    y = pos.y - m_LastPosition.y;
                    z = pos.z - m_LastPosition.z;

                    dist = x * x + y * y + z * z;

                    if(dist <= colliderDistSqr)
                    {
                        GameObject collider = GetColliderForPrototype(hash);

                        // Set the layer collider
                        collider.layer = m_Layer;

                        if (collider != null)
                        {
                            FoliageColliderData data = collider.GetComponent<FoliageColliderData>();

                            if (data == null)
                                data = collider.AddComponent<FoliageColliderData>();

                            // Append the collision data for query at the runtime
                            data.m_FoliageType = foliageTypeKey;
                            data.m_FoliageInstance = batches[i];

                            // Update it's transform values
                            collider.transform.position = batches[i].m_Position;
                            collider.transform.rotation = batches[i].m_Rotation;
                            collider.transform.localScale = batches[i].m_Scale;

                            // Increment the active collider count
                            m_DataIssuedActiveColliders++;
                        }
                    }
                }
            }
        }

        private GameObject GetColliderForPrototype(int hash)
        {
            FoliageType data = m_FoliageTypes[hash];

            if (data.m_EnableCollision == false)
                return null;

            if (m_Cache.ContainsKey(hash) == false)
            {
                // If we don't contain the key create and add it

                // If we don't have a tree with a collider, like a bush or something, just add a null mapping
                if (data.m_Prefab.GetComponentInChildren<Collider>() == null)
                {
                    m_Cache.Add(hash, null);
                    return null;
                }
                else
                {
                    // Create the collider prototype and remove all it's mesh renderers and stuff
                    GameObject colliderPrototype = Instantiate(data.m_Prefab, m_ColliderHolder.transform);
                    colliderPrototype.name = "ColliderPrototype_" + data.m_Prefab.name;
                    					
                    // Clear the lod group
                    LODGroup lod = colliderPrototype.GetComponent<LODGroup>();
                    if (lod) DestroyImmediate(lod);

                    // Clear any owned GObjects that don't have colliders
                    for (int i = colliderPrototype.transform.childCount - 1; i >= 0; i--)
                    {
                        GameObject owned = colliderPrototype.transform.GetChild(i).gameObject;

                        if (owned.GetComponent<Collider>() == null)
                            DestroyImmediate(owned);
                    }

                    Component[] components = colliderPrototype.GetComponentsInChildren<Component>();

                    // Delete all non-colliders
                    for (int i = 0; i < components.Length; i++)
                    {
                        if ((components[i] is Collider) == false && (components[i] is Transform) == false)
                        {
                            DestroyImmediate(components[i]);
                        }
                    }
                    
                    // Deactivate it
                    colliderPrototype.SetActive(false);

                    // Create the cache entry
                    CollisionCache cache = new CollisionCache(colliderPrototype, m_ColliderHolder);

                    // Add the collision cache to our dictionary
                    m_Cache.Add(hash, cache);
                    return cache.RetrieveInstance();
                }
            }
            else
            {
                var cache = m_Cache[hash];

                // We contain the cache, just retrieve an object
                if (cache != null)
                    return m_Cache[hash].RetrieveInstance();
                else
                    return null;
            }
        }
    }

    public class CollisionCache
    {
        private GameObject m_CacheInstanceOwner;
        private GameObject m_CachePrototype;

        private List<GameObject> m_ActiveInstances = new List<GameObject>();
        private List<GameObject> m_InactiveInstances = new List<GameObject>();

        private int m_ExpansionSize;

        public CollisionCache(GameObject collisionPrototype, GameObject instanceOwner = null, int expansionSize = 3)
        {
            m_CacheInstanceOwner = instanceOwner;
            m_CachePrototype = collisionPrototype;
            m_ExpansionSize = expansionSize;
        }

        public GameObject RetrieveInstance()
        {
            if (m_InactiveInstances.Count == 0)
            {
                for (int i = 0; i < m_ExpansionSize; i++)
                {
                    GameObject cached = Object.Instantiate(m_CachePrototype, m_CacheInstanceOwner.transform);
                    cached.SetActive(false);

                    m_InactiveInstances.Add(cached);
                }
            }

            // Get the instance item
            GameObject inst = m_InactiveInstances[0];
            m_InactiveInstances.RemoveAt(0);

            // Add it to the active list
            m_ActiveInstances.Add(inst);

            // Activate and return it
            inst.SetActive(true);

            return inst;
        }

        public void RecycleInstance(GameObject instance)
        {
            if (m_ActiveInstances.Remove(instance))
            {
                instance.SetActive(false);
                m_InactiveInstances.Add(instance);
            }
        }

        /*
         * Marks all the active instances inactive.
         */
        public void Reset()
        {
            // Add all the instances to the inactive
            for (int i = 0; i < m_ActiveInstances.Count; i++)
            {
                GameObject cached = m_ActiveInstances[i];
                cached.SetActive(false);

                m_InactiveInstances.Add(cached);
            }

            // Clear the active instances
            m_ActiveInstances.Clear();
        }
    }
}