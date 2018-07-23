/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    public enum EExtractType
    {
        // Extract the colliders if we have any
        COLLIDERS,
        // Extract the renderers
        RENDERERS,
        // Extract the renderers for meshes that have collision
        RENDERERS_FOR_COLLIDER_MESHES_NAVMESH,
        // Extract everything
        NON_MODIFIED
    }

    public class FoliageUtilities
    {
        /**
         * Extract a foliage prefab type for an operation. Returns null if it couldn't be used for that operation.
         */
        public static GameObject ExtractFromFoliagePrefab(GameObject foliageType, EExtractType extractType, bool shouldBeStatic)
        {
            GameObject prototype = null;

            // Pass unchanged
            if (extractType == EExtractType.COLLIDERS 
                || extractType == EExtractType.RENDERERS_FOR_COLLIDER_MESHES_NAVMESH
                || extractType == EExtractType.RENDERERS)
            {                
                System.Type[] systemMustHaveType = null;
                System.Type systemExtractType = null;

                switch(extractType)
                {
                    case EExtractType.COLLIDERS:
                        systemMustHaveType = new System.Type[] { typeof(Collider) };
                        systemExtractType = typeof(Collider);
                        break;
                    case EExtractType.RENDERERS_FOR_COLLIDER_MESHES_NAVMESH:
                        systemMustHaveType = new System.Type[] { typeof(Collider), typeof(MeshFilter) };
                        systemExtractType = typeof(Renderer);
                        break;
                    case EExtractType.RENDERERS:
                        systemMustHaveType = new System.Type[] { typeof(Collider), typeof(MeshFilter) };
                        systemExtractType = typeof(Renderer);
                        break;

                }

                Debug.Assert(systemExtractType != null);

                // If it's for colliders, can return null
                if (systemMustHaveType != null)
                {
                    for (int i = 0; i < systemMustHaveType.Length; i++)
                    {
                        if (foliageType.GetComponentInChildren(systemMustHaveType[i]) == null)
                            return null;
                    }
                }
                
                prototype = GameObject.Instantiate(foliageType);
                prototype.name = extractType + "_Prototype_" + foliageType.name;

                if (extractType == EExtractType.COLLIDERS)
                {
                    LODGroup lod = prototype.GetComponent<LODGroup>();
                    if (lod) GameObject.DestroyImmediate(lod);
                }

                // Clear any owned GObjects that don't have colliders
                for (int i = prototype.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject owned = prototype.transform.GetChild(i).gameObject;

                    if (owned.GetComponent(systemExtractType) == null)
                        GameObject.DestroyImmediate(owned);
                }

                Component[] components = prototype.GetComponentsInChildren<Component>();

                // Delete all non-colliders
                for (int i = 0; i < components.Length; i++)
                {
                    if (extractType == EExtractType.COLLIDERS)
                    {                        
                        if ((components[i].GetType().IsSubclassOf(systemExtractType)) == false && (components[i] is Transform) == false)
                        {
                            GameObject.DestroyImmediate(components[i]);
                        }
                    }
                    else
                    {
                        if ((components[i].GetType().IsSubclassOf(systemExtractType)) == false 
                            && (components[i] is Transform) == false
                            && (components[i] is LODGroup) == false
                            && (components[i] is MeshFilter) == false)
                        {
                            GameObject.DestroyImmediate(components[i]);
                        }
                    }
                }                
            }
            else if (extractType == EExtractType.NON_MODIFIED)
            {
                prototype = GameObject.Instantiate(foliageType);
                prototype.name = extractType + "_Prototype_" + foliageType.name;                
            }


#if UNITY_EDITOR
            if (prototype != null && shouldBeStatic && extractType != EExtractType.RENDERERS_FOR_COLLIDER_MESHES_NAVMESH)
            {
                prototype.isStatic = true;

                for (int i = prototype.transform.childCount - 1; i >= 0; i--)
                    prototype.transform.GetChild(i).gameObject.isStatic = true;
            }
            else if(extractType == EExtractType.RENDERERS_FOR_COLLIDER_MESHES_NAVMESH && shouldBeStatic)
            {
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(prototype, UnityEditor.StaticEditorFlags.NavigationStatic);

                for (int i = prototype.transform.childCount - 1; i >= 0; i--)
                    UnityEditor.GameObjectUtility.SetStaticEditorFlags(prototype.transform.GetChild(i).gameObject, UnityEditor.StaticEditorFlags.NavigationStatic);
            }
#endif

            return prototype;
        }

        public static Bounds LocalToWorld(ref Bounds box, Matrix4x4 m)
        {
            return LocalToWorld(ref box, ref m);
        }

        /**
         * Transforms the bounds from the local space to world space, including the rotation and scale.
         */
        public static Bounds LocalToWorld(ref Bounds box, ref Matrix4x4 m)
        {
            float av, bv;
            int i, j;

            Bounds newBox = new Bounds(Vector3.zero, Vector3.zero);

            newBox.min = new Vector3(m[12], m[13], m[14]);
            newBox.max = new Vector3(m[12], m[13], m[14]);

            Vector3 min, max;

            for (i = 0; i < 3; i++)
            {
                for (j = 0; j < 3; j++)
                {

                    av = m[i, j] * box.min[j];
                    bv = m[i, j] * box.max[j];

                    if (av < bv)
                    {
                        min = newBox.min;
                        max = newBox.max;

                        min[i] += av;
                        max[i] += bv;

                        newBox.min = min;
                        newBox.max = max;
                    }
                    else
                    {
                        min = newBox.min;
                        max = newBox.max;

                        min[i] += bv;
                        max[i] += av;

                        newBox.min = min;
                        newBox.max = max;
                    }
                }
            }

            return newBox;
        }

        public static void Shuffle<T>(IList<T> collection)
        {
            for (int i = 0; i < collection.Count; i++)
            {
                T temp = collection[i];

                int randomIndex = Random.Range(0, collection.Count);

                collection[i] = collection[randomIndex];
                collection[randomIndex] = temp;
            }
        }

        public static int GetStableHashCode(string str)
        {
            int hash1 = (5381 << 16) + 5381;
            int hash2 = hash1;

            for (int i = 0; i < str.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1)
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }

        public static int GetStableHashCode(ref string str)
        {
            int hash1 = (5381 << 16) + 5381;
            int hash2 = hash1;

            for (int i = 0; i < str.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1)
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }
}