/** Copyright (c) Lazu Ioan-Bogdan */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace CritiasFoliage
{
#if UNITY_EDITOR
    public class FoliagePainterHotkey : ScriptableObject
    {
        [MenuItem("Critias Foliage/Create Painter")]
        static void CreatePainter()
        {
            bool create = true;

            if(FindObjectOfType<FoliagePainter>() != null)
            {
                bool clean = EditorUtility.DisplayDialog("Warning!", "Foliage painter detected! Delete exisisting and create a clean new one?", "Yes", "No");

                // Clean the existing object
                if (clean)
                    Object.DestroyImmediate(FindObjectOfType<FoliagePainter>().gameObject);
                else
                    create = false;
            }

            if(create)
            {
                GameObject painter = new GameObject("Critias_Painter");

                // Create the object data
                FoliagePainter ptr = painter.AddComponent<FoliagePainter>();
                ptr.m_FoliageRenderer = painter.AddComponent<FoliageRenderer>();
                ptr.m_FoliageColliders = painter.AddComponent<FoliageColliders>();
            }

            // Else just select
            if(FindObjectOfType<FoliagePainter>() != null)
            {
                Selection.activeTransform = FindObjectOfType<FoliagePainter>().transform;
            }
        }

        [MenuItem("Critias Foliage/Save _F4")]
        static void SaveGrass()
        {
            FoliagePainter ptr = FindObjectOfType<FoliagePainter>();

            if (ptr)
            {
                Debug.Log("Saving grass to disk!");
                ptr.SaveToFile();
            }
            else
            {
                Debug.LogError("Could not save grass to disk! No 'FoliagePainter' component found!");
            }
        }

        // TODO: Add it for V1.1, since it prolly won't be 100% required for this version since
        // people will mostly add the trees from external sources
        /*
        [MenuItem("Critias Foliage/Undo %x")]
        static void Undo()
        {            
            Debug.Log("Undo grass!");
        }
        */
    }
#endif

    public enum EBeginMode
    {
        BEGIN_VERTICAL,
        BEGIN_HORIZONTAL,
        BEGIN_SCROLLVIEW,
    }


    public class ScopedLayout : System.IDisposable
    {
        readonly EBeginMode m_BeginMode;

        public ScopedLayout(EBeginMode mode)
        {
            switch(mode)
            {
                case EBeginMode.BEGIN_HORIZONTAL:
                    EditorGUILayout.BeginHorizontal();
                    break;
                case EBeginMode.BEGIN_VERTICAL:
                    EditorGUILayout.BeginVertical();
                    break;
            }

            m_BeginMode = mode;
        }

        public ScopedLayout(System.Action action, EBeginMode mode)
        {
            action();
            m_BeginMode = mode;
        }

        private bool m_DisposedValue;
        public void Dispose()
        {
            if (!m_DisposedValue)
            {
                switch(m_BeginMode)
                {
                    case EBeginMode.BEGIN_HORIZONTAL:
                        EditorGUILayout.EndHorizontal();
                        break;
                    case EBeginMode.BEGIN_VERTICAL:
                        EditorGUILayout.EndVertical();
                        break;
                    case EBeginMode.BEGIN_SCROLLVIEW:
                        EditorGUILayout.EndScrollView();
                        break;
                    default:
                        FoliageLog.Assert(false, "Wrong begin type!");
                        break;
                }

                m_DisposedValue = true;
            }
        }
    }

    [ExecuteInEditMode]
    [CustomEditor(typeof(FoliagePainter))]
    public class FoliagePainterEditor : Editor
    {        
        void OnEnable()
        {
            
        }

        private bool m_BaseFoldout;

        public override void OnInspectorGUI()
        {            
            if (EditorApplication.isPlaying)
                return;           

            FoliagePainter painter = target as FoliagePainter;

            EditorGUILayout.Space();

            InspectorHelp();            

            InspectorBrush();

            EditorGUILayout.Space();

            InspectorFoliageTypes();

            EditorGUILayout.Space();

            InspectorFoliageInfo();

            EditorGUILayout.Space();            

            InspectorAdvanced();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUI.changed)
            {
                SceneView.RepaintAll();

                // Mark the scene dirty
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(painter.gameObject.scene);
            }
            
            /*
            m_BaseFoldout = EditorGUILayout.Foldout(m_BaseFoldout, "Base GUI (For Debugging)");
            if (m_BaseFoldout)
                base.OnInspectorGUI();
            */
        }

        void InspectorAdvanced()
        {
            FoliagePainter painter = target as FoliagePainter;

            painter.m_EditorFoldoutAdvanced = EditorGUILayout.Foldout(painter.m_EditorFoldoutAdvanced, "Advanced");
            if (!painter.m_EditorFoldoutAdvanced)
                return;

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                if (GUILayout.Button("Extractor", GUILayout.Height(40)))
                {
                    var extractor = ScriptableWizard.DisplayWizard<FoliageExtracterWindow>("Extract Foliage", "Extract");
                    extractor.Init((List<GameObject> extract, bool autoExtract, bool autoDisable, bool autoDelete) =>
                    {
                        FoliageLog.i("Extracting: " + extract.Count);

                        // Pass it to the foliage extracter                            
                        FoliageExtracterEditor.ExtractFoliage(painter, extract, autoExtract, autoDisable, autoDelete);

                        // Post a save
                        painter.SaveToFile();                        
                    });
                }

                if(GUILayout.Button("Extractor Terrain Details", GUILayout.Height(40)))
                {
                    var extractor = ScriptableWizard.DisplayWizard<FoliageExtracterDetailsWindow>("Extract Terrain Details", "Extract");

                    extractor.Init(painter, (List<FoliageDetailExtracterMapping> toExtract, IEnumerable<Terrain> terrains, bool disable, bool delete) =>
                    {
                        // Extract
                        FoliageExtracterEditor.ExtractDetailsFromTerrains(painter, terrains, toExtract, disable, delete);

                        // Post a save
                        painter.SaveToFile();
                    });
                }

                // Show all labels
                IEnumerable<string> labels = painter.GetFoliageLabelsCached();

                bool shownLabels = false;

                if (labels != null)
                {
                    foreach (string label in labels)
                    {
                        if (!shownLabels)
                        {
                            EditorGUILayout.LabelField("Labels: ");
                            shownLabels = true;
                        }

                        string text = "Remove: ";
                        string terrainName = "";
                        string tooltip = "";
                        bool isTerrainLabel = true;

                        if(label.StartsWith(FoliageGlobals.LABEL_TERRAIN_EXTRACTED))
                        {
                            terrainName = label.Replace(FoliageGlobals.LABEL_TERRAIN_EXTRACTED, "");
                            text += "(T) '" + terrainName + "' (Extracted)";
                            tooltip = "Terrain extracted data. Can use the button on the left to stick the floating foliage to the terrain. Use this button to remove it.";
                        }
                        else if(label.StartsWith(FoliageGlobals.LABEL_TERRAIN_DETAILS_EXTRACTED))
                        {
                            terrainName = label.Replace(FoliageGlobals.LABEL_TERRAIN_DETAILS_EXTRACTED, "");
                            text += "(T) '" + terrainName + "' (Extracted Details)";
                            tooltip = "Terrain extracted details. Can use the button on the left to stick the floating foliage to the terrain. Use this button to remove it.";
                        }
                        else if(label.StartsWith(FoliageGlobals.LABEL_TERRAIN_HAND_PAINTED))
                        {
                            terrainName = label.Replace(FoliageGlobals.LABEL_TERRAIN_HAND_PAINTED, "");
                            text += "(T) '" + terrainName + "' (Hand Painted)";
                            tooltip = "Terrain painted details. Can use the button on the left to stick the floating foliage to the terrain. Use this button to remove it.";
                        }
                        else
                        {
                            text += label;
                            tooltip = "Data with a label. Use this button to remove it.";
                            isTerrainLabel = false;
                        }

                        if (isTerrainLabel)
                        {
                            using (new ScopedLayout(EBeginMode.BEGIN_HORIZONTAL))
                            {
                                if (GUILayout.Button(new GUIContent(text, tooltip)))
                                {
                                    bool sure = EditorUtility.DisplayDialog("Warning!", "This will remove all the foliage with the terrain label: '" + terrainName + "' Are you sure?", 
                                        "Yes", "No");

                                    if (sure)
                                        painter.RemoveFoliageInstancesLabeled(label);
                                }

                                if(GUILayout.Button(new GUIContent("Stick", "Stick the foliage to the terrain named '" + terrainName + "' if we changed it's height."), GUILayout.Width(40)))
                                {
                                    GameObject obj = GameObject.Find(terrainName);

                                    if (obj == null)
                                        EditorUtility.DisplayDialog("Error!", "Could not find terrain named: '" + terrainName + "' to stick the foliage to!", "Ok");
                                    else if(obj.GetComponent<Terrain>() == null)
                                        EditorUtility.DisplayDialog("Error!", "The object named: '" + terrainName + "' is not a terrain! We can only stick foliage to a terrain!", "Ok");
                                    else
                                        painter.StickLabeledGrassToTerrain(label, obj.GetComponent<Terrain>());
                                }
                            }
                        }
                        else
                        {
                            if (GUILayout.Button(new GUIContent(text, tooltip)))
                            {
                                bool sure = EditorUtility.DisplayDialog("Warning!", "This will remove all the foliage with the label: '" + label + "' Are you sure?", "Yes", "No");

                                if (sure)
                                    painter.RemoveFoliageInstancesLabeled(label);
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                EditorGUILayout.LabelField("Bilboards: ");
                EditorGUILayout.Space();

                painter.m_BillboardsGenerateLODGroup = EditorGUILayout.Toggle(new GUIContent(
                    "Generate LOD Group",
                    "If set to true then all the generated billboard will have a LOD group attached and will dissapear"), 
                    painter.m_BillboardsGenerateLODGroup);

                if(painter.m_BillboardsGenerateLODGroup)
                {
                    painter.m_BillboardLODGroupFade = EditorGUILayout.Slider(new GUIContent(
                        "Cull Screen Size",
                        "At what screen percentage the LOD groups will fade"),
                        painter.m_BillboardLODGroupFade, 0.01f, 0.8f);

                    painter.m_BillboardLODGroupWillCrossFade = EditorGUILayout.Toggle(new GUIContent(
                        "Animated CrossFade",
                        "If the billboards will have an animated crossfade when getting out of the maximum distance"), 
                        painter.m_BillboardLODGroupWillCrossFade);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                EditorGUILayout.LabelField("Editor Rendering Data: ");
                EditorGUILayout.Space();

                painter.m_DrawGridsMode = (FoliagePainter.ESpatialGridDrawMode)(EditorGUILayout.EnumPopup(
                    new GUIContent(
                    "Grid Draw Mode",
                    "Set the grid draw mode.\n\n" +
                    "At 'NONE' no grids are drawn.\n" +
                    "At 'DRAW_GRIDS' the tree grids are drawn.\n" +
                    "At 'DRAW_GRIDS_EXTENDED' the extended tree grids are drawn.\n" +
                    "At 'DRAW_SUBDIVIDED_GRIDS' the grass grids are drawn.\n" +
                    "At 'DRAW_DRAWN_GRIDS' only the drawn tree cells are shown.\n" +
                    "At 'DRAW_DRAWN_SUBDIVIDED_GRIDS' only the drawn grass cells are shown."),
                    painter.m_DrawGridsMode));

                painter.m_DrawNeighboringCells = EditorGUILayout.IntSlider(new GUIContent(
                    "Drawn Neighboring Cells",
                    "How many tree cells around the editor camera we draw. Recommended 1 for best performance."),
                    painter.m_DrawNeighboringCells, 1, 3);

                painter.m_DrawGrassCellsDistance = EditorGUILayout.Slider(new GUIContent(
                    "Draw Grass Cell Distance",
                    "At what distance we are going to draw the grass cells. Recommended 25-50 for best performance."), 
                    painter.m_DrawGrassCellsDistance, 25, 100);

                painter.m_DrawTreeShadows = EditorGUILayout.Toggle(new GUIContent(
                    "Draw Tree Shadows",
                    "If we should draw tree shadows. Recommended off."),
                    painter.m_DrawTreeShadows);

                painter.m_DrawTreeLastLOD = EditorGUILayout.Toggle(new GUIContent(
                    "Draw Tree Last LOD",
                    "If we should always choose the last LOD for trees when rendering them. Recommended true for best performance."),
                    painter.m_DrawTreeLastLOD);
            }

            EditorGUILayout.Space();

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                EditorGUILayout.LabelField("Set foliage data file name: ");
                EditorGUILayout.Space();

                using (new ScopedLayout(() => { EditorGUILayout.BeginHorizontal(); }, EBeginMode.BEGIN_HORIZONTAL))
                {
                    painter.m_FoliageDataSaveName = EditorGUILayout.TextField(painter.m_FoliageDataSaveName);

                    if (GUILayout.Button("Force Disk Reload"))
                        painter.LoadFromFile(true, false);
                }
            }
        }

        void InspectorHelp()
        {
            FoliagePainter painter = target as FoliagePainter;
            
            painter.m_EditorFoldoutHelp = EditorGUILayout.Foldout(painter.m_EditorFoldoutHelp, "Help");            
            if (!painter.m_EditorFoldoutHelp)
                return;
            
            EditorGUILayout.HelpBox(
                "PAINTING\n\n" +
                "  Hold down Left Mouse to paint foliage.\n" +
                "  Hold down Shift + Left Mouse to erase foliage.\n" +
                "  Hold down Ctrl + Left Mouse to erase the selected foliage types.\n" +
                "\nCONTROLS\n\n" +
                "  Press F4 to save grass.\n" +
                "\nTYPES INSPECTOR\n\n" +
                "  Press Left Mouse in Foliage Types to select the foliage.\n" +
                "  Press Right Mouse in the Foliage Types to delete the instances.\n" +
                "  Press Middle Mouse in Foliage Types to ping the foliage.\n" +
                "  Press 'Delete' in Foliage Types to delete the type." +
                ""
                ,
                MessageType.None);
        }
        
        void InspectorBrush()
        {
            FoliagePainter painter = target as FoliagePainter;

            painter.m_EditorFoldoutBrush = EditorGUILayout.Foldout(painter.m_EditorFoldoutBrush, "Brush");
            if (!painter.m_EditorFoldoutBrush)
                return;

            FoliagePainter foliagePainter = target as FoliagePainter;

            FoliagePaintParameters paintParams = foliagePainter.m_PaintParameters;

            EditorGUILayout.BeginVertical("Box");

            paintParams.m_BrushSize = EditorGUILayout.Slider(new GUIContent("Brush Radius", "Radius of the brush in meters."),
                paintParams.m_BrushSize, 1f, 100f);

            paintParams.m_FoliageDensity = EditorGUILayout.Slider(
                new GUIContent(
                    "Foliage Density", 
                    "Foliage density per 50m^2 for grass and 2000m^2 for trees. At 1 we'll have 1 tree instance per 2000m^2 and 1 grass instance per 50m^2."),
                paintParams.m_FoliageDensity, 0.05f, 400f);
            
            EditorGUILayout.Space();

            paintParams.m_SlopeFilter = EditorGUILayout.Toggle(
                new GUIContent(
                    "Sloper Filter",
                    "If we should use a slope filter and only draw foliage on items where the angle between the surace's normal and the up direction is between the provided values."), 
                paintParams.m_SlopeFilter);

            if(paintParams.m_SlopeFilter)
            {
                SliderMinMax(ref paintParams.m_SlopeAngles, 0, 180, new GUIContent(
                    string.Format("Angle [{0:F0}°  {1:F0}°]", paintParams.m_SlopeAngles.x, paintParams.m_SlopeAngles.y),
                    "An angle between 0 and 45 means that we are only going to paint on surfaces whose slope is between 0 and 45 degrees." +
                    "Values are between 0 and 180 since we can also want to paint on the down side of surfaces, like floating spheres for example."
                    ));
            }

            EditorGUILayout.Space();

            paintParams.m_ScaleUniform = EditorGUILayout.Toggle(
                new GUIContent(
                    "Scale Uniform XYZ", 
                    "If we should scale uniformly across all XYZ values."), 
                paintParams.m_ScaleUniform);

            if(paintParams.m_ScaleUniform)
            {                
                SliderMinMax(ref paintParams.m_ScaleUniformXYZ, 0.3f, 3f, new GUIContent(
                    string.Format("XYZ [{0:F1}  {1:F1}]", paintParams.m_ScaleUniformXYZ.x, paintParams.m_ScaleUniformXYZ.y),
                    "Scale randomness that we are going to apply over XYZ uniformly."
                    ));
            }
            else
            {
                SliderMinMax(ref paintParams.m_ScaleX, 0.3f, 3f, new GUIContent(
                    string.Format("X [{0:F1}  {1:F1}]", paintParams.m_ScaleX.x, paintParams.m_ScaleX.y),
                    "Scale randomness that we are going to apply over XYZ uniformly."
                    ));

                SliderMinMax(ref paintParams.m_ScaleY, 0.3f, 3f, new GUIContent(
                    string.Format("Y [{0:F1}  {1:F1}]", paintParams.m_ScaleY.x, paintParams.m_ScaleY.y),
                    "Scale randomness that we are going to apply over XYZ uniformly."
                    ));

                SliderMinMax(ref paintParams.m_ScaleZ, 0.3f, 3f, new GUIContent(
                    string.Format("Z [{0:F1}  {1:F1}]", paintParams.m_ScaleY.x, paintParams.m_ScaleY.y),
                    "Scale randomness that we are going to apply over XYZ uniformly."
                    ));
            }

            EditorGUILayout.Space();

            paintParams.m_RotateYOnly = EditorGUILayout.Toggle(
               new GUIContent(
                   "Rotate Y Only",
                   "If we will rotate only along the the foliage instance's normal."),
               paintParams.m_RotateYOnly);

            SliderMinMax(ref paintParams.m_RandomRotation, 0f, 360f, new GUIContent(
                   string.Format("Rotation [{0:F0}°  {1:F0}°]", paintParams.m_RandomRotation.x, paintParams.m_RandomRotation.y),
                   "Random rotation that we are going to apply only along the foliage's normal or along all axes if we don't have the 'Rotate Y Only' option checked."
                   ));

            EditorGUILayout.Space();

            paintParams.m_StaticOnly = EditorGUILayout.Toggle(new GUIContent(
                "Paint Static Only",
                "Paint only on meshes that are flagged as 'Static'"),
            paintParams.m_StaticOnly);

            EditorGUILayout.EndVertical();
        }

        void InspectorFoliageInfo()
        {
            FoliagePainter painter = target as FoliagePainter;

            painter.m_EditorFoldoutFoliageTypeInfo = EditorGUILayout.Foldout(painter.m_EditorFoldoutFoliageTypeInfo, "Foliage Type Info");
            if (!painter.m_EditorFoldoutFoliageTypeInfo)
                return;

            List<FoliageType> inspectorTypes = painter.GetFoliageTypes;

            FoliageType foliageType;

            if (m_SelectedFoliageType >= 0 && m_SelectedFoliageType < inspectorTypes.Count)
            {
                foliageType = inspectorTypes[m_SelectedFoliageType];
            }
            else
            {
                return;
            }

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                EFoliageType type = (EFoliageType)(EditorGUILayout.EnumPopup(foliageType.Type));
                if (type != foliageType.Type)
                {
                    bool canChange = FoliageUtilitiesEditor.CanChangeType(foliageType, type);

                    if (canChange)
                    {
                        bool sure = EditorUtility.DisplayDialog("Warning!",
                            "Changing the foliage type will re-generate the whole foliage hierarchy for that type. It might take some time. Are you sure?",
                            "Yes", "No");

                        if (sure)
                            painter.SetFoliageTypeType(foliageType.m_Hash, type);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Warning!",
                            string.Format("Can't change from type: {0} to new type: {1}. Check the log for more info!", foliageType.Type, type),
                            "Ok");
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rendering: ");

                float maxValue = FoliageGlobals.GetMaxDistance(type);

                float maxDistance = EditorGUILayout.Slider(new GUIContent(
                                "Max Draw Distance",
                                "Maximum draw distance for the foliage type."),
                                foliageType.m_RenderInfo.m_MaxDistance, 0, maxValue);
                if (Mathf.Abs(foliageType.m_RenderInfo.m_MaxDistance - maxDistance) > Mathf.Epsilon)
                    painter.SetFoliageTypeMaxDistance(foliageType.m_Hash, maxDistance);

                bool castShadow = EditorGUILayout.Toggle(new GUIContent(
                        "Enable Shadow",
                        "If we should enable shadow casting."),
                        foliageType.m_RenderInfo.m_CastShadow);
                if (castShadow != foliageType.m_RenderInfo.m_CastShadow)
                    painter.SetFoliageTypeShadow(foliageType.m_Hash, castShadow);

                if (foliageType.IsGrassType == false)
                {
                    bool collision = EditorGUILayout.Toggle(new GUIContent(
                                "Enable Collision",
                                "If we should enable the collision for this foliage type."),
                                foliageType.m_EnableCollision);

                    if (collision != foliageType.m_EnableCollision)
                        painter.SetFoliageTypeCollision(foliageType.m_Hash, collision);
                }

                // Add the special rendering mode, only if we are grass
                if(foliageType.IsGrassType)
                {
                    EFoliageRenderType renderType = (EFoliageRenderType)(EditorGUILayout.EnumPopup(new GUIContent(
                        "Render Type",
                        "If we should draw the data using 'DrawMeshInstanced' or 'DrawMeshInstancedIndirect'. The 'INSTANCED' mode will use the first, the 'INSTANCED_INDIRECT' the second. " +
                        "The shader must be compatible with indirect drawing if you want anything drawn! In your shader you can use 'StructuredBuffer<float4x4> CRITIAS_InstancePositionBuffer' " +
                        "to get your per-instance position data. Check out the examples in the 'WindTree_Grass' shader!"),
                        foliageType.RenderType));

                    if(renderType != foliageType.RenderType)
                        painter.SetFoliageTypeRenderType(foliageType.m_Hash, renderType);
                }

                if(foliageType.IsGrassType)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Bending: ");

                    bool enableBend = EditorGUILayout.Toggle(new GUIContent(
                        "Enable Bend",
                        "If we should bend the grass when we are close to it."), 
                        foliageType.m_EnableBend);

                    if (enableBend != foliageType.m_EnableBend)
                        painter.SetFoliageTypeBending(foliageType.m_Hash, enableBend);

                    if(enableBend)
                    {
                        foliageType.m_BendDistance = EditorGUILayout.Slider(new GUIContent(
                                "Bend Distance",
                                "At what distance from the watched transform we should start bending the grass. 1 means we'll affect the grass at 1 meter around us."),
                                foliageType.m_BendDistance, 0, 10);

                        foliageType.m_BendPower = EditorGUILayout.Slider(new GUIContent(
                                "Bend Power",
                                "How powerfull the bend should be."),
                                foliageType.m_BendPower, 0, 10);
                    }
                }

                if (foliageType.IsSpeedTreeType)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("SpeedTree Misc: ");

                    Color hue = EditorGUILayout.ColorField(new GUIContent(
                        "Hue Color",
                        "Can change the hue sent to the shader"),
                        foliageType.m_RenderInfo.m_Hue);

                    if (hue != foliageType.m_RenderInfo.m_Hue)
                        painter.SetFoliageTypeHue(foliageType.m_Hash, hue);

                    Color color = EditorGUILayout.ColorField(new GUIContent(
                        "Main Color",
                        "Can change the color sent to the shader"),
                        foliageType.m_RenderInfo.m_Color);

                    if (color != foliageType.m_RenderInfo.m_Color)
                        painter.SetFoliageTypeColor(foliageType.m_Hash, color);
                }
            }

            GUILayout.Label("Paint Info: ");
            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {
                FoliageTypePaintInfo paintInfo = foliageType.m_PaintInfo;

                paintInfo.m_SurfaceAlign = EditorGUILayout.Toggle(new GUIContent(
                        "Surface Align",
                        "If we should align the instance to the underlaying surface when extracting, painting or sticking it to the terrain. Not taked into account for billboard trees."),
                        paintInfo.m_SurfaceAlign);
                
                if (paintInfo.m_SurfaceAlign)
                {
                    SliderMinMax(ref paintInfo.m_SurfaceAlignInfluence, 0, 1, new GUIContent(
                        string.Format("Align [{0:F0}%  {1:F0}%]", paintInfo.m_SurfaceAlignInfluence.x * 100, paintInfo.m_SurfaceAlignInfluence.y * 100),
                        "How much we are to align to the painted surface. From 0% to 100%, 100% meaning that we are perfectly aligned with the surface's normal."
                        ));
                }

                EditorGUILayout.Space();

                SliderMinMax(ref paintInfo.m_YOffset, -5, 5, new GUIContent(
                    string.Format("YOffset [{0:F1}  {1:F1}]", paintInfo.m_YOffset.x, paintInfo.m_YOffset.y),
                    "Value that we are going to use to push the foliage instance in or out along it's normal"));
            }
        }

        private static float PREVIEW_ICON_SIZE = 80;
        private static Vector2 m_TypesScroll;        
        private int m_SelectedFoliageType = -1;       

        void InspectorFoliageTypes()
        {                       
            FoliagePainter painter = target as FoliagePainter;

            List<FoliageType> inspectorTypes = painter.GetFoliageTypes;

            painter.m_EditorFoldoutFoliageTypes = EditorGUILayout.Foldout(painter.m_EditorFoldoutFoliageTypes, "Foliage Types");
            if (!painter.m_EditorFoldoutFoliageTypes)
                return;

            InspectorDragDrop();
            
            EditorGUILayout.Space();

            InspectorTypes(inspectorTypes);

            EditorGUILayout.Space();
        }

        private void InspectorTypes(List<FoliageType> inspectorTypes)
        {
            Event currentEvent = Event.current;

            Rect box = new Rect();

            using(new ScopedLayout(() => { box = EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {                
                using(new ScopedLayout(()=> { m_TypesScroll = EditorGUILayout.BeginScrollView(m_TypesScroll, false, false, GUILayout.Height(200f)); }, EBeginMode.BEGIN_SCROLLVIEW))
                {
                    int maxIconsPerRow = (int)(EditorGUIUtility.currentViewWidth / (PREVIEW_ICON_SIZE));

                    for (int i = 0; i < inspectorTypes.Count; i += maxIconsPerRow)
                    {
                        using (new ScopedLayout(() => { EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false)); }, EBeginMode.BEGIN_HORIZONTAL))
                        {
                            for (int r = 0; r < maxIconsPerRow; r++)
                            {
                                int idx = r + i;

                                if (idx >= inspectorTypes.Count)
                                    continue;

                                FoliageType type = inspectorTypes[idx];
                                Object meshToPaint = type.m_Prefab;

                                if(!meshToPaint)
                                {
                                    FoliageLog.e("Foliage type: " + type.m_Hash + " with name: " + type.m_Name + " has been nullified somehow!" +
                                        " Don't delete foliage prefabs/trees before removing them from the foliage system please!");

                                    FoliagePainter painter = target as FoliagePainter;
                                    painter.CheckNullAndRequestUpdate();

                                    continue;
                                }

                                Rect fullArea = GUILayoutUtility.GetRect(PREVIEW_ICON_SIZE, PREVIEW_ICON_SIZE + 15,
                                    GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

                                // Draw the selection outline if we have one
                                if (idx == m_SelectedFoliageType)
                                    GUI.DrawTexture(fullArea, Texture2D.whiteTexture);

                                // Draw the preview
                                Rect previewArea = new Rect(fullArea.position + new Vector2(3f, 3f), fullArea.size - new Vector2(6f, 6f + 15f));

                                GUI.Box(previewArea, new GUIContent(AssetPreview.GetAssetPreview(meshToPaint), meshToPaint.name));

                                // Draw the activation toggle
                                bool selected = GUI.Toggle(new Rect(previewArea.xMin + 4, previewArea.yMin + 4, 20, 20), type.m_PaintInfo.m_PaintEnabled, GUIContent.none);
                                if (selected != type.m_PaintInfo.m_PaintEnabled)
                                    EnableFoliageTypePaint(type.m_Hash, selected);

                                // Count
                                GUI.Label(new Rect(previewArea.xMin + 5, previewArea.yMax - 20f, 100, 20), GetFoliageCountForType(type.m_Hash));

                                string name = type.m_Prefab.name;
                                if (name.Length > 12)
                                    name = name.Substring(0, 12);
                                GUI.Label(new Rect(new Vector2(fullArea.xMin, fullArea.yMax - 20f), new Vector2(fullArea.width, 20)), name, GetDragDropStyle());

                                if (fullArea.Contains(currentEvent.mousePosition))
                                {
                                    switch (currentEvent.type)
                                    {
                                        case EventType.MouseDown:
                                            switch (currentEvent.button)
                                            {
                                                case 0:
                                                    m_SelectedFoliageType = idx;
                                                    Repaint();
                                                    break;
                                                case 1:
                                                    bool sure = EditorUtility.DisplayDialog("Warning!",
                                                        "This will delete all the instances of the foliage type: '" + type.m_Prefab.name + "'! Are you sure?",
                                                        "Yes", "No");
                                                    if (sure) RemoveFoliageTypeHash(type.m_Hash, false);
                                                    break;
                                                case 2:
                                                    EditorGUIUtility.PingObject(meshToPaint);
                                                    break;
                                            }
                                            break;
                                    }
                                }

                                GUILayout.Space(5f);
                            }
                        }
                        
                        GUILayout.Space(5f);
                    }
                }

                GUILayout.Space(4f);

                if (box.Contains(currentEvent.mousePosition))
                {
                    switch (currentEvent.type)
                    {
                        case EventType.KeyDown:
                            if (currentEvent.keyCode == KeyCode.Delete && m_SelectedFoliageType >= 0 && m_SelectedFoliageType < inspectorTypes.Count)
                            {
                                bool sure = EditorUtility.DisplayDialog("Warning!",
                                        "This will the foliage type: '" + inspectorTypes[m_SelectedFoliageType].m_Prefab.name + "'! Are you sure?",
                                        "Yes", "No");
                                if (sure) RemoveFoliageTypeHash(inspectorTypes[m_SelectedFoliageType].m_Hash, true);
                            }
                            break;
                    }
                }
            }

            using (new ScopedLayout(() => { EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_HORIZONTAL))
            {
                GUILayout.Label("Cleaning");

                using (new ScopedLayout(EBeginMode.BEGIN_HORIZONTAL))
                {
                    if (GUILayout.Button(
                        new GUIContent("Refresh Types",
                        "Use if you modified any prefab data."), GUILayout.ExpandWidth(false)))
                    {
                        FoliagePainter painter = target as FoliagePainter;
                        painter.RequestRefreshFoliageTypeData();
                    }

                    if (GUILayout.Button(new GUIContent("Clean Data",
                        "Use if you deleted a prefab without manually removing it from the system. It will remove all the " +
                        "foliage instances of the types that do not exist in the data"),
                        GUILayout.ExpandWidth(false)))
                    {
                        FoliagePainter painter = target as FoliagePainter;
                        painter.CleanFoliageData();
                    }
                }

                EditorGUILayout.Space();
                GUILayout.Label("Generation");

                using (new ScopedLayout(EBeginMode.BEGIN_HORIZONTAL))
                {
                    if (GUILayout.Button("Generate Billboards", GUILayout.ExpandWidth(false)))
                    {
                        FoliagePainter painter = target as FoliagePainter;
                        painter.GenerateTreeBillboards(true);
                    }

                    if (GUILayout.Button(new GUIContent(
                        "Bake Collision",
                        "Bakes the collision for all the trees that support collision. Used in case that you don't " +
                        "want to use the dynamic collision system."),
                        GUILayout.ExpandWidth(false)))
                    {
                        FoliagePainter painter = target as FoliagePainter;
                        painter.GenerateTreeData(EExtractType.COLLIDERS, true);
                    }

                    if (GUILayout.Button(new GUIContent(
                       "Bake Navmesh",
                       "Bakes the renderers for all the trees that support collision. Used in case that you want to bake the navmesh."),
                       GUILayout.ExpandWidth(false)))
                    {
                        FoliagePainter painter = target as FoliagePainter;
                        painter.GenerateTreeData(EExtractType.RENDERERS_FOR_COLLIDER_MESHES_NAVMESH, true);
                    }

                    if (GUILayout.Button(new GUIContent(
                       "Bake All",
                       "Bakes all the game objects out of the system and into Unity's scene hierarchy."),
                       GUILayout.ExpandWidth(false)))
                    {
                        FoliagePainter painter = target as FoliagePainter;

                        // Create a view with checkboxes to allow the stuff to extract
                        var extractor = ScriptableWizard.DisplayWizard<FoliageExporterWindow>("Extract Prefabs", "Extract");
                        extractor.Init(painter, (int[] hashes) => {
                            // Check the count
                            int totalExtractCount = 0;

                            for (int i = 0; i < hashes.Length; i++)
                                totalExtractCount += painter.GetFoliageInstanceCount(hashes[i]);

                            if (totalExtractCount > 1000)
                            {
                                bool clean = EditorUtility.DisplayDialog("Warning!", "[" + totalExtractCount + "] instances will be extracted. " +
                                    "It might take a lot of time or crash the editor. Are you sure?", "Yes", "No");

                                if(clean)
                                    painter.GenerateFullTreeData(hashes, true);
                            }
                            else
                            {
                                painter.GenerateFullTreeData(hashes, true);
                            }
                        });
                    }
                }
            }
        }

        private GUIStyle mInspectorDragDropStyle;
        private GUIStyle GetDragDropStyle()
        {            
            if(mInspectorDragDropStyle == null)
            {
                mInspectorDragDropStyle = new GUIStyle();
                mInspectorDragDropStyle.alignment = TextAnchor.MiddleCenter;
            }

            return mInspectorDragDropStyle;
        }
        
        private void InspectorDragDrop()
        {
            Rect prefabDropArea = new Rect();

            using (new ScopedLayout(() => { prefabDropArea = EditorGUILayout.BeginVertical("Box"); }, EBeginMode.BEGIN_VERTICAL))
            {                
                Event currentEvent = Event.current;

                Rect infoArea = GUILayoutUtility.GetRect(prefabDropArea.width, prefabDropArea.height, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false), GUILayout.Height(40));

                GUI.Label(infoArea, new GUIContent("Drop foliage prefabs here!", "Drop any foliage prefabs here or click here and select a foliage prefab that you wish to add."), GetDragDropStyle());

                if (prefabDropArea.Contains(currentEvent.mousePosition))
                {
                    switch (currentEvent.type)
                    {
                        case EventType.DragUpdated:
                        case EventType.DragPerform:

                            Object[] objects = DragAndDrop.objectReferences;

                            DragAndDropVisualMode mode = DragAndDropVisualMode.Move;

                            for (int i = 0; i < objects.Length; i++)
                            {
                                PrefabType type = PrefabUtility.GetPrefabType(objects[i]);

                                if ((objects[i] is GameObject) == false || (type != PrefabType.ModelPrefab && type != PrefabType.Prefab))
                                {
                                    mode = DragAndDropVisualMode.Rejected;
                                }
                            }

                            DragAndDrop.visualMode = mode;

                            if (currentEvent.type == EventType.DragPerform)
                            {
                                DragAndDrop.AcceptDrag();

                                Object[] dragRefs = DragAndDrop.objectReferences;

                                for (int i = 0; i < dragRefs.Length; i++)
                                {
                                    if (dragRefs[i] is GameObject)
                                    {
                                        GameObject obj = dragRefs[i] as GameObject;
                                        PrefabType type = PrefabUtility.GetPrefabType(dragRefs[i]);

                                        if (type == PrefabType.ModelPrefab || type == PrefabType.Prefab)
                                        {
                                            AddFoliageType(obj);
                                        }
                                    }
                                }
                            }
                            break;
                    }
                } // End drop area stuff
            }            
        }
        
        void OnSceneGUI()
        {
            FoliagePainter pt = target as FoliagePainter;

            if (pt == null || pt.m_PaintParameters == null)
                return;

            Event currentEvent = Event.current;
            
            // Lock while we are selected
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));            

            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            RaycastHit hit;

            float radius = pt.m_PaintParameters.m_BrushSize;

            float rayLength = Mathf.Clamp(pt.m_PaintParameters.m_BrushSize / 100.0f * 2000.0f, 250f, 2000.0f);
            Physics.Raycast(ray, out hit, rayLength, ~0);

            if (!hit.collider)
            {
                return;
            }

            SceneView.RepaintAll();
            
            Handles.color = Color.green;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            
            Handles.DrawWireDisc(hit.point + hit.normal * 0.2f, hit.normal, radius);
            
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                case EventType.MouseDrag:
                    if (currentEvent.button == 0)
                    {
                        if (currentEvent.alt)
                            return;

                        if(pt.m_EditorPaintBegun == false)
                        {
                            pt.BeginPaint();
                            pt.m_EditorPaintBegun = true;
                        }
                        
                        if(currentEvent.shift)
                        {
                            // Delete foliage of all types
                            pt.DeleteFoliage(hit, true);
                        }
                        else if(currentEvent.control)
                        {
                            // Delete foliage only of the selected type
                            pt.DeleteFoliage(hit, false);
                        }
                        else
                        {
                            // Paint foliage
                            pt.PaintFoliage(hit);
                        }                        
                    }
                    break;
                case EventType.MouseUp:
                    if(currentEvent.button == 0)
                    {
                        if (pt.m_EditorPaintBegun == true)
                        {
                            pt.EndPaint();
                            pt.m_EditorPaintBegun = false;
                        }
                    }
                    break;
            }

            // Request an update
            pt.RequestUpdate();
        } // End SceneGUI

        private void SliderMinMax(ref Vector2 minMax, float minValue, float maxValue, GUIContent content)
        {
            float min = minMax.x;
            float max = minMax.y;

            EditorGUILayout.MinMaxSlider(content, ref min, ref max, minValue, maxValue);

            if (min >= max || max <= min)
                min = max;

            minMax = new Vector2(min, max);
        }



        private string GetFoliageCountForType(int typeHash)
        {
            FoliagePainter painter = target as FoliagePainter;

            int count = painter.GetFoliageInstanceCountCached(typeHash);

            string ct = "";

            if(count < 1000)
            {
                ct = "" + count;
            }
            else if(count < 1000000)
            {
                ct = string.Format("{0:F3}K", (count / 1000f));
            }
            else
            {
                ct = string.Format("{0:F3}M", (count / 1000000f));
            }            

            return ct;
        }

        /**
         * Adds a foliage type to the system performing all the necesarry checks of type etc...
         */
        private void AddFoliageType(GameObject foliage)
        {
            FoliagePainter painter = target as FoliagePainter;

            FoliageTypeBuilder builder;
            bool anyWeirdFoliageError;

            FoliageUtilitiesEditor.ConfigurePrefab(foliage, out builder, out anyWeirdFoliageError);

            if (anyWeirdFoliageError == false)
            {
                int hash = painter.AddFoliageType(builder);
                FoliageLog.i("Added foliage type: " + foliage.name + "  with hash: " + hash);
            }
            else
            {
                FoliageLog.e("Could not add grass with name: " + foliage.name);
            }
        }

        public void RemoveFoliageTypeHash(int typeHash, bool deleteTypeToo)
        {
            FoliagePainter painter = target as FoliagePainter;

            painter.RemoveFoliageTypeHash(typeHash, deleteTypeToo);
        }

        public void EnableFoliageTypePaint(int typeHash, bool enabled)
        {
            FoliagePainter painter = target as FoliagePainter;

            painter.EnableTypeForPainting(typeHash, enabled);
        }        
    } // End FoliagePainterEditor Class
}
