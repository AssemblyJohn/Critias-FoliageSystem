/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace CritiasFoliage
{
    public struct FoliageDetailExtracterMapping
    {
        public int m_DetailLayer;
        public int m_FoliageTypeHash;

        // 0..1 Range
        public float m_ExtractedDensity;
    }

    public class FoliageExtracterEditor
    {        
        
        /**
         * Extracts the given detail layers from the terrains.
         * 
         * @param terrains
         *          The terrains from which we extract the data from. All must have the exact same terrain detail layers
         * @param mapping
         *          The mapping of the items that we want to extract. The key is the terrain detail layer that we want to extract from and the value is the foliage type hash
         *          that we are going to instantiate for that detail type's layer
         */
        public static void ExtractDetailsFromTerrains(FoliagePainter painter, IEnumerable<Terrain> terrains, List<FoliageDetailExtracterMapping> mappings, bool disable, bool delete)
        {
            foreach(Terrain t in terrains)
            {
                ExtractDetailsFromTerrain(painter, t, mappings, disable, delete);
            }            
        }

        public static void ExtractFoliage(FoliagePainter painter, IEnumerable<GameObject> objects, bool autoExtract, bool disable, bool delete)
        {
            foreach (GameObject obj in objects)
            {
                if (obj.GetComponent<Terrain>() != null)
                {
                    ExtractFromTerrain(painter, painter.GetEditTime, obj.GetComponent<Terrain>(), autoExtract, disable, delete);
                }
                else
                {
                    ExtractFromRootObject(painter.GetEditTime, obj, disable, delete);
                }
            }

            // Re-generate the billboards
            painter.GetEditTime.GenerateBillboards();
        }

        private static void ExtractDetailsFromTerrain(FoliagePainter painter, Terrain terrain, List<FoliageDetailExtracterMapping> mappings, bool disable, bool delete)
        {
            string label = FoliageGlobals.LABEL_TERRAIN_DETAILS_EXTRACTED + terrain.name;

            FoliagePainterEditTime edit = painter.GetEditTime;

            int detailMapSizeW = terrain.terrainData.detailWidth;
            int detailMapSizeH = terrain.terrainData.detailHeight;

            float patchSizeW = terrain.terrainData.size.x / detailMapSizeW;
            float patchSizeH = terrain.terrainData.size.z / detailMapSizeH;

            int extracted = 0;

            // Extract the types for all the mapping
            for (int m = 0; m < mappings.Count; m++)
            {
                int layer = mappings[m].m_DetailLayer;
                int mapping = mappings[m].m_FoliageTypeHash;
                float density = mappings[m].m_ExtractedDensity;

                FoliageLog.Assert(painter.HasFoliageType(mapping), "Must have foliage hash!!");

                // Get the foliage type
                FoliageType type = painter.GetFoliageTypeByHash(mapping);
                DetailPrototype proto = terrain.terrainData.detailPrototypes[layer];

                // If we should align to the surface
                bool followTerrainNormal = type.m_PaintInfo.m_SurfaceAlign;
                Vector2 alignPercentage = type.m_PaintInfo.m_SurfaceAlignInfluence;

                // Get the terrain data
                int[,] data = terrain.terrainData.GetDetailLayer(0, 0, detailMapSizeW, detailMapSizeH, layer);

                // Iterate data
                for (int i = 0; i < detailMapSizeH; i++)
                {
                    for (int j = 0; j < detailMapSizeW; j++)
                    {
                        // j,i not i,j
                        int count = data[j, i];

                        if (count > 0)
                        {
                            // Minimum 1 never 0
                            count = Mathf.Clamp(Mathf.CeilToInt(count * density), 1, count + 1);

                            // Map from local space cell space to local terrain space
                            Vector2 cellMin = new Vector2(patchSizeW * i, patchSizeH * j);
                            Vector2 cellMax = cellMin + new Vector2(patchSizeW, patchSizeH);

                            for (int d = 0; d < count; d++)
                            {
                                Vector3 randomInCell;
                                randomInCell.x = Random.Range(cellMin.x, cellMax.x);
                                randomInCell.z = Random.Range(cellMin.y, cellMax.y);
                                randomInCell.y = 0;

                                randomInCell = FoliageTerrainUtilities.TerrainLocalToTerrainNormalizedPos(randomInCell, terrain);
                                float y = FoliageTerrainUtilities.TerrainHeight(randomInCell, terrain);

                                // Build the rotation
                                Quaternion rotation = Quaternion.identity;

                                if(followTerrainNormal)
                                {
                                    Quaternion slopeOrientation = Quaternion.LookRotation(FoliageTerrainUtilities.TerrainNormal(randomInCell, terrain)) * Quaternion.Euler(90, 0, 0);

                                    // How much we orient towards the slope
                                    rotation = Quaternion.Slerp(rotation, slopeOrientation,
                                        Random.Range(alignPercentage.x, alignPercentage.y));                                    
                                }
                                
                                // Rotate around the Y axis
                                rotation *= Quaternion.Euler(0, Random.Range(0, 360), 0);

                                // Random in cell in world position
                                randomInCell = FoliageTerrainUtilities.TerrainNormalizedToWorldPos(randomInCell, terrain);
                                randomInCell.y = y;

                                Vector3 scale;
                                float x = Random.Range(proto.minWidth, proto.maxWidth);
                                scale.x = x;
                                scale.z = x;
                                scale.y = Random.Range(proto.minHeight, proto.maxHeight);

                                // Construct a foliage instance based on the foliage type data
                                FoliageInstance instance = new FoliageInstance();

                                // Build the foliage data based on the type
                                instance.m_Position = randomInCell;
                                instance.m_Rotation = rotation;
                                instance.m_Scale = scale;

                                // Instantiate at a random pos in that cell
                                edit.AddFoliageInstance(mapping, instance, label);
                                extracted++;
                            }
                        }

                        // If we should delete
                        if (delete)
                        {
                            data[j, i] = 0;
                        }
                    }
                } // End detail array iteration

                // If we deleted set the new detail layer data
                if (delete)
                {
                    terrain.terrainData.SetDetailLayer(0, 0, layer, data);
                }
            } // End types iteration

            // If we should disable the trees and foliage draw
            if (disable)
            {
                terrain.drawTreesAndFoliage = false;
            }

            // If we deleted anything we need to save
            if (delete)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            }

            FoliageLog.i("Extracted details: " + extracted + " from: " + terrain.name);
        }

        private static void ExtractFromTerrain(FoliagePainter painterRaw, FoliagePainterEditTime painter, Terrain terrain, bool autoExtract, bool disable, bool delete)
        {
            string label = FoliageGlobals.LABEL_TERRAIN_EXTRACTED + terrain.name;

            // Ensure that we have all the required foliage types            
            List<TreeInstance> terrainTreeInstances = new List<TreeInstance>(terrain.terrainData.treeInstances);
            TreePrototype[] terrainTreePrototypes = terrain.terrainData.treePrototypes;

            // Attempt to build the prefab's that we don't have
            if (autoExtract)
            {
                AutoExtractTypes(painter, terrain);
            }

            int extracted = 0;

            for (int i = terrainTreeInstances.Count - 1; i >= 0; i--)
            {
                GameObject proto = terrainTreePrototypes[terrainTreeInstances[i].prototypeIndex].prefab;

                if (painter.HasFoliageType(proto) == false)
                    continue;

                int hash = painter.GetFoliageTypeHash(proto);
                FoliageType type = painterRaw.GetFoliageTypeByHash(hash);

                FoliageInstance instance = new FoliageInstance();

                // Populate the data

                // Get the world data
                float YOffset = Random.Range(type.m_PaintInfo.m_YOffset.x, type.m_PaintInfo.m_YOffset.y);
                Vector3 worldPosition = FoliageTerrainUtilities.TerrainNormalizedToWorldPos(terrainTreeInstances[i].position, terrain) + new Vector3(0, YOffset, 0); // YOffset too
                Vector3 worldScale = new Vector3(terrainTreeInstances[i].widthScale, terrainTreeInstances[i].heightScale, terrainTreeInstances[i].widthScale);
                Vector3 worldRotation = new Vector3(0, terrainTreeInstances[i].rotation * Mathf.Rad2Deg, 0);

                instance.m_Position = worldPosition;
                instance.m_Scale = worldScale;
                instance.m_Rotation = Quaternion.Euler(worldRotation.x, worldRotation.y, worldRotation.z);

                // Add the foliage instance
                painter.AddFoliageInstance(hash, instance, label);
                extracted++;

                // Delete the instance from the terrain if we have to
                if (delete)
                {
                    terrainTreeInstances.RemoveAt(i);
                }
            }

            if(disable)
            {
                terrain.drawTreesAndFoliage = false;
            }

            // If we should delete then delete the instance
            if(delete)
            {
                terrain.terrainData.treeInstances = terrainTreeInstances.ToArray();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            }

            FoliageLog.i("Extracted objects: " + extracted + " from: " + terrain.name);
        }

        private static void ExtractFromRootObject(FoliagePainterEditTime painter, GameObject root, bool disable, bool delete)
        {
            FoliageLog.Assert(root.transform.parent == null, "Must have root object!");

            List<GameObject> foliageInstances = new List<GameObject>();
            RecursivelyExtract(root, foliageInstances);

            int extracted = 0;

            // Process them and add them
            for (int i = foliageInstances.Count - 1; i >= 0; i--)
            {
                GameObject proto = foliageInstances[i];

                GameObject protoPrefab = PrefabUtility.GetCorrespondingObjectFromSource(proto) as GameObject;

                if (painter.HasFoliageType(protoPrefab) == false)
                    continue;

                int hash = painter.GetFoliageTypeHash(protoPrefab);
                FoliageInstance instance = new FoliageInstance();

                // Populate the data

                // Get the world data
                Vector3 worldPosition = proto.transform.position;
                Vector3 worldScale = proto.transform.localScale;
                Quaternion worldRotation = proto.transform.rotation;
                
                instance.m_Position = worldPosition;
                instance.m_Scale = worldScale;
                instance.m_Rotation = worldRotation;

                // Add the foliage instance
                painter.AddFoliageInstance(hash, instance, root.name);
                extracted++;

                // Auto disable
                if (disable)
                {
                    proto.SetActive(false);
                }

                // Auto delete
                if(delete)
                {
                    GameObject.DestroyImmediate(proto);
                }
            }

            FoliageLog.i("Extracted objects: " + extracted + " from: " + root.name);
        }

        private static void RecursivelyExtract(GameObject extract, List<GameObject> foliageInstances)
        {
            // If it is a prefab instance and we have
            PrefabType type = PrefabUtility.GetPrefabType(extract);            

            if (type == PrefabType.ModelPrefabInstance || type == PrefabType.PrefabInstance)
            {
                foliageInstances.Add(extract);
            }

            // else
            {
                // Extract it's owned entities. Only extract if the object is NOT a prefab, we don't want to extract owned entities from prefabs
                for (int i = 0; i < extract.transform.childCount; i++)
                    RecursivelyExtract(extract.transform.GetChild(i).gameObject, foliageInstances);
            }
        }

        private static void AutoExtractTypes(FoliagePainterEditTime painter, Terrain terrain)
        {
            TreeInstance[] terrainTreeInstances = terrain.terrainData.treeInstances;
            TreePrototype[] terrainTreePrototypes = terrain.terrainData.treePrototypes;

            for (int i = 0; i < terrainTreePrototypes.Length; i++)
            {
                TreePrototype proto = terrainTreePrototypes[i];
                GameObject prefab = proto.prefab;

                // See if we already have it
                if (painter.HasFoliageType(prefab) == false)
                {
                    // Attempt to add it
                    FoliageTypeBuilder builder;
                    bool anyError;

                    FoliageUtilitiesEditor.ConfigurePrefab(prefab, out builder, out anyError);

                    if (anyError == false)
                    {
                        painter.AddFoliageType(builder);
                    }
                    else
                    {
                        FoliageLog.e("Could not add foliage type: " + prefab.name + " due to error. Check the log for more info.");
                    }
                }
            }
        }
    }
}