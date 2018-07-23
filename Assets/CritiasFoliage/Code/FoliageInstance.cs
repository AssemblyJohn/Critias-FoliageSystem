/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    /**
     * Foliage instance. Will only be used for trees at runtime since on them
     * we will have distance, frustum culling operations per instance rather
     * than per batch like the grass.
     * 
     * We will not have the trees organised in pre-organised batches since they are not
     * as many as the grass.
     */
    public struct FoliageInstance
    {
        // Position in the world
        public Vector3 m_Position;
        // Rotation in the world
        public Quaternion m_Rotation;
        // Scale of the instance
        public Vector3 m_Scale;

        // Local to world matrix
        public Matrix4x4 m_Matrix;

        // Stored world space bounds
        public Bounds m_Bounds;

        // 16 bytes, as much as a Vector4
        public System.Guid m_UniqueId;

        public Matrix4x4 GetWorldTransform()
        {
            return Matrix4x4.TRS(m_Position, m_Rotation, m_Scale);
        }

        public void BuildWorldMatrix()
        {
            m_Matrix = GetWorldTransform();
        }
    }
}