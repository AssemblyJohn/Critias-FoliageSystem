/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    public class FoliageTerrainUtilities
    {
        /**
         * Converts from the 0..1 range to the terrain's local position. For example, on a 
         * 1000x1000 terrain 0 is 0, 0.5 is 500, 1 is 1000. Does not apply the terrain's world transform.
         */
        public static Vector3 TerrainNormalizedToTerrainLocalPos(Vector3 terrainNormalizedLocalPos, Terrain terrain)
        {
            Vector3 size = terrain.terrainData.size;
            Vector3 worldPos = new Vector3(Mathf.Lerp(0.0f, size.x, terrainNormalizedLocalPos.x),
                                           Mathf.Lerp(0.0f, size.y, terrainNormalizedLocalPos.y),
                                           Mathf.Lerp(0.0f, size.z, terrainNormalizedLocalPos.z));

            return worldPos;
        }

        /**
         * Converts from the 0..1 range to the world possition. As a difference between this and
         * 'TerrainToTerrainPos' this also applies the terrain's world position.
         */
        public static Vector3 TerrainNormalizedToWorldPos(Vector3 terrainNormalizedLocalPos, Terrain terrain)
        {
            Vector3 size = terrain.terrainData.size;
            Vector3 worldPos = new Vector3(Mathf.Lerp(0.0f, size.x, terrainNormalizedLocalPos.x),
                                           Mathf.Lerp(0.0f, size.y, terrainNormalizedLocalPos.y),
                                           Mathf.Lerp(0.0f, size.z, terrainNormalizedLocalPos.z));

            worldPos += terrain.transform.position;

            return worldPos;
        }

        /**
         * Converts from the 0...TerrainData.Size.Max range to the 0..1 range.
         */
        public static Vector3 TerrainLocalToTerrainNormalizedPos(Vector3 terrainLocalPos, Terrain terrain)
        {
            Vector3 normalizedPos = new Vector3(Mathf.InverseLerp(0.0f, terrain.terrainData.size.x, terrainLocalPos.x),
                                                Mathf.InverseLerp(0.0f, terrain.terrainData.size.y, terrainLocalPos.y),
                                                Mathf.InverseLerp(0.0f, terrain.terrainData.size.z, terrainLocalPos.z));

            return normalizedPos;
        }

        /** Transform a world possition to terrain local position */
        public static Vector3 WorldToTerrainNormalizedPos(Vector3 worldPos, Terrain terrain)
        {
            Vector3 terrainLocalPos = terrain.transform.InverseTransformPoint(worldPos);

            Vector3 normalizedPos = new Vector3(Mathf.InverseLerp(0.0f, terrain.terrainData.size.x, terrainLocalPos.x),
                                                Mathf.InverseLerp(0.0f, terrain.terrainData.size.y, terrainLocalPos.y),
                                                Mathf.InverseLerp(0.0f, terrain.terrainData.size.z, terrainLocalPos.z));

            return normalizedPos;
        }

        /** Get a terrain normal at the terrain local position (0..1 range) */
        public static Vector3 TerrainNormal(Vector3 terrainNormalizedPos, Terrain terrain)
        {
            return terrain.terrainData.GetInterpolatedNormal(terrainNormalizedPos.x, terrainNormalizedPos.z);
        }

        /** Get a terrain height at the terrain local position (0..1 range) */
        public static float TerrainHeight(Vector3 terrainNormalizedPos, Terrain terrain)
        {
            return terrain.terrainData.GetInterpolatedHeight(terrainNormalizedPos.x, terrainNormalizedPos.z);
        }
    }
}
