/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{
    /**
     * Foliage types supported by the system.
     * 
     * NOTE: 
     * 
     * Objects that have only 1 LOD or no LOD at all must use 'OTHER_GRASS' or 'SPEEDTREE_GRASS'.
     * Objects that have more than 1 LOD must use 'SPEEDTREE_TREE' 'SPEED_TREE_BILLBOARD' or 'OTHER_TREE'.
     * 
     * The system will attempt to auto-configure the type based on the added types. For prefabs ending in the
     * '.spm' will set the type to 'SPEEDTREE' and based on the LOD group options will set it to grass/tree or
     * tree-billboard.
     * 
     * IMPORTANT NOTES:
     * Objects that have multiple LOD groups do NOT support LOD's that have more than one renderer! The default maximum 
     * number of LODs per object is 6!
     */
    [System.Serializable]
    public enum EFoliageType
    {
        /**
         * Simple SpeedTree without billboard
         * 
         * It can use a LOD groups with multiple LOD's and that will not use the billboard.
         */
        SPEEDTREE_TREE,

        /**
         * SpeedTree with billboard
         * 
         * It can use a LOD groups with multiple LOD's and that will use the billboard. Make sure that it has a billboard if you
         * set it to this type.
         */
        SPEEDTREE_TREE_BILLBOARD,

        /**
         * SpeedTree grass with 1 LOD and 1 material
         */
        SPEEDTREE_GRASS,

        /**
         * Other type that uses LOD groups
         */
        OTHER_TREE,

        /**
         * Other type that uses 1 LOD and 1 material or no LOD and 1 material 
         */
        OTHER_GRASS
    }

    /**
     * Mode of rendering for this foliage type.
     */
     [System.Serializable]
    public enum EFoliageRenderType
    {
        /**
         * If we should draw the type using the standard 'DrawMeshInstanced' API
         */
        INSTANCED,

        /**
         * If we should draw the type using the 'DrawMeshInstancedIndirect' API. Only
         * available for grass, and is the recommended way of drawing grass.
         */
        INSTANCED_INDIRECT
    }

    [System.Serializable]
    public class FoliageTypeLODTree
    {
        public float m_EndDistance;

        public Material[] m_Materials;
        public Mesh m_Mesh;
    }

    [System.Serializable]
    public class FoliageTypeLODGrass
    {
        public Material m_Material;
        public Mesh m_Mesh;
    }

    [System.Serializable]
    public class FoliageTypeSpeedTreeData
    {
        public GameObject m_SpeedTreeWindObject;
        public MeshRenderer m_SpeedTreeWindObjectMesh;

        // Billboard data if we have a SpeedTree billboard type
        public Vector4 m_Size; // Width, height, bottom, 1. To be used by the tree system for each instance
        public Vector4[] m_VertBillboardU;
        public Vector4[] m_VertBillboardV;
        public BillboardRenderer m_BillboardRenderer;
        public Material m_BillboardMaterial; // If this is not a billboard type, then this can be null
    }

    [System.Serializable]
    public class FoliageTypeRuntimeData
    {
        // Grass set differently for trees and for grass
        public FoliageTypeLODTree[] m_LODDataTree;
        public FoliageTypeLODGrass m_LODDataGrass;

        public MaterialPropertyBlock m_TypeMPB;

        // BEGIN Speedtree only data
        public FoliageTypeSpeedTreeData m_SpeedTreeData; // Will be null if it is not a SpeedTree
        // END Speedtree only data

        public void CopyBlock()
        {
#if UNITY_EDITOR
            Debug.Assert(m_SpeedTreeData != null);
            Debug.Assert(m_SpeedTreeData.m_SpeedTreeWindObject != null);
            Debug.Assert(m_SpeedTreeData.m_SpeedTreeWindObjectMesh != null);
            Debug.Assert(m_TypeMPB != null);
#endif

            // Copy wind block data per type
            m_SpeedTreeData.m_SpeedTreeWindObjectMesh.GetPropertyBlock(m_TypeMPB);
        }
    }

    [System.Serializable]
    public struct FoliageTypeRenderInfo
    {
        // Maximum distance this guy is going to be drawn at
        public float m_MaxDistance;

        // If it casts shadows. Defaults to false for grass
        public bool m_CastShadow;

        // If we have a custom hue for SpeedTrees, that can be changed at runtime to create color variations
        public Color m_Hue;
        // If we have a custom color for SpeedTrees, that can be changed at runtime to create color variations
        public Color m_Color;
    }

    /**
     * Foliage type builder used for adding foliage types to the system. The only method of adding
     * foliage types to the system.
     */
    public struct FoliageTypeBuilder
    {
        public EFoliageType m_Type;
        public GameObject m_Prefab;

        public Bounds m_Bounds;

        public FoliageTypeRenderInfo m_RenderInfo;

        public bool m_EnableCollision;
        public bool m_PaintEnabled;

        // Paint info
        public FoliageTypePaintInfo m_PaintInfo;
    }

    [System.Serializable]
    public class FoliageTypePaintInfo
    {
        // Per-type surface align
        public bool m_SurfaceAlign = false;
        public Vector2 m_SurfaceAlignInfluence = new Vector2(1, 1);

        // Per-type surface offset
        /** How deep or far aweay from the ground we want to push the instance */
        public Vector2 m_YOffset = new Vector2(0, 0);

        // If painting is enabled for this type
        public bool m_PaintEnabled;
    }

    /**
     * Foliage type that should hold all the data related to painting and runtime rendering of this foliage type.
     * 
     * WARNING: Must not directly make the changes to this class, but one must make the changes to the foliage type through
     * the 'FoliagePainter' only!!! Bad things will happen if you don't.
     */
    [System.Serializable]
    public class FoliageType
    {
        // BEGIN Auto-generated data (don't touch)

        // Foliage type hash. Ensured uniqueness across all foliage types
        public int m_Hash;
        // Name in case the prefab goes away somehow by being nullified
        public string m_Name;
        // Foliage runtime data
        public FoliageTypeRuntimeData m_RuntimeData;
        // Foliage type bounds
        public Bounds m_Bounds;

        // END Audo-generated data

        // Type data
        [SerializeField] private EFoliageType m_Type;
        [SerializeField] private bool m_IsGrassType = false;
        [SerializeField] private bool m_IsSpeedTreeType = false;

        public EFoliageType Type
        {
            get { return m_Type; }
            set
            {
                m_Type = value;

                switch(m_Type)
                {
                    case EFoliageType.OTHER_GRASS:
                    case EFoliageType.SPEEDTREE_GRASS:
                        m_IsGrassType = true;
                        break;
                    case EFoliageType.OTHER_TREE:
                    case EFoliageType.SPEEDTREE_TREE:
                    case EFoliageType.SPEEDTREE_TREE_BILLBOARD:
                        m_IsGrassType = false;
                        break;
                    default:
                        FoliageLog.Assert(false, "Wrong value: " + m_Type);
                        break;
                }

                if (m_Type == EFoliageType.SPEEDTREE_GRASS || m_Type == EFoliageType.SPEEDTREE_TREE || m_Type == EFoliageType.SPEEDTREE_TREE_BILLBOARD)
                    m_IsSpeedTreeType = true;
                else
                    m_IsSpeedTreeType = false;

                // Also check that we are compatible with the render type
                if(m_RenderType == EFoliageRenderType.INSTANCED_INDIRECT && m_IsGrassType == false)
                {
                    FoliageLog.w("Changed render type from 'INSTANCED_INDIRECT' to 'INSTANCED' since we changed a type!");
                    RenderType = EFoliageRenderType.INSTANCED;
                }
            }
        }

        [SerializeField] private EFoliageRenderType m_RenderType = EFoliageRenderType.INSTANCED;
        [SerializeField] private bool m_RenderIndirect = false;

        public EFoliageRenderType RenderType
        {
            get { return m_RenderType; }

            set
            {
#if UNITY_EDITOR
                if (value == EFoliageRenderType.INSTANCED_INDIRECT)
                {
                    // Do the checks
                    if (m_Type != EFoliageType.OTHER_GRASS && m_Type != EFoliageType.SPEEDTREE_GRASS)
                    {
                        FoliageLog.Assert(false, "Cannot set the 'INSTANCED_INDIRECT' mode to non-grass types!");
                    }
                }
#endif

                m_RenderType = value;
                m_RenderIndirect = (value == EFoliageRenderType.INSTANCED_INDIRECT);
            }
        }

        public bool RenderIndirect
        {
            get { return m_RenderIndirect; }
        }

        public bool IsGrassType
        {
            get { return m_IsGrassType; }
        }

        public bool IsSpeedTreeType
        {
            get { return m_IsSpeedTreeType; }
        }

        // Prefab linked to this foliage type
        public GameObject m_Prefab;       

        // Render info for the foliage type
        public FoliageTypeRenderInfo m_RenderInfo;

        // Physics properties, disabled for grass
        public bool m_EnableCollision;

        // If we should enable the grass bending based on a transform
        public bool m_EnableBend = false;
        public float m_BendDistance = 1;
        public float m_BendPower = 2;

        // The painting data
#if UNITY_EDITOR
        public FoliageTypePaintInfo m_PaintInfo;        
#endif

        private bool m_RuntimeDataCreated = false;

        public bool IsRuntimeInitialized
        {
            get { return m_RuntimeDataCreated; }
            set
            {
                FoliageLog.Assert(m_RuntimeDataCreated == false, "Must not create foliage runtime data twice!");
                m_RuntimeDataCreated = true;                
            }
        }

        /** Called in case that we need to set the volatile data for a billboard batch material or something like that */
        public void UpdateValues()
        {
            // Update all billboard distances
            if (m_IsSpeedTreeType)
            {
                FoliageTypeSpeedTreeData speedTreeData = m_RuntimeData.m_SpeedTreeData;
                Material billboardMaterialBatch = m_RuntimeData.m_SpeedTreeData.m_BillboardMaterial;

                if (billboardMaterialBatch != null)
                {
                    billboardMaterialBatch.SetFloat("CRITIAS_MaxFoliageTypeDistance", m_RenderInfo.m_MaxDistance);
                    billboardMaterialBatch.SetFloat("CRITIAS_MaxFoliageTypeDistanceSqr", m_RenderInfo.m_MaxDistance * m_RenderInfo.m_MaxDistance);

                    billboardMaterialBatch.SetVectorArray("_UVVert_U", speedTreeData.m_VertBillboardU);
                    billboardMaterialBatch.SetVectorArray("_UVVert_V", speedTreeData.m_VertBillboardV);

                    billboardMaterialBatch.SetVector("_UVHorz_U", speedTreeData.m_VertBillboardU[0]);
                    billboardMaterialBatch.SetVector("_UVHorz_V", speedTreeData.m_VertBillboardV[0]);

                    billboardMaterialBatch.SetColor("_HueVariation", m_RenderInfo.m_Hue);
                    billboardMaterialBatch.SetColor("_Color", m_RenderInfo.m_Color);
                }

                // Update the hue variation
                if(m_IsGrassType)
                {
                    m_RuntimeData.m_LODDataGrass.m_Material.SetColor("_HueVariation", m_RenderInfo.m_Hue);
                    m_RuntimeData.m_LODDataGrass.m_Material.SetColor("_Color", m_RenderInfo.m_Color);
                }
                else
                {
                    for(int i = 0; i < m_RuntimeData.m_LODDataTree.Length; i++)
                    {
                        for(int m = 0; m < m_RuntimeData.m_LODDataTree[i].m_Materials.Length; m++)
                            m_RuntimeData.m_LODDataTree[i].m_Materials[m].SetColor("_Color", m_RenderInfo.m_Color);
                    }
                }
            }

            // Update all LOD distances
            if (IsGrassType == false)
            {
                LODGroup grp = m_Prefab.GetComponent<LODGroup>();
                LOD[] lods = grp != null ? grp.GetLODs() : null;

                FoliageTypeUtilities.UpdateDistancesLOD(m_RuntimeData.m_LODDataTree, lods, m_RenderInfo.m_MaxDistance, IsSpeedTreeType);
            }
        }        

        public void CopyBlock()
        {
#if UNITY_EDITOR
            // Debug/editor only checks
            Debug.Assert(m_Type != EFoliageType.OTHER_GRASS && m_Type != EFoliageType.OTHER_GRASS);
            Debug.Assert(m_IsSpeedTreeType);
#endif

            m_RuntimeData.CopyBlock();
        }
    }

    public class FoliageTypeUtilities
    {
        /** To be used at edit-time */
        public static void BuildDataPartialEditTime(FoliagePainter painter, FoliageType type)
        {
            GameObject prefab = type.m_Prefab;

            // Update the type
            type.Type = type.Type;

            FoliageLog.Assert(prefab != null, "Null foliage prefab!");

            if (type.m_RuntimeData == null)
                type.m_RuntimeData = new FoliageTypeRuntimeData();

            FoliageTypeRuntimeData runtime = type.m_RuntimeData;

            List<Material> checkMaterials = new List<Material>();

            // Init the SpeedTree data
            if (type.IsSpeedTreeType)
            {
                if(runtime.m_SpeedTreeData == null)
                    runtime.m_SpeedTreeData = new FoliageTypeSpeedTreeData();
            }

            if (type.IsGrassType)
            {
                // Build the data universally for all grass types
                if (runtime.m_LODDataGrass == null)
                    runtime.m_LODDataGrass = new FoliageTypeLODGrass();

                runtime.m_LODDataGrass.m_Mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                runtime.m_LODDataGrass.m_Material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;

                checkMaterials.Add(runtime.m_LODDataGrass.m_Material);

                FoliageLog.Assert(runtime.m_LODDataGrass.m_Mesh != null, "Could not find mesh for type: " + prefab.name + ". Make sure that is has at least one mesh and one material");
                FoliageLog.Assert(runtime.m_LODDataGrass.m_Material != null, "Could not find material for type: " + prefab.name + ". Make sure that is has at least one mesh and one material");
            }
            else
            {
                LODGroup group = prefab.GetComponent<LODGroup>();

                if (group == null)
                {
                    FoliageLog.w("Detected tree: " + prefab.name + " without a lod group. Are you sure that you require a tree for this?");

                    if (runtime.m_LODDataTree == null || runtime.m_LODDataTree.Length == 0)
                        runtime.m_LODDataTree = new FoliageTypeLODTree[1];

                    runtime.m_LODDataTree[0] = new FoliageTypeLODTree();
                    runtime.m_LODDataTree[0].m_Mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                    runtime.m_LODDataTree[0].m_Materials = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;
                    runtime.m_LODDataTree[0].m_EndDistance = type.m_RenderInfo.m_MaxDistance;

                    checkMaterials.AddRange(runtime.m_LODDataTree[0].m_Materials);
                }
                else
                {
                    List<FoliageTypeLODTree> treeLods = new List<FoliageTypeLODTree>(group.lodCount);
                    LOD[] lods = group.GetLODs();

                    for (int i = 0; i < group.lodCount; i++)
                    {
                        if (lods[i].renderers[0].gameObject.GetComponent<BillboardRenderer>() != null)
                        {
                            // Extract the billboard data                            
                            var speedData  = runtime.m_SpeedTreeData;
                            FoliageWindTreeUtilities.ExtractBillboardData(lods[i].renderers[0].gameObject.GetComponent<BillboardRenderer>(), speedData);
                            
                            continue;
                        }

                        FoliageTypeLODTree treeLod = new FoliageTypeLODTree();

                        MeshRenderer rend = lods[i].renderers[0].gameObject.GetComponent<MeshRenderer>();
                        MeshFilter filter = lods[i].renderers[0].gameObject.GetComponent<MeshFilter>();

                        treeLod.m_Mesh = filter.sharedMesh;
                        treeLod.m_Materials = rend.sharedMaterials;

                        checkMaterials.AddRange(rend.sharedMaterials);

                        treeLods.Add(treeLod);
                    }

                    runtime.m_LODDataTree = treeLods.ToArray();

                    // Update the LOD distances
                    UpdateDistancesLOD(runtime.m_LODDataTree, lods, type.m_RenderInfo.m_MaxDistance, type.IsSpeedTreeType);
                }
            }

            if (checkMaterials.Count > 0)
            {
                if (type.IsSpeedTreeType)
                {
                    if (type.m_RenderInfo.m_Hue == new Color(0, 0, 0, 0))
                        type.m_RenderInfo.m_Hue = checkMaterials[0].GetColor("_HueVariation");

                    if (type.m_RenderInfo.m_Color == new Color(0, 0, 0, 0))
                        type.m_RenderInfo.m_Color = checkMaterials[0].GetColor("_Color");
                }

                for (int i = 0; i < checkMaterials.Count; i++)
                {

                    if (checkMaterials[i].enableInstancing == false)
                    {
                        checkMaterials[i].enableInstancing = true;
                        FoliageLog.w("Material: [" + checkMaterials[i].name + "] did not had instancing enabled! We enabled it!");
                    }
                }
            }

            // Moved the build at partial edit-time
            if (type.Type == EFoliageType.SPEEDTREE_GRASS)
            {
                Shader shader = painter.GetShaderGrass();
                FoliageLog.Assert(shader, "Could not find shader: Critias/SpeedTree_Grass! Make sure that it is added to the project and that it compiled!");

                FoliageTypeLODGrass lodGrass = type.m_RuntimeData.m_LODDataGrass;

                // Override the material at runtime
                lodGrass.m_Material = new Material(lodGrass.m_Material);
                lodGrass.m_Material.shader = shader;

                // Enable it for instancing
                lodGrass.m_Material.enableInstancing = true;
            }
            else if (type.Type == EFoliageType.SPEEDTREE_TREE || type.Type == EFoliageType.SPEEDTREE_TREE_BILLBOARD)
            {
                Shader shader = painter.GetShaderTreeMaster();
                FoliageLog.Assert(shader, "Could not find shader: Critias/SpeedTree_Master! Make sure that it is added to the project and that it compiled!");

                FoliageTypeLODTree[] lodTree = type.m_RuntimeData.m_LODDataTree;

                for (int i = 0; i < lodTree.Length; i++)
                {
                    FoliageTypeLODTree tree = lodTree[i];

                    Material[] mats = tree.m_Materials;

                    for (int m = 0; m < mats.Length; m++)
                    {
                        // Set the new material
                        mats[m] = new Material(mats[m]);
                        mats[m].shader = shader;

                        // Enable instancing
                        mats[m].enableInstancing = true;
                    }

                    tree.m_Materials = mats;
                }
            }

            // Set the materials the values for enabling the bend stuff if we have it
            if (type.IsGrassType)
            {
                if (type.m_EnableBend)
                    type.m_RuntimeData.m_LODDataGrass.m_Material.EnableKeyword("CRITIAS_DISTANCE_BEND");
                else
                    type.m_RuntimeData.m_LODDataGrass.m_Material.DisableKeyword("CRITIAS_DISTANCE_BEND");
            }
        }

        /** To be used at runtime. Will create all we need for SpeedTree wind and other data */
        public static void BuildDataRuntime(FoliagePainter painter, FoliageType type, Transform attachmentPoint)
        {
            FoliageLog.Assert(type.IsRuntimeInitialized == false, "Runtime data already initialized!");
            
            // Everyone has MPB's
            type.m_RuntimeData.m_TypeMPB = new MaterialPropertyBlock();
            
            if (type.IsSpeedTreeType)
            {
                // Create the glued mesh
                var speedData = type.m_RuntimeData.m_SpeedTreeData;
                FoliageLog.Assert(speedData != null, "Speed data must already be partly generated if we have a SpeedTree!");

                // Get the lod and instnatiade the one with the least instructions                
                LOD[] lods = type.m_Prefab.GetComponent<LODGroup>().GetLODs();

                for(int i = lods.Length - 1; i >= 0; i--)
                {
                    if (lods[i].renderers[0].GetComponent<BillboardRenderer>() != null)
                        continue;
                    else
                    {
                        // Init the object with the lowest possible LOD
                        speedData.m_SpeedTreeWindObject = GameObject.Instantiate(lods[i].renderers[0].gameObject, attachmentPoint);
                        break;
                    }
                }

                // Set the data
                speedData.m_SpeedTreeWindObjectMesh = speedData.m_SpeedTreeWindObject.GetComponentInChildren<MeshRenderer>();

                // Set the NULL invisible shader
                Shader nullShader = painter.GetShaderNull();
                FoliageLog.Assert(nullShader, "Null shader not found! Make sure it exists and that it compiled!");

                // Set the invisible null shader, we only need the wind
                Material[] mats = speedData.m_SpeedTreeWindObjectMesh.materials;
                for(int i = 0; i < mats.Length; i++)
                    mats[i].shader = nullShader;

                // Attach the wind object. Ensures that we are enabled.
                speedData.m_SpeedTreeWindObject.AddComponent<FoliageWindTreeWind>();

                speedData.m_SpeedTreeWindObjectMesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                speedData.m_SpeedTreeWindObject.transform.SetParent(attachmentPoint, false);
                speedData.m_SpeedTreeWindObject.transform.localPosition = new Vector3(0, 0, 0);

                MeshFilter m = speedData.m_SpeedTreeWindObject.GetComponentInChildren<MeshFilter>();

                Bounds b = m.mesh.bounds;
                b.Expand(4.5f);

                m.mesh.bounds = b;
                speedData.m_SpeedTreeWindObject.GetComponentInChildren<MeshFilter>().mesh = m.mesh;                
            }

            // Not used here, since at edit time we already created the materials
            /*
            if (type.Type == EFoliageType.SPEEDTREE_GRASS)
            {
                Shader shader = painter.GetShaderGrass();
                FoliageLog.Assert(shader, "Could not find shader: Critias/SpeedTree_Grass! Make sure that it is added to the project and that it compiled!");
                
                FoliageTypeLODGrass lodGrass = type.m_RuntimeData.m_LODDataGrass;
                
                // Override the material at runtime
                // lodGrass.m_Material = new Material(lodGrass.m_Material);
                // lodGrass.m_Material.shader = shader;

                // Enable it for instancing
                // lodGrass.m_Material.enableInstancing = true;
            }
            else if(type.Type == EFoliageType.SPEEDTREE_TREE || type.Type == EFoliageType.SPEEDTREE_TREE_BILLBOARD)
            {
                Shader shader = painter.GetShaderTreeMaster();
                FoliageLog.Assert(shader, "Could not find shader: Critias/SpeedTree_Master! Make sure that it is added to the project and that it compiled!");

                FoliageTypeLODTree[] lodTree = type.m_RuntimeData.m_LODDataTree;

                for(int i = 0; i < lodTree.Length; i++)
                {
                    FoliageTypeLODTree tree = lodTree[i];

                    Material[] mats = tree.m_Materials;

                    for(int m = 0; m < mats.Length; m++)
                    {
                        // Set the new material
                        //mats[m] = new Material(mats[m]);
                        //mats[m].shader = shader;

                        // Enable instancing
                        //mats[m].enableInstancing = true;
                    }

                    tree.m_Materials = mats;
                }
            }
            */

            // We did initialize
            type.IsRuntimeInitialized = true;
        }
        
        /**
         * Update the LOD min/max distances for an object.
         * 
         * @param isSpeedTree
         *          If this is set to true, then we will check if we have a billboard renderer to an LOD
         *          and we will skip it in case that we have one
         */
        public static void UpdateDistancesLOD(FoliageTypeLODTree[] treeLods, LOD[] groupLods, float maxDistance, bool isSpeedTree)
        {            
            if (groupLods != null && groupLods.Length > 0)
            {
                FoliageLog.Assert(groupLods.Length >= treeLods.Length, "Must have same or more lods than the tree lods!");

                for (int i = 0; i < treeLods.Length; i++)
                {
                    // If we are a speedtree check if we have a billboard renderer
                    if (isSpeedTree && groupLods[i].renderers[0].GetComponent<BillboardRenderer>() != null)
                        continue;

                    FoliageTypeLODTree lodTree = treeLods[i];
                    LOD lodGroupCurrent = groupLods[i];

                    lodTree.m_EndDistance = ((1.0f - lodGroupCurrent.screenRelativeTransitionHeight) * maxDistance);
                }
            }
            else
            {
                treeLods[0].m_EndDistance = maxDistance;
            }
        }
    }
}