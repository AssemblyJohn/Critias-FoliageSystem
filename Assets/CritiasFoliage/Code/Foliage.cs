/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    public class FoliageLog
    {
#if DEBUG_MODE_FOLIAGE        
        private static int DEBUG_ERROR = 0;
        private static int DEBUG_WARNING = 1;
        private static int DEBUG_DEBUG = 2;
        private static int DEBUG_INFO = 3;

        // Print all until error
        private static int DEBUG_LEVEL = DEBUG_INFO;
#endif

        [System.Diagnostics.Conditional("DEBUG_MODE_FOLIAGE")]
        public static void i(string info)
        {
#if DEBUG_MODE_FOLIAGE
            if (DEBUG_LEVEL >= DEBUG_INFO)
                Debug.Log(info);
#endif
        }

        [System.Diagnostics.Conditional("DEBUG_MODE_FOLIAGE")]
        public static void d(string debug)
        {
#if DEBUG_MODE_FOLIAGE
            if (DEBUG_LEVEL >= DEBUG_DEBUG)
                Debug.Log(debug);
#endif
        }

        [System.Diagnostics.Conditional("DEBUG_MODE_FOLIAGE")]
        public static void w(string warning)
        {
#if DEBUG_MODE_FOLIAGE
            if (DEBUG_LEVEL >= DEBUG_WARNING)
                Debug.LogWarning(warning);
#endif
        }

        [System.Diagnostics.Conditional("DEBUG_MODE_FOLIAGE")]
        public static void e(string error)
        {
#if DEBUG_MODE_FOLIAGE
            if (DEBUG_LEVEL >= DEBUG_ERROR)
                Debug.LogError(error);
#endif
        }

        [System.Diagnostics.Conditional("DEBUG_MODE_FOLIAGE")]
        public static void SecureLog(string secure)
        {
#if DEBUG_MODE_FOLIAGE
            Debug.Log("[SECURE LOG]: " + secure);
#endif
        }

        [System.Diagnostics.Conditional("DEBUG_MODE_FOLIAGE")]
        public static void Assert(bool value, string message = null)
        {
#if DEBUG_MODE_FOLIAGE
            Debug.Assert(value, message);
#endif
        }
    }

    public class FoliageGlobals
    {
        public static void Config()
        {
            Debug.LogWarning("Remove this config if you don't  want any foliage logs and delete the 'DEBUG_MODE_FOLIAGE' define from the build settings or set 'DEBUG_LEVEL' to 0!");

#if UNITY_EDITOR
            string buildSettings = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
            
            if (!buildSettings.Contains("DEBUG_MODE_FOLIAGE"))
            {
                UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup, buildSettings + ";DEBUG_MODE_FOLIAGE");
            }

            if(SystemInfo.supportsInstancing == false)
            {
                Debug.LogError("Instancing is not supported on this platform! The system will not work!");
            }
#endif
        }

        public static float ClampDistance(EFoliageType type, float maxViewDistance)
        {
            switch (type)
            {
                case EFoliageType.OTHER_GRASS:
                case EFoliageType.SPEEDTREE_GRASS:
                    return Mathf.Clamp(maxViewDistance, 0, FOLIAGE_MAX_GRASS_DISTANCE);
                case EFoliageType.OTHER_TREE:
                case EFoliageType.SPEEDTREE_TREE:
                case EFoliageType.SPEEDTREE_TREE_BILLBOARD:
                    return Mathf.Clamp(maxViewDistance, 0, FOLIAGE_MAX_TREE_DISTANCE);
                default:
                    FoliageLog.Assert(false, "Wrong type!");
                    return Mathf.Clamp(maxViewDistance, 0, FOLIAGE_MAX_GRASS_DISTANCE);
            }
        }

        public static float GetMaxDistance(EFoliageType type)
        {
            switch (type)
            {
                case EFoliageType.OTHER_GRASS:
                case EFoliageType.SPEEDTREE_GRASS:
                    return FOLIAGE_MAX_GRASS_DISTANCE;
                case EFoliageType.OTHER_TREE:
                case EFoliageType.SPEEDTREE_TREE:
                case EFoliageType.SPEEDTREE_TREE_BILLBOARD:
                    return FOLIAGE_MAX_TREE_DISTANCE;
                default:
                    FoliageLog.Assert(false, "Wrong type!");
                    return FOLIAGE_MAX_GRASS_DISTANCE;
            }
        }

        /** Delay for painting foliage */
        public const float EDITOR_DELAY_PAINT_FOLIAGE = 0.02f;
        /** Delay for refreshing editor foliage */
        public const float EDITOR_DELAY_REQUEST_UPDATE = 0.5f;


        /** Name of the file that we'll save to disk */
        public const string DISK_FILENAME = "FoliageData";
        /** File data identifier, will never change */
        public const ulong DISK_IDENTIFIER = 0x43524954464F4C49;
        /** Disk file version, used for backward compatibility grass loading in case we need */
        public const int DISK_VERSION = 1;

        /**
         * Modify this only if you want the bigger cells to be a different size than 100m. Will require you
         * to manually clear the existing foliage data. It will result in foliage data loss.
         * 
         * 1. Larger cells will result in fewer iterations of the renderer and larger batches of grass and worst visibility/distance culling
         * 2. Smaller cells will result in more iterations of the renderer and smaller batches of grass with best visibility/sistance culling
         * 
         * We'll have a default hard-coded cell size of 100m. Chosen the value by comparing it to some AAA studios that had large terrains(10x10km) and
         * had 100x100m cells.
         * 
         * The larger cells are used by trees and the smaller cells are used by grass.
         */
        public const float CELL_SIZE = 100.0f;
        public const float CELL_SIZE_HALF = CELL_SIZE * 0.5f;

        /**
         * Modify this only if you want the smaller subdivided cells to be a different size than 20m. Will require
         * you to manually clear the existing foliage data. It wil result in foliage data loss.
         * 
         * Only use in special situations where your game map requires larger/smaller subdivided cells. 
         * 
         * 1. Larger cells will result in fewer iterations of the renderer and larger batches of grass and worst visibility/distance culling
         * 2. Smaller cells will result in more iterations of the renderer and smaller batches of grass with best visibility/sistance culling
         * 
         * In how many parts we're going to subdivide those big cells.
         * 
         * * The larger cells are used by trees and the smaller cells are used by grass.
         */
        public const int CELL_SUBDIVISIONS = 5;

        /** Maximum draw distance for grass foliage */
        public const float FOLIAGE_MAX_GRASS_DISTANCE = 100;
        /** Maximum draw distance for tree foliage */
        public const float FOLIAGE_MAX_TREE_DISTANCE = 1000;

        /** Objects that were hand-painted into the system */
        public const string LABEL_PAINTED = "Hand Painted";

        /** Label added at the beginning of anything painted or extracted from a terrain */
        public const string LABEL_TERRAIN_EXTRACTED = "[TERRAIN]";
        public const string LABEL_TERRAIN_DETAILS_EXTRACTED = "[TERRAIN DETAILS]";
        public const string LABEL_TERRAIN_HAND_PAINTED = "[TERRAIN HAND PAINTED]";

        /** Batch size sent to GPU for rendering. We'll have a default of 1000 instances sent per batch. */
        public const int RENDER_BATCH_SIZE = 1000;
        /** We can only have a maximum LOD count of 6 */
        public const int RENDER_MAX_LOD_COUNT = 6;
        /** Maximum count of small cells that we are going to have in our cache for the 'DrawMeshInstancedIndirect' API. Estimated at all the cells around the player plus 5 foliage types */
        public const int RENDER_MAX_GPU_INDIRECT_BATCH_COUNT = CELL_SUBDIVISIONS * CELL_SUBDIVISIONS * 5 * 10;
        /** How many buffers we'll evict from the cache */
        public const int RENDER_MAX_GPU_INDIRECT_EVICTION_COUNT = CELL_SUBDIVISIONS * CELL_SUBDIVISIONS * 5;

        #region DO NO MODIFY! - Don't tamper with these values, you don't need to change them even if you change the values above!
        //  Cell sizes
        public static readonly Vector3 CELL_SIZE3 = new Vector3(CELL_SIZE, CELL_SIZE, CELL_SIZE);
        public static readonly Vector3 CELL_SIZE3_HALF = new Vector3(CELL_SIZE_HALF, CELL_SIZE_HALF, CELL_SIZE_HALF);

        // Subdivided cell size, no need for any tampering here
        public const float CELL_SUBDIVIDED_SIZE = CELL_SIZE / CELL_SUBDIVISIONS;
        public const float CELL_SUBDIVIDED_SIZE_HALF = CELL_SUBDIVIDED_SIZE * 0.5f;

        public static readonly Vector3 CELL_SUBDIVIDED_SIZE3 = new Vector3(CELL_SUBDIVIDED_SIZE, CELL_SUBDIVIDED_SIZE, CELL_SUBDIVIDED_SIZE);
        public static readonly Vector3 CELL_SUBDIVIDED_SIZE3_HALF = new Vector3(CELL_SUBDIVIDED_SIZE_HALF, CELL_SUBDIVIDED_SIZE_HALF, CELL_SUBDIVIDED_SIZE_HALF);
        #endregion
    }
}
