/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CritiasFoliage
{   
    public class FoliageUtilitiesEditor
    {      
        public static bool CanChangeType(FoliageType type, EFoliageType newType)
        {
            GameObject prefab = type.m_Prefab;
            // PrefabType prefabType = PrefabUtility.GetPrefabType(prefab);
            string path = AssetDatabase.GetAssetPath(prefab);
            
            if(IsSpeedTree(newType))
            {
                // We want to change to a SpeedTree type

                if(path.EndsWith(".spm") == false)
                {
                    FoliageLog.e("Can't change to SpeedTree foliage: " + type.m_Name + " since it's path does not end with '.spm'! It means that it's not a SpeedTree!");
                    return false;
                }

                LODGroup group = prefab.GetComponent<LODGroup>();

                if (group == null)
                {
                    FoliageLog.e("Can't change to SpeedTree foliage: " + type.m_Name + " since it doesn't contain a 'LODGroup' component!");
                    return false;
                }

                if (newType == EFoliageType.SPEEDTREE_TREE_BILLBOARD)
                {
                    bool containsBillboardRenderer = false;

                    LOD[] lods = group.GetLODs();

                    foreach(LOD lod in lods)
                    {
                        if(lod.renderers[0].GetComponent<BillboardRenderer>() != null)
                        {
                            containsBillboardRenderer = true;
                            break;
                        }
                    }

                    if (containsBillboardRenderer == false)
                    {
                        FoliageLog.e("Can't change to SpeedTree with billboard the foliage: " + type.m_Name + " since it doesn't contain a billboard renderer!");
                        return false;
                    }
                }
            }

            // We're cool no error
            return true;
        }

        public static bool IsSpeedTree(EFoliageType type)
        {
            switch (type)
            {
                case EFoliageType.SPEEDTREE_GRASS:
                case EFoliageType.SPEEDTREE_TREE:
                case EFoliageType.SPEEDTREE_TREE_BILLBOARD:
                    return true;
            }


            return false;
        }

        public static void ConfigurePrefab(GameObject foliage, out FoliageTypeBuilder outBuilder, out bool outAnyError)
        {
            PrefabType type = PrefabUtility.GetPrefabType(foliage);

            if (type != PrefabType.ModelPrefab && type != PrefabType.Prefab)
            {
                FoliageLog.e("The prefab type of: " + foliage.name + " is: '" + type + "'! Must be a 'ModelPrefab' or a 'Prefab'!");
                outAnyError = true;
            }

            string path = AssetDatabase.GetAssetPath(foliage);
            FoliageLog.i("Path of added foliage type: " + path);

            FoliageTypeBuilder builder = new FoliageTypeBuilder();

            // Build all it's required data
            builder.m_PaintInfo = new FoliageTypePaintInfo();
            builder.m_RenderInfo = new FoliageTypeRenderInfo();

            bool anyWeirdGrassError = false;
            
            // Attempt to auto-configure the foliage for the easiest possible user interaction
            if (path.EndsWith(".spm"))
            {
                // We have a SpeedTree
                LODGroup lodGroup = foliage.GetComponent<LODGroup>();

                if (lodGroup != null)
                {
                    if (lodGroup.lodCount == 1)
                    {
                        FoliageLog.i("Detected a SpeedTree grass, since it has 1 LOD!");
                        builder.m_Type = EFoliageType.SPEEDTREE_GRASS;
                        builder.m_EnableCollision = false;

                        if (foliage.GetComponentInChildren<MeshRenderer>() == null)
                        {
                            anyWeirdGrassError = true;
                            FoliageLog.e("SpeedTree grass without any MeshRenderer detected!");
                        }                        
                    }
                    else
                    {
                        FoliageLog.i("Detected a SpeedTree tree, since it has 1+ LODS!");
                        builder.m_Type = EFoliageType.SPEEDTREE_TREE;
                        builder.m_EnableCollision = foliage.GetComponentInChildren<Collider>() != null ? true : false;

                        if (foliage.GetComponentInChildren<BillboardRenderer>() != null)
                        {
                            builder.m_Type = EFoliageType.SPEEDTREE_TREE_BILLBOARD;
                        }

                        if (foliage.GetComponentInChildren<MeshRenderer>() == null)
                        {
                            anyWeirdGrassError = true;
                            FoliageLog.e("SpeedTree tree without any MeshRenderer detected!");
                        }
                    }
                    
                    // Set the bounds
                    builder.m_Bounds = lodGroup.GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.bounds;
                }
                else
                {
                    FoliageLog.e("Weird, we have a SpeedTree without a lod group. Anything wrong here? Please fix.");

                    anyWeirdGrassError = true;
                    builder.m_Bounds = foliage.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                }
            }
            else
            {
                LODGroup lodGroup = foliage.GetComponent<LODGroup>();

                if (lodGroup != null && lodGroup.lodCount > 1)
                {
                    FoliageLog.i("Detected an object tree!");
                    builder.m_Type = EFoliageType.OTHER_TREE;
                    builder.m_EnableCollision = foliage.GetComponentInChildren<Collider>() != null ? true : false;

                    if (foliage.GetComponentInChildren<MeshRenderer>() == null)
                    {
                        anyWeirdGrassError = true;
                        FoliageLog.e("Object tree without any MeshRenderer detected!");
                    }

                    // Set the bounds
                    builder.m_Bounds = lodGroup.GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.bounds;
                }
                else
                {
                    FoliageLog.i("Detected an object grass!");
                    builder.m_Type = EFoliageType.OTHER_GRASS;
                    builder.m_EnableCollision = false;

                    if (foliage.GetComponentInChildren<MeshRenderer>() == null)
                    {
                        anyWeirdGrassError = true;
                        FoliageLog.e("Object grass without any LOD group or MeshRenderer detected!");
                    }

                    // Set the bounds
                    builder.m_Bounds = foliage.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                }
            }

            LODGroup group = foliage.GetComponent<LODGroup>();

            if(group != null && group.lodCount > 0)
            {                
                LOD[] lods = group.GetLODs();

                for(int i = 0; i < lods.Length; i++)
                {
                    Renderer[] rends = lods[i].renderers;

                    if(rends == null || rends.Length != 1)
                    {
                        anyWeirdGrassError = true;
                        FoliageLog.e("Detected object with a lod without any renderers on it or with more than one renderer attached to it!");
                    }
                }
            }

            if (anyWeirdGrassError)
            {
                EditorUtility.DisplayDialog("Error", "Found error for foliage: " + foliage.name + "! Could not add to system! Check the log for more info!", "Ok");
                FoliageLog.e("Found error for foliage: " + foliage.name + "! Could not add to system!");
            }
            else
            {
                // Set the prefab
                builder.m_Prefab = foliage;
                builder.m_PaintEnabled = true;

                switch (builder.m_Type)
                {
                    case EFoliageType.OTHER_GRASS:
                    case EFoliageType.SPEEDTREE_GRASS:
                        builder.m_RenderInfo.m_CastShadow = false;
                        builder.m_RenderInfo.m_MaxDistance = 30;
                        break;
                    case EFoliageType.OTHER_TREE:
                    case EFoliageType.SPEEDTREE_TREE:
                    case EFoliageType.SPEEDTREE_TREE_BILLBOARD:
                        builder.m_RenderInfo.m_CastShadow = true;
                        builder.m_RenderInfo.m_MaxDistance = 100;
                        break;
                }
            }

            switch (builder.m_Type)
            {
                case EFoliageType.OTHER_GRASS:
                case EFoliageType.SPEEDTREE_GRASS:
                    builder.m_PaintInfo.m_SurfaceAlign = true;
                    break;
                case EFoliageType.OTHER_TREE:
                case EFoliageType.SPEEDTREE_TREE_BILLBOARD:
                case EFoliageType.SPEEDTREE_TREE:
                    builder.m_PaintInfo.m_SurfaceAlign = false;
                    break;
            }

            if(anyWeirdGrassError == false)
            {
                if(builder.m_Type == EFoliageType.SPEEDTREE_GRASS || builder.m_Type == EFoliageType.SPEEDTREE_TREE || builder.m_Type == EFoliageType.SPEEDTREE_TREE_BILLBOARD)
                {
                    // Get a hue
                    builder.m_RenderInfo.m_Hue = foliage.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetColor("_HueVariation");
                    builder.m_RenderInfo.m_Color = foliage.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetColor("_Color");
                    

                    if (builder.m_RenderInfo.m_Hue == new Color(0, 0, 0, 0))
                        builder.m_RenderInfo.m_Hue = Color.white;

                    if (builder.m_RenderInfo.m_Color == new Color(0, 0, 0, 0))
                        builder.m_RenderInfo.m_Color = Color.white;
                }
                else
                {
                    builder.m_RenderInfo.m_Hue = Color.white;
                    builder.m_RenderInfo.m_Color = Color.white;
                }
            }

            outAnyError = anyWeirdGrassError;
            outBuilder = builder;
        }
    }
}
