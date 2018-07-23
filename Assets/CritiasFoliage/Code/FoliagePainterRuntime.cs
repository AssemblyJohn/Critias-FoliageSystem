/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    /**
     * Runtime struct for extra data
     */
    public struct FoliageTypeRuntime
    {
        public int m_Hash;
        public string m_Name;

        public EFoliageType m_Type;

        public bool m_IsGrassType;
        public bool m_IsSpeedTreeType;
    }


    /**
     * Class that must be be used when runtime operations take place upon the foliage data.
     * 
     * WARNING: Any non-public operations that are NOT included in this class, are NOT supported at runtime!
     */
    public struct FoliagePainterRuntime
    {
        /** 
         * Get the foliage types with a pair of hash/name as a string. 
         * 
         * NOTE: Since this creates a copy don't call often, but keep a copy of this. Only if we 
         * remove/add a new foliage type to the system at runtime request a new list of types.
         */
        public List<FoliageTypeRuntime> GetFoliageTypes()
        {
            return m_Painter.GetFoliageTypesRuntime();
        }

        /**
         * Set the foliage hue variation at runtime. Works for SpeedTrees only
         */
        public void SetFoliageTypeHue(int typeHash, Color hue)
        {
            m_Painter.SetFoliageTypeHueRuntime(typeHash, hue);
        }

        public Color GetFoliageTypeHue(int typeHash)
        {
            return m_Painter.GetFoliageTypeHueRuntime(typeHash);
        }

        /**
         * Set the foliage color variation at runtime. Works for SpeedTrees only
         */
        public void SetFoliageTypeColor(int typeHash, Color color)
        {
            m_Painter.SetFoliageTypeColorRuntime(typeHash, color);
        }

        public Color GetFoliageTypeColor(int typeHash)
        {
            return m_Painter.GetFoliageTypeColorRuntime(typeHash);
        }

        /**
         * Set if a foliage type should cast shadows
         */
        public void SetFoliageTypeCastShadow(int typeHash, bool castShadow)
        {
            m_Painter.SetFoliageTypeCastShadowRuntime(typeHash, castShadow);
        }

        public bool GetFoliageTypeCastShadow(int typeHash)
        {
            return m_Painter.GetFoliageTypeCastShadowRuntime(typeHash);
        }

        /**
         * Set the maximum draw distance for a foliage type
         */
        public void SetFoliageTypeMaxDistance(int typeHash, float maxDistance)
        {
            m_Painter.SetFoliageTypeMaxDistanceRuntime(typeHash, maxDistance);
        }

        public float GetFoliageTypeMaxDistance(int typeHash)
        {
            return m_Painter.GetFoliageTypeMaxDistanceRuntime(typeHash);
        }

        /**
         * Removes a single foliage instance from the system, based on the type hash and guid. It is very usefull for destructible foliage (trees only). 
         * It can be used in conjunction with the runtime colliders that will have all that data attached to them.
         * 
         * NOTE: This is the slowest of the functions. Always prefer the other two that are faster.
         * 
         * @param guid
         *          Guid of the foliage that we are going to delete
         */
        public void RemoveFoliageInstance(System.Guid guid)
        {
            m_Painter.RemoveFoliageInstanceRuntime(guid);
        }

        /**
         * Removes a single foliage instance from the system, based on the type hash and guid. It is very usefull for destructible foliage (trees only). 
         * It can be used in conjunction with the runtime colliders that will have all that data attached to them.
         * 
         * @param typeHash
         *          Foliage type hash that will be used for adding the foliage. Must exist in order to take effect.
         * @param guid
         *          Guid of the foliage that we are going to delete
         */
        public void RemoveFoliageInstance(int typeHash, System.Guid guid)
        {
            m_Painter.RemoveFoliageInstanceRuntime(typeHash, guid);
        }

        /**
         * Just as the above variant, just that it's a lot faster since the cell can be automatically
         * computed from the position, and we don't have to look through the whole types
         */
        public void RemoveFoliageInstance(int typeHash, System.Guid guid, Vector3 position)
        {
            m_Painter.RemoveFoliageInstanceRuntime(typeHash, guid, position);
        }

        /**
         * Adds a new instance to the runtime data. Adding a foliage instance requires a 'typeHash'. One can be
         * retrieved either by 'AddFoliageType' at runtime, or by querying the type hash from the existing list
         * of added types at edit time, using 'GetFoliageTypes'.
         * 
         * @param typeHash
         *          Foliage type hash that will be used for adding the foliage. Must exist in order to take effect
         * @param instance
         *          Foliage data. All the data this has to have set is the position, rotation and scale, the other is auto-set
         */
        public void AddFoliageInstance(int typeHash, FoliageInstance instance)
        {
            m_Painter.AddFoliageInstanceRuntime(typeHash, instance);
        }

        // NOT SUPPORTED:

        /**
         * Removed the foliage type from the runtime data. Will force the painter NOT to save
         * the grass to disk, since we're at runtime and not at edit mode.
         * 
         * @param typeHash
         *          Type hash of the foliage to remove
         */
        private void RemoveFoliageType(int typeHash)
        {
            throw new System.NotImplementedException();
        }

        /**
         * Add a new foliage type to the runtime data. After it has been added it can be used for
         * painting grass around. 
         * 
         * NOTE: Make sure that it's a normal prefab or a SpeedTree prefab! Or of it's generated at runtime make 
         * absolutely sure that it will not go away with some scene unloading or that it will get eventualy nulled.
         * 
         * @param builder
         *          Builder that contains the data that will populate the type
         * @return The foliage type hash that can be later used to remove the foliage type and add/remove foliage instances
         */
        private int AddFoliageType(FoliageTypeBuilder builder)
        {
            throw new System.NotImplementedException();
        }
        
        /**
         * Removes all the foliage instances of the specified type within a radius of the specified position.
         * 
         * @param typeHash
         *          Foliage type hash that will be used for adding the foliage. Must exist in order to take effect.
         * @param position
         *          Position around which we are going to delete
         * @param radius
         *          Radius around the position that is going to be erased
         */
        private void RemoveFoliageInstances(int typeHash, Vector3 position, float radius = 0.3f)
        {
            throw new System.NotImplementedException();
        }

        /**
         * TODO: See if there's a request for such functionality
         * Should allow for origin rebasing and rebuilding of the whole grass hierarchy. 
         */
        private void WorldOriginRebase(Vector3 offset)
        { Debug.Assert(false, "Ask the developer for details!"); }

        public FoliagePainterRuntime(FoliagePainter painter)
        {
            m_Painter = painter;
        }

        private FoliagePainter m_Painter;
    }
}
