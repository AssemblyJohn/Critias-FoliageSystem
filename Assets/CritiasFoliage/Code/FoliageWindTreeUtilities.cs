/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CritiasFoliage
{
    public class FoliageWindTreeUtilities
    {
        public static void ExtractBillboardData(BillboardRenderer billboardData, FoliageTypeSpeedTreeData data)
        {
            BillboardAsset billAsset = billboardData.billboard;

            Vector4 size = new Vector4(billAsset.width, billAsset.height, billAsset.bottom, 1);

            // UV Extraction
            Vector4[] uvs = billAsset.GetImageTexCoords();

            Vector2[] vertBillUV = new Vector2[uvs.Length * 4];
            Vector2[] horzBillUV = new Vector2[4];

            // Build the UV's
            for (int uvIdx = 0, billUv = 0; uvIdx < uvs.Length; uvIdx++, billUv += 4)
            {
                Vector4 extract = uvs[uvIdx];

                if (uvIdx == 0)
                {
                    horzBillUV[0] = new Vector2(extract.x, extract.y);
                    horzBillUV[1] = new Vector2(extract.x, extract.y) + new Vector2(0, Mathf.Abs(extract.w));
                    horzBillUV[2] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, Mathf.Abs(extract.w));
                    horzBillUV[3] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, 0);
                }

                // We are rotated
                if (extract.w < 0)
                {
                    vertBillUV[billUv + 0] = new Vector2(extract.x, extract.y);
                    vertBillUV[billUv + 1] = new Vector2(extract.x, extract.y) + new Vector2(0, Mathf.Abs(extract.w));
                    vertBillUV[billUv + 2] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, Mathf.Abs(extract.w));
                    vertBillUV[billUv + 3] = new Vector2(extract.x, extract.y) + new Vector2(-extract.z, 0);
                }
                else
                {
                    vertBillUV[billUv + 0] = new Vector2(extract.x, extract.y);
                    vertBillUV[billUv + 1] = new Vector2(extract.x, extract.y) + new Vector2(extract.z, 0);
                    vertBillUV[billUv + 2] = new Vector2(extract.x, extract.y) + new Vector2(extract.z, extract.w);
                    vertBillUV[billUv + 3] = new Vector2(extract.x, extract.y) + new Vector2(0, extract.w);
                }
            }

            // Build the UVs ready for the shader
            Vector4[] UV_U = new Vector4[8];
            Vector4[] UV_V = new Vector4[8];

            Vector2[] uv = vertBillUV;

            for (int i = 0; i < 8; i++)
            {
                // 4 by 4 elements
                UV_U[i].x = uv[4 * i + 0].x;
                UV_U[i].y = uv[4 * i + 1].x;
                UV_U[i].z = uv[4 * i + 2].x;
                UV_U[i].w = uv[4 * i + 3].x;

                UV_V[i].x = uv[4 * i + 0].y;
                UV_V[i].y = uv[4 * i + 1].y;
                UV_V[i].z = uv[4 * i + 2].y;
                UV_V[i].w = uv[4 * i + 3].y;
            }

            // Assign the data
            data.m_Size = size;
            data.m_VertBillboardU = UV_U;
            data.m_VertBillboardV = UV_V;
            data.m_BillboardRenderer = billboardData;
            data.m_BillboardMaterial = GenerateBillboardMaterial(data);
        }

        public static Material GenerateBillboardMaterial(FoliageTypeSpeedTreeData speedTreeData)
        {
            Material billboardMaterialBatch;

            // Try and retrieve it first
            billboardMaterialBatch = speedTreeData.m_BillboardMaterial;
            if (billboardMaterialBatch == null)
            {
                // Else create it
                Shader billboardShader = Shader.Find("Critias/WindTree_Billboard");
                billboardMaterialBatch = new Material(billboardShader);

                speedTreeData.m_BillboardMaterial = billboardMaterialBatch;
            }
            
            // Set the material universal data
            billboardMaterialBatch.SetTexture("_MainTex", speedTreeData.m_BillboardRenderer.sharedMaterial.GetTexture("_MainTex"));
            billboardMaterialBatch.SetTexture("_BumpMap", speedTreeData.m_BillboardRenderer.sharedMaterial.GetTexture("_BumpMap"));
            billboardMaterialBatch.SetColor("_HueVariation", speedTreeData.m_BillboardRenderer.sharedMaterial.GetColor("_HueVariation"));
            billboardMaterialBatch.SetVector("_Size", speedTreeData.m_Size);

            // Set the material UV data
            billboardMaterialBatch.SetVectorArray("_UVVert_U", speedTreeData.m_VertBillboardU);
            billboardMaterialBatch.SetVectorArray("_UVVert_V", speedTreeData.m_VertBillboardV);

            billboardMaterialBatch.SetVector("_UVHorz_U", speedTreeData.m_VertBillboardU[0]);
            billboardMaterialBatch.SetVector("_UVHorz_V", speedTreeData.m_VertBillboardV[0]);

            billboardMaterialBatch.enableInstancing = true;

            return billboardMaterialBatch;
        }

        public static void DestroyBillboards(GameObject owner, int cellHash, FoliageType type)
        {
            string name = string.Format("MeshCell[{0}_{1}]", cellHash, type.m_Prefab.name);

            Transform existing = owner.transform.Find(name);
            if (existing != null)
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPlaying == false)
                    Object.DestroyImmediate(existing.gameObject);
                else
                    Object.Destroy(existing.gameObject);
#else
                Object.Destroy(existing.gameObject);
#endif                
                existing = null;
            }
        }

		// TODO: Replace the system quad
		public static int[] m_SystemQuadTriangles = new int[] { 0, 1, 2, 1, 0, 3 };
		public static Vector3[] m_SystemQuadVertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(0.5f, 0.5f, 0.0f), new Vector3(0.5f, -0.5f, 0.0f), new Vector3(-0.5f, 0.5f, 0.0f) };
		public static Vector3[] m_SystemQuadNormals = new Vector3[] { new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, 0.0f, -1.0f) };
		public static Vector4[] m_SystemQuadTangents = new Vector4[] { new Vector4(1.0f, 0.0f, 0.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, -1.0f) };
		
        public static void GenerateBillboards(Bounds bounds, FoliageCell cell, GameObject owner, List<FoliageInstance> trees, FoliageType type, bool addLodGroup, float screenFadeSize, bool animatedCrossFade)
        {
            int[] originalTriangles = m_SystemQuadTriangles;
            
            GameObject meshObj = new GameObject();

            // Mark object as static
#if UNITY_EDITOR
            GameObjectUtility.SetStaticEditorFlags(meshObj, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
#endif            

            string name = string.Format("MeshCell[{0}_{1}]", cell.GetHashCode(), type.m_Prefab.name);

            Transform existing = owner.transform.Find(name);
            if (existing != null)
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPlaying == false)
                    Object.DestroyImmediate(existing.gameObject);
                else
                    Object.Destroy(existing.gameObject);
#else
                Object.Destroy(existing.gameObject);
#endif                
                existing = null;
            }

            meshObj.transform.SetParent(owner.transform);
            meshObj.name = name;

            var data = type.m_RuntimeData.m_SpeedTreeData;

            Vector3 worldScale = new Vector3(data.m_Size.x, data.m_Size.y, data.m_Size.x);
            
            // Set material
            MeshRenderer rend = meshObj.AddComponent<MeshRenderer>();
            rend.sharedMaterial = GenerateBillboardMaterial(type.m_RuntimeData.m_SpeedTreeData);

            MeshFilter filter = meshObj.AddComponent<MeshFilter>();

            Mesh treeMesh = new Mesh();
            treeMesh.name = meshObj.name;

            List<Vector4> m_TempWorldPositions = new List<Vector4>();
            List<Vector3> m_TempWorldScales = new List<Vector3>();
            List<Vector3> m_TempQuadVertices = new List<Vector3>();
            List<Vector4> m_TempQuadTangents = new List<Vector4>();
            List<Vector3> m_TempQuadNormals = new List<Vector3>();
            List<int> m_TempQuadIndices = new List<int>();
            
            for (int treeIndex = 0; treeIndex < trees.Count; treeIndex++)
            {
                Vector3 position = trees[treeIndex].m_Position;
                Vector3 scale = trees[treeIndex].m_Scale;
                float rot = trees[treeIndex].m_Rotation.eulerAngles.y * Mathf.Deg2Rad;
                
                // Offset world position, by the grounding factor
                Vector3 instancePos = position;

                // Don't use this, but offset in shader, so that we can have that correct hue
                // instancePos.y += data.m_Size.z;

                // Scale by the world scale too so that we don't have to do an extra multip
                Vector3 instanceScale = scale;
                instanceScale.Scale(worldScale);
                
                // Add the world and scale data
                for (int index = 0; index < 4; index++)
                {
                    Vector4 posAndRot = instancePos;
                    posAndRot.w = rot;

                    m_TempWorldPositions.Add(posAndRot);
                    m_TempWorldScales.Add(instanceScale);
                }

                // Add stanard quad data            
                m_TempQuadVertices.AddRange(m_SystemQuadVertices);
                m_TempQuadTangents.AddRange(m_SystemQuadTangents);
                m_TempQuadNormals.AddRange(m_SystemQuadNormals);

                // Calculate triangle indixes
                m_TempQuadIndices.AddRange(originalTriangles);
                for (int triIndex = 0; triIndex < 6; triIndex++)
                {
                    // Just add to the triangles the existing triangles + the new indices
                    m_TempQuadIndices[triIndex + 6 * treeIndex] = originalTriangles[triIndex] + 4 * treeIndex;
                }
            }

            treeMesh.Clear();

            // Set standard data
            treeMesh.SetVertices(m_TempQuadVertices);
            treeMesh.SetNormals(m_TempQuadNormals);
            treeMesh.SetTangents(m_TempQuadTangents);

            // Set the custom data
            treeMesh.SetUVs(1, m_TempWorldPositions);
            treeMesh.SetUVs(2, m_TempWorldScales);

            // Set triangles and do not calculate bounds
            treeMesh.SetTriangles(m_TempQuadIndices, 0, false);

            // Set the manually calculated bounds
            treeMesh.bounds = bounds;

            treeMesh.UploadMeshData(true);

            // Set the mesh
            filter.mesh = treeMesh;
            
            if (addLodGroup)
            {
                // Add the mesh' lod group
                LODGroup group = meshObj.AddComponent<LODGroup>();
                group.animateCrossFading = false;

                if (animatedCrossFade)
                {
                    group.fadeMode = LODFadeMode.CrossFade;
                    group.animateCrossFading = true;
                }
                else
                {
                    group.fadeMode = LODFadeMode.None;
                    group.animateCrossFading = false;
                }

                group.SetLODs(new LOD[] { new LOD(screenFadeSize, new Renderer[] { rend }) });
                group.RecalculateBounds();
            }

#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(meshObj.scene);
#endif
        }
    }
}