/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CritiasFoliage
{    
    /**
     * Foliage brush painting parameters.
     */
    [System.Serializable]
    public class FoliagePaintParameters
    {
        /** Bush size in meters */
        [Range(1, 100)]
        public float m_BrushSize = 2;
        [Range(1f, 100)]
        public float m_FoliageDensity = 50;
        
        /** If we should only add to some slopes foliage instances */
        public bool m_SlopeFilter = false;
        /** Angles at which we can draw the foliage instances at */
        public Vector2 m_SlopeAngles = new Vector2(0, 180);

        public bool m_ScaleUniform = true;
        public Vector2 m_ScaleUniformXYZ = new Vector2(1, 1);
        public Vector2 m_ScaleX = new Vector2(1, 1);
        public Vector2 m_ScaleY = new Vector2(1, 1);
        public Vector2 m_ScaleZ = new Vector2(1, 1);

        public bool m_RotateYOnly = true;
        public Vector2 m_RandomRotation = new Vector2(0, 360);

        // If we draw only on static objects
        public bool m_StaticOnly = false;
    }
    
    public class PaintedColliderData
    {
        // If it is a terrain
        public bool m_IsTerrain;

        // The terrain if it is a terrain
        public Terrain m_Terrain;

        // Terrain name if we have one
        public string m_TerrainName;
        
        // The terrain listener if it has one
        public FoliageTerrainListener m_TerrainListener;
    }

    /**
     * Foliage painter intended for use at both edit time and runtime.
     * 
     * NOTE: Do not use this class directly at runtime! Might result in unexpected behaviour. Make sure
     * that you use the 'GetRuntime' property and make any calls to this class through it.
     */
    [ExecuteInEditMode]
    public class FoliagePainter : MonoBehaviour
    {
        [System.Serializable]
        public enum ESpatialGridDrawMode
        {
            NONE,
            DRAW_GRIDS,
            DRAW_GRIDS_EXTENDED,
            DRAW_SUBDIVIDED_GRIDS,
            DRAW_DRAWN_GRIDS,
            DRAW_DRAWN_SUBDIVIDED_GRIDS,
        }

#if UNITY_EDITOR
        // BEGIN Editor data
        public bool m_EditorFoldoutHelp = true;
        public bool m_EditorFoldoutBrush = true;
        public bool m_EditorFoldoutFoliageTypes = true;
        public bool m_EditorFoldoutFoliageTypeInfo = false;
        public bool m_EditorFoldoutAdvanced = false;
        public bool m_EditorPaintBegun = false;       
        // END Editor data
#endif

        // Foliage renderer
        public FoliageRenderer m_FoliageRenderer;
        public FoliageColliders m_FoliageColliders;

        // List of foliage types
        [SerializeField] private List<FoliageType> m_FoliageTypes = new List<FoliageType>(); 
        private Dictionary<int, FoliageType> m_FoliageTypeIndexed;

        // Data that we are going to hold
#if UNITY_EDITOR
        private Dictionary<int, int> m_FoliageTypeIndexedCachedCount;
        private HashSet<string> m_FoliageTypeLabelsCached;

        public FoliageData m_FoliageData;
        public FoliagePaintParameters m_PaintParameters;
#endif

        public FoliageDataRuntime m_FoliageDataRuntime;
        
        public string m_FoliageDataSaveName;

#if UNITY_EDITOR
        // Rendering data
        public ESpatialGridDrawMode m_DrawGridsMode;
        public bool m_DrawTreeShadows = false;       
        public bool m_DrawTreeLastLOD = true;
        // 1...3 range
        public int m_DrawNeighboringCells = 1;
        // 25...100 range
        public float m_DrawGrassCellsDistance = 50;
#endif

        public bool m_BillboardsGenerateLODGroup = true;
        public float m_BillboardLODGroupFade = 0.2f;
        public bool m_BillboardLODGroupWillCrossFade = true;

        public Shader m_ShaderTreeMaster;
        public Shader m_ShaderGrass;
        public Shader m_ShaderNull;

#if UNITY_EDITOR
        /** WARNING: This class is for viewing data only. Do NOT modify it directly! */
        public List<FoliageType> GetFoliageTypes
        {
            get { return m_FoliageTypes; }
        }
#endif

        /** Get runtime operation interface. All runtime operations must be done through this interface. */
        public FoliagePainterRuntime GetRuntime
        {
            get { return new FoliagePainterRuntime(this); }
        }

#if UNITY_EDITOR
        /** Get edit time operation interface. All edit time modifications must be done through this interface. */
        public FoliagePainterEditTime GetEditTime
        {
            get { return new FoliagePainterEditTime(this); }
        }
#endif

        void Awake()
        {
#if UNITY_EDITOR
            FoliageGlobals.Config();
#endif
        }

        void Start()
        {
#if UNITY_EDITOR
            GetShaderGrass();
            GetShaderNull();
            GetShaderTreeMaster();

            if (UnityEditor.EditorApplication.isPlaying == false)
            {
                LoadFromFile(false, false);

                for (int i = 0; i < m_FoliageTypes.Count; i++)
                {
                    // Refresh type data with it's edit/runtime data
                    RefreshFoliageTypeData();
                }
            }
            else
            {
                // TODO: uncoment
                // We don't need to be enabled
                enabled = false;

                // Autoconfig the foliage renderer
                LoadFromFile(false, true);

                FoliageLog.Assert(m_FoliageDataRuntime != null, "Must have foliage runtime data!");

                if (!m_FoliageRenderer)
                    m_FoliageRenderer = FindObjectOfType<FoliageRenderer>();

                if (!m_FoliageColliders)
                    m_FoliageColliders = FindObjectOfType<FoliageColliders>();

                FoliageLog.Assert(m_FoliageRenderer, "Must have a foliage renderer at runtime!");
                FoliageLog.Assert(m_FoliageColliders, "Must have foliage colliders at runtime!");

                // Start the renderer
                m_FoliageRenderer.InitRenderer(this, m_FoliageDataRuntime, m_FoliageTypes);
                m_FoliageColliders.InitCollider(m_FoliageDataRuntime, m_FoliageTypes);

                // Update all the values
                for (int i = 0; i < m_FoliageTypes.Count; i++)
                    m_FoliageTypes[i].UpdateValues();
            }
#else           
            // We don't need to be enabled
            enabled = false;

            // Autoconfig the foliage renderer
            LoadFromFile(false, true);

            FoliageLog.Assert(m_FoliageDataRuntime != null, "Must have foliage runtime data!");

            if (!m_FoliageRenderer)
                    m_FoliageRenderer = FindObjectOfType<FoliageRenderer>();

            if (!m_FoliageColliders)
                m_FoliageColliders = FindObjectOfType<FoliageColliders>();

            FoliageLog.Assert(m_FoliageRenderer, "Must have a foliage renderer at runtime!");
            FoliageLog.Assert(m_FoliageColliders, "Must have foliage colliders at runtime!");
            
            m_FoliageRenderer.InitRenderer(this, m_FoliageDataRuntime, m_FoliageTypes);
            m_FoliageColliders.InitCollider(m_FoliageDataRuntime, m_FoliageTypes);

            // Update all the values
            for (int i = 0; i < m_FoliageTypes.Count; i++)
                m_FoliageTypes[i].UpdateValues();
#endif
        }

        public Shader GetShaderTreeMaster()
        {
            if (m_ShaderTreeMaster == null)
            {
                m_ShaderTreeMaster = Shader.Find("Critias/WindTree_Master");
                FoliageLog.Assert(m_ShaderTreeMaster, "Could not find shader 'SpeedTreeMaster'! Make sure that it exists and that it compiled!");
            }

            return m_ShaderTreeMaster;
        }

        public Shader GetShaderNull()
        {
            if (m_ShaderNull == null)
            {
                m_ShaderNull = Shader.Find("Critias/NullShader");
                FoliageLog.Assert(m_ShaderNull, "Null shader not found! Make sure it exists and that it compiled!");
            }

            return m_ShaderNull;
        }

        public Shader GetShaderGrass()
        {
            if (m_ShaderGrass == null)
            {
                m_ShaderGrass = Shader.Find("Critias/WindTree_Grass");
                FoliageLog.Assert(m_ShaderGrass, "Could not find shader: Critias/SpeedTree_Grass! Make sure that it is added to the project and that it compiled!");
            }

            return m_ShaderGrass;
        }

        public string GetFileSaveName()
        {
            if(m_FoliageDataSaveName == null || m_FoliageDataSaveName.Length == 0)
            {
                m_FoliageDataSaveName = FoliageGlobals.DISK_FILENAME + "_" + gameObject.scene.name;
            }

            return m_FoliageDataSaveName;
        }

        public void SaveToFile()
        {
#if UNITY_EDITOR
            string saveFile = GetFileSaveName();

            Debug.Log("Saving grass to file: " + saveFile);
            FoliageDataSerializer.SaveToFile(saveFile, m_FoliageData);

            // Request a count refresh
            RequestCountRefresh();

            // And a label refresh
            RequestLabelRefresh();
#else
            Debug.LogError("Can't save to file while we are not in the editor!");
#endif
        }

        public void LoadFromFile(bool forceReload, bool runtimeOnly)
        {
#if UNITY_EDITOR
            if (runtimeOnly)
            {
                m_FoliageDataRuntime = FoliageDataSerializer.LoadFromFileRuntime(GetFileSaveName());                
            }
            else
            {
                if (m_FoliageData == null || forceReload)
                {
                    string saveFile = GetFileSaveName();

                    Debug.Log("Foliage data null! lost the data, building from disk: " + saveFile);
                    m_FoliageData = FoliageDataSerializer.LoadFromFileEditTime(saveFile);

                    // Attempt data refresh
                    RefreshData();
                }
            }
#else
            if (runtimeOnly)
            {
                m_FoliageDataRuntime = FoliageDataSerializer.LoadFromFileRuntime(GetFileSaveName());                
            }
#endif
        }

        private void RefreshFoliageTypeData()
        {
            FoliageLog.i("Refreshing foliage type data!");

            // Re-build the partial edittime required data
            for (int i = 0; i < m_FoliageTypes.Count; i++)
            {
                FoliageTypeUtilities.BuildDataPartialEditTime(this, m_FoliageTypes[i]);
                m_FoliageTypes[i].UpdateValues();

            }

            // If we are in editor only apply the changes that are done when we are not playing since we make all modifictions
            // without the need of rendering the wind and other specialized data
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying == true)
            {
                FoliageLog.Assert(m_FoliageRenderer, "Foliage renderer must not be null!");
                m_FoliageRenderer.UpdateFoliageTypes(m_FoliageTypes);          
            }
#else
            FoliageLog.Assert(m_FoliageRenderer, "Foliage renderer must not be null!");
            m_FoliageRenderer.UpdateFoliageTypes(m_FoliageTypes);
#endif
        }

        #region RUNTIME_OPERATIONS
        
        public List<FoliageTypeRuntime> GetFoliageTypesRuntime()
        {
            return m_FoliageTypes.ConvertAll((x =>
            {
                FoliageTypeRuntime rt = new FoliageTypeRuntime();

                rt.m_Hash = x.m_Hash;
                rt.m_Name = x.m_Name;
                rt.m_Type = x.Type;
                rt.m_IsGrassType = x.IsGrassType;
                rt.m_IsSpeedTreeType = x.IsSpeedTreeType;

                return rt;
            }));
        }

        public void RemoveFoliageInstanceRuntime(System.Guid guid)
        {
            m_FoliageDataRuntime.RemoveFoliageInstance(guid);
        }
        
        public void RemoveFoliageInstanceRuntime(int typeHash, System.Guid guid)
        {
            m_FoliageDataRuntime.RemoveFoliageInstance(typeHash, guid);
        }
        
        public void RemoveFoliageInstanceRuntime(int typeHash, System.Guid guid, Vector3 position)
        {
            m_FoliageDataRuntime.RemoveFoliageInstance(typeHash, guid, position);
        }

        public void AddFoliageInstanceRuntime(int typeHash, FoliageInstance instance)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if(type.IsGrassType == false)
                {
                    PrepareFoliageInstanceRuntime(type, ref instance);
                    m_FoliageDataRuntime.AddFoliageInstance(typeHash, instance);
                }
                else
                    FoliageLog.e("Can only add foliage instance for tree types! Type: " + type.m_Name + " is not a tree!");
            }
            else
                FoliageLog.e("Cannot add foliage instance for hash: " + typeHash);
        }

        public void SetFoliageTypeCastShadowRuntime(int typeHash, bool castShadow)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.m_RenderInfo.m_CastShadow != castShadow)
                {
                    type.m_RenderInfo.m_CastShadow = castShadow;
                    RefreshFoliageTypeDataRuntime(type);
                }
            }
            else
                FoliageLog.e("Cannot set shadow for hash: " + typeHash);

            m_FoliageRenderer.UpdateFoliageTypes(m_FoliageTypes);
        }

        public bool GetFoliageTypeCastShadowRuntime(int typeHash)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
                return type.m_RenderInfo.m_CastShadow;
            else
                FoliageLog.e("Cannot get shadow for hash: " + typeHash);

            return false;
        }

        public void SetFoliageTypeMaxDistanceRuntime(int typeHash, float maxDistance)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (Mathf.Abs(type.m_RenderInfo.m_MaxDistance - maxDistance) > Mathf.Epsilon)
                {
                    type.m_RenderInfo.m_MaxDistance = FoliageGlobals.ClampDistance(type.Type, maxDistance);
                    RefreshFoliageTypeDataRuntime(type);
                }
            }
            else
                FoliageLog.e("Cannot set max distance for hash: " + typeHash);            
        }

        public float GetFoliageTypeMaxDistanceRuntime(int typeHash)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
                return type.m_RenderInfo.m_MaxDistance;
            else
                FoliageLog.e("Cannot get max distance for hash: " + typeHash);

            return -1;
        }

        public void SetFoliageTypeHueRuntime(int typeHash, Color hue)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.m_RenderInfo.m_Hue != hue)
                {
                    type.m_RenderInfo.m_Hue = hue;
                    RefreshFoliageTypeDataRuntime(type);
                }
            }
            else
                FoliageLog.e("Cannot set hue for hash: " + typeHash);
        }

        public Color GetFoliageTypeHueRuntime(int typeHash)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
                return type.m_RenderInfo.m_Hue;
            else
                FoliageLog.e("Cannot get hue for hash: " + typeHash);

            return Color.black;
        }

        public void SetFoliageTypeColorRuntime(int typeHash, Color color)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.m_RenderInfo.m_Color != color)
                {
                    type.m_RenderInfo.m_Color = color;
                    RefreshFoliageTypeDataRuntime(type);
                }
            }
            else
                FoliageLog.e("Cannot set hue for hash: " + typeHash);
        }

        public Color GetFoliageTypeColorRuntime(int typeHash)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
                return type.m_RenderInfo.m_Color;
            else
                FoliageLog.e("Cannot get hue for hash: " + typeHash);

            return Color.black;
        }

        private void RefreshFoliageTypeDataRuntime(FoliageType modifiedType = null)
        {
            FoliageLog.Assert(m_FoliageRenderer, "Renderer must not be null!");

            if (modifiedType != null)
            {
                // Update only one type
                modifiedType.UpdateValues();
            }
            else
            {
                // Update all types
                for (int i = 0; i < m_FoliageTypes.Count; i++)
                    m_FoliageTypes[i].UpdateValues();
            }

            m_FoliageRenderer.UpdateFoliageTypes(m_FoliageTypes);
        }
        
        private Dictionary<int, FoliageType> GetFoliageTypeSet()
        {
            if (m_FoliageTypeIndexed == null)
            {
                m_FoliageTypeIndexed = new Dictionary<int, FoliageType>();
                m_FoliageTypes.ForEach((x) => m_FoliageTypeIndexed.Add(x.m_Hash, x));
            }

            return m_FoliageTypeIndexed;
        }

        /** Do not modify directly the returned type! */
        public FoliageType GetFoliageTypeByHash(int typeHash)
        {
            var types = GetFoliageTypeSet();
            return types.ContainsKey(typeHash) ? types[typeHash] : null;
        }

        private void PrepareFoliageInstanceRuntime(FoliageType type, ref FoliageInstance instance)
        {
            // Add a new unique ID
            instance.m_UniqueId = System.Guid.NewGuid();

            // Set the bounds
            instance.m_Bounds = type.m_Bounds;
            instance.m_Bounds = FoliageUtilities.LocalToWorld(ref instance.m_Bounds, instance.GetWorldTransform());

            instance.BuildWorldMatrix();            
        }

        #endregion

        #region EDIT_TIME_OPERATIONS
#if UNITY_EDITOR

        /*
        void OnGUI()
        {
            for (int i = 0; i < m_FoliageTypes.Count; i++)
            {
                FoliageType type = m_FoliageTypes[i];

                float posY = 80 * i;

                GUI.Label(new Rect(20, posY, 200, 20), "Name: " + type.m_Name);
                float max = GUI.HorizontalSlider(new Rect(20, posY + 20, 100, 20), type.m_RenderInfo.m_MaxDistance, 0, 
                    type.IsGrassType ? FoliageGlobals.FOLIAGE_MAX_GRASS_DISTANCE : FoliageGlobals.FOLIAGE_MAX_TREE_DISTANCE);
                bool shadow = GUI.Toggle(new Rect(20, posY + 40, 100, 20), type.m_RenderInfo.m_CastShadow, "Shadow");

                // Set the distance only if we switched so that we don't call that every frame (or more often)
                if (Mathf.Abs(max - type.m_RenderInfo.m_MaxDistance) > Mathf.Epsilon)
                    SetFoliageTypeMaxDistanceRuntime(type.m_Hash, max);
                
                // Same
                if(shadow != type.m_RenderInfo.m_CastShadow)
                    SetFoliageTypeCastShadowRuntime(type.m_Hash, shadow);
            }
        }
        */

        void OnDrawGizmos()
        {
            if (m_FoliageData == null)
                return;
            
            foreach (FoliageCellData data in m_FoliageData.m_FoliageData.Values)
            {
                if(m_DrawGridsMode == ESpatialGridDrawMode.DRAW_GRIDS)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireCube(data.m_Bounds.center, data.m_Bounds.size);
                }
                else if(m_DrawGridsMode == ESpatialGridDrawMode.DRAW_GRIDS_EXTENDED)
                {
                    Gizmos.color = Color.cyan - new Color(0.2f, 0.2f, 0.2f, 0);
                    Gizmos.DrawWireCube(data.m_BoundsExtended.center, data.m_BoundsExtended.size);
                }
                else if(m_DrawGridsMode == ESpatialGridDrawMode.DRAW_SUBDIVIDED_GRIDS)
                {
                    Gizmos.color = Color.green - new Color(0.2f, 0.2f, 0.2f, 0);
                    foreach(FoliageCellSubdividedData subdivData in data.m_FoliageDataSubdivided.Values)
                    {
                        Gizmos.DrawWireCube(subdivData.m_Bounds.center, subdivData.m_Bounds.size);
                    }
                }
                else if(m_DrawGridsMode == ESpatialGridDrawMode.DRAW_DRAWN_GRIDS)
                {
                    Gizmos.color = Color.blue + new Color(0.2f, 0.2f, 0.2f, 0);
                    for(int i = 0; i < m_DrawnCells.Count; i++)
                    {
                        Gizmos.DrawWireCube(m_DrawnCells[i].center, m_DrawnCells[i].size);
                    }
                }
                else if(m_DrawGridsMode == ESpatialGridDrawMode.DRAW_DRAWN_SUBDIVIDED_GRIDS)
                {
                    Gizmos.color = Color.yellow + new Color(0.2f, 0.2f, 0.2f, 0);
                    for (int i = 0; i < m_DrawnCellsSubdivided.Count; i++)
                    {
                        Gizmos.DrawWireCube(m_DrawnCellsSubdivided[i].center, m_DrawnCellsSubdivided[i].size);
                    }
                }
            }
        }

        public void SetFoliageTypeType(int typeHash, EFoliageType newFoliageType)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.Type != newFoliageType)
                {
                    bool wasGrass = type.IsGrassType;
                    type.Type = newFoliageType;

                    // If we are not a grass any more we'll have to change the hierarchy
                    if(wasGrass != type.IsGrassType)
                    {
                        FoliageLog.i("Changed from tree/grass to the opposite type! Rebuilding hierarchy!");
                        m_FoliageData.RebuildType(type.m_Hash, type.IsGrassType);

                        // Update the count
                        RefreshCachedCountForType(type.m_Hash, false);
                    }                 
                    else
                        FoliageLog.i("Did not changed from tree/grass to opposite type! Not rebuilding hierarchy!");

                    // Refresh the foliage type data though
                    RefreshFoliageTypeData();
                }
            }
            else
                FoliageLog.e("Cannot set new type for hash: " + typeHash);
        }

        public void SetFoliageTypeRenderType(int typeHash, EFoliageRenderType renderType)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.IsGrassType == true)
                {
                    if (type.RenderType != renderType)
                        type.RenderType = renderType;
                }
                else
                    FoliageLog.e("Cannot set render type for non-grass!");
            }
            else
                FoliageLog.e("Cannot set render type for hash: " + typeHash);
        }

        public void SetFoliageTypeHue(int typeHash, Color hue)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.m_RenderInfo.m_Hue != hue)
                {
                    type.m_RenderInfo.m_Hue = hue;

                    // Refresh type data with it's edit/runtime data
                    RefreshFoliageTypeData();
                }
            }
            else
                FoliageLog.e("Cannot set hue for hash: " + typeHash);
        }

        public void SetFoliageTypeColor(int typeHash, Color color)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.m_RenderInfo.m_Color != color)
                {
                    type.m_RenderInfo.m_Color = color;

                    // Refresh type data with it's edit/runtime data
                    RefreshFoliageTypeData();
                }
            }
            else
                FoliageLog.e("Cannot set color for hash: " + typeHash);
        }

        public void SetFoliageTypeShadow(int typeHash, bool castShadow)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.m_RenderInfo.m_CastShadow != castShadow)
                {
                    type.m_RenderInfo.m_CastShadow = castShadow;
                }
            }
            else
                FoliageLog.e("Cannot set shadow for hash: " + typeHash);
        }

        public void SetFoliageTypeMaxDistance(int typeHash, float maxDistance)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (Mathf.Abs(type.m_RenderInfo.m_MaxDistance - maxDistance) > Mathf.Epsilon)
                {
                    type.m_RenderInfo.m_MaxDistance = FoliageGlobals.ClampDistance(type.Type, maxDistance);
                    
                    // Refresh type data with it's edit/runtime data
                    RefreshFoliageTypeData();
                }
            }
            else
                FoliageLog.e("Cannot set max distance for hash: " + typeHash);
        }

        public void SetFoliageTypeCollision(int typeHash, bool enableCollision)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if(type.m_EnableCollision != enableCollision)
                    type.m_EnableCollision = enableCollision;
            }
            else
                FoliageLog.e("Cannot set collision for hash: " + typeHash);
        }
        
        public void SetFoliageTypeBending(int typeHash, bool enableBending)
        {
            FoliageType type = GetFoliageTypeByHash(typeHash);

            if (type != null)
            {
                if (type.m_EnableBend != enableBending)
                {
                    type.m_EnableBend = enableBending;

                    // Refresh type data that also enables the bend keyword
                    RefreshFoliageTypeData();
                }
            }
            else
                FoliageLog.e("Cannot set collision for hash: " + typeHash);
        }

        public void EnableTypeForPainting(int typeHash, bool enabled)
        {
            if (GetFoliageTypeSet().ContainsKey(typeHash))
            {
                // Enable the painting mode
                GetFoliageTypeByHash(typeHash).m_PaintInfo.m_PaintEnabled = enabled;
            }
            else
                FoliageLog.e("Cannot enable type for painting! Type not found: " + typeHash);
        }

        public bool HasFoliageType(int hash)
        {
            return (GetFoliageTypeByHash(hash) != null);
        }

        public bool HasFoliageType(GameObject foliage)
        {
            return (GetFoliageTypeByHash(GetUniqueHash(foliage)) != null);
        }

        public int GetFoliageTypeHash(GameObject foliage)
        {
            return GetFoliageTypeByHash(GetUniqueHash(foliage)).m_Hash;
        }

        public void CleanFoliageData()
        {
            HashSet<int> hashes = m_FoliageData.GetFoliageHashes();

            int clearedHashes = 0;

            // Compare the hashses with 
            if (hashes != null)
            {
                foreach (int dataContainedHashes in hashes)
                {
                    if (GetFoliageTypeByHash(dataContainedHashes) == null)
                    {
                        FoliageLog.w("Found dangling hash data: " + dataContainedHashes + " Removing!");
                        m_FoliageData.RemoveType(dataContainedHashes);

                        clearedHashes++;
                    }
                }
            }

            if (clearedHashes > 0)
            {
                SaveToFile();
                GenerateTreeBillboards(true);
                RefreshData();

                FoliageLog.i("Found: " + clearedHashes + " dangling hash data!");
            }
            else
                FoliageLog.i("Could not find any data to clear!");
        }

        public void StickLabeledGrassToTerrain(string label, Terrain terrain)
        {
            // Collect all the foliage instances added with that label and after that add it back to the data
            Dictionary<int, List<FoliageInstance>> existingInstances = m_FoliageData.CollectLabeledInstances(label);

            // Remove all the foliage labeled instances
            m_FoliageData.RemoveInstancesLabeled(label);

            // Stick all the instances to the terrain on the Y axis
            foreach(var data in existingInstances)
            {
                FoliageType type = GetFoliageTypeByHash(data.Key);
                List<FoliageInstance> instances = data.Value;

                bool terrainSurfaceAlign = type.m_PaintInfo.m_SurfaceAlign;
                Vector2 alignPercentage = type.m_PaintInfo.m_SurfaceAlignInfluence;

                for (int i = 0; i < instances.Count; i++)
                {
                    FoliageInstance inst = instances[i];

                    // Stick it to the terrain
                    Vector3 normPos = FoliageTerrainUtilities.WorldToTerrainNormalizedPos(inst.m_Position, terrain);
                    float y = FoliageTerrainUtilities.TerrainHeight(normPos, terrain);

                    // Build the rotation all over again
                    Quaternion rotation = Quaternion.identity;

                    if (terrainSurfaceAlign)
                    {
                        Quaternion slopeOrientation = Quaternion.LookRotation(FoliageTerrainUtilities.TerrainNormal(normPos, terrain)) * Quaternion.Euler(90, 0, 0);

                        // How much we orient towards the slope
                        rotation = Quaternion.Slerp(rotation, slopeOrientation,
                            Random.Range(alignPercentage.x, alignPercentage.y));
                    }

                    // Rotate around the Y axis
                    rotation *= Quaternion.Euler(0, Random.Range(0, 360), 0);
                    
                    normPos = FoliageTerrainUtilities.TerrainNormalizedToWorldPos(normPos, terrain);
                    normPos.y = y;

                    // Set the new data
                    inst.m_Position = normPos;
                    inst.m_Rotation = rotation;

                    // Set the new instance data
                    instances[i] = inst;
                }
            }

            // Add them back to the system
            foreach(var typeInstances in existingInstances)
                AddFoliageInstancesInternal(typeInstances.Key, typeInstances.Value, label);
            
            // Post a file save so that nothing gets lost
            SaveToFile();

            // Generate the billboards and refresh the UI data
            GenerateTreeBillboards(true);
            RefreshData();
        }

        public void GenerateFullTreeData(int[] hashes, bool clearExisting)
        {
            string label = "CRITIAS_Holder_Prefabs";
            GameObject owner = GameObject.Find(label);

            if (clearExisting && owner != null)
                DestroyImmediate(owner);

            if (owner == null)
            {
                owner = new GameObject(label);
                owner.isStatic = true;
            }

            HashSet<int> hashSet = new HashSet<int>(hashes);

            // Generate all the collider data
            foreach (FoliageCellData cell in m_FoliageData.m_FoliageData.Values)
            {
                foreach (var typedData in cell.m_TypeHashLocationsEditor)
                {
                    FoliageType type = GetFoliageTypeByHash(typedData.Key);

                    if (hashSet.Contains(type.m_Hash) == false)
                        continue;

                    GameObject prefab = type.m_Prefab;

                    // Build all the instances
                    foreach (var inst in typedData.Value.Values)
                    {
                        for(int i = 0; i < inst.Count; i++)
                        {
							GameObject instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
							instance.transform.parent = owner.transform;


                            instance.transform.position = inst[i].m_Position;
                            instance.transform.rotation = inst[i].m_Rotation;
                            instance.transform.localScale = inst[i].m_Scale;
                        }
                    }                    
                }

                foreach(FoliageCellSubdividedData cellSubdivided in cell.m_FoliageDataSubdivided.Values)
                {
                    foreach(var typedData in cellSubdivided.m_TypeHashLocationsEditor)
                    {
                        FoliageType type = GetFoliageTypeByHash(typedData.Key);

                        if (hashSet.Contains(type.m_Hash) == false)
                            continue;

                        GameObject prefab = type.m_Prefab;

                        // Build all the instances
                        foreach (var inst in typedData.Value.Values)
                        {
                            for (int i = 0; i < inst.Count; i++)
                            {
								GameObject instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
								instance.transform.parent = owner.transform;

                                instance.transform.position = inst[i].m_Position;
                                instance.transform.rotation = inst[i].m_Rotation;
                                instance.transform.localScale = inst[i].m_Scale;
                            }
                        }
                    }
                }
            }
        }

        public void GenerateTreeData(EExtractType extractType, bool clearExisting)
        {
            string label = "CRITIAS_Holder_" + extractType;
            GameObject owner = GameObject.Find(label);

            if (clearExisting && owner != null)
                DestroyImmediate(owner);

            if (owner == null)
            {
                owner = new GameObject(label);
                owner.isStatic = true;
            }

            // Temp dict for all the data
            Dictionary<int, GameObject> templateCache = new Dictionary<int, GameObject>();

            // Generate all the collider data
            foreach (FoliageCellData cellTree in m_FoliageData.m_FoliageData.Values)
            {
                foreach (var typedData in cellTree.m_TypeHashLocationsEditor)
                {
                    FoliageType type = GetFoliageTypeByHash(typedData.Key);

                    // We extracted once but it is a no-collider type
                    if (templateCache.ContainsKey(type.m_Hash) && templateCache[type.m_Hash] == null)
                        continue;

                    List<FoliageInstance> instances = new List<FoliageInstance>();

                    // Build all the instances
                    foreach (var inst in typedData.Value.Values)
                        instances.AddRange(inst);

                    if (instances.Count == 0)
                        continue;

                    // Add the data
                    if(templateCache.ContainsKey(type.m_Hash) == false)
                        templateCache.Add(type.m_Hash, FoliageUtilities.ExtractFromFoliagePrefab(type.m_Prefab, extractType, true));

                    GameObject template = templateCache[type.m_Hash];

                    // Instantiate the item
                    if(template != null)
                    {
                        for (int i = 0; i < instances.Count; i++)
                        {
                            GameObject collider = Instantiate(template, owner.transform);

                            collider.transform.position = instances[i].m_Position;
                            collider.transform.rotation = instances[i].m_Rotation;
                            collider.transform.localScale = instances[i].m_Scale;
                        }
                    }
                }
            }

            // Delete all the template cache
            foreach (GameObject template in templateCache.Values)
                DestroyImmediate(template);
            templateCache.Clear();
        }

        /** Generates all the billboards */
        public void GenerateTreeBillboards(bool clearExisting)
        {
            GameObject owner = GameObject.Find("CRITIAS_BillboardHolder");

            if(clearExisting && owner != null)
                DestroyImmediate(owner);

            if (owner == null)
                owner = new GameObject("CRITIAS_BillboardHolder");
            
            foreach (FoliageCellData cellTree in m_FoliageData.m_FoliageData.Values)
            {
                GenerateTreeBillboards(owner, cellTree);
            }
        }

        /** Generate billboards selectively per cell */
        private void GenerateTreeBillboards(GameObject owner, FoliageCellData cell)
        {            
            foreach (var typedData in cell.m_TypeHashLocationsEditor)
            {
                FoliageType type = GetFoliageTypeByHash(typedData.Key);

                if (type.Type != EFoliageType.SPEEDTREE_TREE_BILLBOARD)
                    continue;

                List<FoliageInstance> instances = new List<FoliageInstance>();

                // Build all the instances
                foreach (var inst in typedData.Value.Values)
                    instances.AddRange(inst);

                if (instances.Count == 0)
                    continue;
                
                FoliageWindTreeUtilities.GenerateBillboards(cell.m_BoundsExtended, cell.m_Position, owner, instances, type, m_BillboardsGenerateLODGroup, m_BillboardLODGroupFade, m_BillboardLODGroupWillCrossFade);
            }
        }

        public int AddFoliageType(FoliageTypeBuilder foliageType)
        {            
            int hash = GetUniqueHash(foliageType.m_Prefab);

            if(GetFoliageTypeByHash(hash) != null)
            {
                FoliageLog.w("Foliage prefab with hash: [" + hash + "] and name: [" + foliageType.m_Prefab.name + "] already contained!");
                return hash;
            }

            FoliageType type = new FoliageType();

            // Get an unique hash
            type.m_Hash = hash;

            // Set data
            type.m_Name = foliageType.m_Prefab.name;
            type.Type = foliageType.m_Type;
            type.m_Prefab = foliageType.m_Prefab;
            type.m_Bounds = foliageType.m_Bounds;
            type.m_EnableCollision = foliageType.m_EnableCollision;

            // Render info
            type.m_RenderInfo = foliageType.m_RenderInfo;

            // Paint data
            type.m_PaintInfo = foliageType.m_PaintInfo;

            // Paint enabling
            type.m_PaintInfo.m_PaintEnabled = foliageType.m_PaintEnabled;

            AddFoliageTypeInternal(type);

            return type.m_Hash;
        }

        public void RemoveFoliageTypeHash(int foliageTypeHash, bool deleteTypeToo, bool forcefullyProhibitSaving = false)
        {            
            // Remove the data's types first
            m_FoliageTypes.ForEach((x =>
            {
                if(x.m_Hash == foliageTypeHash)
                {
                    m_FoliageData.RemoveType(x.m_Hash);
                }
            }));

            if (deleteTypeToo)
            {
                // Remove our hash data too
                GetFoliageTypeSet().Remove(foliageTypeHash);

                // Remove our data
                int deleted = m_FoliageTypes.RemoveAll((x) => x.m_Hash == foliageTypeHash);
                FoliageLog.Assert(deleted == 1 || deleted == 0);

                if(deleted > 0)
                    RefreshFoliageTypeData();
            }

            if (forcefullyProhibitSaving == false)
            {
                // Save grass to file after we removed the types so that we don't have any bad data in the grass hierarchy if a reloading somehow takes place
                SaveToFile();
            }
            
            GenerateTreeBillboards(true);
            RefreshData();
        }

        public void RemoveFoliageType(GameObject foliage, bool forcefullyProhibitSaving = false)
        {
            // Remove the data's types too
            m_FoliageTypes.ForEach((x =>
            {
                if (x.m_Prefab == foliage)
                {
                    m_FoliageData.RemoveType(x.m_Hash);

                    // Remove our hash data too
                    GetFoliageTypeSet().Remove(x.m_Hash);
                }
            }));

            // Remove our data
            if (m_FoliageTypes.RemoveAll((x) => x.m_Prefab == foliage) > 0)
                RefreshFoliageTypeData();

            if (forcefullyProhibitSaving == false)
            {
                // Save grass to file after we removed the types so that we don't have any bad data in the grass hierarchy if a reloading somehow takes place
                SaveToFile();
            }

            GenerateTreeBillboards(true);
            RefreshData();
        }
        
        public void RemoveFoliageInstances(int typeHash, Vector3 position, float distanceDelta = 0.3f)
        {
            if (GetFoliageTypeSet().ContainsKey(typeHash))
            {
                m_FoliageData.RemoveInstances(typeHash, position, distanceDelta);
            }
            else
                FoliageLog.e("Trying to remove wrong foliage with non existent type hash: " + typeHash);
        }

        public void RemoveFoliageInstancesLabeled(string label, bool forcefullyProhibitSaving = false)
        {
            m_FoliageData.RemoveInstancesLabeled(label);

            if(forcefullyProhibitSaving == false)
            {
                SaveToFile();
            }

            GenerateTreeBillboards(true);
            RefreshData();
        }

        public void RemoveFoliageInstanceGuid(int typeHash, Vector3 position, System.Guid guid)
        {
            if (GetFoliageTypeSet().ContainsKey(typeHash))
            {                
                m_FoliageData.RemoveInstanceGuid(typeHash, position, guid);
            }
            else
                FoliageLog.e("Trying to remove wrong foliage with non existent type hash: " + typeHash);
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
            AddFoliageInstanceInternal(typeHash, instance, label);
        }
        
        /** Same as the single value mode. */
        public void AddFoliageInstances(int typeHash, List<FoliageInstance> instances, string label = FoliageGlobals.LABEL_PAINTED)
        {
            AddFoliageInstancesInternal(typeHash, instances, label);
        }

        public void RequestCountRefresh()
        {
            RefreshCachedCountForType(-1, true);
        }

        private void RefreshCachedCountForType(int type, bool all)
        {
            if(all)
            {
                GetFoliageTypeCachedCountSet().Clear();
            }
            else
            {
                GetFoliageTypeCachedCountSet().Remove(type);
            }
        }

        private Dictionary<int, int> GetFoliageTypeCachedCountSet()
        {
            if (m_FoliageTypeIndexedCachedCount == null)
                m_FoliageTypeIndexedCachedCount = new Dictionary<int, int>();

            return m_FoliageTypeIndexedCachedCount;
        }

        public int GetFoliageInstanceCountCached(int typeHash)
        {
            Dictionary<int, int> cached = GetFoliageTypeCachedCountSet();

            if (cached.ContainsKey(typeHash) == false)
                cached.Add(typeHash, GetFoliageInstanceCount(typeHash));

            return cached[typeHash];
        }

        public int GetFoliageInstanceCount(int typeHash)
        {
            if (GetFoliageTypeSet().ContainsKey(typeHash))
            {
                // We can crash due to the UI requesting this data b4 the update
                return m_FoliageData != null ? m_FoliageData.GetInstanceCount(typeHash) : 0;
            }
            else
                FoliageLog.e("Trying to get foliage count for non existent type hash: " + typeHash);

            return -1;
        }
        
        public void RequestLabelRefresh()
        {
            m_FoliageTypeLabelsCached = null;
        }

        public IEnumerable<string> GetFoliageLabelsCached()
        {
            if (m_FoliageTypeLabelsCached == null || m_FoliageTypeLabelsCached.Count == 0)
            {
                m_FoliageTypeLabelsCached = m_FoliageData != null ? m_FoliageData.GetFoliageLabels() : new HashSet<string>();
            }

            return m_FoliageTypeLabelsCached;
        }

        public IEnumerable<string> GetFoliageLabels()
        {
            return m_FoliageData.GetFoliageLabels();
        }

        // BEGIN Private utilities

        private void AddFoliageInstanceInternal(int typeHash, FoliageInstance instance,  string label = FoliageGlobals.LABEL_PAINTED)
        {
            FoliageType type;

            if (GetFoliageTypeSet().TryGetValue(typeHash, out type))
            {
                // Prepare the foliage instances
                instance = PrepareFoliageInstance(type, instance);

                m_FoliageData.AddInstance(typeHash, instance, type.IsGrassType, label);
            }
            else
                FoliageLog.e("Trying to add wrong foliage with non existent type hash: " + typeHash);
        }

        private void AddFoliageInstancesInternal(int typeHash, List<FoliageInstance> instances, string label = FoliageGlobals.LABEL_PAINTED)
        {
            FoliageType type;

            if (GetFoliageTypeSet().TryGetValue(typeHash, out type))
            {
                for(int i = 0; i < instances.Count; i++)
                    instances[i] = PrepareFoliageInstance(type, instances[i]);

                m_FoliageData.AddInstances(typeHash, instances, type.IsGrassType, label);
            }
            else
                FoliageLog.e("Trying to add wrong foliage with non existent type hash: " + typeHash);
        }

        private void AddFoliageTypeInternal(FoliageType type)
        {
            m_FoliageTypes.Add(type);
            GetFoliageTypeSet().Add(type.m_Hash, type);

            // Refresh type data with it's edit/runtime data
            RefreshFoliageTypeData();
        }

        private FoliageInstance PrepareFoliageInstance(FoliageType type, FoliageInstance instance)
        {
            // Add a new unique ID
            instance.m_UniqueId = System.Guid.NewGuid();

            // Set the bounds
            instance.m_Bounds = type.m_Bounds;
            instance.m_Bounds = FoliageUtilities.LocalToWorld(ref instance.m_Bounds, instance.GetWorldTransform());

            return instance;
        }
        
        public void RequestRefreshFoliageTypeData()
        {
            RefreshFoliageTypeData();
        }
        
        List<FoliageType> m_TempPainEnabled = new List<FoliageType>();
        private List<FoliageType> GetPaintEnabledFoliageTypesGrass()
        {
            // Clear the temp
            m_TempPainEnabled.Clear();

            for (int i = 0; i < m_FoliageTypes.Count; i++)
            {
                if (m_FoliageTypes[i].m_PaintInfo.m_PaintEnabled && m_FoliageTypes[i].IsGrassType == true)
                    m_TempPainEnabled.Add(m_FoliageTypes[i]);
            }

            return m_TempPainEnabled;
        }

        private List<FoliageType> GetPaintEnabledFoliageTypesTree()
        {
            // Clear the temp
            m_TempPainEnabled.Clear();

            for (int i = 0; i < m_FoliageTypes.Count; i++)
            {
                if (m_FoliageTypes[i].m_PaintInfo.m_PaintEnabled && m_FoliageTypes[i].IsGrassType == false)
                    m_TempPainEnabled.Add(m_FoliageTypes[i]);
            }

            return m_TempPainEnabled;
        }

        private void RefreshData()
        {
            gameObject.SetActive(false);
            gameObject.SetActive(true);
        }
        
        private int GetUniqueHash(GameObject foliage)
        {
            return FoliageUtilities.GetStableHashCode(foliage.name);
        }


        // BEGIN Edit-time painting

        // Painter collider cache for colliders, very usefull for fast retrieval of data
        private Dictionary<Collider, PaintedColliderData> m_PaintedColliderCache = new Dictionary<Collider, PaintedColliderData>();

        float m_TimeRealtimePaint;        
        public void PaintFoliage(RaycastHit brushHit)
        {
            if (Time.realtimeSinceStartup - m_TimeRealtimePaint < FoliageGlobals.EDITOR_DELAY_PAINT_FOLIAGE)
                return;
            m_TimeRealtimePaint = Time.realtimeSinceStartup;
            
            List<FoliageType> tempPaintEnabled = GetPaintEnabledFoliageTypesTree();
            if (tempPaintEnabled.Count > 0)
                PaintFoliageType(tempPaintEnabled, true, brushHit);

            tempPaintEnabled = GetPaintEnabledFoliageTypesGrass();
            if (tempPaintEnabled.Count > 0)
                PaintFoliageType(tempPaintEnabled, false, brushHit);

            RefreshData();
        }
        
        public void DeleteFoliage(RaycastHit hit, bool allTypes)
        {
            if (Time.realtimeSinceStartup - m_TimeRealtimePaint < FoliageGlobals.EDITOR_DELAY_PAINT_FOLIAGE)
                return;
            m_TimeRealtimePaint = Time.realtimeSinceStartup;

            bool anyRemoved = false;

            if (allTypes)
            {
                // Add the deleted cells
                Vector3 min = hit.point - new Vector3(m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize);
                Vector3 max = hit.point + new Vector3(m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize);
                FoliageCell.IterateMinMax(min, max, false, (int hash) =>
                {
                    m_PaintedCells.Add(hash);
                });

                for (int i = 0; i < m_FoliageTypes.Count; i++)
                {
                    if (m_FoliageData.RemoveInstances(m_FoliageTypes[i].m_Hash, hit.point, m_PaintParameters.m_BrushSize))
                        anyRemoved = true;
                }
            }
            else
            {
                // Add the deleted cells
                Vector3 min = hit.point - new Vector3(m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize);
                Vector3 max = hit.point + new Vector3(m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize, m_PaintParameters.m_BrushSize);
                FoliageCell.IterateMinMax(min, max, false, (int hash) =>
                {
                    m_PaintedCells.Add(hash);
                });

                // First remove trees
                List<FoliageType> tempPaintEnabled = GetPaintEnabledFoliageTypesTree();
                if (tempPaintEnabled.Count > 0)
                {
                    for (int i = 0; i < tempPaintEnabled.Count; i++)
                    {
                        if (m_FoliageData.RemoveInstances(tempPaintEnabled[i].m_Hash, hit.point, m_PaintParameters.m_BrushSize))
                            m_PaintedTypes.Add(tempPaintEnabled[i].m_Hash);
                    }
                }

                // Secondly remove grass
                tempPaintEnabled = GetPaintEnabledFoliageTypesGrass();
                if (tempPaintEnabled.Count > 0)
                {
                    for (int i = 0; i < tempPaintEnabled.Count; i++)
                    {
                        if (m_FoliageData.RemoveInstances(tempPaintEnabled[i].m_Hash, hit.point, m_PaintParameters.m_BrushSize))
                            m_PaintedTypes.Add(tempPaintEnabled[i].m_Hash);
                    }
                }
            }

            if (anyRemoved && allTypes)
            {
                RequestCountRefresh();
            }

            RefreshData();
        }

        private HashSet<int> m_PaintedCells = new HashSet<int>();
        private HashSet<int> m_PaintedTypes = new HashSet<int>();


        public void BeginPaint()
        {
            FoliageLog.i("Begun painting!");
            m_PaintedCells.Clear();
            m_PaintedTypes.Clear();
        }

        public void EndPaint()
        {
            FoliageLog.i("Ended painting!");
            if (m_PaintedCells.Count > 0)
            {
                FoliageLog.i("Refreshing: " + m_PaintedCells.Count + " cells for billboards.");

                GameObject owner = GameObject.Find("CRITIAS_BillboardHolder");

                if (owner == null)
                    owner = new GameObject("CRITIAS_BillboardHolder");

                foreach (int key in m_PaintedCells)
                {
                    FoliageCellData data;

                    if (m_FoliageData.m_FoliageData.TryGetValue(key, out data))
                    {
                        GenerateTreeBillboards(owner, data);
                    }
                    else
                    {
                        // If we couldn't find that cell it mean's it's empty, so destroy it
                        for (int i = 0; i < m_FoliageTypes.Count; i++)
                            FoliageWindTreeUtilities.DestroyBillboards(owner, key, m_FoliageTypes[i]);
                    }
                }

                m_PaintedCells.Clear();
            }

            if(m_PaintedTypes.Count > 0)
            {
                foreach (int key in m_PaintedTypes)
                    RefreshCachedCountForType(key, false);

                m_PaintedTypes.Clear();
            }

            // Only now request a label refresh
            RequestLabelRefresh();
        }

        private void PaintFoliageType(List<FoliageType> enabledType, bool areTrees, RaycastHit brushHit)
        {
            int existingInstanceCount = m_FoliageData.GetInstanceCountLocation(brushHit.point, m_PaintParameters.m_BrushSize, areTrees == false);
            
            // Instance count is count of instances per 1000(tree) / 100(grass) square meters
            float density = m_PaintParameters.m_FoliageDensity;

            int instanceCount;

            float area = (Mathf.PI * m_PaintParameters.m_BrushSize * m_PaintParameters.m_BrushSize);

            if (areTrees)
                instanceCount = (int)((area / 2000f) * density);
            else
                instanceCount = (int)((area / 50f) * density);

            // Required instance count
            int requiredInstanceCount = Mathf.Clamp(instanceCount - existingInstanceCount, 1, areTrees ? 10000 : 100000);            

            Vector3 brushHitNormalInverted = -brushHit.normal;

            for (int i = 0; i < requiredInstanceCount; i++)
            {
                // Sample the same locations every time
                RaycastHit hit;

                // Get a random, offset it to the hit's position and offset it a little up with the normal so that we don't touch the surface but are a little far from it
                Vector3 pos = Random.insideUnitCircle * m_PaintParameters.m_BrushSize;

                pos = Quaternion.LookRotation(brushHit.normal) * pos;
                pos += brushHit.point;
                pos += brushHit.normal * 0.5f;

                // Usefull for debugging rays
                // Debug.DrawRay(pos, brushHitNormalInverted * 2, Color.green, 2);

                if (Physics.Raycast(pos, brushHitNormalInverted, out hit, 2f, ~0))
                {
                    // If we only paint on static items, then skip the non-static objects
                    if (m_PaintParameters.m_StaticOnly && hit.collider != null && hit.collider.gameObject.isStatic == false)
                        continue;

                    PaintedColliderData paintedData = null;
                    Collider collider = hit.collider;

                    // If we don't have the collider key
                    if (m_PaintedColliderCache.ContainsKey(collider) == false)
                    {                        
                        PaintedColliderData data = new PaintedColliderData();

                        Terrain t = collider.gameObject.GetComponent<Terrain>();
                        data.m_IsTerrain = t != null;

                        if(data.m_IsTerrain)
                        {
                            data.m_Terrain = t;
                            data.m_TerrainName = t.name;

                            FoliageTerrainListener listener = t.gameObject.GetComponent<FoliageTerrainListener>();

                            if(listener == null)
                            {
                                data.m_TerrainListener = t.gameObject.AddComponent<FoliageTerrainListener>();
                                data.m_TerrainListener.m_FoliagePainter = this;
                            }
                        }

                        // Add the collider data
                        m_PaintedColliderCache.Add(collider, data);
                    }

                    paintedData = m_PaintedColliderCache[collider];

                    // If we have the slope filter and the ray does not scale withing the slope's angles continue
                    if (m_PaintParameters.m_SlopeFilter)
                    {
                        float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);

                        // If we're under the value or over it
                        if (slopeAngle < m_PaintParameters.m_SlopeAngles.x || slopeAngle > m_PaintParameters.m_SlopeAngles.y)
                            continue;
                    }

                    // Add the painted cells
                    m_PaintedCells.Add(FoliageCell.MakeHash(hit.point));

                    // Type
                    FoliageType type = enabledType[Random.Range(0, enabledType.Count)];
                    FoliageTypePaintInfo paintInfo = type.m_PaintInfo;

                    Quaternion standardRot = Quaternion.identity;

                    if (type.Type == EFoliageType.SPEEDTREE_TREE_BILLBOARD)
                    {
                        // Only allow around Y rotation and don't orientate on terrain normal
                        standardRot = Quaternion.Euler(0, Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y), 0);
                    }
                    else
                    {
                        if (paintInfo.m_SurfaceAlign)
                        {
                            Quaternion slopeOrientation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90, 0, 0);

                            // How much we orient towards the slope
                            standardRot = Quaternion.Slerp(standardRot, slopeOrientation, 
                                Random.Range(paintInfo.m_SurfaceAlignInfluence.x, paintInfo.m_SurfaceAlignInfluence.y));

                            // To the slope orientation apply the rotation
                            if(m_PaintParameters.m_RotateYOnly)
                            {
                                standardRot *= Quaternion.Euler(0, Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y), 0);
                            }
                            else
                            {
                                standardRot *= Quaternion.Euler(Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y), 
                                    Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y),
                                    Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y));
                            }
                        }
                        else
                        {
                            if (m_PaintParameters.m_RotateYOnly)
                            {
                                standardRot *= Quaternion.Euler(0, Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y), 0);
                            }
                            else
                            {
                                standardRot *= Quaternion.Euler(Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y),
                                    Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y),
                                    Random.Range(m_PaintParameters.m_RandomRotation.x, m_PaintParameters.m_RandomRotation.y));
                            }
                        }
                    }

                    Vector3 scale;
                    
                    if(m_PaintParameters.m_ScaleUniform)
                    {
                        float scaleUni = Random.Range(m_PaintParameters.m_ScaleUniformXYZ.x, m_PaintParameters.m_ScaleUniformXYZ.y);
                        scale = new Vector3(scaleUni, scaleUni, scaleUni);
                    }
                    else
                    {
                        float scaleX = Random.Range(m_PaintParameters.m_ScaleX.x, m_PaintParameters.m_ScaleX.y);
                        float scaleY = Random.Range(m_PaintParameters.m_ScaleY.x, m_PaintParameters.m_ScaleY.y);
                        float scaleZ = Random.Range(m_PaintParameters.m_ScaleZ.x, m_PaintParameters.m_ScaleZ.y);
                        scale = new Vector3(scaleX, scaleY, scaleZ);
                    }

                    // Y Offset it by the values
                    float YOffset = Random.Range(type.m_PaintInfo.m_YOffset.x, type.m_PaintInfo.m_YOffset.y); 
                    Vector3 foliageInstancePos = hit.point + (hit.normal * YOffset);
                    
                    // We don't need to populate all the data
                    FoliageInstance instance = new FoliageInstance();
                    instance.m_Position = foliageInstancePos;
                    instance.m_Rotation = standardRot;
                    instance.m_Scale = scale;

                    // Only add instances of the active types
                    if (paintedData.m_IsTerrain)
                    {
                        AddFoliageInstance(type.m_Hash, instance, FoliageGlobals.LABEL_TERRAIN_HAND_PAINTED + paintedData.m_TerrainName);

                        // Add the painted type
                        m_PaintedTypes.Add(type.m_Hash);
                    }
                    else
                    {
                        AddFoliageInstance(type.m_Hash, instance);

                        // Add the painted type
                        m_PaintedTypes.Add(type.m_Hash);
                    }
                }
            }
        }

#endif
#endregion

        
#region EDIT_TIME_RENDERING
#if UNITY_EDITOR

        public void CheckNullAndRequestUpdate()
        {
            for (int i = m_FoliageTypes.Count - 1; i >= 0; i--)
            {
                FoliageType type = m_FoliageTypes[i];

                if (type.m_Prefab == null)
                {
                    FoliageLog.e("Foliage type: " + type.m_Hash + " with name: " + type.m_Name + " has been nullified somehow!" +
                        " Don't delete foliage prefabs/trees before removing them from the foliage system please!");

                    // Remove it from the file references and from our type list
                    RemoveFoliageTypeHash(type.m_Hash, true);
                    RefreshFoliageTypeData();
                }
                else
                {
                    m_FoliageTypes[i].UpdateValues();
                }
            }
        }

        float m_TimeRequestUpdate;
        /**
         * Requests an update so that when we are moving around the scene even in edit mode,
         * we should only load the foliage around the player to save performance.
         */
        public void RequestUpdate()
        {
            if (UnityEditor.EditorApplication.isPlaying)
                return;

            if (Time.realtimeSinceStartup - m_TimeRequestUpdate < FoliageGlobals.EDITOR_DELAY_REQUEST_UPDATE)
                return;
            m_TimeRequestUpdate = Time.realtimeSinceStartup;

            // FoliageLog.i("Update requested!");

            for (int i = 0; i < m_FoliageTypes.Count; i++)
            {
                m_FoliageTypes[i].UpdateValues();
            }

            RefreshData();
        }
        
        private List<Bounds> m_DrawnCells = new List<Bounds>();
        private List<Bounds> m_DrawnCellsSubdivided = new List<Bounds>();

        private Camera m_DrawCamera;
        private Plane[] m_DrawPlanes;

        // [SerializeField] private Vector3 m_CachedCameraPos;
        // [SerializeField] private Quaternion m_CachedCameraRot;

        public void Update()
        {            
            if (UnityEditor.EditorApplication.isPlaying)
                return;

            if (m_FoliageData == null)
                LoadFromFile(false, false);

            // Perform a check to see if we have any nulls 
            CheckNullAndRequestUpdate();

            Camera cam;

            if (UnityEditor.SceneView.lastActiveSceneView && UnityEditor.SceneView.lastActiveSceneView.camera)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
            else
                return;
            
            m_DrawCamera = cam;
            
            // Debug.Log("Updating draw!");

            m_DrawnCells.Clear();
            m_DrawnCellsSubdivided.Clear();

            m_DrawPlanes = GeometryUtility.CalculateFrustumPlanes(m_DrawCamera);

            FoliageCell currentCell = new FoliageCell();
            currentCell.Set(cam.transform.position);
            
            // We'll only get through the neighboring cells
            FoliageCell.IterateNeighboring(currentCell, m_DrawNeighboringCells, (int hash) =>
            {
                FoliageCellData data;

                if (m_FoliageData.m_FoliageData.TryGetValue(hash, out data) && GeometryUtility.TestPlanesAABB(m_DrawPlanes, data.m_Bounds))
                {
                    ProcessCell(data);
                }
            });            
        }

        private void ProcessCell(FoliageCellData cell)
        {   
            m_DrawnCells.Add(cell.m_Bounds);
            
            UnityEngine.Rendering.ShadowCastingMode shadow = m_DrawTreeShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (var pair in cell.m_TypeHashLocationsEditor)
            {
                FoliageType type = GetFoliageTypeByHash(pair.Key);
                Dictionary<string, List<FoliageInstance>> instances = pair.Value;

                foreach (var insts in instances.Values)
                {
                    List<FoliageInstance> foliageInsts = insts;
                    List<Matrix4x4> mtx = new List<Matrix4x4>();

                    // We build them when we need them... Ok at edit time, not ok at runtime
                    for (int i = 0; i < foliageInsts.Count; i++)
                        mtx.Add(foliageInsts[i].GetWorldTransform());

                    if (mtx.Count < 1000)
                    {
                        Mesh m = null;
                        Material[] mats = null;

                        if (m_DrawTreeLastLOD)
                        {
                            int lastLod = type.m_RuntimeData.m_LODDataTree.Length - 1;

                            m = type.m_RuntimeData.m_LODDataTree[lastLod].m_Mesh;
                            mats = type.m_RuntimeData.m_LODDataTree[lastLod].m_Materials;
                        }
                        else
                        {
                            m = type.m_RuntimeData.m_LODDataTree[0].m_Mesh;
                            mats = type.m_RuntimeData.m_LODDataTree[0].m_Materials;
                        }
                        
                        for (int sub = 0; sub < m.subMeshCount; sub++)
                            Graphics.DrawMeshInstanced(m, sub, mats[sub], mtx, null, shadow);
                    }
                    else
                    {
                        // Split data
                        int ranges = Mathf.CeilToInt(mtx.Count / 1000f);

                        for (int i = 0; i < ranges; i++)
                        {
                            List<Matrix4x4> range = mtx.GetRange(i * 1000, i * 1000 + 1000 > mtx.Count ? mtx.Count - i * 1000 : 1000);

                            Mesh m = null;
                            Material[] mats = null;

                            if (m_DrawTreeLastLOD)
                            {
                                int lastLod = type.m_RuntimeData.m_LODDataTree.Length - 1;

                                m = type.m_RuntimeData.m_LODDataTree[lastLod].m_Mesh;
                                mats = type.m_RuntimeData.m_LODDataTree[lastLod].m_Materials;
                            }
                            else
                            {
                                m = type.m_RuntimeData.m_LODDataTree[0].m_Mesh;
                                mats = type.m_RuntimeData.m_LODDataTree[0].m_Materials;
                            }

                            for (int sub = 0; sub < m.subMeshCount; sub++)
                                Graphics.DrawMeshInstanced(m, sub, mats[sub], range, null, shadow);
                        }
                    }
                }
            }

            // Render subdivided data
            foreach (FoliageCellSubdividedData cellSubdiv in cell.m_FoliageDataSubdivided.Values)
            {
                if (cellSubdiv.m_Bounds.SqrDistance(m_DrawCamera.transform.position) < m_DrawGrassCellsDistance * m_DrawGrassCellsDistance
                        && GeometryUtility.TestPlanesAABB(m_DrawPlanes, cellSubdiv.m_Bounds))
                {
                    ProcessSubCell(cellSubdiv);
                }
            }
        }

        private void ProcessSubCell(FoliageCellSubdividedData cell)
        {            
            m_DrawnCellsSubdivided.Add(cell.m_Bounds);
            
            foreach (var pair in cell.m_TypeHashLocationsEditor)
            {
                FoliageType type = GetFoliageTypeByHash(pair.Key);
                Dictionary<string, List<FoliageInstance>> instances = pair.Value;

                foreach (var insts in instances.Values)
                {
                    List<FoliageInstance> foliageInsts = insts;
                    List<Matrix4x4> mtx = new List<Matrix4x4>();

                    // We build them when we need them... Ok at edit time, not ok at runtime
                    for (int i = 0; i < foliageInsts.Count; i++)
                        mtx.Add(foliageInsts[i].GetWorldTransform());

                    if (mtx.Count < 1000)
                    {
                        Mesh m = type.m_RuntimeData.m_LODDataGrass.m_Mesh;
                        Material mat = type.m_RuntimeData.m_LODDataGrass.m_Material;
                        
                        Graphics.DrawMeshInstanced(m, 0, mat, mtx, null, UnityEngine.Rendering.ShadowCastingMode.Off);
                    }
                    else
                    {
                        // Split data
                        int ranges = Mathf.CeilToInt(mtx.Count / 1000f);

                        for (int i = 0; i < ranges; i++)
                        {
                            List<Matrix4x4> range = mtx.GetRange(i * 1000, i * 1000 + 1000 > mtx.Count ? mtx.Count - i * 1000 : 1000);

                            Mesh m = type.m_RuntimeData.m_LODDataGrass.m_Mesh;
                            Material mat = type.m_RuntimeData.m_LODDataGrass.m_Material;
                            
                            Graphics.DrawMeshInstanced(m, 0, mat, range, null,UnityEngine.Rendering.ShadowCastingMode.Off);
                        }
                    }
                }
            }
        }
#endif
#endregion

    } // End class 'FoliagePainter'
} // End namespace 'Critias'
