using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    [ExecuteInEditMode]
    public class FoliageTerrainListener : MonoBehaviour
    {
        public FoliagePainter m_FoliagePainter;
        
        /**
         * TODO: Listen to all the changes and stick the grass to the terrain again based on the label. Not implemented at the
         * moment due to possibly high recomputation cost
         */
        void OnTerrainChanged(TerrainChangedFlags flags)
        {
            if ((flags & TerrainChangedFlags.Heightmap) != 0)
            { }

            if ((flags & TerrainChangedFlags.DelayedHeightmapUpdate) != 0)
            { }            
        }
    }
}
