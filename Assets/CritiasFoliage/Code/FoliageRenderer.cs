/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;

using System;
using System.Reflection;

using UnityEngine;
using UnityEngine.Rendering;

namespace CritiasFoliage
{
    [Serializable]
    public class FoliageRenderSettings
    {
        // If we disable instancing we can't have grass. There's no point in drawing grass without instancing
        [Tooltip("If we should use GPU instancing. If false, no grass will be drawn.")]
        public bool m_DrawInstanced = true;

        // If instancing is disabled we can have the option to use light probes
        [Tooltip("If instancing is disabled each tree will be drawn individually and with light probes based on this setting.")]
        public bool m_UseLightProbes = true;

        [Tooltip("If we allow the usage of 'DrawMeshInstancedIndirect' as set per type in the foliage type inspector. Set to false to globally disable the indirect drawing.")]
        public bool m_AllowDrawInstancedIndirect = true;

        // Global grass density
        [Tooltip("Global grass density. Does not apply to trees.")]
        [Range(0.1f, 1f)]
        public float m_GrassDensity = 1f;

        [Tooltip("Transform used for the wind. The SpeedTree wind objects are going to be attached to this transform. Defaults to 'Camera.main.transform' if null.")]
        public Transform m_WindTransform;

        [Tooltip("Transform that we are going to use when bending the foliage. Set it to a dummy object at your character's feet.")]
        public Transform m_BendTransform;

        [Tooltip("Camera used for frustum culling. Defaults to 'Camera.main' if it is null.")]
        public Camera m_UsedCameraCulling;
        [Tooltip("Camera used for drawing. Defaults to 'null', that is everything is drawn to all cameras. Recomended option.")]
        public Camera m_UsedCameraDrawing;
        [Tooltip("Layer to use for rendering. Defaults to 'Default'")]
        public string m_UsedLayer = "Default";

        // TODO: See if this is a performance issue based on user response

        // Even if invisible, trees that are at a distance smaller than this one will be drawn 'ShadowsOnly'
        // Used to mitigate the shadow popping, until we'll have a decent shadow-caster culling algorithm
        public bool m_ApplyShadowPoppingCorrection = true;
        // How many meters we are going to apply the shadow popping correction
        public float m_ShadowPoppingCorrection = 40;
    }

    /**
     * Foliage renderer. Will be configurated by the 'Foliage Painter'. It is  dependant upon the foliage painter since 
     * in that painter we manage all the complicated data that is loaded in one way at runtime and in another way at
     * edit time.
     * 
     * 
     */
    public class FoliageRenderer : MonoBehaviour
    {
        // Foliage rendering settings
        public FoliageRenderSettings m_Settings;

        private FoliageDataRuntime m_FoliageData;
        private Dictionary<int, FoliageType> m_FoliageTypes = new Dictionary<int, FoliageType>();
        private FoliageType[] m_FoliageTypesArray;

        private float m_MaxDistanceGrass;
        private float m_MaxDistanceGrassSqr;

        private float m_MaxDistanceTree;
        private float m_MaxDistanceTreeSqr;
		
		private float m_MaxDistanceAll;
		private float m_MaxDistanceAllSqr;

        private int m_CellNeighborCount;

        // Property ID's
        private int m_ShaderIDCritiasFoliageDistance;
        private int m_ShaderIDCritiasFoliageDistanceSqr;
        private int m_ShaderIDCritiasFoliageLOD;
        private int m_ShaderIDCritiasFoliageLODSqr;

        // Indirect ID's
        private int m_ShaderIDCritiasInstanceBuffer;

        // Bend ID's
        private int m_ShaderIDCritiasBendPosition;
        private int m_ShaderIDCritiasBendDistance;
        private int m_ShaderIDCritiasBendScale;

        private void Awake()
        {
            // Extract the planes methods
            MethodInfo info = typeof(GeometryUtility).GetMethod("Internal_ExtractPlanes", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(Plane[]), typeof(Matrix4x4) }, null);
            ExtractPlanes = Delegate.CreateDelegate(typeof(Action<Plane[], Matrix4x4>), info) as Action<Plane[], Matrix4x4>;

            // Matrices init
            {
                m_MtxLODTemp = new Matrix4x4[FoliageGlobals.RENDER_MAX_LOD_COUNT][];
                m_MtxLODTempShadow = new Matrix4x4[FoliageGlobals.RENDER_BATCH_SIZE][];

                // Fill the matrices with the identity
                for (int i = 0; i < FoliageGlobals.RENDER_MAX_LOD_COUNT; i++)
                {
                    m_MtxLODTemp[i] = new Matrix4x4[FoliageGlobals.RENDER_BATCH_SIZE];
                    m_MtxLODTempShadow[i] = new Matrix4x4[FoliageGlobals.RENDER_BATCH_SIZE];

                    for (int mtx = 0; mtx < FoliageGlobals.RENDER_BATCH_SIZE; mtx++)
                    {
                        m_MtxLODTemp[i][mtx] = Matrix4x4.identity;
                        m_MtxLODTempShadow[i][mtx] = Matrix4x4.identity;
                    }
                }              
            }

            // Set the system data
            if(!m_Settings.m_WindTransform)
            {
                FoliageLog.i("Wind transform not found, defaulting to 'Camera.main.transform'!");
                m_Settings.m_WindTransform = Camera.main.transform;
            }

            if(!m_Settings.m_UsedCameraCulling)
            {
                FoliageLog.i("Culling camera not found, defaulting to 'Camera.main'!");
                m_Settings.m_UsedCameraCulling = Camera.main;
            }

            // Get shader data

            // Used for maximum distance
            m_ShaderIDCritiasFoliageDistance = Shader.PropertyToID("CRITIAS_MaxFoliageTypeDistance");
            m_ShaderIDCritiasFoliageDistanceSqr = Shader.PropertyToID("CRITIAS_MaxFoliageTypeDistanceSqr");

            // Used for LOD distance
            m_ShaderIDCritiasFoliageLOD = Shader.PropertyToID("CRITIAS_FoliageMaxDistanceLOD");
            m_ShaderIDCritiasFoliageLODSqr = Shader.PropertyToID("CRITIAS_FoliageMaxDistanceLODSqr");

            // Used for the indirect buffer
            m_ShaderIDCritiasInstanceBuffer = Shader.PropertyToID("CRITIAS_InstancePositionBuffer");

            // Used for the positions
            m_ShaderIDCritiasBendPosition = Shader.PropertyToID("CRITIAS_Bend_Position");
            m_ShaderIDCritiasBendDistance = Shader.PropertyToID("CRITIAS_Bend_Distance");
            m_ShaderIDCritiasBendScale = Shader.PropertyToID("CRITIAS_Bend_Scale");
        }

        /**
         * Prepare the renderer for the rendering.
         */
        public void InitRenderer(FoliagePainter painter, FoliageDataRuntime dataToRender, List<FoliageType> foliageTypes)
        {
            m_FoliageData = dataToRender;            
            
            foreach (FoliageType type in foliageTypes)
                FoliageTypeUtilities.BuildDataRuntime(painter, type, m_Settings.m_WindTransform);

            UpdateFoliageTypes(foliageTypes);
        }

        /**
         * Update the foliage types.
         */
        public void UpdateFoliageTypes(List<FoliageType> foliageTypes)
        {
            m_MaxDistanceGrass = 0;
            m_MaxDistanceTree = 0;
			m_MaxDistanceAll = 0;

            m_FoliageTypes.Clear();
            foreach (FoliageType type in foliageTypes)
            {
                if (type.IsGrassType == true && type.m_RenderInfo.m_MaxDistance > m_MaxDistanceGrass)
                    m_MaxDistanceGrass = type.m_RenderInfo.m_MaxDistance;
                else if (type.IsGrassType == false && type.m_RenderInfo.m_MaxDistance > m_MaxDistanceTree)
                    m_MaxDistanceTree = type.m_RenderInfo.m_MaxDistance;

                m_FoliageTypes.Add(type.m_Hash, type);
            }

            m_FoliageTypesArray = foliageTypes.ToArray();

            // Update the maximum grass distance
            m_MaxDistanceGrass = Mathf.Clamp(m_MaxDistanceGrass, 0, FoliageGlobals.FOLIAGE_MAX_GRASS_DISTANCE);
            m_MaxDistanceGrassSqr = m_MaxDistanceGrass * m_MaxDistanceGrass;

            m_MaxDistanceTree = Mathf.Clamp(m_MaxDistanceTree, 0, FoliageGlobals.FOLIAGE_MAX_TREE_DISTANCE);
            m_MaxDistanceTreeSqr = m_MaxDistanceTree * m_MaxDistanceTree;

			m_MaxDistanceAll = Mathf.Max(m_MaxDistanceGrass, m_MaxDistanceTree);
			m_MaxDistanceAllSqr = m_MaxDistanceAll * m_MaxDistanceAll;
			
            // Cell recursion count based on the maximum value of all values
            m_CellNeighborCount = Mathf.CeilToInt(m_MaxDistanceAll / FoliageGlobals.CELL_SIZE);

            FoliageLog.i("Neighbor cell count: " + m_CellNeighborCount);
        }
        
        private Action<Plane[], Matrix4x4> ExtractPlanes;
        private Plane[] m_CameraPlanes = new Plane[6];
        
        private FoliageCell currentCell;

        struct FoliageRendererStats
        {
            public int m_ProcessedCells;
            public int m_ProcessedInstances;
            public int m_ProcessedDrawCalls;
            public int m_ProcessedCellsSubdiv;

            public void Reset()
            {
                m_ProcessedCells = 0;
                m_ProcessedCellsSubdiv = 0;
                m_ProcessedInstances = 0;
                m_ProcessedDrawCalls = 0;
            }
        }

        private FoliageRendererStats m_DrawStats = new FoliageRendererStats();

        private Camera m_CurrentFrameCameraCull;
        private Camera m_CurrentFrameCameraDraw;
        private Vector3 m_CurrentFrameCameraPosition;
        private int m_CurrentFrameLayer;
        private bool m_CurrentFrameAllowIndirect;
        private Vector3 m_CurrentFrameBendPosition;

        void Update()
        {
#if UNITY_EDITOR
            FoliageLog.Assert(m_FoliageData != null);
#endif
            // Camera used for culling
            m_CurrentFrameCameraCull = m_Settings.m_UsedCameraCulling;

            // Camera used for drawing
            m_CurrentFrameCameraDraw = m_Settings.m_UsedCameraDrawing;
            m_CurrentFrameLayer = LayerMask.NameToLayer(m_Settings.m_UsedLayer);

            // Extract the planes
            ExtractPlanes(m_CameraPlanes, m_CurrentFrameCameraCull.projectionMatrix * m_CurrentFrameCameraCull.worldToCameraMatrix);

            // Set the position
            m_CurrentFrameCameraPosition = m_CurrentFrameCameraCull.transform.position;

            // Set the bend position
            m_CurrentFrameBendPosition = m_Settings.m_BendTransform != null ? m_Settings.m_BendTransform.position : m_CurrentFrameCameraPosition;

            // Current cell position
            currentCell.Set(m_CurrentFrameCameraPosition);

            m_CurrentFrameAllowIndirect = m_Settings.m_AllowDrawInstancedIndirect;

            m_DrawStats.Reset();

            // Copy the wind for SpeedTree types
            for (int i = 0; i < m_FoliageTypesArray.Length; i++)
            {
                if(m_FoliageTypesArray[i].IsSpeedTreeType)
                    m_FoliageTypesArray[i].CopyBlock();
            }

            bool applyShadowCorrection = m_Settings.m_ApplyShadowPoppingCorrection;
            float shadowCorrectionDistanceSqr = m_Settings.m_ShadowPoppingCorrection * m_Settings.m_ShadowPoppingCorrection;
            
            // We iterate only as many cells as we need
            FoliageCell.IterateNeighboring(currentCell, m_CellNeighborCount, (int hash) =>
            {
                FoliageCellDataRuntime data;
                
                if(m_FoliageData.m_FoliageData.TryGetValue(hash, out data))
                {
                    // If it is within distance and in the frustum
                    float distanceSqr = data.m_Bounds.SqrDistance(m_CurrentFrameCameraPosition);

					// Check for the maximum distance
					if(distanceSqr <= m_MaxDistanceAllSqr && GeometryUtility.TestPlanesAABB(m_CameraPlanes, data.m_Bounds))
					{	
						// Process the big cells if we are withing the tree distance
						if(distanceSqr <= m_MaxDistanceTreeSqr)
						{						
							ProcessCellTree(data, distanceSqr, applyShadowCorrection, shadowCorrectionDistanceSqr, false);	
						}
						
						// Process subdivided cells only if we have instancing enabled and only if it is within the distance proximity for grass
						if(distanceSqr <= m_MaxDistanceGrassSqr && m_Settings.m_DrawInstanced)
						{							
							ProcessCellGrass(data);
						}
						
                        m_DrawStats.m_ProcessedCells++;
					}
                    else if(distanceSqr <= shadowCorrectionDistanceSqr)
                    {
                        ProcessCellTree(data, distanceSqr, applyShadowCorrection, shadowCorrectionDistanceSqr, true);
                    }
                }
            });

#if UNITY_EDITOR
            if(Time.frameCount % 300 == 0)
            {
                FoliageLog.i(string.Format("Proc cells:{0} Proc subdiv cells: {1} Proc tree instances: {2} Proc draw calls: {3}",
                    m_DrawStats.m_ProcessedCells,
                    m_DrawStats.m_ProcessedCellsSubdiv,
                    m_DrawStats.m_ProcessedInstances,
                    m_DrawStats.m_ProcessedDrawCalls ));
            }
#endif
        }

        void OnDisable()
        {
            // Dispose all the GPU data
            m_CachedGPUBufferData.Dispose();           
        }

        private void ProcessCellGrass(FoliageCellDataRuntime runtimeCell)
		{			
            for (int i = 0, len = runtimeCell.m_FoliageDataSubdivided.Length; i < len; i++)
            {
                FoliageCellSubdividedDataRuntime runtimeCellSubdivided = runtimeCell.m_FoliageDataSubdivided[i].Value;
                float subdivDistance = runtimeCellSubdivided.m_Bounds.SqrDistance(m_CurrentFrameCameraPosition);

                if (subdivDistance <= m_MaxDistanceGrassSqr && GeometryUtility.TestPlanesAABB(m_CameraPlanes, runtimeCellSubdivided.m_Bounds))
                {
                    ProcessSubdividedCell(runtimeCell, runtimeCellSubdivided, subdivDistance);
                    m_DrawStats.m_ProcessedCellsSubdiv++;
                }
            }
		}
		
        // Matrix for each lod        
        private Matrix4x4[][] m_MtxLODTemp;
        private int[] m_MtxLODTempCount = new int[FoliageGlobals.RENDER_MAX_LOD_COUNT];

        // Matrix for shadow correction
        private Matrix4x4[][] m_MtxLODTempShadow;
        private int[] m_MtxLODTempShadowCount = new int[FoliageGlobals.RENDER_MAX_LOD_COUNT];

        private void ProcessCellTree(FoliageCellDataRuntime cell, float distanceSqr, bool shadowCorrection, float shadowCorrectionDistanceSqr, bool shadowOnly)
        {            
            // Process tree cell content with types
            for (int foliageType = 0, foliageTypeCount = cell.m_TypeHashLocationsRuntime.Length; foliageType < foliageTypeCount; foliageType++)
            {
                FoliageType type = m_FoliageTypes[cell.m_TypeHashLocationsRuntime[foliageType].Key];                
                var batches = cell.m_TypeHashLocationsRuntime[foliageType].Value.m_EditTime;

                float maxDistance = type.m_RenderInfo.m_MaxDistance;
                float maxDistanceSqr = maxDistance * maxDistance;

                MaterialPropertyBlock mpb = type.m_RuntimeData.m_TypeMPB;

                // Set the global per-type data
                mpb.SetFloat(m_ShaderIDCritiasFoliageDistance, maxDistance);
                mpb.SetFloat(m_ShaderIDCritiasFoliageDistanceSqr, maxDistanceSqr);

                // Set the per-lod data
                FoliageTypeLODTree[] treeLods = type.m_RuntimeData.m_LODDataTree;

                bool castAnyShadow = type.m_RenderInfo.m_CastShadow;
                ShadowCastingMode shadow = castAnyShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;

                // Reset the temp count
                for (int i = 0; i < m_MtxLODTempCount.Length; i++)
                {
                    m_MtxLODTempCount[i] = 0;
                    m_MtxLODTempShadowCount[i] = 0;
                }
                
                float x, y, z;
                float dist;
                
                for (int treeIndex = 0, treeIndexCount = batches.Length; treeIndex < treeIndexCount; treeIndex++)
                {
                    // Test the distance and the frustum cull
                    Vector3 pos = batches[treeIndex].m_Position;

                    x = pos.x - m_CurrentFrameCameraPosition.x;
                    y = pos.y - m_CurrentFrameCameraPosition.y;
                    z = pos.z - m_CurrentFrameCameraPosition.z;

                    dist = x * x + y * y + z * z;
                    
                    if (dist <= maxDistanceSqr
                            && GeometryUtility.TestPlanesAABB(m_CameraPlanes, batches[treeIndex].m_Bounds) && shadowOnly == false)
                    {
                        // Get the current LOD
                        int currentLOD = GetCurrentLOD(ref treeLods, Mathf.Sqrt(dist));
                        int currentIdx = m_MtxLODTempCount[currentLOD];

                        // Add it to the LOD matrix
                        m_MtxLODTemp[currentLOD][currentIdx].m00 = batches[treeIndex].m_Matrix.m00;
                        m_MtxLODTemp[currentLOD][currentIdx].m01 = batches[treeIndex].m_Matrix.m01;
                        m_MtxLODTemp[currentLOD][currentIdx].m02 = batches[treeIndex].m_Matrix.m02;
                        m_MtxLODTemp[currentLOD][currentIdx].m03 = batches[treeIndex].m_Matrix.m03;
                        m_MtxLODTemp[currentLOD][currentIdx].m10 = batches[treeIndex].m_Matrix.m10;
                        m_MtxLODTemp[currentLOD][currentIdx].m11 = batches[treeIndex].m_Matrix.m11;
                        m_MtxLODTemp[currentLOD][currentIdx].m12 = batches[treeIndex].m_Matrix.m12;
                        m_MtxLODTemp[currentLOD][currentIdx].m13 = batches[treeIndex].m_Matrix.m13;
                        m_MtxLODTemp[currentLOD][currentIdx].m20 = batches[treeIndex].m_Matrix.m20;
                        m_MtxLODTemp[currentLOD][currentIdx].m21 = batches[treeIndex].m_Matrix.m21;
                        m_MtxLODTemp[currentLOD][currentIdx].m22 = batches[treeIndex].m_Matrix.m22;
                        m_MtxLODTemp[currentLOD][currentIdx].m23 = batches[treeIndex].m_Matrix.m23;                        

                        // Increment the LOD count
                        m_MtxLODTempCount[currentLOD]++;

                        // If we reached 1000 elements, submit the batch
                        if(m_MtxLODTempCount[currentLOD] >= FoliageGlobals.RENDER_BATCH_SIZE)
                        {                            
                            // Issue the draw and reset the count
                            IssueBatchLOD(m_MtxLODTemp[currentLOD], m_MtxLODTempCount[currentLOD], treeLods[currentLOD], mpb, shadow);
                            m_MtxLODTempCount[currentLOD] = 0;
                        }                        
                    }
                    else if(castAnyShadow && shadowCorrection && dist <= shadowCorrectionDistanceSqr)
                    {
                        int currentLOD = GetCurrentLOD(ref treeLods, Mathf.Sqrt(dist));
                        int currentIdx = m_MtxLODTempShadowCount[currentLOD];

                        // Add it to the shadow matrix
                        m_MtxLODTempShadow[currentLOD][currentIdx].m00 = batches[treeIndex].m_Matrix.m00;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m01 = batches[treeIndex].m_Matrix.m01;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m02 = batches[treeIndex].m_Matrix.m02;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m03 = batches[treeIndex].m_Matrix.m03;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m10 = batches[treeIndex].m_Matrix.m10;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m11 = batches[treeIndex].m_Matrix.m11;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m12 = batches[treeIndex].m_Matrix.m12;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m13 = batches[treeIndex].m_Matrix.m13;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m20 = batches[treeIndex].m_Matrix.m20;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m21 = batches[treeIndex].m_Matrix.m21;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m22 = batches[treeIndex].m_Matrix.m22;
                        m_MtxLODTempShadow[currentLOD][currentIdx].m23 = batches[treeIndex].m_Matrix.m23;

                        // Increment count
                        m_MtxLODTempShadowCount[currentLOD]++;

                        if (m_MtxLODTempShadowCount[currentLOD] >= FoliageGlobals.RENDER_BATCH_SIZE)
                        {
                            IssueBatchLOD(m_MtxLODTempShadow[currentLOD], m_MtxLODTempShadowCount[currentLOD], treeLods[currentLOD], mpb, ShadowCastingMode.ShadowsOnly);
                            m_MtxLODTempShadowCount[currentLOD] = 0;
                        }
                    }
                }
                
                // If we have any leftovers
                for(int i = 0; i < treeLods.Length; i++)
                {
                    if(m_MtxLODTempCount[i] > 0)
                    {
                        // Issue the draw and reset the count
                        IssueBatchLOD(m_MtxLODTemp[i], m_MtxLODTempCount[i], treeLods[i], mpb, shadow);
                        m_MtxLODTempCount[i] = 0;
                    }

                    if(m_MtxLODTempShadowCount[i] > 0)
                    {
                        IssueBatchLOD(m_MtxLODTempShadow[i], m_MtxLODTempShadowCount[i], treeLods[i], mpb, ShadowCastingMode.ShadowsOnly);
                        m_MtxLODTempShadowCount[i] = 0;
                    }
                }
                
                m_DrawStats.m_ProcessedInstances += batches.Length;
            }
        }    
        
        private void IssueBatchLOD(Matrix4x4[] batch, int count, FoliageTypeLODTree lod, MaterialPropertyBlock mpb, ShadowCastingMode shadow)
        {
            // Set the MPB data
            mpb.SetFloat(m_ShaderIDCritiasFoliageLOD, lod.m_EndDistance);
            mpb.SetFloat(m_ShaderIDCritiasFoliageLODSqr, lod.m_EndDistance * lod.m_EndDistance);

            Mesh mesh = lod.m_Mesh;
            Material[] materials = lod.m_Materials;

            if (m_Settings.m_DrawInstanced)
            {
                for (int sub = 0, subCount = mesh.subMeshCount; sub < subCount; sub++)
                {
                    Graphics.DrawMeshInstanced(mesh, sub, materials[sub], batch, count, mpb, shadow, true, m_CurrentFrameLayer, m_CurrentFrameCameraDraw);
                    m_DrawStats.m_ProcessedDrawCalls++;
                }
            }
            else
            {
                for (int sub = 0, subCount = mesh.subMeshCount; sub < subCount; sub++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        Graphics.DrawMesh(mesh, batch[i], materials[sub], m_CurrentFrameLayer, m_CurrentFrameCameraDraw, sub, mpb, shadow, true, null, m_Settings.m_UseLightProbes);
                        m_DrawStats.m_ProcessedDrawCalls++;
                    }
                }
            }

            m_DrawStats.m_ProcessedInstances += count;
        }

        private int GetCurrentLOD(ref FoliageTypeLODTree[] treeLods, float treeDistance)
        {
            for(int i = 0; i < treeLods.Length; i++)
            {
                if (treeDistance < treeLods[i].m_EndDistance)
                    return i;
            }

            return treeLods.Length - 1;
        }

        class GPUBufferCellCachedData : IDisposable
        {
            public ComputeBuffer m_BufferPositions;
            public ComputeBuffer m_BufferArguments;
            public uint m_IndexCount;
            public uint m_InstanceCount;

            public void Dispose()
            {
                if (m_BufferPositions != null)
                {
                    m_BufferPositions.Release();
                    m_BufferPositions = null;
                }

                if (m_BufferArguments != null)
                {
                    m_BufferArguments.Release();
                    m_BufferArguments = null;
                }
            }
        }

        private FoliageDisposableCache<long, GPUBufferCellCachedData> m_CachedGPUBufferData = 
            new FoliageDisposableCache<long, GPUBufferCellCachedData>(FoliageGlobals.RENDER_MAX_GPU_INDIRECT_BATCH_COUNT, FoliageGlobals.RENDER_MAX_GPU_INDIRECT_EVICTION_COUNT);
        
        private uint[] m_TempDrawArgs = new uint[5] { 0, 0, 0, 0, 0 };
       
        private void ProcessSubdividedCell(FoliageCellDataRuntime cell, FoliageCellSubdividedDataRuntime cellSubdivided, float distance)
        {                                    
            for (int foliageType = 0, foliageTypeCount = cellSubdivided.m_TypeHashLocationsRuntime.Length; foliageType < foliageTypeCount; foliageType++)
            {
                FoliageType type = m_FoliageTypes[cellSubdivided.m_TypeHashLocationsRuntime[foliageType].Key];

                float maxTypeDist = type.m_RenderInfo.m_MaxDistance * type.m_RenderInfo.m_MaxDistance;

                if (distance <= maxTypeDist)
                {                    
                    var batches = cellSubdivided.m_TypeHashLocationsRuntime[foliageType].Value.m_EditTime;
                    
                    // Set the MPB values that are universal for all grass types
                    MaterialPropertyBlock mpb = type.m_RuntimeData.m_TypeMPB;

                    mpb.SetFloat(m_ShaderIDCritiasFoliageDistance, type.m_RenderInfo.m_MaxDistance);
                    mpb.SetFloat(m_ShaderIDCritiasFoliageDistanceSqr, maxTypeDist);

                    // TODO: Set bend data if we have it for this type
                    if(type.m_EnableBend)
                    {
                        mpb.SetFloat(m_ShaderIDCritiasBendDistance, type.m_BendDistance);
                        mpb.SetFloat(m_ShaderIDCritiasBendScale, type.m_BendPower);
                        mpb.SetVector(m_ShaderIDCritiasBendPosition, m_CurrentFrameBendPosition);
                    }

                    // Get data from the type
                    Mesh mesh = type.m_RuntimeData.m_LODDataGrass.m_Mesh;
                    Material mat = type.m_RuntimeData.m_LODDataGrass.m_Material;

                    // Only if we have the type for rendering indirect
                    if (type.RenderIndirect && m_CurrentFrameAllowIndirect)
                    {                        
                        long indirectCachedDataKey = ((((long)cell.m_Position.GetHashCode()) << 32)| ((long)cellSubdivided.m_Position.GetHashCode())) + type.m_Hash; // * 0xF01226E02D41B

                        if (m_CachedGPUBufferData.ContainsKey(indirectCachedDataKey) == false)
                        {
                            GPUBufferCellCachedData data = new GPUBufferCellCachedData();

                            // Merge the buffers
                            Matrix4x4[] allInstances;

                            // Build all the data
                            if (batches.Length > 1)
                            {
                                int totalCount = 0;
                                for (int batchIdx = 0; batchIdx < batches.Length; batchIdx++)
                                    totalCount += batches[batchIdx].Length;

                                List<Matrix4x4> concat = new List<Matrix4x4>(totalCount);

                                for (int batchIdx = 0; batchIdx < batches.Length; batchIdx++)
                                    concat.AddRange(batches[batchIdx]);

                                allInstances = concat.ToArray();
                            }
                            else
                            {
                                allInstances = batches[0];
                            }

                            // Set the position data
                            data.m_BufferPositions = new ComputeBuffer(allInstances.Length, 64);
                            data.m_BufferPositions.SetData(allInstances);
                            
                            // Set the arguments
                            data.m_BufferArguments = new ComputeBuffer(1, m_TempDrawArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                            data.m_IndexCount = mesh.GetIndexCount(0);

                            // Set the instance count
                            data.m_InstanceCount = (uint)allInstances.Length;

                            // Add the data. The cache will take care of clearing the data that is in excess
                            m_CachedGPUBufferData.Add(indirectCachedDataKey, data);                            
                        }

                        GPUBufferCellCachedData indirectData = m_CachedGPUBufferData[indirectCachedDataKey];

                        // Set the buffer positions
                        mpb.SetBuffer(m_ShaderIDCritiasInstanceBuffer, indirectData.m_BufferPositions);

                        // Set the draw count and send it
                        m_TempDrawArgs[0] = indirectData.m_IndexCount;
                        m_TempDrawArgs[1] = (uint)(indirectData.m_InstanceCount * m_Settings.m_GrassDensity);
                        indirectData.m_BufferArguments.SetData(m_TempDrawArgs);

                        // Draw without shadows
                        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, cellSubdivided.m_Bounds, indirectData.m_BufferArguments, 0, mpb, ShadowCastingMode.Off, true, m_CurrentFrameLayer, m_CurrentFrameCameraDraw);
                    }
                    else
                    {
                        // Cast shadow only if the type allows
                        ShadowCastingMode castShadow = type.m_RenderInfo.m_CastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;

                        // Pass with the MPB the data related to per-type distance
                        for (int i = 0, batchCount = batches.Length; i < batchCount; i++)
                        {
                            Graphics.DrawMeshInstanced(mesh, 0, mat, batches[i], (int)(batches[i].Length * m_Settings.m_GrassDensity), mpb, castShadow, true, m_CurrentFrameLayer, m_CurrentFrameCameraDraw);
                            m_DrawStats.m_ProcessedDrawCalls++;
                        }
                    }                    
                }
            }
        }
    }
}
