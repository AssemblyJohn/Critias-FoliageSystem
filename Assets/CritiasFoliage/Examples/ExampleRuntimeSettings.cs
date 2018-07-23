using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using CritiasFoliage;

public class ExampleRuntimeSettings : MonoBehaviour
{
    public FoliagePainter m_Painter;

    private void Awake()
    {
        if (m_Painter == null)
            m_Painter = FindObjectOfType<FoliagePainter>();
    }

    private List<FoliageTypeRuntime> m_CachedTypes;
    private FoliagePainterRuntime m_CachedRuntime;

    /**
     * As a general note it is a very bad idea to call this every frame. It is better to cache your values and apply them only
     * when the user is tampering with them.
     * 
     * By using this model you can change any runtime data that you wish.
     */
    void OnGUI()
    {
        if(m_Painter == null)
        {
            Debug.LogError("Null painter, please set!");
            return;
        }

        if (m_CachedTypes == null)
        {
            m_CachedRuntime = m_Painter.GetRuntime;
            m_CachedTypes = m_CachedRuntime.GetFoliageTypes();
        }

        int XOffset = -1;

        for (int i = 0; i < m_CachedTypes.Count; i++)
        {
            FoliageTypeRuntime type = m_CachedTypes[i];

            float posY = 80 * (i % 8);

            if(i % 8 == 0)
                XOffset++;
            
            GUI.Label(new Rect(20 + XOffset * 220, posY, 200, 20), "Name: " + type.m_Name);

            // Distance
            float currentDistance = m_CachedRuntime.GetFoliageTypeMaxDistance(type.m_Hash);
            float max = GUI.HorizontalSlider(new Rect(20 + XOffset * 220, posY + 20, 100, 20), currentDistance, 0,
                type.m_IsGrassType ? FoliageGlobals.FOLIAGE_MAX_GRASS_DISTANCE : FoliageGlobals.FOLIAGE_MAX_TREE_DISTANCE);

            // Shadow
            bool currentShadow = m_CachedRuntime.GetFoliageTypeCastShadow(type.m_Hash);
            bool shadow = GUI.Toggle(new Rect(20 + XOffset * 220, posY + 40, 100, 20), currentShadow, "Shadow");
            
            if (Mathf.Abs(max - currentDistance) > Mathf.Epsilon)
                m_CachedRuntime.SetFoliageTypeMaxDistance(type.m_Hash, max);
            
            if (shadow != currentShadow)
                m_CachedRuntime.SetFoliageTypeCastShadow(type.m_Hash, shadow);
        }
    }
}
