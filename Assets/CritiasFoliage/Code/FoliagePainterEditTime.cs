/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

namespace CritiasFoliage
{

    /**
    * Class that must be be used when edit time operations take place upon the foliage data. It is a lot easyier
    * to use than searching and looking through the 'FoliagePainter' class and try and guess how to use some
    * functionality.
    * 
    * WARNING: Any functionality not directly exposed here is not supported. Use at your own risk!
    */
    public class FoliagePainterEditTime
    {
        /** Adds a new foliage type to the painter */
        public void AddFoliageType(FoliageTypeBuilder foliageType)
        {
            m_Painter.AddFoliageType(foliageType);
        }

        /** If we already have that foliage type */
        public bool HasFoliageType(GameObject foliage)
        {
            return m_Painter.HasFoliageType(foliage);
        }

        /** Get the hash of a foliage type. Do not use without checking if we have that foliage type with 'HasFoliageType'! */
        public int GetFoliageTypeHash(GameObject foliage)
        {
            return m_Painter.GetFoliageTypeHash(foliage);
        }

        /**
         * Add a new foliage instance to the system. For the foliage instance
         * we only need to populate it's world position, rotation and scale
         * there's no need to populate the bounds matrix and GUID.
         * 
         * @param typeHash
         *          Type hash of the foliage that we want to add
         * @param instance
         *          Foliage instance data containing world position, scale and rotation
         * @param label
         *          Optional label in case we want to add it to a different than 'hand painted' label. Usefull when
         *          we want to quickly remove all the foliage with a specified label from the list of foliages
         */
        public void AddFoliageInstance(int typeHash, FoliageInstance instance, string label = FoliageGlobals.LABEL_PAINTED)
        {
            m_Painter.AddFoliageInstance(typeHash, instance, label);
        }

        /** Add a list foliage instances of the specified type. Same as above */
        public void AddFoliageInstances(int typeHash, List<FoliageInstance> instances, string label = FoliageGlobals.LABEL_PAINTED)
        {
            m_Painter.AddFoliageInstances(typeHash, instances, label);
        }

        public void GenerateBillboards()
        {
            m_Painter.GenerateTreeBillboards(true);
        }
        
        public FoliagePainterEditTime(FoliagePainter painter)
        {
            m_Painter = painter;
        }

        private FoliagePainter m_Painter;
    }

}

#endif